using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using SalesHierarchy.Core.Interfaces.Repositories;
using SalesHierarchy.Core.Models;
using SalesHierarchy.Core.Validation;
using SalesHierarchy.Web.Data;
using SalesHierarchy.Web.Services;

namespace SalesHierarchy.Web.Controllers
{
    [Authorize(Roles = "SalesPerson")]
    public class SalesPersonController : Controller
    {
        private readonly ISalesPersonRepository _salesPersonRepository;
        private readonly IPODRepository _podRepository;
        private readonly ISalesDealerMappingRepository _mappingRepository;
        private readonly IDealerRepository _dealerRepository;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly AuditService _audit;

        public SalesPersonController(
            ISalesPersonRepository salesPersonRepository,
            IPODRepository podRepository,
            ISalesDealerMappingRepository mappingRepository,
            IDealerRepository dealerRepository,
            UserManager<ApplicationUser> userManager,
            AuditService audit)
        {
            _salesPersonRepository = salesPersonRepository;
            _podRepository = podRepository;
            _mappingRepository = mappingRepository;
            _dealerRepository = dealerRepository;
            _userManager = userManager;
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
            var reports = HierarchyValidator.GetDescendants(person.ID, all);

            ViewBag.PodCount = _podRepository.GetBySalesPersonId(person.ID).Count();
            ViewBag.DealerCount = _mappingRepository.GetBySalesPersonId(person.ID).Count();
            ViewBag.TeamCount = reports.Count;
            ViewBag.Manager = person.ManagerId.HasValue ? all.FirstOrDefault(p => p.ID == person.ManagerId.Value) : null;

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
            ViewBag.Manager = person.ManagerId.HasValue ? all.FirstOrDefault(p => p.ID == person.ManagerId.Value) : null;
            ViewBag.ManagerChain = HierarchyValidator.GetManagerChain(person.ID, all);
            ViewBag.DirectReports = _salesPersonRepository.GetDirectReports(person.ID).ToList();
            return View(person);
        }

        public async Task<IActionResult> MyPODs()
        {
            var person = await GetCurrentSalesPersonAsync();
            if (person == null)
            {
                return View(new List<POD>());
            }

            return View(_podRepository.GetBySalesPersonId(person.ID).ToList());
        }

        public async Task<IActionResult> MyDealers()
        {
            var person = await GetCurrentSalesPersonAsync();
            if (person == null)
            {
                return View(new List<Dealer>());
            }

            var dealerIds = _mappingRepository.GetBySalesPersonId(person.ID).Select(m => m.dealerid).ToHashSet();
            return View(_dealerRepository.GetAll().Where(d => dealerIds.Contains(d.UniqueID)).ToList());
        }

        public async Task<IActionResult> MyTeam()
        {
            var person = await GetCurrentSalesPersonAsync();
            if (person == null)
            {
                return View(new List<SalesHierarchy.Core.Models.SalesPerson>());
            }

            var all = _salesPersonRepository.GetAll().ToList();
            ViewBag.People = all.GroupBy(p => p.ID).ToDictionary(g => g.Key, g => g.First());
            return View(HierarchyValidator.GetDescendants(person.ID, all));
        }

        public async Task<IActionResult> ReportingHierarchy()
        {
            var person = await GetCurrentSalesPersonAsync();
            if (person == null)
            {
                return View(new List<SalesHierarchy.Core.Models.SalesPerson>());
            }

            var all = _salesPersonRepository.GetAll().ToList();
            ViewBag.Current = person;
            ViewBag.ManagerChain = HierarchyValidator.GetManagerChain(person.ID, all);
            ViewBag.People = all.GroupBy(p => p.ID).ToDictionary(g => g.Key, g => g.First());
            return View(HierarchyValidator.GetDescendants(person.ID, all));
        }

        public async Task<IActionResult> TeamDealerAssignments()
        {
            var person = await GetCurrentSalesPersonAsync();
            if (person == null)
            {
                return View(new List<SalesDealerMapping>());
            }

            var teamIds = GetTeamIds(person);
            var people = _salesPersonRepository.GetAll().GroupBy(p => p.ID).ToDictionary(g => g.Key, g => g.First());
            var dealers = _dealerRepository.GetAll().GroupBy(d => d.UniqueID).ToDictionary(g => g.Key, g => g.First());

            ViewBag.People = people;
            ViewBag.Dealers = dealers;
            ViewBag.DealerList = dealers.Values.OrderBy(d => d.Name).ToList();
            ViewBag.Team = teamIds
                .Where(id => people.ContainsKey(id))
                .Select(id => people[id])
                .OrderBy(p => p.Name)
                .ToList();
            ViewBag.CurrentId = person.ID;

            var mappings = _mappingRepository.GetAll().Where(m => teamIds.Contains(m.salespersonid)).ToList();
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

            var id = _mappingRepository.Add(new SalesDealerMapping { salespersonid = salespersonId, dealerid = dealerId });
            if (id < 0)
            {
                TempData["ErrorMessage"] = "That salesperson is already mapped to the selected dealer.";
                return RedirectToAction(nameof(TeamDealerAssignments));
            }

            var assignedPerson = _salesPersonRepository.GetById(salespersonId)?.Name ?? salespersonId.ToString();
            var assignedDealer = _dealerRepository.GetById(dealerId)?.Name ?? dealerId.ToString();
            _audit.Record("AddMapping", "SalesDealerMapping", id.ToString(), $"Team dealer assignment added: '{assignedPerson}' to '{assignedDealer}'.", new { SalesPersonId = salespersonId, DealerId = dealerId });
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

            var mapping = _mappingRepository.GetById(id);
            if (mapping == null)
            {
                return NotFound();
            }

            if (!GetTeamIds(person).Contains(mapping.salespersonid))
            {
                TempData["ErrorMessage"] = "You can only remove dealer assignments for your own team.";
                return RedirectToAction(nameof(TeamDealerAssignments));
            }

            _mappingRepository.Delete(id);
            var removedPerson = _salesPersonRepository.GetById(mapping.salespersonid)?.Name ?? mapping.salespersonid.ToString();
            var removedDealer = _dealerRepository.GetById(mapping.dealerid)?.Name ?? mapping.dealerid.ToString();
            _audit.Record("RemoveMapping", "SalesDealerMapping", mapping.UniqueID.ToString(), $"Team dealer assignment removed: '{removedPerson}' from '{removedDealer}'.", new { mapping.salespersonid, mapping.dealerid });
            TempData["SuccessMessage"] = "Dealer assignment removed.";
            return RedirectToAction(nameof(TeamDealerAssignments));
        }

        private HashSet<int> GetTeamIds(SalesHierarchy.Core.Models.SalesPerson person)
        {
            var all = _salesPersonRepository.GetAll().ToList();
            var ids = new HashSet<int> { person.ID };
            foreach (var report in HierarchyValidator.GetDescendants(person.ID, all))
            {
                ids.Add(report.ID);
            }

            return ids;
        }

        private async Task<SalesHierarchy.Core.Models.SalesPerson?> GetCurrentSalesPersonAsync()
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
