using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using TaxFormFunction.Entities;
using System.IO.Compression;
using TaxFormFunction.Interfaces.Services;
using TaxFormFunction.Interfaces.Repositories;
using System.Linq;
using Microsoft.Data.SqlClient;

public class TaxFormFunctions
{
    private readonly ITaxFormService _taxFormService;
    private readonly ITransactionLogService _transactionLogService;
    private readonly IErrorLogService _errorLogService;
    private readonly IPdfService _pdfService;
    private readonly ISchemaValidationService _schemaValidationService;

    private const string StatusSingle = "Single";
    private const string StatusMarried = "Married (Registered)";
    private const string StatusDivorcedWidowed = "Divorced/Widowed";
    private const string StatusDeceased = "Deceased";

    public TaxFormFunctions(
        ITaxFormService taxFormService,
        ITransactionLogService transactionLogService,
        IErrorLogService errorLogService,
        IPdfService pdfService,
        ISchemaValidationService validationService)
    {
        _taxFormService = taxFormService;
        _transactionLogService = transactionLogService;
        _errorLogService = errorLogService;
        _pdfService = pdfService;
        _schemaValidationService = validationService;
    }

    [FunctionName("ViewUserTaxForm")]
    public async Task<IActionResult> ViewUserTaxForm(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "ViewUserTaxForm")] HttpRequest req,
        ILogger log)
    {
        log.LogInformation("Fetching all user tax forms.");
        try
        {
            // Parse integer parameters
            int? employeeId = string.IsNullOrEmpty(req.Query["employeeId"]) ? (int?)null : int.Parse(req.Query["employeeId"]);
            int? pageNumber = string.IsNullOrEmpty(req.Query["pageNumber"]) ? (int?)null : int.Parse(req.Query["pageNumber"]);
            int? pageSize = string.IsNullOrEmpty(req.Query["pageSize"]) ? (int?)null : int.Parse(req.Query["pageSize"]);

            var taxForms = await _taxFormService.GetTaxFormsAsync(
                req.Query["year"].ToString(),
                req.Query["company"].ToString(),
                employeeId,
                req.Query["startDate"].ToString(),
                req.Query["endDate"].ToString(),
                pageNumber,
                pageSize);

            return new OkObjectResult(taxForms);
        }
        catch (Exception ex)
        {
            log.LogError(ex, "Error fetching tax forms");
            return new StatusCodeResult(StatusCodes.Status500InternalServerError);
        }
    }

    [FunctionName("CreateUserTaxForm")]
    public async Task<IActionResult> CreateUserTaxForm(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "CreateUserTaxForm")] HttpRequest req,
        ILogger log)
    {
        log.LogInformation("Creating a new user tax form.");

        TaxForm taxForm = null;
        try
        {
            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();

            // Validate incoming JSON against the schema
            var validationErrors = _schemaValidationService.ValidateRequestBody(requestBody);

            if (validationErrors.Any())
            {
                var errorDetails = validationErrors.Select(error => new
                {
                    Message = error.Message,
                    Path = error.Path
                }).ToList();

                return new BadRequestObjectResult(new { Errors = errorDetails });
            }

            taxForm = JsonConvert.DeserializeObject<TaxForm>(requestBody);

            // Validate the taxForm object
            var modelErrors = _schemaValidationService.ValidateModel(taxForm);
            if (modelErrors.Any())
            {
                return new BadRequestObjectResult(new { Errors = string.Join(", ", modelErrors) });
            }

            // Check if the employee has already registered for this year
            bool exists = await _taxFormService.EmployeeTaxFormExistsAsync(taxForm.EmployeeId, taxForm.CalendarYear);
            if (exists)
            {
                return new BadRequestObjectResult(new { Errors = "Employee has already registered for the current year." });
            }

            SetTaxFormStatusDescription(taxForm);

            var result = await _taxFormService.CreateUserTaxFormAsync(taxForm);

            await _transactionLogService.CreateTransactionLogAsync("Create", taxForm.EmployeeName, taxForm.CreatedDatetime ?? DateTime.UtcNow);

            return new CreatedAtActionResult("CreateTaxForm", "TaxFormFunctions", new { id = 1 }, taxForm);
        }

        catch (SqlException ex)
        {
            await _errorLogService.CreateErrorLogAsync(ex.Message, taxForm.EmployeeName, DateTime.UtcNow);
            return new BadRequestObjectResult(ex.Number + "" + ex.Message);
        }

        catch (Exception ex)
        {
            log.LogError(ex, "Error creating tax form");
            await _errorLogService.CreateErrorLogAsync(ex.Message, taxForm.EmployeeName, DateTime.UtcNow);
            return new StatusCodeResult(StatusCodes.Status500InternalServerError);
        }
    }

    [FunctionName("UpdateUserTaxForm")]
    public async Task<IActionResult> UpdateUserTaxForm(
        [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "UpdateUserTaxForm")] HttpRequest req,
        ILogger log)
    {
        log.LogInformation($"Updating tax form");
        TaxForm taxForm = null;

        try
        {
            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();

            //Validate incoming JSON against the schema
            var validationErrors = _schemaValidationService.ValidateRequestBody(requestBody);

            if (validationErrors.Any())
            {
                var errorDetails = validationErrors.Select(error => new
                {
                    Message = error.Message,
                    Path = error.Path
                }).ToList();

                return new BadRequestObjectResult(new { Errors = errorDetails });
            }

            taxForm = JsonConvert.DeserializeObject<TaxForm>(requestBody);

            // Validate the taxForm object
            var modelErrors = _schemaValidationService.ValidateModel(taxForm);
            if (modelErrors.Any())
            {
                return new BadRequestObjectResult(new { Errors = string.Join(", ", modelErrors) });
            }

            SetTaxFormStatusDescription(taxForm);

            var result = await _taxFormService.UpdateTaxFormAsync(taxForm);

            await _transactionLogService.UpdateTransactionLogAsync("Update", taxForm.EmployeeName, taxForm.UpdatedDatetime ?? DateTime.UtcNow);

            return new OkObjectResult(result);
        }
        catch (SqlException ex)
        {
            await _errorLogService.UpdateErrorLogAsync(ex.Message, taxForm.EmployeeName, DateTime.UtcNow);
            return new BadRequestObjectResult(ex.Number + "" + ex.Message);
        }
        catch (Exception ex)
        {
            log.LogError(ex, "Error updating tax form");
            return new StatusCodeResult(StatusCodes.Status500InternalServerError);
        }
    }

    [FunctionName("GenerateTaxFormReport")]
    public async Task<IActionResult> GenerateTaxFormReport(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "GenerateTaxFormReport")] HttpRequest req,
        ILogger log)
    {
        log.LogInformation("Generating PDFs for all payroll tax forms.");

        try
        {
            // Parse integer parameters
            int? employeeId = string.IsNullOrEmpty(req.Query["employeeId"]) ? (int?)null : int.Parse(req.Query["employeeId"]);
            int? pageNumber = string.IsNullOrEmpty(req.Query["pageNumber"]) ? (int?)null : int.Parse(req.Query["pageNumber"]);
            int? pageSize = string.IsNullOrEmpty(req.Query["pageSize"]) ? (int?)null : int.Parse(req.Query["pageSize"]);

            var taxForms = await _taxFormService.GetTaxFormsAsync(
                req.Query["year"].ToString(),
                req.Query["company"].ToString(),
                employeeId,
                req.Query["startDate"].ToString(),
                req.Query["endDate"].ToString(),
                pageNumber,
                pageSize);

            if (employeeId.HasValue && taxForms.Count == 1)
            {
                var pdfBytes = _pdfService.GeneratePdf(taxForms.FirstOrDefault()).ToArray();
                return _pdfService.CreatePdfFileResult(pdfBytes, taxForms.FirstOrDefault().EmployeeId);
            }

            else
            {
                using (var zipStream = new MemoryStream())
                {
                    using (var archive = new ZipArchive(zipStream, ZipArchiveMode.Create, true))
                    {
                        foreach (var form in taxForms)
                        {
                            var pdfBytes = _pdfService.GeneratePdf(form).ToArray();
                            var zipEntry = archive.CreateEntry($"TaxForm_{form.EmployeeId}.pdf", CompressionLevel.Optimal);
                            using (var entryStream = zipEntry.Open())
                            {
                                entryStream.Write(pdfBytes, 0, pdfBytes.Length);
                            }
                        }
                    }

                    zipStream.Position = 0;
                    return new FileContentResult(zipStream.ToArray(), "application/zip")
                    {
                        FileDownloadName = "TaxFormFormReport.zip"
                    };
                }
            }
        }
        catch (Exception ex)
        {
            log.LogError(ex, "An error occurred while generating PDFs.");
            return new StatusCodeResult(StatusCodes.Status500InternalServerError);
        }
    }

    private void SetTaxFormStatusDescription(TaxForm taxForm)
    {
        taxForm.StatusDesc = taxForm.Status switch
        {
            1 => StatusSingle,
            2 => StatusMarried,
            3 => StatusDivorcedWidowed,
            4 => StatusDeceased,
            _ => taxForm.StatusDesc 
        };
    }
}
