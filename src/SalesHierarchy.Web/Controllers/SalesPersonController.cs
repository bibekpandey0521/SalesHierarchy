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
    /// <summary>
    /// Personal workspace and hierarchy-scoped team management.
    ///
    /// A SalesPerson with direct reports acts as a manager. Managers may manage only
    /// themselves and their descendants; only a SystemAdmin can create or change the
    /// reporting manager of a salesperson. Every write is scope-checked again on POST.
    /// </summary>
    [Authorize(Roles = "SalesPerson")]
    public class SalesPersonController : Controller
    {
        private readonly ISalesPersonRepository _salesPersonRepository;
        private readonly IPodRepository _podRepository;
        private readonly IPodStationRepository _podStationRepository;
        private readonly IDealerMappingRepository _mappingRepository;
        private readonly IDealerRepository _dealerRepository;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IEmailSender _emailSender;
        private readonly IAuditLogRepository _auditLogRepository;
        private readonly AuditService _audit;

        public SalesPersonController(
            ISalesPersonRepository salesPersonRepository,
            IPodRepository podRepository,
            IPodStationRepository podStationRepository,
            IDealerMappingRepository mappingRepository,
            IDealerRepository dealerRepository,
            UserManager<ApplicationUser> userManager,
            IEmailSender emailSender,
            IAuditLogRepository auditLogRepository,
            AuditService audit)
        {
            _salesPersonRepository = salesPersonRepository;
            _podRepository = podRepository;
            _podStationRepository = podStationRepository;
            _mappingRepository = mappingRepository;
            _dealerRepository = dealerRepository;
            _userManager = userManager;
            _emailSender = emailSender;
            _auditLogRepository = auditLogRepository;
            _audit = audit;
        }

        public async Task<IActionResult> Dashboard()
        {
            var person = await GetCurrentSalesPersonAsync();
            if (person == null)
            {
                return View(null);
            }

            var all = _salesPersonRepository.GetAll().ToList();
            var reports = HierarchyRules.GetTeamMembers(person.Id, all);

            ViewBag.PodCount = _podRepository.GetBySalesPersonId(person.Id).Count();
            ViewBag.DealerCount = _mappingRepository.GetBySalesPersonId(person.Id).Count();
            ViewBag.TeamCount = reports.Count;
            ViewBag.Manager = person.ManagerId.HasValue ? all.FirstOrDefault(p => p.Id == person.ManagerId.Value) : null;

            return View(person);
        }

        public async Task<IActionResult> MyProfile()
        {
            var person = await GetCurrentSalesPersonAsync();
            if (person == null)
            {
                return View(null);
            }

            var all = _salesPersonRepository.GetAll().ToList();
            ViewBag.Manager = person.ManagerId.HasValue ? all.FirstOrDefault(p => p.Id == person.ManagerId.Value) : null;
            ViewBag.ManagerChain = HierarchyRules.GetManagers(person.Id, all);
            ViewBag.DirectReports = _salesPersonRepository.GetDirectReports(person.Id).ToList();
            return View(person);
        }

        public async Task<IActionResult> MyPods()
        {
            var person = await GetCurrentSalesPersonAsync();
            if (person == null)
            {
                return View(new List<Pod>());
            }

            return View(_podRepository.GetBySalesPersonId(person.Id).ToList());
        }

        public async Task<IActionResult> MyDealers()
        {
            var person = await GetCurrentSalesPersonAsync();
            if (person == null)
            {
                return View(new List<Dealer>());
            }

            var dealerIds = _mappingRepository.GetBySalesPersonId(person.Id).Select(m => m.DealerId).ToHashSet();
            return View(_dealerRepository.GetAll().Where(d => dealerIds.Contains(d.UniqueID)).ToList());
        }

        // ------------------------------------------------------------ team members

        public async Task<IActionResult> MyTeam()
        {
            var person = await GetCurrentSalesPersonAsync();
            if (person == null)
            {
                ViewBag.IsManager = false;
                return View(new List<SalesPerson>());
            }

            var all = _salesPersonRepository.GetAll().ToList();
            var team = HierarchyRules.GetTeamMembers(person.Id, all);
            var teamIds = team.Select(p => p.Id).ToHashSet();

            var accounts = _userManager.Users
                .Where(u => u.SalesPersonId.HasValue && teamIds.Contains(u.SalesPersonId.Value))
                .ToList()
                .GroupBy(u => u.SalesPersonId!.Value)
                .ToDictionary(g => g.Key, g => g.First());
            var podCounts = _salesPersonRepository.GetAllPodAssignments()
                .Where(a => teamIds.Contains(a.SalesPersonId))
                .GroupBy(a => a.SalesPersonId)
                .ToDictionary(g => g.Key, g => g.Select(a => a.PodId).Distinct().Count());
            var mappingCounts = _mappingRepository.GetAll()
                .Where(m => teamIds.Contains(m.SalesPersonId))
                .GroupBy(m => m.SalesPersonId)
                .ToDictionary(g => g.Key, g => g.Count());
            var directReportCounts = all
                .Where(p => p.ManagerId.HasValue)
                .GroupBy(p => p.ManagerId!.Value)
                .ToDictionary(g => g.Key, g => g.Count());

            ViewBag.Current = person;
            ViewBag.People = all.GroupBy(p => p.Id).ToDictionary(g => g.Key, g => g.First());
            ViewBag.Accounts = accounts;
            ViewBag.PodCounts = podCounts;
            ViewBag.MappingCounts = mappingCounts;
            ViewBag.DirectReportCounts = directReportCounts;
            ViewBag.IsManager = team.Count > 0;

            return View(team);
        }

        public async Task<IActionResult> CreateTeamMember()
        {
            var person = await GetCurrentSalesPersonAsync();
            if (person == null)
            {
                return Forbid();
            }

            if (!IsManager(person))
            {
                TempData["ErrorMessage"] = "Only managers can add team members. Ask a System Administrator to assign reports to you first.";
                return RedirectToAction(nameof(MyTeam));
            }

            ViewBag.ManagerName = person.Name;
            return View(new SalesPerson { ManagerId = person.Id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateTeamMember(SalesPerson model)
        {
            var person = await GetCurrentSalesPersonAsync();
            if (person == null)
            {
                return Forbid();
            }

            if (!IsManager(person))
            {
                TempData["ErrorMessage"] = "Only managers can add team members. Ask a System Administrator to assign reports to you first.";
                return RedirectToAction(nameof(MyTeam));
            }

            model.Id = 0;
            model.Name = model.Name?.Trim() ?? string.Empty;
            model.Designation = string.IsNullOrWhiteSpace(model.Designation) ? null : model.Designation.Trim();
            // Reporting managers are always assigned by a SystemAdmin. A manager can only
            // add a person into their own team, never into another team or above themselves.
            model.ManagerId = person.Id;

            if (!ModelState.IsValid)
            {
                ViewBag.ManagerName = person.Name;
                return View(model);
            }

            var id = _salesPersonRepository.Add(model);
            _audit.Record(
                "Create",
                "SalesPerson",
                id.ToString(),
                $"Team member '{model.Name}' created in {person.Name}'s team.",
                model,
                relatedSalesPersonId: id);

            TempData["SuccessMessage"] = $"Team member '{model.Name}' added. Create a login account when they are ready to sign in.";
            return RedirectToAction(nameof(MyTeam));
        }

        public async Task<IActionResult> EditTeamMember(int id)
        {
            var person = await GetCurrentSalesPersonAsync();
            if (person == null)
            {
                return Forbid();
            }

            var member = _salesPersonRepository.GetById(id);
            if (member == null)
            {
                return NotFound();
            }

            if (!CanManage(person, id))
            {
                return Forbid();
            }

            ViewBag.ManagerName = GetManagerName(member.ManagerId);
            return View(member);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditTeamMember(int id, SalesPerson model)
        {
            var person = await GetCurrentSalesPersonAsync();
            if (person == null)
            {
                return Forbid();
            }

            var existing = _salesPersonRepository.GetById(id);
            if (existing == null)
            {
                return NotFound();
            }

            if (!CanManage(person, id))
            {
                return Forbid();
            }

            model.Id = id;
            model.Name = model.Name?.Trim() ?? string.Empty;
            model.Designation = string.IsNullOrWhiteSpace(model.Designation) ? null : model.Designation.Trim();
            // Keep the existing manager. Changing a reporting line is an admin operation.
            model.ManagerId = existing.ManagerId;

            if (!ModelState.IsValid)
            {
                ViewBag.ManagerName = GetManagerName(existing.ManagerId);
                return View(model);
            }

            if (!_salesPersonRepository.Update(model))
            {
                ModelState.AddModelError(string.Empty, "The team member could not be updated. It may have been deleted already.");
                ViewBag.ManagerName = GetManagerName(existing.ManagerId);
                return View(model);
            }

            _audit.Record(
                "Update",
                "SalesPerson",
                model.Id.ToString(),
                $"Team member '{model.Name}' updated by {person.Name}.",
                new { Before = existing, After = model },
                relatedSalesPersonId: model.Id);

            TempData["SuccessMessage"] = $"Team member '{model.Name}' updated.";
            return RedirectToAction(nameof(MyTeam));
        }

        public async Task<IActionResult> DeleteTeamMember(int id)
        {
            var person = await GetCurrentSalesPersonAsync();
            if (person == null)
            {
                return Forbid();
            }

            var member = _salesPersonRepository.GetById(id);
            if (member == null)
            {
                return NotFound();
            }

            if (!CanManage(person, id))
            {
                return Forbid();
            }

            ViewBag.Dependencies = GetSalesPersonDependencies(id);
            return View(member);
        }

        [HttpPost, ActionName("DeleteTeamMember")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteTeamMemberConfirmed(int id)
        {
            var person = await GetCurrentSalesPersonAsync();
            if (person == null)
            {
                return Forbid();
            }

            var member = _salesPersonRepository.GetById(id);
            if (member == null)
            {
                return NotFound();
            }

            if (!CanManage(person, id))
            {
                return Forbid();
            }

            var dependencies = GetSalesPersonDependencies(id);
            if (dependencies.Count > 0)
            {
                TempData["ErrorMessage"] = $"'{member.Name}' cannot be deleted. {string.Join(" ", dependencies)}";
                return RedirectToAction(nameof(MyTeam));
            }

            try
            {
                if (!_salesPersonRepository.Delete(id))
                {
                    TempData["ErrorMessage"] = $"'{member.Name}' could not be deleted. It may have been deleted already.";
                    return RedirectToAction(nameof(MyTeam));
                }

                _audit.Record(
                    "Delete",
                    "SalesPerson",
                    member.Id.ToString(),
                    $"Team member '{member.Name}' deleted by {person.Name}.",
                    member,
                    relatedSalesPersonId: member.Id);

                TempData["SuccessMessage"] = $"Team member '{member.Name}' deleted.";
            }
            catch
            {
                TempData["ErrorMessage"] = $"'{member.Name}' could not be deleted because related records still reference them.";
            }

            return RedirectToAction(nameof(MyTeam));
        }

        // ----------------------------------------------------------- team accounts

        public async Task<IActionResult> TeamAccounts()
        {
            var person = await GetCurrentSalesPersonAsync();
            if (person == null)
            {
                ViewBag.IsManager = false;
                ViewBag.Unlinked = new List<SalesPerson>();
                return View(new List<UserAccountRow>());
            }

            var team = HierarchyRules.GetTeamMembers(person.Id, _salesPersonRepository.GetAll()).ToList();
            var teamIds = team.Select(p => p.Id).ToHashSet();
            var users = _userManager.Users
                .Where(u => u.SalesPersonId.HasValue && teamIds.Contains(u.SalesPersonId.Value))
                .OrderBy(u => u.Email)
                .ToList();

            var rows = new List<UserAccountRow>();
            foreach (var user in users)
            {
                rows.Add(new UserAccountRow
                {
                    User = user,
                    HasPassword = await _userManager.HasPasswordAsync(user),
                    IsDisabled = user.LockoutEnd.HasValue && user.LockoutEnd.Value > DateTimeOffset.UtcNow,
                    SalesPersonName = team.FirstOrDefault(p => p.Id == user.SalesPersonId)?.Name
                });
            }

            var linkedIds = users.Where(u => u.SalesPersonId.HasValue).Select(u => u.SalesPersonId!.Value).ToHashSet();
            ViewBag.IsManager = team.Count > 0;
            ViewBag.Current = person;
            ViewBag.Unlinked = team.Where(p => !linkedIds.Contains(p.Id)).OrderBy(p => p.Name).ToList();
            return View(rows);
        }

        public async Task<IActionResult> CreateTeamAccount(int salespersonId)
        {
            var person = await GetCurrentSalesPersonAsync();
            if (person == null)
            {
                return Forbid();
            }

            var member = _salesPersonRepository.GetById(salespersonId);
            if (member == null)
            {
                return NotFound();
            }

            if (!CanManage(person, salespersonId))
            {
                return Forbid();
            }

            if (_userManager.Users.Any(u => u.SalesPersonId == salespersonId))
            {
                TempData["ErrorMessage"] = $"{member.Name} already has a login account.";
                return RedirectToAction(nameof(TeamAccounts));
            }

            ViewBag.Person = member;
            return View(new CreateTeamAccountViewModel { SalesPersonId = member.Id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateTeamAccount(CreateTeamAccountViewModel model)
        {
            var person = await GetCurrentSalesPersonAsync();
            if (person == null)
            {
                return Forbid();
            }

            var member = _salesPersonRepository.GetById(model.SalesPersonId);
            if (member == null)
            {
                return NotFound();
            }

            if (!CanManage(person, member.Id))
            {
                return Forbid();
            }

            ViewBag.Person = member;
            model.Email = model.Email?.Trim() ?? string.Empty;

            if (_userManager.Users.Any(u => u.SalesPersonId == member.Id))
            {
                ModelState.AddModelError(string.Empty, $"{member.Name} already has a login account.");
            }

            if (await _userManager.FindByEmailAsync(model.Email) != null)
            {
                ModelState.AddModelError(nameof(model.Email), "An account with this email already exists.");
            }

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            ApplicationUser? userCreated = null;
            try
            {
                var user = new ApplicationUser
                {
                    UserName = model.Email,
                    Email = model.Email,
                    EmailConfirmed = true,
                    SalesPersonId = member.Id
                };

                var createResult = await _userManager.CreateAsync(user);
                if (!createResult.Succeeded)
                {
                    throw new InvalidOperationException(string.Join("; ", createResult.Errors.Select(e => e.Description)));
                }

                userCreated = user;
                var roleResult = await _userManager.AddToRoleAsync(user, "SalesPerson");
                if (!roleResult.Succeeded)
                {
                    throw new InvalidOperationException(string.Join("; ", roleResult.Errors.Select(e => e.Description)));
                }

                var token = await _userManager.GeneratePasswordResetTokenAsync(user);
                var callbackUrl = Url.Action("ResetPassword", "Account", new { token, email = user.Email }, Request.Scheme)!;

                var invitationStatus = "Sent";
                try
                {
                    await _emailSender.SendEmailAsync(
                        user.Email!,
                        "Set your SalesHierarchy password",
                        $"<p>Welcome to SalesHierarchy.</p><p><a href=\"{System.Net.WebUtility.HtmlEncode(callbackUrl)}\">Set my password</a></p>");
                }
                catch
                {
                    invitationStatus = "Failed";
                }

                _audit.Record(
                    "Create",
                    "UserAccount",
                    user.Id,
                    $"Account {user.Email} created for team member '{member.Name}'.",
                    new { user.Email, SalesPersonId = member.Id, Invitation = invitationStatus },
                    relatedSalesPersonId: member.Id);

                TempData["SuccessMessage"] = invitationStatus == "Sent"
                    ? $"Login account created and an invitation e-mail was sent to {model.Email}."
                    : $"Login account created for {member.Name}, but the invitation e-mail could not be sent. Use 'Resend invitation' once e-mail is configured.";
                return RedirectToAction(nameof(TeamAccounts));
            }
            catch (Exception ex)
            {
                if (userCreated != null)
                {
                    try { await _userManager.DeleteAsync(userCreated); } catch { /* best-effort compensation */ }
                }

                ModelState.AddModelError(string.Empty, "Account creation failed: " + ex.Message);
                return View(model);
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResendTeamInvitation(string userId)
        {
            var person = await GetCurrentSalesPersonAsync();
            if (person == null)
            {
                return Forbid();
            }

            var (user, error) = await GetManagedTeamAccountAsync(userId, person);
            if (user == null)
            {
                TempData["ErrorMessage"] = error;
                return RedirectToAction(nameof(TeamAccounts));
            }

            var token = await _userManager.GeneratePasswordResetTokenAsync(user);
            var callbackUrl = Url.Action("ResetPassword", "Account", new { token, email = user.Email }, Request.Scheme)!;

            try
            {
                await _emailSender.SendEmailAsync(
                    user.Email!,
                    "Set your SalesHierarchy password",
                    $"<p>Your manager has re-sent your invitation.</p><p><a href=\"{System.Net.WebUtility.HtmlEncode(callbackUrl)}\">Set my password</a></p>");

                _audit.Record(
                    "InviteResent",
                    "UserAccount",
                    user.Id,
                    $"Invitation re-sent to {user.Email} by {person.Name}.",
                    new { Outcome = "Sent" },
                    relatedSalesPersonId: user.SalesPersonId);

                TempData["SuccessMessage"] = $"Invitation e-mail sent to {user.Email}.";
            }
            catch
            {
                _audit.Record(
                    "InviteResent",
                    "UserAccount",
                    user.Id,
                    $"Invitation re-send failed for {user.Email} by {person.Name}.",
                    new { Outcome = "Failed" },
                    relatedSalesPersonId: user.SalesPersonId);

                TempData["ErrorMessage"] = $"The invitation e-mail could not be sent to {user.Email}. Check the e-mail configuration and try again.";
            }

            return RedirectToAction(nameof(TeamAccounts));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DisableTeamAccount(string userId)
        {
            var person = await GetCurrentSalesPersonAsync();
            if (person == null)
            {
                return Forbid();
            }

            var (user, error) = await GetManagedTeamAccountAsync(userId, person);
            if (user == null)
            {
                TempData["ErrorMessage"] = error;
                return RedirectToAction(nameof(TeamAccounts));
            }

            user.LockoutEnabled = true;
            user.LockoutEnd = DateTimeOffset.UtcNow.AddYears(100);
            var result = await _userManager.UpdateAsync(user);
            if (!result.Succeeded)
            {
                TempData["ErrorMessage"] = "The account could not be disabled: " + string.Join("; ", result.Errors.Select(e => e.Description));
                return RedirectToAction(nameof(TeamAccounts));
            }

            _audit.Record(
                "Disable",
                "UserAccount",
                user.Id,
                $"Account {user.Email} disabled by {person.Name}.",
                relatedSalesPersonId: user.SalesPersonId);

            TempData["SuccessMessage"] = $"Account {user.Email} disabled.";
            return RedirectToAction(nameof(TeamAccounts));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EnableTeamAccount(string userId)
        {
            var person = await GetCurrentSalesPersonAsync();
            if (person == null)
            {
                return Forbid();
            }

            var (user, error) = await GetManagedTeamAccountAsync(userId, person);
            if (user == null)
            {
                TempData["ErrorMessage"] = error;
                return RedirectToAction(nameof(TeamAccounts));
            }

            user.LockoutEnd = null;
            var result = await _userManager.UpdateAsync(user);
            if (!result.Succeeded)
            {
                TempData["ErrorMessage"] = "The account could not be enabled: " + string.Join("; ", result.Errors.Select(e => e.Description));
                return RedirectToAction(nameof(TeamAccounts));
            }

            _audit.Record(
                "Enable",
                "UserAccount",
                user.Id,
                $"Account {user.Email} enabled by {person.Name}.",
                relatedSalesPersonId: user.SalesPersonId);

            TempData["SuccessMessage"] = $"Account {user.Email} enabled.";
            return RedirectToAction(nameof(TeamAccounts));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteTeamAccount(string userId)
        {
            var person = await GetCurrentSalesPersonAsync();
            if (person == null)
            {
                return Forbid();
            }

            var (user, error) = await GetManagedTeamAccountAsync(userId, person);
            if (user == null)
            {
                TempData["ErrorMessage"] = error;
                return RedirectToAction(nameof(TeamAccounts));
            }

            var personId = user.SalesPersonId;
            var email = user.Email;
            var result = await _userManager.DeleteAsync(user);
            if (!result.Succeeded)
            {
                TempData["ErrorMessage"] = "The account could not be deleted: " + string.Join("; ", result.Errors.Select(e => e.Description));
                return RedirectToAction(nameof(TeamAccounts));
            }

            _audit.Record(
                "Delete",
                "UserAccount",
                user.Id,
                $"Account {email} deleted by {person.Name}. The salesperson record remains.",
                new { Email = email, user.SalesPersonId },
                relatedSalesPersonId: personId);

            TempData["SuccessMessage"] = $"Account {email} deleted. The salesperson record is still in your team.";
            return RedirectToAction(nameof(TeamAccounts));
        }

        // --------------------------------------------------------------- team Pods

        public async Task<IActionResult> TeamPods()
        {
            var person = await GetCurrentSalesPersonAsync();
            if (person == null)
            {
                ViewBag.IsManager = false;
                return View(new List<Pod>());
            }

            var all = _salesPersonRepository.GetAll().ToList();
            var team = HierarchyRules.GetTeamMembers(person.Id, all);
            var teamIds = new HashSet<int>(team.Select(p => p.Id)) { person.Id };

            // SalesPersonPod is the source of truth for ownership. A Pod can have more
            // than one owner, so collect every team assignment and group it by Pod.
            var assignments = _salesPersonRepository.GetAllPodAssignments()
                .Where(a => teamIds.Contains(a.SalesPersonId))
                .ToList();
            var ownedPodIds = assignments.Select(a => a.PodId).ToHashSet();
            var pods = _podRepository.GetAll()
                .Where(p => ownedPodIds.Contains(p.Id))
                .OrderBy(p => p.Name)
                .ToList();
            var podOwners = assignments
                .GroupBy(a => a.PodId)
                .ToDictionary(
                    g => g.Key,
                    g => g.Select(a => a.SalesPersonId).Distinct().OrderBy(id => id).ToList());

            ViewBag.Current = person;
            ViewBag.IsManager = team.Count > 0;
            ViewBag.People = all.GroupBy(p => p.Id).ToDictionary(g => g.Key, g => g.First());
            ViewBag.Team = all
                .Where(p => teamIds.Contains(p.Id))
                .OrderBy(p => p.Name)
                .ToList();
            ViewBag.Stations = _podStationRepository.GetAll().GroupBy(s => s.Id).ToDictionary(g => g.Key, g => g.First());
            ViewBag.PodOwners = podOwners;

            return View(pods);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AssignTeamPod(int podId, int? salespersonId)
        {
            var person = await GetCurrentSalesPersonAsync();
            if (person == null)
            {
                return Forbid();
            }

            if (!IsManager(person))
            {
                TempData["ErrorMessage"] = "Only managers can reassign Pods. Ask a System Administrator or your manager to update them.";
                return RedirectToAction(nameof(TeamPods));
            }

            var pod = _podRepository.GetById(podId);
            if (pod == null)
            {
                return NotFound();
            }

            var teamIds = GetTeamIds(person);
            var currentOwners = _salesPersonRepository.GetAllPodAssignments()
                .Where(a => a.PodId == podId)
                .Select(a => a.SalesPersonId)
                .Distinct()
                .ToList();
            if (currentOwners.Count == 0 || !currentOwners.Any(teamIds.Contains))
            {
                TempData["ErrorMessage"] = "You can only reassign Pods that are already assigned within your own team.";
                return RedirectToAction(nameof(TeamPods));
            }

            if (salespersonId.HasValue && !teamIds.Contains(salespersonId.Value))
            {
                TempData["ErrorMessage"] = "The selected salesperson is not part of your team.";
                return RedirectToAction(nameof(TeamPods));
            }

            var newOwners = salespersonId.HasValue
                ? new[] { salespersonId.Value }
                : Array.Empty<int>();
            if (currentOwners.Count == newOwners.Length && currentOwners.All(newOwners.Contains))
            {
                TempData["Message"] = "The Pod assignment is already up to date.";
                return RedirectToAction(nameof(TeamPods));
            }

            try
            {
                _salesPersonRepository.ReplacePodOwners(podId, newOwners);
            }
            catch (Exception)
            {
                TempData["ErrorMessage"] = $"Pod '{pod.Name}' could not be updated. It may have been changed by another user.";
                return RedirectToAction(nameof(TeamPods));
            }

            var previousName = DescribeOwners(currentOwners);
            var newName = DescribeOwners(newOwners);

            _audit.Record(
                "AssignPod",
                "Pod",
                pod.Id.ToString(),
                $"Pod '{pod.Name}' reassigned from {previousName} to {newName} by {person.Name}.",
                new { Before = currentOwners, After = newOwners },
                relatedSalesPersonId: salespersonId ?? currentOwners.FirstOrDefault());

            TempData["SuccessMessage"] = $"Pod '{pod.Name}' assigned to {newName}.";
            return RedirectToAction(nameof(TeamPods));
        }

        // ----------------------------------------------------------- team history

        public async Task<IActionResult> TeamActivity(string? q, string? actionFilter)
        {
            var person = await GetCurrentSalesPersonAsync();
            if (person == null)
            {
                ViewBag.IsManager = false;
                return View(Array.Empty<AuditLog>());
            }

            var all = _salesPersonRepository.GetAll().ToList();
            var team = HierarchyRules.GetTeamMembers(person.Id, all);
            var teamIds = new HashSet<int>(team.Select(p => p.Id)) { person.Id };

            ViewBag.Current = person;
            ViewBag.IsManager = team.Count > 0;
            ViewBag.TeamCount = teamIds.Count;
            ViewBag.Query = q;
            ViewBag.Action = actionFilter;
            ViewBag.Actions = new[]
            {
                "Create", "Update", "Delete", "AddMapping", "RemoveMapping", "AssignPod", "InviteResent", "Disable", "Enable", "PasswordSet"
            };

            if (team.Count == 0)
            {
                return View(Array.Empty<AuditLog>());
            }

            var emails = _userManager.Users
                .Where(u => u.SalesPersonId.HasValue && teamIds.Contains(u.SalesPersonId.Value))
                .Select(u => u.Email!)
                .Where(e => !string.IsNullOrWhiteSpace(e))
                .ToList();

            try
            {
                IEnumerable<AuditLog> entries = _auditLogRepository.GetForTeam(teamIds, emails, 500);

                if (!string.IsNullOrWhiteSpace(actionFilter))
                {
                    entries = entries.Where(e => string.Equals(e.Action, actionFilter, StringComparison.OrdinalIgnoreCase));
                }

                if (!string.IsNullOrWhiteSpace(q))
                {
                    entries = entries.Where(e =>
                        (e.Summary?.Contains(q, StringComparison.OrdinalIgnoreCase) ?? false) ||
                        (e.UserEmail?.Contains(q, StringComparison.OrdinalIgnoreCase) ?? false) ||
                        e.EntityName.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                        (e.EntityId?.Contains(q, StringComparison.OrdinalIgnoreCase) ?? false));
                }

                return View(entries.ToList());
            }
            catch
            {
                ViewBag.LogUnavailable = true;
                return View(Array.Empty<AuditLog>());
            }
        }

        // ------------------------------------------------------ hierarchy / mappings

        public async Task<IActionResult> ReportingHierarchy()
        {
            var person = await GetCurrentSalesPersonAsync();
            if (person == null)
            {
                return View(new List<SalesPerson>());
            }

            var all = _salesPersonRepository.GetAll().ToList();
            ViewBag.Current = person;
            ViewBag.ManagerChain = HierarchyRules.GetManagers(person.Id, all);
            ViewBag.People = all.GroupBy(p => p.Id).ToDictionary(g => g.Key, g => g.First());
            return View(HierarchyRules.GetTeamMembers(person.Id, all));
        }

        public async Task<IActionResult> TeamDealerAssignments()
        {
            var person = await GetCurrentSalesPersonAsync();
            if (person == null)
            {
                return View(new List<DealerMapping>());
            }

            var teamIds = GetTeamIds(person);
            var people = _salesPersonRepository.GetAll().GroupBy(p => p.Id).ToDictionary(g => g.Key, g => g.First());
            var dealers = _dealerRepository.GetAll().GroupBy(d => d.UniqueID).ToDictionary(g => g.Key, g => g.First());

            ViewBag.People = people;
            ViewBag.Dealers = dealers;
            ViewBag.DealerList = dealers.Values.OrderBy(d => d.Name).ToList();
            ViewBag.Team = teamIds
                .Where(id => people.ContainsKey(id))
                .Select(id => people[id])
                .OrderBy(p => p.Name)
                .ToList();
            ViewBag.CurrentId = person.Id;
            ViewBag.IsManager = IsManager(person);

            var mappings = _mappingRepository.GetAll().Where(m => teamIds.Contains(m.SalesPersonId)).ToList();
            return View(mappings);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddTeamMapping(int salespersonId, int dealerId)
        {
            var person = await GetCurrentSalesPersonAsync();
            if (person == null)
            {
                return Forbid();
            }

            if (!IsManager(person))
            {
                TempData["ErrorMessage"] = "Only managers can change dealer assignments. Ask a System Administrator or your manager to update them.";
                return RedirectToAction(nameof(TeamDealerAssignments));
            }

            if (!GetTeamIds(person).Contains(salespersonId))
            {
                TempData["ErrorMessage"] = "You can only manage dealer assignments for your own team.";
                return RedirectToAction(nameof(TeamDealerAssignments));
            }

            if (_dealerRepository.GetById(dealerId) == null)
            {
                TempData["ErrorMessage"] = "The selected dealer does not exist.";
                return RedirectToAction(nameof(TeamDealerAssignments));
            }

            var id = _mappingRepository.Add(new DealerMapping { SalesPersonId = salespersonId, DealerId = dealerId });
            if (id < 0)
            {
                TempData["ErrorMessage"] = "That salesperson is already mapped to the selected dealer.";
                return RedirectToAction(nameof(TeamDealerAssignments));
            }

            var assignedPerson = _salesPersonRepository.GetById(salespersonId)?.Name ?? salespersonId.ToString();
            var assignedDealer = _dealerRepository.GetById(dealerId)?.Name ?? dealerId.ToString();
            _audit.Record(
                "AddMapping",
                "DealerMapping",
                id.ToString(),
                $"Team dealer assignment added: '{assignedPerson}' to '{assignedDealer}'.",
                new { SalesPersonId = salespersonId, DealerId = dealerId },
                relatedSalesPersonId: salespersonId);

            TempData["SuccessMessage"] = "Dealer assignment created for your team.";
            return RedirectToAction(nameof(TeamDealerAssignments));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RemoveTeamMapping(int id)
        {
            var person = await GetCurrentSalesPersonAsync();
            if (person == null)
            {
                return Forbid();
            }

            if (!IsManager(person))
            {
                TempData["ErrorMessage"] = "Only managers can change dealer assignments. Ask a System Administrator or your manager to update them.";
                return RedirectToAction(nameof(TeamDealerAssignments));
            }

            var mapping = _mappingRepository.GetById(id);
            if (mapping == null)
            {
                return NotFound();
            }

            if (!GetTeamIds(person).Contains(mapping.SalesPersonId))
            {
                TempData["ErrorMessage"] = "You can only remove dealer assignments for your own team.";
                return RedirectToAction(nameof(TeamDealerAssignments));
            }

            if (!_mappingRepository.Delete(id))
            {
                TempData["ErrorMessage"] = "The dealer assignment could not be removed. It may have been deleted already.";
                return RedirectToAction(nameof(TeamDealerAssignments));
            }

            var removedPerson = _salesPersonRepository.GetById(mapping.SalesPersonId)?.Name ?? mapping.SalesPersonId.ToString();
            var removedDealer = _dealerRepository.GetById(mapping.DealerId)?.Name ?? mapping.DealerId.ToString();
            _audit.Record(
                "RemoveMapping",
                "DealerMapping",
                mapping.UniqueID.ToString(),
                $"Team dealer assignment removed: '{removedPerson}' from '{removedDealer}'.",
                new { mapping.SalesPersonId, mapping.DealerId },
                relatedSalesPersonId: mapping.SalesPersonId);

            TempData["SuccessMessage"] = "Dealer assignment removed.";
            return RedirectToAction(nameof(TeamDealerAssignments));
        }

        // ------------------------------------------------------------------ helpers

        private bool IsManager(SalesPerson person)
        {
            return _salesPersonRepository.GetAll().Any(p => p.ManagerId == person.Id);
        }

        private bool CanManage(SalesPerson current, int targetId)
        {
            return targetId != current.Id && GetTeamIds(current).Contains(targetId);
        }

        private string? GetManagerName(int? managerId)
        {
            return managerId.HasValue ? _salesPersonRepository.GetById(managerId.Value)?.Name : null;
        }

        private string DescribeOwners(IReadOnlyCollection<int> ownerIds)
        {
            if (ownerIds.Count == 0)
            {
                return "Unassigned";
            }

            var names = ownerIds
                .Select(id => _salesPersonRepository.GetById(id)?.Name ?? id.ToString())
                .ToList();
            return string.Join(", ", names);
        }

        private async Task<(ApplicationUser? User, string? Error)> GetManagedTeamAccountAsync(string userId, SalesPerson current)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
            {
                return (null, "The account could not be found.");
            }

            if (!user.SalesPersonId.HasValue)
            {
                return (null, "That account is not linked to a salesperson.");
            }

            if (user.SalesPersonId.Value == current.Id)
            {
                return (null, "You cannot manage your own account from team management.");
            }

            if (!GetTeamIds(current).Contains(user.SalesPersonId.Value))
            {
                return (null, "You can only manage accounts for your own team.");
            }

            if (await _userManager.IsInRoleAsync(user, "SystemAdmin"))
            {
                return (null, "System administrator accounts cannot be managed here.");
            }

            return (user, null);
        }

        private List<string> GetSalesPersonDependencies(int id)
        {
            var dependencies = new List<string>();

            if (_salesPersonRepository.HasSubordinates(id))
            {
                dependencies.Add("They still have direct reports.");
            }

            if (_podRepository.HasPodsForSalesPerson(id))
            {
                dependencies.Add("Pods are still assigned to them.");
            }

            if (_mappingRepository.HasMappingsForSalesPerson(id))
            {
                dependencies.Add("Dealer mappings are still assigned to them.");
            }

            if (_userManager.Users.Any(u => u.SalesPersonId == id))
            {
                dependencies.Add("A login account is linked to this record.");
            }

            return dependencies;
        }

        private HashSet<int> GetTeamIds(SalesPerson person)
        {
            var all = _salesPersonRepository.GetAll().ToList();
            var ids = new HashSet<int> { person.Id };
            foreach (var report in HierarchyRules.GetTeamMembers(person.Id, all))
            {
                ids.Add(report.Id);
            }

            return ids;
        }

        private async Task<SalesPerson?> GetCurrentSalesPersonAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user?.SalesPersonId == null)
            {
                return null;
            }

            return _salesPersonRepository.GetById(user.SalesPersonId.Value);
        }
    }
}
