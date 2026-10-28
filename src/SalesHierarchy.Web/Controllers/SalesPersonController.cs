using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using SalesHierarchy.Core.Interfaces.Repositories;
using SalesHierarchy.Core.Models;
using SalesHierarchy.Web.Data;
using System.Collections.Generic;
using System.Linq;

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

        public SalesPersonController(ISalesPersonRepository salesPersonRepository, IPODRepository podRepository, ISalesDealerMappingRepository mappingRepository, IDealerRepository dealerRepository, UserManager<ApplicationUser> userManager)
        {
            _salesPersonRepository = salesPersonRepository;
            _podRepository = podRepository;
            _mappingRepository = mappingRepository;
            _dealerRepository = dealerRepository;
            _userManager = userManager;
        }

        private SalesHierarchy.Core.Models.SalesPerson? GetCurrentSalesPerson()
        {
            var user = _userManager.GetUserAsync(User).Result;
            if (user == null || !user.SalesPersonId.HasValue) return null;
            return _salesPersonRepository.GetById(user.SalesPersonId.Value);
        }

        public IActionResult Dashboard()
        {
            return View();
        }

        public IActionResult MyProfile()
        {
            var sp = GetCurrentSalesPerson();
            return View(sp);
        }

        public IActionResult MyPODs()
        {
            var sp = GetCurrentSalesPerson();
            if (sp == null) return View(new List<SalesHierarchy.Core.Models.POD>());
            var pods = _podRepository.GetBySalesPersonId(sp.ID);
            return View(pods);
        }

        public IActionResult MyDealers()
        {
            var sp = GetCurrentSalesPerson();
            if (sp == null) return View(new List<SalesHierarchy.Core.Models.Dealer>());
            var mappings = _mappingRepository.GetBySalesPersonId(sp.ID);
            var dealerIds = mappings.Select(m => m.dealerid).ToHashSet();
            var dealers = _dealerRepository.GetAll().Where(d => dealerIds.Contains(d.UniqueID)).ToList();
            return View(dealers);
        }

        public IActionResult MyTeam()
        {
            var sp = GetCurrentSalesPerson();
            if (sp == null) return View(new List<SalesHierarchy.Core.Models.SalesPerson>());
            var all = _salesPersonRepository.GetAll().ToList();
            var team = GetDescendants(sp.ID, all).ToList();
            return View(team);
        }

        public IActionResult ReportingHierarchy()
        {
            var sp = GetCurrentSalesPerson();
            if (sp == null) return View(new List<SalesHierarchy.Core.Models.SalesPerson>());
            var all = _salesPersonRepository.GetAll().ToList();
            var result = new List<SalesHierarchy.Core.Models.SalesPerson>();
            result.Add(sp);
            foreach (var r in GetDescendants(sp.ID, all)) result.Add(r);
            return View(result.GroupBy(x => x.ID).Select(g => g.First()).ToList());
        }

        public IActionResult TeamDealerAssignments()
        {
            var sp = GetCurrentSalesPerson();
            if (sp == null) return View(new List<SalesHierarchy.Core.Models.SalesDealerMapping>());
            var all = _salesPersonRepository.GetAll().ToList();
            var teamIds = new HashSet<int> { sp.ID };
            foreach (var id in GetDescendantIds(sp.ID, all)) teamIds.Add(id);
            var allMappings = _mappingRepository.GetAll().Where(m => teamIds.Contains(m.salespersonid)).ToList();
            return View(allMappings);
        }

        private IEnumerable<SalesHierarchy.Core.Models.SalesPerson> GetDescendants(int managerId, List<SalesHierarchy.Core.Models.SalesPerson> all)
        {
            var direct = all.Where(p => p.ManagerId == managerId).ToList();
            var result = new List<SalesHierarchy.Core.Models.SalesPerson>();
            foreach (var d in direct)
            {
                result.Add(d);
                result.AddRange(GetDescendants(d.ID, all));
            }
            return result;
        }

        private IEnumerable<int> GetDescendantIds(int managerId, List<SalesHierarchy.Core.Models.SalesPerson> all)
        {
            foreach (var d in all.Where(p => p.ManagerId == managerId))
            {
                yield return d.ID;
                foreach (var id in GetDescendantIds(d.ID, all)) yield return id;
            }
        }
    }
}
