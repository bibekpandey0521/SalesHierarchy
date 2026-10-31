using System.Net.Mail;
using ClosedXML.Excel;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.Extensions.Caching.Memory;
using SalesHierarchy.Core.Interfaces.Repositories;
using SalesHierarchy.Core.Models;
using SalesHierarchy.Core.Validation;
using SalesHierarchy.Web.Data;

namespace SalesHierarchy.Web.Services
{
    /// <summary>
    /// Parses, validates, stages and imports the Excel bulk-upload of salespersons.
    /// Staged batches are kept in memory for a short period so the preview/confirm
    /// step does not require re-uploading the file.
    /// </summary>
    public class BulkUploadService
    {
        public const long MaxFileSizeBytes = 5 * 1024 * 1024;
        public const int MaxRows = 1000;
        private static readonly string[] RequiredHeaders = { "Name", "Designation", "Email", "ManagerEmail" };
        private static readonly TimeSpan CacheLifetime = TimeSpan.FromMinutes(30);

        private readonly ISalesPersonRepository _salesPersonRepository;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IEmailSender _emailSender;
        private readonly IMemoryCache _cache;

        public BulkUploadService(
            ISalesPersonRepository salesPersonRepository,
            UserManager<ApplicationUser> userManager,
            IEmailSender emailSender,
            IMemoryCache cache)
        {
            _salesPersonRepository = salesPersonRepository;
            _userManager = userManager;
            _emailSender = emailSender;
            _cache = cache;
        }

        public BulkUploadParseResult ParseAndValidate(Stream stream, string fileName, long fileSize)
        {
            if (fileSize <= 0)
            {
                return Fail("Please choose a non-empty .xlsx file.");
            }

            if (fileSize > MaxFileSizeBytes)
            {
                return Fail("The file exceeds the 5 MB limit.");
            }

            if (!Path.GetExtension(fileName).Equals(".xlsx", StringComparison.OrdinalIgnoreCase))
            {
                return Fail("Only .xlsx files are supported. Download the template to get started.");
            }

            XLWorkbook workbook;
            try
            {
                workbook = new XLWorkbook(stream);
            }
            catch
            {
                return Fail("The file could not be read as an Excel workbook.");
            }

            using (workbook)
            {
                var worksheet = workbook.Worksheets.FirstOrDefault();
                if (worksheet == null)
                {
                    return Fail("The workbook does not contain any worksheet.");
                }

                var usedRange = worksheet.RangeUsed();
                if (usedRange == null)
                {
                    return Fail("The worksheet is empty.");
                }

                var headerRow = usedRange.FirstRow();
                for (var i = 0; i < RequiredHeaders.Length; i++)
                {
                    var header = headerRow.Cell(i + 1).GetString().Trim();
                    if (!header.Equals(RequiredHeaders[i], StringComparison.OrdinalIgnoreCase))
                    {
                        return Fail($"Invalid template. Column {i + 1} must be '{RequiredHeaders[i]}' but found '{header}'.");
                    }
                }

                var existingUsers = _userManager.Users.ToList();
                var existingEmails = existingUsers
                    .Where(u => !string.IsNullOrWhiteSpace(u.Email))
                    .Select(u => u.Email!.Trim().ToLowerInvariant())
                    .ToHashSet();
                var existingPersonIdsByEmail = existingUsers
                    .Where(u => !string.IsNullOrWhiteSpace(u.Email) && u.SalesPersonId.HasValue)
                    .GroupBy(u => u.Email!.Trim().ToLowerInvariant())
                    .ToDictionary(g => g.Key, g => g.First().SalesPersonId!.Value);

                var batch = new BulkUploadBatch { FileName = Path.GetFileName(fileName) };
                var rows = new List<BulkRow>();
                var seenEmails = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var rowCount = 0;

                foreach (var row in usedRange.RowsUsed().Skip(1))
                {
                    if (++rowCount > MaxRows)
                    {
                        return Fail($"The file contains more than {MaxRows} data rows. Split the upload into smaller files.");
                    }

                    var name = row.Cell(1).GetString().Trim();
                    var designation = row.Cell(2).GetString().Trim();
                    var email = row.Cell(3).GetString().Trim();
                    var managerEmail = row.Cell(4).GetString().Trim();

                    if (string.IsNullOrWhiteSpace(name) &&
                        string.IsNullOrWhiteSpace(designation) &&
                        string.IsNullOrWhiteSpace(email) &&
                        string.IsNullOrWhiteSpace(managerEmail))
                    {
                        continue;
                    }

                    var bulkRow = new BulkRow
                    {
                        RowNumber = row.RowNumber(),
                        Name = name,
                        Designation = designation,
                        Email = email,
                        ManagerEmail = managerEmail
                    };

                    bulkRow.Errors.AddRange(BulkRowValidator.ValidateFields(name, designation, email, managerEmail));

                    if (BulkRowValidator.IsValidEmail(email))
                    {
                        var normalizedEmail = BulkRowValidator.Normalize(email);
                        if (existingEmails.Contains(normalizedEmail))
                        {
                            bulkRow.Status = BulkRowStatus.Duplicate;
                            bulkRow.Errors.Add("An account with this email already exists. The row will be skipped.");
                        }
                        else if (!seenEmails.Add(normalizedEmail))
                        {
                            bulkRow.Status = BulkRowStatus.Duplicate;
                            bulkRow.Errors.Add("Duplicate email inside this file. The row will be skipped.");
                        }
                    }

                    rows.Add(bulkRow);
                }

                if (rows.Count == 0)
                {
                    return Fail("The file does not contain any data rows.");
                }

                ValidateManagers(rows, existingPersonIdsByEmail);

                batch.Rows = rows;
                return new BulkUploadParseResult { Success = true, Batch = batch };
            }
        }

        private static void ValidateManagers(List<BulkRow> rows, Dictionary<string, int> existingPersonIdsByEmail)
        {
            var validRows = rows
                .Where(r => !string.IsNullOrWhiteSpace(r.Email) && BulkRowValidator.IsValidEmail(r.Email) && r.Status != BulkRowStatus.Duplicate)
                .GroupBy(r => BulkRowValidator.Normalize(r.Email))
                .ToDictionary(g => g.Key, g => g.First());

            // In-file manager graph: email -> manager email.
            var managerByEmail = validRows.Values
                .Where(r => !string.IsNullOrWhiteSpace(r.ManagerEmail))
                .ToDictionary(
                    r => BulkRowValidator.Normalize(r.Email),
                    r => BulkRowValidator.Normalize(r.ManagerEmail));

            foreach (var row in rows)
            {
                if (row.Status == BulkRowStatus.Duplicate || string.IsNullOrWhiteSpace(row.ManagerEmail))
                {
                    continue;
                }

                if (!BulkRowValidator.IsValidEmail(row.Email) || !BulkRowValidator.IsValidEmail(row.ManagerEmail))
                {
                    // Field validation already reported the invalid address.
                    continue;
                }

                var managerEmail = BulkRowValidator.Normalize(row.ManagerEmail);

                if (BulkRowValidator.IsSelfReport(row.Email, row.ManagerEmail))
                {
                    row.Errors.Add("A salesperson cannot report to themselves.");
                    continue;
                }

                if (!managerByEmail.ContainsKey(managerEmail) && !existingPersonIdsByEmail.ContainsKey(managerEmail))
                {
                    row.Errors.Add($"Reporting manager '{row.ManagerEmail}' was not found in the file or in existing accounts.");
                    continue;
                }

                if (BulkRowValidator.WouldCreateCycle(row.Email, row.ManagerEmail, managerByEmail))
                {
                    row.Errors.Add("Circular reporting relationship detected using ManagerEmail.");
                }
            }

            foreach (var row in rows)
            {
                if (row.Errors.Count > 0 && row.Status == BulkRowStatus.Valid)
                {
                    row.Status = BulkRowStatus.Invalid;
                }
            }
        }

        public void Stage(BulkUploadBatch batch)
        {
            _cache.Set(CacheKey(batch.Key), batch, CacheLifetime);
        }

        public BulkUploadBatch? Get(string key)
        {
            return _cache.TryGetValue(CacheKey(key), out BulkUploadBatch? batch) ? batch : null;
        }

        public async Task<BulkUploadBatch> ImportAsync(BulkUploadBatch batch, Func<string, string, string> buildResetLink)
        {
            // First pass: create salesperson records and linked identity accounts without passwords.
            foreach (var row in batch.Rows.Where(r => r.IsImportable))
            {
                var personCreatedId = 0;
                ApplicationUser? userCreated = null;
                try
                {
                    var person = new SalesPerson { Name = row.Name, Designation = row.Designation, ManagerId = null };
                    personCreatedId = _salesPersonRepository.Add(person);
                    row.CreatedSalesPersonId = personCreatedId;

                    var user = new ApplicationUser
                    {
                        UserName = row.Email,
                        Email = row.Email,
                        EmailConfirmed = true,
                        SalesPersonId = personCreatedId
                    };

                    var createResult = await _userManager.CreateAsync(user);
                    if (!createResult.Succeeded)
                    {
                        throw new InvalidOperationException(string.Join("; ", createResult.Errors.Select(e => e.Description)));
                    }

                    userCreated = user;
                    await _userManager.AddToRoleAsync(user, "SalesPerson");

                    var token = await _userManager.GeneratePasswordResetTokenAsync(user);
                    var link = buildResetLink(token, row.Email);
                    try
                    {
                        await _emailSender.SendEmailAsync(
                            row.Email,
                            "Set your SalesHierarchy password",
                            $"<p>Welcome to SalesHierarchy.</p><p>Set your password using this link: <a href=\"{System.Net.WebUtility.HtmlEncode(link)}\">Set my password</a></p>");
                        row.InvitationStatus = "Sent";
                    }
                    catch
                    {
                        row.InvitationStatus = "Failed / pending";
                    }

                    row.Status = BulkRowStatus.Imported;
                }
                catch (Exception ex)
                {
                    if (userCreated != null)
                    {
                        try { await _userManager.DeleteAsync(userCreated); } catch { /* best-effort compensation */ }
                    }

                    if (personCreatedId > 0)
                    {
                        try { _salesPersonRepository.Delete(personCreatedId); } catch { /* best-effort compensation */ }
                    }

                    row.Status = BulkRowStatus.Failed;
                    row.CreatedSalesPersonId = null;
                    row.Errors.Add("Account creation failed: " + ex.Message);
                }
            }

            // Second pass: resolve reporting managers now that every record exists.
            var allPeople = _salesPersonRepository.GetAll().ToList();
            var existingUsers = _userManager.Users.ToList();
            var personIdByEmail = existingUsers
                .Where(u => !string.IsNullOrWhiteSpace(u.Email) && u.SalesPersonId.HasValue)
                .GroupBy(u => u.Email!.Trim().ToLowerInvariant())
                .ToDictionary(g => g.Key, g => g.First().SalesPersonId!.Value);

            foreach (var row in batch.Rows.Where(r => r.Status == BulkRowStatus.Imported))
            {
                if (string.IsNullOrWhiteSpace(row.ManagerEmail))
                {
                    continue;
                }

                var managerEmail = BulkRowValidator.Normalize(row.ManagerEmail);
                if (!personIdByEmail.TryGetValue(managerEmail, out var managerId))
                {
                    row.Warnings.Add("Reporting manager could not be resolved; record left without a manager.");
                    continue;
                }

                if (managerId == row.CreatedSalesPersonId)
                {
                    row.Warnings.Add("Self-reporting assignment skipped.");
                    continue;
                }

                var person = _salesPersonRepository.GetById(row.CreatedSalesPersonId!.Value);
                if (person == null)
                {
                    continue;
                }

                if (HierarchyRules.CreatesCycle(person.Id, managerId, allPeople))
                {
                    row.Warnings.Add("Circular hierarchy detected; record left without a manager.");
                    continue;
                }

                person.ManagerId = managerId;
                if (_salesPersonRepository.Update(person))
                {
                    allPeople.RemoveAll(p => p.Id == person.Id);
                    allPeople.Add(person);
                }
            }

            batch.IsImported = true;
            _cache.Set(CacheKey(batch.Key), batch, CacheLifetime);
            return batch;
        }

        public byte[] BuildErrorReport(BulkUploadBatch batch)
        {
            using var workbook = new XLWorkbook();
            var ws = workbook.Worksheets.Add("Upload Results");
            ws.Cell(1, 1).Value = "Row";
            ws.Cell(1, 2).Value = "Name";
            ws.Cell(1, 3).Value = "Designation";
            ws.Cell(1, 4).Value = "Email";
            ws.Cell(1, 5).Value = "ManagerEmail";
            ws.Cell(1, 6).Value = "Status";
            ws.Cell(1, 7).Value = "Errors / Warnings";
            ws.Cell(1, 8).Value = "Invitation";

            var headerRange = ws.Range(1, 1, 1, 8);
            headerRange.Style.Font.Bold = true;
            headerRange.Style.Fill.BackgroundColor = XLColor.FromHtml("#1d4f91");
            headerRange.Style.Font.FontColor = XLColor.White;

            var rowIndex = 2;
            foreach (var row in batch.Rows.Where(r => r.Status != BulkRowStatus.Imported || r.Errors.Count > 0 || r.Warnings.Count > 0))
            {
                ws.Cell(rowIndex, 1).Value = row.RowNumber;
                ws.Cell(rowIndex, 2).Value = row.Name;
                ws.Cell(rowIndex, 3).Value = row.Designation;
                ws.Cell(rowIndex, 4).Value = row.Email;
                ws.Cell(rowIndex, 5).Value = row.ManagerEmail;
                ws.Cell(rowIndex, 6).Value = row.Status.ToString();
                ws.Cell(rowIndex, 7).Value = string.Join(" | ", row.Errors.Concat(row.Warnings));
                ws.Cell(rowIndex, 8).Value = row.InvitationStatus ?? string.Empty;
                rowIndex++;
            }

            ws.Columns().AdjustToContents();
            using var stream = new MemoryStream();
            workbook.SaveAs(stream);
            return stream.ToArray();
        }

        public byte[] BuildTemplate()
        {
            using var workbook = new XLWorkbook();
            var ws = workbook.Worksheets.Add("SalesPersons");
            ws.Cell(1, 1).Value = "Name";
            ws.Cell(1, 2).Value = "Designation";
            ws.Cell(1, 3).Value = "Email";
            ws.Cell(1, 4).Value = "ManagerEmail";

            var headerRange = ws.Range(1, 1, 1, 4);
            headerRange.Style.Font.Bold = true;
            headerRange.Style.Fill.BackgroundColor = XLColor.FromHtml("#1d4f91");
            headerRange.Style.Font.FontColor = XLColor.White;

            ws.Cell(2, 1).Value = "Asha Verma";
            ws.Cell(2, 2).Value = "Sales Officer";
            ws.Cell(2, 3).Value = "asha.verma@example.com";
            ws.Cell(2, 4).Value = "manager@example.com";
            ws.Row(2).Style.Font.FontColor = XLColor.FromHtml("#7b8394");

            ws.Cell(4, 1).Value = "Instructions";
            ws.Cell(4, 1).Style.Font.Bold = true;
            ws.Cell(5, 1).Value = "Name and Designation are required, Email must be unique, ManagerEmail is optional and must match another row or an existing account.";
            ws.Cell(6, 1).Value = "Delete the example row before uploading. Never include passwords in this file.";
            ws.Column(1).Width = 28;
            ws.Column(2).Width = 22;
            ws.Column(3).Width = 34;
            ws.Column(4).Width = 34;

            using var stream = new MemoryStream();
            workbook.SaveAs(stream);
            return stream.ToArray();
        }

        private static string CacheKey(string key) => "bulk-upload:" + key;

        private static BulkUploadParseResult Fail(string message) =>
            new() { Success = false, FileError = message };
    }
}
