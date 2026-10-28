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
        private readonly IPODStationRepository _podStationRepository;
        private readonly ISalesPersonRepository _salesPersonRepository;
        private readonly IPODRepository _podRepository;
        private readonly IDealerRepository _dealerRepository;
        private readonly ISalesDealerMappingRepository _mappingRepository;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IEmailSender _emailSender;
        private readonly BulkUploadService _bulkUploadService;
        private readonly AuditService _audit;
        private readonly IAuditLogRepository _auditLogRepository;

        public AdminController(
            IPODStationRepository podStationRepository,
            ISalesPersonRepository salesPersonRepository,
            IPODRepository podRepository,
            IDealerRepository dealerRepository,
            ISalesDealerMappingRepository mappingRepository,
            UserManager<ApplicationUser> userManager,
            IEmailSender emailSender,
            BulkUploadService bulkUploadService,
            AuditService audit,
            IAuditLogRepository auditLogRepository)
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

        // --------------------------------------------------------------- audit trail

        public IActionResult AuditLog(string? entity, string? actionFilter, string? q)
        {
            ViewBag.Entity = entity;
            ViewBag.Action = actionFilter;
            ViewBag.Query = q;
            ViewBag.Entities = new[]
            {
                "PODStation", "SalesPerson", "POD", "Dealer", "SalesDealerMapping", "UserAccount", "BulkUpload", "Database"
            };
            ViewBag.Actions = new[]
            {
                "Create", "Update", "Delete", "InviteResent", "Disable", "Enable", "Import", "AddMapping", "RemoveMapping", "PasswordSet", "Seed"
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
            var people = _salesPersonRepository.GetAll().GroupBy(p => p.ID).ToDictionary(g => g.Key, g => g.First());
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
                _audit.Record("InviteResent", "UserAccount", user.Id, $"Invitation re-sent to {user.Email}.", new { Outcome = "Sent" });
            }
            catch
            {
                TempData["ErrorMessage"] = $"The invitation e-mail could not be sent to {user.Email}. Check the e-mail configuration and try again.";
                _audit.Record("InviteResent", "UserAccount", user.Id, $"Invitation re-send failed for {user.Email}.", new { Outcome = "Failed" });
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
            _audit.Record("Disable", "UserAccount", user.Id, $"Account {user.Email} disabled.");
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
            _audit.Record("Enable", "UserAccount", user.Id, $"Account {user.Email} enabled.");
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

            _audit.Record("Delete", "UserAccount", user.Id, $"Account {user.Email} deleted.", new { user.Email, user.SalesPersonId });
            TempData["SuccessMessage"] = personId.HasValue
                ? $"Account {user.Email} deleted. The linked salesperson record is now free of account dependencies."
                : $"Account {user.Email} deleted.";
            return RedirectToAction(nameof(UserAccountManagement));
        }

        // ------------------------------------------------------------- POD stations

        public IActionResult PODStations(string? q)
        {
            var stations = _podStationRepository.GetAll().AsEnumerable();
            if (!string.IsNullOrWhiteSpace(q))
            {
                stations = stations.Where(s =>
                    s.Name.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                    (s.OSSPODStationId?.Contains(q, StringComparison.OrdinalIgnoreCase) ?? false));
            }

            ViewBag.Query = q;
            return View(stations.ToList());
        }

        public IActionResult DetailsPODStation(int id)
        {
            var station = _podStationRepository.GetById(id);
            if (station == null)
            {
                return NotFound();
            }

            ViewBag.Pods = _podRepository.GetAll().Where(p => p.PODStationId == id).ToList();
            return View(station);
        }

        public IActionResult CreatePODStation()
        {
            return View(new PODStation());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult CreatePODStation(PODStation station)
        {
            station.Name = station.Name?.Trim() ?? string.Empty;
            station.OSSPODStationId = string.IsNullOrWhiteSpace(station.OSSPODStationId) ? null : station.OSSPODStationId.Trim();

            if (_podStationRepository.ExistsByName(station.Name))
            {
                ModelState.AddModelError(nameof(PODStation.Name), "A POD station with this name already exists.");
            }

            if (!ModelState.IsValid)
            {
                return View(station);
            }

            var stationId = _podStationRepository.Add(station);
            _audit.Record("Create", "PODStation", stationId.ToString(), $"POD station '{station.Name}' created.", station);
            TempData["SuccessMessage"] = $"POD station '{station.Name}' created.";
            return RedirectToAction(nameof(PODStations));
        }

        public IActionResult EditPODStation(int id)
        {
            var station = _podStationRepository.GetById(id);
            if (station == null)
            {
                return NotFound();
            }

            return View(station);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult EditPODStation(PODStation station)
        {
            station.Name = station.Name?.Trim() ?? string.Empty;
            station.OSSPODStationId = string.IsNullOrWhiteSpace(station.OSSPODStationId) ? null : station.OSSPODStationId.Trim();

            var existingStation = _podStationRepository.GetById(station.ID);
            if (existingStation == null)
            {
                return NotFound();
            }

            if (_podStationRepository.ExistsByName(station.Name, station.ID))
            {
                ModelState.AddModelError(nameof(PODStation.Name), "A POD station with this name already exists.");
            }

            if (!ModelState.IsValid)
            {
                return View(station);
            }

            _podStationRepository.Update(station);
            _audit.Record("Update", "PODStation", station.ID.ToString(), $"POD station '{station.Name}' updated.", new { Before = existingStation, After = station });
            TempData["SuccessMessage"] = $"POD station '{station.Name}' updated.";
            return RedirectToAction(nameof(PODStations));
        }

        public IActionResult DeletePODStation(int id)
        {
            var station = _podStationRepository.GetById(id);
            if (station == null)
            {
                return NotFound();
            }

            ViewBag.Pods = _podRepository.GetAll().Where(p => p.PODStationId == id).ToList();
            return View(station);
        }

        [HttpPost, ActionName("DeletePODStation")]
        [ValidateAntiForgeryToken]
        public IActionResult DeletePODStationConfirmed(int id)
        {
            var station = _podStationRepository.GetById(id);
            if (station == null)
            {
                return NotFound();
            }

            if (_podRepository.HasPodsForStation(id))
            {
                TempData["ErrorMessage"] = $"POD station '{station.Name}' cannot be deleted because PODs are still assigned to it. Reassign or delete those PODs first.";
                return RedirectToAction(nameof(PODStations));
            }

            try
            {
                _podStationRepository.Delete(id);
                _audit.Record("Delete", "PODStation", station.ID.ToString(), $"POD station '{station.Name}' deleted.", station);
                TempData["SuccessMessage"] = $"POD station '{station.Name}' deleted.";
            }
            catch
            {
                TempData["ErrorMessage"] = $"POD station '{station.Name}' could not be deleted because related records still reference it.";
            }

            return RedirectToAction(nameof(PODStations));
        }

        // ------------------------------------------------------------ salespersons

        public IActionResult SalesPersons(string? q)
        {
            var all = _salesPersonRepository.GetAll().ToList();
            var people = all.GroupBy(p => p.ID).ToDictionary(g => g.Key, g => g.First());

            IEnumerable<SalesPerson> peopleFiltered = all;
            if (!string.IsNullOrWhiteSpace(q))
            {
                peopleFiltered = peopleFiltered.Where(p =>
                    p.Name.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                    (p.Designation?.Contains(q, StringComparison.OrdinalIgnoreCase) ?? false));
            }

            ViewBag.Query = q;
            ViewBag.People = people;
            return View(peopleFiltered.OrderBy(p => p.Name).ToList());
        }

        public IActionResult DetailsSalesPerson(int id)
        {
            var person = _salesPersonRepository.GetById(id);
            if (person == null)
            {
                return NotFound();
            }

            var all = _salesPersonRepository.GetAll().ToList();
            ViewBag.Manager = person.ManagerId.HasValue
                ? all.FirstOrDefault(p => p.ID == person.ManagerId.Value)
                : null;
            ViewBag.DirectReports = _salesPersonRepository.GetDirectReports(id).ToList();
            ViewBag.AllReports = HierarchyValidator.GetDescendants(id, all);
            ViewBag.ManagerChain = HierarchyValidator.GetManagerChain(id, all);
            ViewBag.Pods = _podRepository.GetBySalesPersonId(id).ToList();
            ViewBag.Mappings = _mappingRepository.GetBySalesPersonId(id).ToList();
            ViewBag.Dealers = _dealerRepository.GetAll().GroupBy(d => d.UniqueID).ToDictionary(g => g.Key, g => g.First());
            ViewBag.Account = _userManager.Users.FirstOrDefault(u => u.SalesPersonId == id);
            return View(person);
        }

        public IActionResult CreateSalesPerson()
        {
            LoadManagers();
            return View(new SalesPerson());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult CreateSalesPerson(SalesPerson person)
        {
            person.Name = person.Name?.Trim() ?? string.Empty;
            person.Designation = string.IsNullOrWhiteSpace(person.Designation) ? null : person.Designation.Trim();

            if (!ModelState.IsValid)
            {
                LoadManagers();
                return View(person);
            }

            if (person.ManagerId.HasValue && _salesPersonRepository.GetById(person.ManagerId.Value) == null)
            {
                ModelState.AddModelError(nameof(SalesPerson.ManagerId), "The selected reporting manager does not exist.");
                LoadManagers();
                return View(person);
            }

            var personId = _salesPersonRepository.Add(person);
            _audit.Record("Create", "SalesPerson", personId.ToString(), $"Salesperson '{person.Name}' created.", person);
            TempData["SuccessMessage"] = $"Salesperson '{person.Name}' created. Create a login account to give them access.";
            return RedirectToAction(nameof(SalesPersons));
        }

        public IActionResult EditSalesPerson(int id)
        {
            var person = _salesPersonRepository.GetById(id);
            if (person == null)
            {
                return NotFound();
            }

            LoadManagers(id);
            return View(person);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult EditSalesPerson(SalesPerson person)
        {
            person.Name = person.Name?.Trim() ?? string.Empty;
            person.Designation = string.IsNullOrWhiteSpace(person.Designation) ? null : person.Designation.Trim();

            var existing = _salesPersonRepository.GetById(person.ID);
            if (existing == null)
            {
                return NotFound();
            }

            if (person.ManagerId == person.ID)
            {
                ModelState.AddModelError(nameof(SalesPerson.ManagerId), "A salesperson cannot report to themselves.");
            }

            if (person.ManagerId.HasValue)
            {
                if (_salesPersonRepository.GetById(person.ManagerId.Value) == null)
                {
                    ModelState.AddModelError(nameof(SalesPerson.ManagerId), "The selected reporting manager does not exist.");
                }
                else if (HierarchyValidator.WouldCreateCycle(person.ID, person.ManagerId, _salesPersonRepository.GetAll()))
                {
                    ModelState.AddModelError(nameof(SalesPerson.ManagerId), "This manager would create a circular reporting relationship.");
                }
            }

            if (!ModelState.IsValid)
            {
                LoadManagers(person.ID);
                return View(person);
            }

            _salesPersonRepository.Update(person);
            _audit.Record("Update", "SalesPerson", person.ID.ToString(), $"Salesperson '{person.Name}' updated.", new { Before = existing, After = person });
            TempData["SuccessMessage"] = $"Salesperson '{person.Name}' updated.";
            return RedirectToAction(nameof(SalesPersons));
        }

        public IActionResult DeleteSalesPerson(int id)
        {
            var person = _salesPersonRepository.GetById(id);
            if (person == null)
            {
                return NotFound();
            }

            ViewBag.Dependencies = GetSalesPersonDependencies(id);
            return View(person);
        }

        [HttpPost, ActionName("DeleteSalesPerson")]
        [ValidateAntiForgeryToken]
        public IActionResult DeleteSalesPersonConfirmed(int id)
        {
            var person = _salesPersonRepository.GetById(id);
            if (person == null)
            {
                return NotFound();
            }

            var dependencies = GetSalesPersonDependencies(id);
            if (dependencies.Count > 0)
            {
                TempData["ErrorMessage"] = $"'{person.Name}' cannot be deleted. {string.Join(" ", dependencies)}";
                return RedirectToAction(nameof(SalesPersons));
            }

            try
            {
                _salesPersonRepository.Delete(id);
                _audit.Record("Delete", "SalesPerson", person.ID.ToString(), $"Salesperson '{person.Name}' deleted.", person);
                TempData["SuccessMessage"] = $"Salesperson '{person.Name}' deleted.";
            }
            catch
            {
                TempData["ErrorMessage"] = $"'{person.Name}' could not be deleted because related records still reference it.";
            }

            return RedirectToAction(nameof(SalesPersons));
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
                dependencies.Add("PODs are still assigned to them.");
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

        private void LoadManagers(int? excludeId = null)
        {
            var managers = _salesPersonRepository.GetAll()
                .Where(p => p.ID != excludeId)
                .OrderBy(p => p.Name)
                .ToList();
            ViewBag.Managers = managers;
        }

        // --------------------------------------------------------------------- PODs

        public IActionResult PODs(string? q)
        {
            var all = _podRepository.GetAll().ToList();
            var stations = _podStationRepository.GetAll().GroupBy(s => s.ID).ToDictionary(g => g.Key, g => g.First());
            var people = _salesPersonRepository.GetAll().GroupBy(p => p.ID).ToDictionary(g => g.Key, g => g.First());

            if (!string.IsNullOrWhiteSpace(q))
            {
                all = all.Where(p =>
                    p.Name.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                    (p.OSSPODID?.Contains(q, StringComparison.OrdinalIgnoreCase) ?? false) ||
                    (stations.TryGetValue(p.PODStationId, out var station) && station.Name.Contains(q, StringComparison.OrdinalIgnoreCase)) ||
                    (p.salespersonId.HasValue && people.TryGetValue(p.salespersonId.Value, out var person) && person.Name.Contains(q, StringComparison.OrdinalIgnoreCase)))
                    .ToList();
            }

            ViewBag.Query = q;
            ViewBag.Stations = stations;
            ViewBag.People = people;
            return View(all.OrderBy(p => p.Name).ToList());
        }

        public IActionResult DetailsPOD(int id)
        {
            var pod = _podRepository.GetById(id);
            if (pod == null)
            {
                return NotFound();
            }

            ViewBag.Station = _podStationRepository.GetById(pod.PODStationId);
            ViewBag.SalesPerson = pod.salespersonId.HasValue ? _salesPersonRepository.GetById(pod.salespersonId.Value) : null;
            return View(pod);
        }

        public IActionResult CreatePOD()
        {
            LoadPodFormLists();
            return View(new POD());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult CreatePOD(POD pod)
        {
            pod.Name = pod.Name?.Trim() ?? string.Empty;
            pod.OSSPODID = string.IsNullOrWhiteSpace(pod.OSSPODID) ? null : pod.OSSPODID.Trim();
            ValidatePod(pod);

            if (!ModelState.IsValid)
            {
                LoadPodFormLists();
                return View(pod);
            }

            var podId = _podRepository.Add(pod);
            _audit.Record("Create", "POD", podId.ToString(), $"POD '{pod.Name}' created.", pod);
            TempData["SuccessMessage"] = $"POD '{pod.Name}' created.";
            return RedirectToAction(nameof(PODs));
        }

        public IActionResult EditPOD(int id)
        {
            var pod = _podRepository.GetById(id);
            if (pod == null)
            {
                return NotFound();
            }

            LoadPodFormLists();
            return View(pod);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult EditPOD(POD pod)
        {
            pod.Name = pod.Name?.Trim() ?? string.Empty;
            pod.OSSPODID = string.IsNullOrWhiteSpace(pod.OSSPODID) ? null : pod.OSSPODID.Trim();

            var existingPod = _podRepository.GetById(pod.ID);
            if (existingPod == null)
            {
                return NotFound();
            }

            ValidatePod(pod);
            if (!ModelState.IsValid)
            {
                LoadPodFormLists();
                return View(pod);
            }

            _podRepository.Update(pod);
            _audit.Record("Update", "POD", pod.ID.ToString(), $"POD '{pod.Name}' updated.", new { Before = existingPod, After = pod });
            TempData["SuccessMessage"] = $"POD '{pod.Name}' updated.";
            return RedirectToAction(nameof(PODs));
        }

        public IActionResult DeletePOD(int id)
        {
            var pod = _podRepository.GetById(id);
            if (pod == null)
            {
                return NotFound();
            }

            ViewBag.Station = _podStationRepository.GetById(pod.PODStationId);
            ViewBag.SalesPerson = pod.salespersonId.HasValue ? _salesPersonRepository.GetById(pod.salespersonId.Value) : null;
            return View(pod);
        }

        [HttpPost, ActionName("DeletePOD")]
        [ValidateAntiForgeryToken]
        public IActionResult DeletePODConfirmed(int id)
        {
            var pod = _podRepository.GetById(id);
            if (pod == null)
            {
                return NotFound();
            }

            try
            {
                _podRepository.Delete(id);
                _audit.Record("Delete", "POD", pod.ID.ToString(), $"POD '{pod.Name}' deleted.", pod);
                TempData["SuccessMessage"] = $"POD '{pod.Name}' deleted.";
            }
            catch
            {
                TempData["ErrorMessage"] = $"POD '{pod.Name}' could not be deleted because related records still reference it.";
            }

            return RedirectToAction(nameof(PODs));
        }

        private void ValidatePod(POD pod)
        {
            if (pod.PODStationId <= 0 || _podStationRepository.GetById(pod.PODStationId) == null)
            {
                ModelState.AddModelError(nameof(POD.PODStationId), "Select a valid POD station.");
            }

            if (pod.salespersonId.HasValue && _salesPersonRepository.GetById(pod.salespersonId.Value) == null)
            {
                ModelState.AddModelError(nameof(POD.salespersonId), "Select a valid salesperson or leave the field empty.");
            }
        }

        private void LoadPodFormLists()
        {
            ViewBag.PODStations = _podStationRepository.GetAll().OrderBy(s => s.Name).ToList();
            ViewBag.SalesPersons = _salesPersonRepository.GetAll().OrderBy(p => p.Name).ToList();
        }

        // ------------------------------------------------------------------ dealers

        public IActionResult Dealers(string? q)
        {
            var dealers = _dealerRepository.GetAll().AsEnumerable();
            if (!string.IsNullOrWhiteSpace(q))
            {
                dealers = dealers.Where(d =>
                    d.Name.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                    (d.Code?.Contains(q, StringComparison.OrdinalIgnoreCase) ?? false));
            }

            ViewBag.Query = q;
            ViewBag.MappingCounts = _mappingRepository.GetAll()
                .GroupBy(m => m.dealerid)
                .ToDictionary(g => g.Key, g => g.Count());
            return View(dealers.OrderBy(d => d.Name).ToList());
        }

        public IActionResult DetailsDealer(int id)
        {
            var dealer = _dealerRepository.GetById(id);
            if (dealer == null)
            {
                return NotFound();
            }

            var people = _salesPersonRepository.GetAll().GroupBy(p => p.ID).ToDictionary(g => g.Key, g => g.First());
            ViewBag.People = people;
            ViewBag.Mappings = _mappingRepository.GetAll().Where(m => m.dealerid == id).ToList();
            return View(dealer);
        }

        public IActionResult CreateDealer()
        {
            return View(new Dealer());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult CreateDealer(Dealer dealer)
        {
            dealer.Name = dealer.Name?.Trim() ?? string.Empty;
            dealer.Code = dealer.Code?.Trim();

            if (!string.IsNullOrWhiteSpace(dealer.Code) && _dealerRepository.ExistsByCode(dealer.Code))
            {
                ModelState.AddModelError(nameof(Dealer.Code), "A dealer with this code already exists.");
            }

            if (!ModelState.IsValid)
            {
                return View(dealer);
            }

            var dealerId = _dealerRepository.Add(dealer);
            _audit.Record("Create", "Dealer", dealerId.ToString(), $"Dealer '{dealer.Name}' created.", dealer);
            TempData["SuccessMessage"] = $"Dealer '{dealer.Name}' created.";
            return RedirectToAction(nameof(Dealers));
        }

        public IActionResult EditDealer(int id)
        {
            var dealer = _dealerRepository.GetById(id);
            if (dealer == null)
            {
                return NotFound();
            }

            return View(dealer);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult EditDealer(Dealer dealer)
        {
            dealer.Name = dealer.Name?.Trim() ?? string.Empty;
            dealer.Code = dealer.Code?.Trim();

            var existingDealer = _dealerRepository.GetById(dealer.UniqueID);
            if (existingDealer == null)
            {
                return NotFound();
            }

            if (!string.IsNullOrWhiteSpace(dealer.Code) && _dealerRepository.ExistsByCode(dealer.Code, dealer.UniqueID))
            {
                ModelState.AddModelError(nameof(Dealer.Code), "A dealer with this code already exists.");
            }

            if (!ModelState.IsValid)
            {
                return View(dealer);
            }

            _dealerRepository.Update(dealer);
            _audit.Record("Update", "Dealer", dealer.UniqueID.ToString(), $"Dealer '{dealer.Name}' updated.", new { Before = existingDealer, After = dealer });
            TempData["SuccessMessage"] = $"Dealer '{dealer.Name}' updated.";
            return RedirectToAction(nameof(Dealers));
        }

        public IActionResult DeleteDealer(int id)
        {
            var dealer = _dealerRepository.GetById(id);
            if (dealer == null)
            {
                return NotFound();
            }

            ViewBag.MappingCount = _mappingRepository.GetAll().Count(m => m.dealerid == id);
            return View(dealer);
        }

        [HttpPost, ActionName("DeleteDealer")]
        [ValidateAntiForgeryToken]
        public IActionResult DeleteDealerConfirmed(int id)
        {
            var dealer = _dealerRepository.GetById(id);
            if (dealer == null)
            {
                return NotFound();
            }

            if (_dealerRepository.HasMappings(id))
            {
                TempData["ErrorMessage"] = $"Dealer '{dealer.Name}' cannot be deleted while dealer mappings reference it. Remove those mappings first.";
                return RedirectToAction(nameof(Dealers));
            }

            try
            {
                _dealerRepository.Delete(id);
                _audit.Record("Delete", "Dealer", dealer.UniqueID.ToString(), $"Dealer '{dealer.Name}' deleted.", dealer);
                TempData["SuccessMessage"] = $"Dealer '{dealer.Name}' deleted.";
            }
            catch
            {
                TempData["ErrorMessage"] = $"Dealer '{dealer.Name}' could not be deleted because related records still reference it.";
            }

            return RedirectToAction(nameof(Dealers));
        }

        // ----------------------------------------------------------------- mappings

        public IActionResult SalesDealerMappings(string? q)
        {
            var mappings = _mappingRepository.GetAll().ToList();
            var people = _salesPersonRepository.GetAll().GroupBy(p => p.ID).ToDictionary(g => g.Key, g => g.First());
            var dealers = _dealerRepository.GetAll().GroupBy(d => d.UniqueID).ToDictionary(g => g.Key, g => g.First());

            if (!string.IsNullOrWhiteSpace(q))
            {
                mappings = mappings.Where(m =>
                    (people.TryGetValue(m.salespersonid, out var person) && person.Name.Contains(q, StringComparison.OrdinalIgnoreCase)) ||
                    (dealers.TryGetValue(m.dealerid, out var dealer) && dealer.Name.Contains(q, StringComparison.OrdinalIgnoreCase)) ||
                    (dealers.TryGetValue(m.dealerid, out var dealerCode) && (dealerCode.Code?.Contains(q, StringComparison.OrdinalIgnoreCase) ?? false)))
                    .ToList();
            }

            ViewBag.Query = q;
            ViewBag.People = people;
            ViewBag.Dealers = dealers;
            return View(mappings);
        }

        public IActionResult DetailsSalesDealerMapping(int id)
        {
            var mapping = _mappingRepository.GetById(id);
            if (mapping == null)
            {
                return NotFound();
            }

            ViewBag.SalesPerson = _salesPersonRepository.GetById(mapping.salespersonid);
            ViewBag.Dealer = _dealerRepository.GetById(mapping.dealerid);
            return View(mapping);
        }

        public IActionResult CreateSalesDealerMapping()
        {
            LoadMappingFormLists();
            return View(new SalesDealerMapping());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult CreateSalesDealerMapping(SalesDealerMapping mapping)
        {
            ValidateMapping(mapping, null);
            if (!ModelState.IsValid)
            {
                LoadMappingFormLists();
                return View(mapping);
            }

            var id = _mappingRepository.Add(mapping);
            if (id < 0)
            {
                ModelState.AddModelError(string.Empty, "This salesperson is already mapped to this dealer.");
                LoadMappingFormLists();
                return View(mapping);
            }

            var mappingPerson = _salesPersonRepository.GetById(mapping.salespersonid)?.Name ?? mapping.salespersonid.ToString();
            var mappingDealer = _dealerRepository.GetById(mapping.dealerid)?.Name ?? mapping.dealerid.ToString();
            _audit.Record("Create", "SalesDealerMapping", id.ToString(), $"Dealer mapping created: '{mappingPerson}' to '{mappingDealer}'.", mapping);
            TempData["SuccessMessage"] = "Dealer mapping created.";
            return RedirectToAction(nameof(SalesDealerMappings));
        }

        public IActionResult EditSalesDealerMapping(int id)
        {
            var mapping = _mappingRepository.GetById(id);
            if (mapping == null)
            {
                return NotFound();
            }

            LoadMappingFormLists();
            return View(mapping);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult EditSalesDealerMapping(SalesDealerMapping mapping)
        {
            var existingMapping = _mappingRepository.GetById(mapping.UniqueID);
            if (existingMapping == null)
            {
                return NotFound();
            }

            ValidateMapping(mapping, mapping.UniqueID);
            if (!ModelState.IsValid)
            {
                LoadMappingFormLists();
                return View(mapping);
            }

            if (!_mappingRepository.Update(mapping))
            {
                ModelState.AddModelError(string.Empty, "This salesperson is already mapped to this dealer.");
                LoadMappingFormLists();
                return View(mapping);
            }

            _audit.Record("Update", "SalesDealerMapping", mapping.UniqueID.ToString(), "Dealer mapping updated.", new { Before = existingMapping, After = mapping });
            TempData["SuccessMessage"] = "Dealer mapping updated.";
            return RedirectToAction(nameof(SalesDealerMappings));
        }

        public IActionResult DeleteSalesDealerMapping(int id)
        {
            var mapping = _mappingRepository.GetById(id);
            if (mapping == null)
            {
                return NotFound();
            }

            ViewBag.SalesPerson = _salesPersonRepository.GetById(mapping.salespersonid);
            ViewBag.Dealer = _dealerRepository.GetById(mapping.dealerid);
            return View(mapping);
        }

        [HttpPost, ActionName("DeleteSalesDealerMapping")]
        [ValidateAntiForgeryToken]
        public IActionResult DeleteSalesDealerMappingConfirmed(int id)
        {
            var mapping = _mappingRepository.GetById(id);
            if (mapping == null)
            {
                return NotFound();
            }

            _mappingRepository.Delete(id);
            var deletedPerson = _salesPersonRepository.GetById(mapping.salespersonid)?.Name ?? mapping.salespersonid.ToString();
            var deletedDealer = _dealerRepository.GetById(mapping.dealerid)?.Name ?? mapping.dealerid.ToString();
            _audit.Record("Delete", "SalesDealerMapping", mapping.UniqueID.ToString(), $"Dealer mapping deleted: '{deletedPerson}' from '{deletedDealer}'.", mapping);
            TempData["SuccessMessage"] = "Dealer mapping deleted.";
            return RedirectToAction(nameof(SalesDealerMappings));
        }

        private void ValidateMapping(SalesDealerMapping mapping, int? excludeId)
        {
            if (_salesPersonRepository.GetById(mapping.salespersonid) == null)
            {
                ModelState.AddModelError(nameof(SalesDealerMapping.salespersonid), "Select a valid salesperson.");
            }

            if (_dealerRepository.GetById(mapping.dealerid) == null)
            {
                ModelState.AddModelError(nameof(SalesDealerMapping.dealerid), "Select a valid dealer.");
            }

            if (ModelState.IsValid && _mappingRepository.Exists(mapping.salespersonid, mapping.dealerid, excludeId))
            {
                ModelState.AddModelError(string.Empty, "This salesperson is already mapped to this dealer.");
            }
        }

        private void LoadMappingFormLists()
        {
            ViewBag.SalesPersons = _salesPersonRepository.GetAll().OrderBy(p => p.Name).ToList();
            ViewBag.Dealers = _dealerRepository.GetAll().OrderBy(d => d.Name).ToList();
        }

        // ------------------------------------------------------ individual accounts

        public IActionResult CreateSalesPersonAccount()
        {
            LoadManagers();
            return View(new CreateSalesPersonAccountViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateSalesPersonAccount(CreateSalesPersonAccountViewModel model)
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

                _audit.Record("Create", "SalesPerson", personCreatedId.ToString(), $"Salesperson '{model.Name}' created with a login account.", new { model.Name, model.Designation, model.ManagerId });
                _audit.Record("Create", "UserAccount", user.Id, $"Account {user.Email} created for salesperson '{model.Name}'.", new { user.Email, SalesPersonId = personCreatedId, Invitation = invitationStatus });

                return RedirectToAction(nameof(SalesPersons));
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
