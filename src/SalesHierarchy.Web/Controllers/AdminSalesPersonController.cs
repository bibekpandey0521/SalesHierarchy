using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
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
    public class AdminSalesPersonController : Controller
    {
        private readonly ISalesPersonRepository _salesPersonRepository;
        private readonly IPodRepository _podRepository;
        private readonly IPodStationRepository _podStationRepository;
        private readonly IDealerRepository _dealerRepository;
        private readonly IDealerMappingRepository _mappingRepository;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly AuditService _audit;
        private readonly ILogger<AdminSalesPersonController> _logger;

        public AdminSalesPersonController(
            ISalesPersonRepository salesPersonRepository,
            IPodRepository podRepository,
            IPodStationRepository podStationRepository,
            IDealerRepository dealerRepository,
            IDealerMappingRepository mappingRepository,
            UserManager<ApplicationUser> userManager,
            AuditService audit,
            ILogger<AdminSalesPersonController> logger)
        {
            _salesPersonRepository = salesPersonRepository;
            _podRepository = podRepository;
            _podStationRepository = podStationRepository;
            _dealerRepository = dealerRepository;
            _mappingRepository = mappingRepository;
            _userManager = userManager;
            _audit = audit;
            _logger = logger;
        }

        public IActionResult Index(string? q, int page = 1)
        {
            var all = _salesPersonRepository.GetAll().ToList();
            var people = all.GroupBy(p => p.Id).ToDictionary(g => g.Key, g => g.First());

            IEnumerable<SalesPerson> peopleFiltered = all;
            if (!string.IsNullOrWhiteSpace(q))
            {
                peopleFiltered = peopleFiltered.Where(p =>
                    p.Name.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                    (p.Designation?.Contains(q, StringComparison.OrdinalIgnoreCase) ?? false));
            }

            var filtered = peopleFiltered.OrderBy(p => p.Name).ToList();
            var (items, pager) = Paging.Paginate(filtered, page);
            pager.Action = nameof(Index);
            if (!string.IsNullOrWhiteSpace(q))
            {
                pager.RouteValues["q"] = q;
            }

            ViewBag.Query = q;
            ViewBag.People = people;
            ViewBag.TotalCount = filtered.Count;
            ViewBag.Pager = pager;
            return View(items);
        }

        public IActionResult Details(int id)
        {
            var person = _salesPersonRepository.GetById(id);
            if (person == null)
            {
                return NotFound();
            }

            var all = _salesPersonRepository.GetAll().ToList();
            ViewBag.Manager = person.ManagerId.HasValue
                ? all.FirstOrDefault(p => p.Id == person.ManagerId.Value)
                : null;
            ViewBag.DirectReports = _salesPersonRepository.GetDirectReports(id).ToList();
            ViewBag.AllReports = HierarchyRules.GetTeamMembers(id, all);
            ViewBag.ManagerChain = HierarchyRules.GetManagers(id, all);
            ViewBag.Pods = _podRepository.GetBySalesPersonId(id).ToList();
            ViewBag.Mappings = _mappingRepository.GetBySalesPersonId(id).ToList();
            ViewBag.Dealers = _dealerRepository.GetAll().GroupBy(d => d.UniqueID).ToDictionary(g => g.Key, g => g.First());
            ViewBag.Account = _userManager.Users.FirstOrDefault(u => u.SalesPersonId == id);
            return View(person);
        }

        public IActionResult Create()
        {
            var model = new SalesPersonFormViewModel();
            LoadSalesPersonForm(model);
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Create(SalesPersonFormViewModel model)
        {
            NormalizeSalesPersonForm(model);
            ValidateSalesPersonForm(model, null);

            if (!ModelState.IsValid)
            {
                LoadSalesPersonForm(model);
                return View(model);
            }

            var person = MapToSalesPerson(model);
            try
            {
                var personId = _salesPersonRepository.AddWithPods(person, model.PodIds);
                _audit.Record(
                    "Create",
                    "SalesPerson",
                    personId.ToString(),
                    $"Salesperson '{person.Name}' created with {model.PodIds.Count} Pod assignment(s).",
                    new { Person = person, PodIds = model.PodIds },
                    relatedSalesPersonId: personId);
                TempData["SuccessMessage"] = $"Salesperson '{person.Name}' created. Create a login account to give them access.";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Creating salesperson {Name} failed.", person.Name);
                ModelState.AddModelError(string.Empty, "The salesperson could not be created. No changes were saved. Please review the details and try again.");
                LoadSalesPersonForm(model);
                return View(model);
            }
        }

        public IActionResult Edit(int id)
        {
            var person = _salesPersonRepository.GetById(id);
            if (person == null)
            {
                return NotFound();
            }

            var model = new SalesPersonFormViewModel
            {
                Id = person.Id,
                Name = person.Name,
                Designation = person.Designation,
                ManagerId = person.ManagerId,
                PodIds = _salesPersonRepository.GetPodIds(person.Id).ToList()
            };

            LoadSalesPersonForm(model, person.Id);
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Edit(SalesPersonFormViewModel model)
        {
            NormalizeSalesPersonForm(model);

            var existing = _salesPersonRepository.GetById(model.Id);
            if (existing == null)
            {
                return NotFound();
            }

            ValidateSalesPersonForm(model, model.Id);

            if (!ModelState.IsValid)
            {
                LoadSalesPersonForm(model, model.Id);
                return View(model);
            }

            var person = MapToSalesPerson(model);
            try
            {
                if (!_salesPersonRepository.UpdateWithPods(person, model.PodIds))
                {
                    ModelState.AddModelError(string.Empty, "The salesperson could not be updated. It may have been deleted already.");
                    LoadSalesPersonForm(model, model.Id);
                    return View(model);
                }

                _audit.Record(
                    "Update",
                    "SalesPerson",
                    person.Id.ToString(),
                    $"Salesperson '{person.Name}' updated with {model.PodIds.Count} Pod assignment(s).",
                    new { Before = existing, After = person, PodIds = model.PodIds },
                    relatedSalesPersonId: person.Id);
                TempData["SuccessMessage"] = $"Salesperson '{person.Name}' updated.";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Updating salesperson {SalesPersonId} failed.", person.Id);
                ModelState.AddModelError(string.Empty, "The salesperson could not be updated. No changes were saved. Please review the details and try again.");
                LoadSalesPersonForm(model, model.Id);
                return View(model);
            }
        }

        private void NormalizeSalesPersonForm(SalesPersonFormViewModel model)
        {
            model.Name = model.Name?.Trim() ?? string.Empty;
            model.Designation = string.IsNullOrWhiteSpace(model.Designation) ? null : model.Designation.Trim();
            model.PodIds = PodAssignment.Normalize(model.PodIds).ToList();
        }

        private void ValidateSalesPersonForm(SalesPersonFormViewModel model, int? editingId)
        {
            if (model.PodIds.Any(id => id <= 0))
            {
                ModelState.AddModelError(nameof(model.PodIds), "One or more selected Pods are invalid.");
            }

            var validPodIds = model.PodIds.Where(id => id > 0).Distinct().ToList();
            var existingPodIds = _podRepository.GetExistingIds(validPodIds).ToHashSet();
            var missingPodIds = validPodIds.Where(id => !existingPodIds.Contains(id)).ToList();
            if (missingPodIds.Count > 0)
            {
                ModelState.AddModelError(
                    nameof(model.PodIds),
                    "One or more selected Pods no longer exist. Refresh the page and choose again.");
            }

            var allPeople = _salesPersonRepository.GetAll().ToList();
            var hierarchyError = HierarchyRules.ValidateAssignment(
                editingId ?? 0,
                model.Designation,
                model.ManagerId,
                allPeople);
            if (!string.IsNullOrWhiteSpace(hierarchyError))
            {
                ModelState.AddModelError(nameof(model.ManagerId), hierarchyError);
            }
        }

        private static SalesPerson MapToSalesPerson(SalesPersonFormViewModel model)
        {
            return new SalesPerson
            {
                Id = model.Id,
                Name = model.Name,
                Designation = model.Designation,
                ManagerId = model.ManagerId
            };
        }

        private void LoadSalesPersonForm(SalesPersonFormViewModel model, int? excludeId = null)
        {
            model.Managers = _salesPersonRepository.GetAll()
                .Where(p => p.Id != excludeId)
                .OrderBy(p => p.Name)
                .ToList();

            var pods = _podRepository.GetAll().OrderBy(p => p.Name).ToList();
            var stations = _podStationRepository.GetAll()
                .GroupBy(s => s.Id)
                .ToDictionary(g => g.Key, g => g.First());

            model.AvailablePods = pods;
            model.Stations = stations;
            model.DesignationOptions = HierarchyRules.GetDesignationLevelMap().Keys
                .Select(key => key switch
                {
                    "cxo" => "CXO",
                    "head" => "Head",
                    "sales manager" => "Sales Manager",
                    "supervisor" => "Supervisor",
                    "sales manager/supervisor" => "Sales Manager/Supervisor",
                    "sales manager / supervisor" => "Sales Manager / Supervisor",
                    "sales officer" => "Sales Officer",
                    _ => key
                })
                .Concat(_salesPersonRepository.GetAll()
                    .Select(p => p.Designation)
                    .Where(d => !string.IsNullOrWhiteSpace(d))
                    .Select(d => d!.Trim()))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(d => d)
                .ToList();
        }

        public IActionResult Delete(int id)
        {
            var person = _salesPersonRepository.GetById(id);
            if (person == null)
            {
                return NotFound();
            }

            ViewBag.Dependencies = GetSalesPersonDependencies(id);
            return View(person);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public IActionResult DeleteConfirmed(int id)
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
                return RedirectToAction(nameof(Index));
            }

            try
            {
                _salesPersonRepository.Delete(id);
                _audit.Record("Delete", "SalesPerson", person.Id.ToString(), $"Salesperson '{person.Name}' deleted.", person, relatedSalesPersonId: person.Id);
                TempData["SuccessMessage"] = $"Salesperson '{person.Name}' deleted.";
            }
            catch
            {
                TempData["ErrorMessage"] = $"'{person.Name}' could not be deleted because related records still reference it.";
            }

            return RedirectToAction(nameof(Index));
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
    }
}
