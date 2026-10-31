using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;
using SalesHierarchy.Core.Interfaces.Repositories;
using SalesHierarchy.Core.Models;
using SalesHierarchy.Core.Validation;
using SalesHierarchy.Web.Data;
using SalesHierarchy.Web.Services;
using SalesHierarchy.Web.ViewModels;

namespace SalesHierarchy.Web.Controllers
{
    [Authorize(Roles = "SystemAdmin")]
    public class AdminController : Controller
    {
        private readonly IPodStationRepository _podStationRepository;
        private readonly ISalesPersonRepository _salesPersonRepository;
        private readonly IPodRepository _podRepository;
        private readonly IDealerRepository _dealerRepository;
        private readonly IDealerMappingRepository _mappingRepository;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IEmailSender _emailSender;
        private readonly BulkUploadService _bulkUploadService;
        private readonly AuditService _audit;
        private readonly IAuditLogRepository _auditLogRepository;
        private readonly ILogger<AdminController> _logger;

        public AdminController(
            IPodStationRepository podStationRepository,
            ISalesPersonRepository salesPersonRepository,
            IPodRepository podRepository,
            IDealerRepository dealerRepository,
            IDealerMappingRepository mappingRepository,
            UserManager<ApplicationUser> userManager,
            IEmailSender emailSender,
            BulkUploadService bulkUploadService,
            AuditService audit,
            IAuditLogRepository auditLogRepository,
            ILogger<AdminController> logger)
        {
            _podStationRepository = podStationRepository;
            _salesPersonRepository = salesPersonRepository;
            _podRepository = podRepository;
            _dealerRepository = dealerRepository;
            _mappingRepository = mappingRepository;
            _userManager = userManager;
            _emailSender = emailSender;
            _bulkUploadService = bulkUploadService;
            _audit = audit;
            _auditLogRepository = auditLogRepository;
            _logger = logger;
        }

        public IActionResult Index()
        {
            return RedirectToAction(nameof(Dashboard));
        }

        // ---------------------------------------------------------------- dashboard

        public IActionResult Dashboard()
        {
            ViewBag.PodStationCount = _podStationRepository.GetAll().Count();
            ViewBag.PodCount = _podRepository.GetAll().Count();
            ViewBag.SalesPersonCount = _salesPersonRepository.GetAll().Count();
            ViewBag.DealerCount = _dealerRepository.GetAll().Count();
            ViewBag.MappingCount = _mappingRepository.GetAll().Count();
            ViewBag.UserCount = _userManager.Users.Count();
            ViewBag.PendingInvitations = _userManager.Users.Count(u => u.SalesPersonId != null);
            return View();
        }

        public IActionResult Hierarchy(string? q)
        {
            var allPeople = _salesPersonRepository.GetAll().ToList();
            var people = allPeople;

            if (!string.IsNullOrWhiteSpace(q))
            {
                people = allPeople
                    .Where(p =>
                        p.Name.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                        (p.Designation?.Contains(q, StringComparison.OrdinalIgnoreCase) ?? false))
                    .ToList();
            }

            ViewBag.Query = q;
            ViewBag.TotalPeople = allPeople.Count;
            ViewBag.VisiblePeople = people.Count;
            ViewBag.People = people;
            ViewBag.PodCount = _podRepository.GetAll().Count();
            ViewBag.PodStationCount = _podStationRepository.GetAll().Count();
            ViewBag.Roots = BuildHierarchy(people);
            return View();
        }

        private static List<HierarchyNode> BuildHierarchy(IReadOnlyCollection<SalesPerson> people)
        {
            var byId = people.ToDictionary(p => p.Id);
            var childrenByManager = people
                .Where(p => p.ManagerId.HasValue && byId.ContainsKey(p.ManagerId.Value))
                .GroupBy(p => p.ManagerId!.Value)
                .ToDictionary(g => g.Key, g => g.OrderBy(p => p.Name).ToList());

            var roots = people
                .Where(p => !p.ManagerId.HasValue || !byId.ContainsKey(p.ManagerId.Value))
                .OrderBy(p => p.Name)
                .ToList();

            var visited = new HashSet<int>();

            HierarchyNode Build(SalesPerson person)
            {
                var node = new HierarchyNode { Person = person };
                if (!visited.Add(person.Id) || !childrenByManager.TryGetValue(person.Id, out var children))
                {
                    return node;
                }

                node.Children = children.Select(Build).ToList();
                return node;
            }

            return roots.Select(Build).ToList();
        }

        // --------------------------------------------------------------- audit trail

        public IActionResult AuditLog(string? entity, string? actionFilter, string? q)
        {
            ViewBag.Entity = entity;
            ViewBag.Action = actionFilter;
            ViewBag.Query = q;
            ViewBag.Entities = new[]
            {
                "PodStation", "SalesPerson", "Pod", "Dealer", "DealerMapping", "UserAccount", "BulkUpload", "Database"
            };
            ViewBag.Actions = new[]
            {
                "Create", "Update", "Delete", "InviteResent", "Disable", "Enable", "Import", "AddMapping", "RemoveMapping", "AssignPod", "PasswordSet", "Seed"
            };

            try
            {
                return View(_auditLogRepository.GetRecent(300, entity, actionFilter, q));
            }
            catch
            {
                // History must never take the admin area down, e.g. before AuditLog.sql is deployed.
                ViewBag.LogUnavailable = true;
                return View(Array.Empty<AuditLog>());
            }
        }

        // ---------------------------------------------------------------- accounts

        public async Task<IActionResult> UserAccountManagement()
        {
            var people = _salesPersonRepository.GetAll().GroupBy(p => p.Id).ToDictionary(g => g.Key, g => g.First());
            var users = _userManager.Users.OrderBy(u => u.Email).ToList();
            var rows = new List<UserAccountRow>();

            foreach (var user in users)
            {
                var hasPassword = await _userManager.HasPasswordAsync(user);
                rows.Add(new UserAccountRow
                {
                    User = user,
                    HasPassword = hasPassword,
                    IsDisabled = user.LockoutEnd.HasValue && user.LockoutEnd.Value > DateTimeOffset.UtcNow,
                    SalesPersonName = user.SalesPersonId.HasValue && people.TryGetValue(user.SalesPersonId.Value, out var person)
                        ? person.Name
                        : null
                });
            }

            ViewBag.People = people.ToDictionary(p => p.Key, p => p.Value.Name);
            return View(rows);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResendInvitation(string userId)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null || string.IsNullOrWhiteSpace(user.Email))
            {
                return NotFound();
            }

            var token = await _userManager.GeneratePasswordResetTokenAsync(user);
            var callbackUrl = Url.Action("ResetPassword", "Account", new { token, email = user.Email }, Request.Scheme)!;

            try
            {
                await _emailSender.SendEmailAsync(
                    user.Email,
                    "Set your SalesHierarchy password",
                    $"<p>An administrator has re-sent your invitation.</p><p><a href=\"{System.Net.WebUtility.HtmlEncode(callbackUrl)}\">Set my password</a></p>");
                TempData["SuccessMessage"] = $"Invitation e-mail sent to {user.Email}.";
                _audit.Record("InviteResent", "UserAccount", user.Id, $"Invitation re-sent to {user.Email}.", new { Outcome = "Sent" }, relatedSalesPersonId: user.SalesPersonId);
            }
            catch
            {
                TempData["ErrorMessage"] = $"The invitation e-mail could not be sent to {user.Email}. Check the e-mail configuration and try again.";
                _audit.Record("InviteResent", "UserAccount", user.Id, $"Invitation re-send failed for {user.Email}.", new { Outcome = "Failed" }, relatedSalesPersonId: user.SalesPersonId);
            }

            return RedirectToAction(nameof(UserAccountManagement));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DisableAccount(string userId)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
            {
                return NotFound();
            }

            if (await _userManager.IsInRoleAsync(user, "SystemAdmin"))
            {
                TempData["ErrorMessage"] = "System administrator accounts cannot be disabled from this screen.";
                return RedirectToAction(nameof(UserAccountManagement));
            }

            user.LockoutEnabled = true;
            user.LockoutEnd = DateTimeOffset.UtcNow.AddYears(100);
            await _userManager.UpdateAsync(user);
            _audit.Record("Disable", "UserAccount", user.Id, $"Account {user.Email} disabled.", relatedSalesPersonId: user.SalesPersonId);
            TempData["SuccessMessage"] = $"Account {user.Email} disabled.";
            return RedirectToAction(nameof(UserAccountManagement));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EnableAccount(string userId)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
            {
                return NotFound();
            }

            user.LockoutEnd = null;
            await _userManager.UpdateAsync(user);
            _audit.Record("Enable", "UserAccount", user.Id, $"Account {user.Email} enabled.", relatedSalesPersonId: user.SalesPersonId);
            TempData["SuccessMessage"] = $"Account {user.Email} enabled.";
            return RedirectToAction(nameof(UserAccountManagement));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteAccount(string userId)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
            {
                return NotFound();
            }

            if (await _userManager.IsInRoleAsync(user, "SystemAdmin"))
            {
                TempData["ErrorMessage"] = "System administrator accounts cannot be deleted from this screen.";
                return RedirectToAction(nameof(UserAccountManagement));
            }

            var personId = user.SalesPersonId;
            var result = await _userManager.DeleteAsync(user);
            if (!result.Succeeded)
            {
                TempData["ErrorMessage"] = "The account could not be deleted: " + string.Join("; ", result.Errors.Select(e => e.Description));
                return RedirectToAction(nameof(UserAccountManagement));
            }

            _audit.Record("Delete", "UserAccount", user.Id, $"Account {user.Email} deleted.", new { user.Email, user.SalesPersonId }, relatedSalesPersonId: personId);
            TempData["SuccessMessage"] = personId.HasValue
                ? $"Account {user.Email} deleted. The linked salesperson record is now free of account dependencies."
                : $"Account {user.Email} deleted.";
            return RedirectToAction(nameof(UserAccountManagement));
        }

        private void LoadManagers(int? excludeId = null)
        {
            var managers = _salesPersonRepository.GetAll()
                .Where(p => p.Id != excludeId)
                .OrderBy(p => p.Name)
                .ToList();
            ViewBag.Managers = managers;
        }

        // ------------------------------------------------------ individual accounts

        public IActionResult CreateAccount()
        {
            LoadManagers();
            return View(new CreateAccountViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateAccount(CreateAccountViewModel model)
        {
            model.Name = model.Name?.Trim() ?? string.Empty;
            model.Designation = model.Designation?.Trim() ?? string.Empty;
            model.Email = model.Email?.Trim() ?? string.Empty;

            if (!ModelState.IsValid)
            {
                LoadManagers();
                return View(model);
            }

            if (await _userManager.FindByEmailAsync(model.Email) != null)
            {
                ModelState.AddModelError(nameof(model.Email), "An account with this email already exists.");
                LoadManagers();
                return View(model);
            }

            if (model.ManagerId.HasValue && _salesPersonRepository.GetById(model.ManagerId.Value) == null)
            {
                ModelState.AddModelError(nameof(model.ManagerId), "The selected reporting manager does not exist.");
                LoadManagers();
                return View(model);
            }

            var personCreatedId = 0;
            ApplicationUser? userCreated = null;

            try
            {
                var person = new SalesPerson
                {
                    Name = model.Name,
                    Designation = model.Designation,
                    ManagerId = model.ManagerId
                };

                personCreatedId = _salesPersonRepository.Add(person);

                var user = new ApplicationUser
                {
                    UserName = model.Email,
                    Email = model.Email,
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
                var callbackUrl = Url.Action("ResetPassword", "Account", new { token, email = user.Email }, Request.Scheme)!;

                var invitationStatus = "Sent";
                try
                {
                    await _emailSender.SendEmailAsync(
                        user.Email!,
                        "Set your SalesHierarchy password",
                        $"<p>Welcome to SalesHierarchy.</p><p><a href=\"{System.Net.WebUtility.HtmlEncode(callbackUrl)}\">Set my password</a></p>");
                    TempData["SuccessMessage"] = $"Salesperson '{model.Name}' created and an invitation e-mail was sent to {model.Email}.";
                }
                catch
                {
                    invitationStatus = "Failed";
                    TempData["SuccessMessage"] = $"Salesperson '{model.Name}' created, but the invitation e-mail could not be sent. Use 'Resend invitation' from User Accounts once e-mail is configured.";
                }

                _audit.Record("Create", "SalesPerson", personCreatedId.ToString(), $"Salesperson '{model.Name}' created with a login account.", new { model.Name, model.Designation, model.ManagerId }, relatedSalesPersonId: personCreatedId);
                _audit.Record("Create", "UserAccount", user.Id, $"Account {user.Email} created for salesperson '{model.Name}'.", new { user.Email, SalesPersonId = personCreatedId, Invitation = invitationStatus }, relatedSalesPersonId: personCreatedId);

                return RedirectToAction("Index", "AdminSalesPerson");
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

                ModelState.AddModelError(string.Empty, "Account creation failed: " + ex.Message);
                LoadManagers();
                return View(model);
            }
        }

        // --------------------------------------------------------------- bulk upload

        public IActionResult BulkUpload()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult BulkUpload(IFormFile? file)
        {
            if (file == null || file.Length == 0)
            {
                ModelState.AddModelError(string.Empty, "Please choose an .xlsx file to upload.");
                return View();
            }

            BulkUploadParseResult result;
            using (var stream = file.OpenReadStream())
            {
                result = _bulkUploadService.ParseAndValidate(stream, file.FileName, file.Length);
            }

            if (!result.Success || result.Batch == null)
            {
                ModelState.AddModelError(string.Empty, result.FileError ?? "The file could not be processed.");
                return View();
            }

            _bulkUploadService.Stage(result.Batch);
            return RedirectToAction(nameof(BulkPreview), new { key = result.Batch.Key });
        }

        public IActionResult BulkPreview(string key)
        {
            var batch = _bulkUploadService.Get(key);
            if (batch == null)
            {
                TempData["ErrorMessage"] = "The upload session has expired. Please upload the file again.";
                return RedirectToAction(nameof(BulkUpload));
            }

            return View(batch);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ConfirmBulkUpload(string key)
        {
            var batch = _bulkUploadService.Get(key);
            if (batch == null)
            {
                TempData["ErrorMessage"] = "The upload session has expired. Please upload the file again.";
                return RedirectToAction(nameof(BulkUpload));
            }

            var importedNow = false;
            if (!batch.IsImported)
            {
                var callback = (string token, string email) =>
                    Url.Action("ResetPassword", "Account", new { token, email }, Request.Scheme) ?? string.Empty;

                await _bulkUploadService.ImportAsync(batch, callback);
                importedNow = true;
            }

            if (importedNow)
            {
                var imported = batch.Rows.Count(r => r.Status == BulkRowStatus.Imported);
                var failed = batch.Rows.Count(r => r.Status == BulkRowStatus.Failed);
                var skipped = batch.Rows.Count(r => r.Status is BulkRowStatus.Duplicate or BulkRowStatus.Invalid or BulkRowStatus.Skipped);
                var invitationsSent = batch.Rows.Count(r => r.InvitationStatus == "Sent");

                _audit.Record(
                    "Import",
                    "BulkUpload",
                    batch.Key,
                    $"Bulk import of '{batch.FileName}' completed: {imported} imported, {skipped} skipped, {failed} failed.",
                    new
                    {
                        batch.FileName,
                        TotalRows = batch.Rows.Count,
                        Imported = imported,
                        Skipped = skipped,
                        Failed = failed,
                        InvitationsSent = invitationsSent
                    });
            }

            return RedirectToAction(nameof(BulkResult), new { key = batch.Key });
        }

        public IActionResult BulkResult(string key)
        {
            var batch = _bulkUploadService.Get(key);
            if (batch == null)
            {
                TempData["ErrorMessage"] = "The upload session has expired. Please upload the file again.";
                return RedirectToAction(nameof(BulkUpload));
            }

            return View(batch);
        }

        public IActionResult DownloadErrorReport(string key)
        {
            var batch = _bulkUploadService.Get(key);
            if (batch == null)
            {
                return NotFound();
            }

            var content = _bulkUploadService.BuildErrorReport(batch);
            return File(content, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"salesperson-upload-results-{DateTime.UtcNow:yyyyMMdd-HHmm}.xlsx");
        }

        public IActionResult DownloadTemplate()
        {
            var content = _bulkUploadService.BuildTemplate();
            return File(content, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "SalesPersonTemplate.xlsx");
        }
    }
}
