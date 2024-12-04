using TaxFormFunction.Entities;
using System;
using TaxFormFunction.Interfaces.Services;
using System.IO;
using Microsoft.AspNetCore.Mvc;
using iText.Kernel.Pdf;
using iText.Layout.Element;
using iText.Layout;

namespace TaxFormFunction.Services
{
    public class PdfService : IPdfService
    {
        
        public MemoryStream GeneratePdf(TaxForm taxForm)
        {
            var pdfStream = new MemoryStream();
            using (var pdfWriter = new PdfWriter(pdfStream))
            {
                using (var pdfDocument = new PdfDocument(pdfWriter))
                {
                    var document = new Document(pdfDocument);
                    document.Add(new Paragraph($"Tax Form for {taxForm.EmployeeName}"));
                    document.Add(new Paragraph($"Calendar Year: {taxForm.CalendarYear}"));
                    document.Add(new Paragraph($"Company: {taxForm.Company}"));
                    document.Add(new Paragraph($"Date: {taxForm.Date}"));
                    document.Add(new Paragraph($"Employee ID: {taxForm.EmployeeId}"));
                    document.Add(new Paragraph($"Department: {taxForm.Department}"));
                    document.Add(new Paragraph($"Status: {taxForm.Status}"));
                    document.Add(new Paragraph($"Status Description: {taxForm.StatusDesc}"));
                    document.Add(new Paragraph($"Child Allowance: {taxForm.ChildAllowance}"));
                    document.Add(new Paragraph($"Amount Child Allowance: {taxForm.AmtChildAllowance}"));
                    document.Add(new Paragraph($"Child in After 2018: {taxForm.ChildInAfter2018}"));
                    document.Add(new Paragraph($"Amount Child in After 2018: {taxForm.AmtChildInAfter2018}"));
                    document.Add(new Paragraph($"Parental Care Taxpayer Father: {taxForm.ParentalCareTaxpayerFather}"));
                    document.Add(new Paragraph($"Amount Parental Care Taxpayer Father: {taxForm.AmtParentalCareTaxpayerFather}"));
                    document.Add(new Paragraph($"Parental Care Taxpayer Mother: {taxForm.ParentalCareTaxpayerMother}"));
                    document.Add(new Paragraph($"Amount Parental Care Taxpayer Mother: {taxForm.AmtParentalCareTaxpayerMother}"));
                    document.Add(new Paragraph($"Parental Care Spouse Father: {taxForm.ParentalCareSpouseFather}"));
                    document.Add(new Paragraph($"Amount Parental Care Spouse Father: {taxForm.AmtParentalCareSpouseFather}"));
                    document.Add(new Paragraph($"Parental Care Spouse Mother: {taxForm.ParentalCareSpouseMother}"));
                    document.Add(new Paragraph($"Amount Parental Care Spouse Mother: {taxForm.AmtParentalCareSpouseMother}"));
                    document.Add(new Paragraph($"Disabled Person Support: {taxForm.DisabledPersonSupport}"));
                    document.Add(new Paragraph($"Amount Disabled Person Support: {taxForm.AmtDisabledPersonSupport}"));
                    document.Add(new Paragraph($"Health Insurance Taxpayer Father: {taxForm.HealthInsuranceTaxpayerFather}"));
                    document.Add(new Paragraph($"Health Insurance Taxpayer Mother: {taxForm.HealthInsuranceTaxpayerMother}"));
                    document.Add(new Paragraph($"Health Insurance Taxpayer Spouse Father: {taxForm.HealthInsuranceTaxpayerSpouseFather}"));
                    document.Add(new Paragraph($"Health Insurance Taxpayer Spouse Mother: {taxForm.HealthInsuranceTaxpayerSpouseMother}"));
                    document.Add(new Paragraph($"Life Insurance Paid: {taxForm.LifeInsurancePaid}"));
                    document.Add(new Paragraph($"Pension Insurance Paid: {taxForm.PensionInsurancePaid}"));
                    document.Add(new Paragraph($"RMF: {taxForm.Rmf}"));
                    document.Add(new Paragraph($"SSF: {taxForm.Ssf}"));
                    document.Add(new Paragraph($"Interest Paid on Loan Purchase: {taxForm.InterestPdOnLoanPurchase}"));
                    document.Add(new Paragraph($"Donation Supporting Education/Sports: {taxForm.DonationSupportingEducSports}"));
                    document.Add(new Paragraph($"Other Donation: {taxForm.OtherDonation}"));
                    document.Add(new Paragraph($"Health Insurance Taxpayer: {taxForm.HealthInsuranceTaxpayer}"));
                    document.Add(new Paragraph($"Taxable Income Earned from Previous Company: {taxForm.TaxableIncomeEarnedPrevComp}"));
                    document.Add(new Paragraph($"Withholding Tax from Previous Company: {taxForm.WithholdingTaxPrevComp}"));
                    document.Add(new Paragraph($"SS from Previous Company: {taxForm.SsPrevComp}"));
                    document.Add(new Paragraph($"PF from Previous Company: {taxForm.PfPrevComp}"));
                    document.Add(new Paragraph($"Updated By: {taxForm.UpdatedBy}"));
                    document.Add(new Paragraph($"Updated Datetime: {taxForm.UpdatedDatetime}"));
                    document.Add(new Paragraph($"Created By: {taxForm.CreatedBy}"));
                    document.Add(new Paragraph($"Created Datetime: {taxForm.CreatedDatetime}"));
                    document.Close();
                }
            }
            return pdfStream;
        }

        public IActionResult CreatePdfFileResult(byte[] pdfBytes, int? employeeId)
        {
            var fileResult = new FileContentResult(pdfBytes, "application/pdf")
            {
                FileDownloadName = $"TaxForm_{employeeId}.pdf"
            };
            return fileResult;
        }
    }
}
