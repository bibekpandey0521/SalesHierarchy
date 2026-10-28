using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SalesHierarchy.Core.Interfaces.Repositories;
using SalesHierarchy.Core.Models;
using System.Collections.Generic;

namespace SalesHierarchy.Web.Controllers
{
    [Authorize(Roles = "SalesPerson")]
    public class SalesPersonController : Controller
    {
        private readonly ISalesPersonRepository _salesPersonRepository;
        private readonly IPODRepository _podRepository;
        private readonly ISalesDealerMappingRepository _mappingRepository;
        private readonly IDealerRepository _dealerRepository;

        public SalesPersonController(ISalesPersonRepository salesPersonRepository, IPODRepository podRepository, ISalesDealerMappingRepository mappingRepository, IDealerRepository dealerRepository)
        {
            _salesPersonRepository = salesPersonRepository;
            _podRepository = podRepository;
            _mappingRepository = mappingRepository;
            _dealerRepository = dealerRepository;
        }

        public IActionResult Dashboard()
        {
            return View();
        }

        public IActionResult MyProfile()
        {
            return View();
        }

        public IActionResult MyPODs()
        {
            return View(new List<POD>());
        }

        public IActionResult MyDealers()
        {
            return View(new List<Dealer>());
        }

        public IActionResult MyTeam()
        {
            return View();
        }

        public IActionResult ReportingHierarchy()
        {
            return View(_salesPersonRepository.GetAll());
        }

        public IActionResult TeamDealerAssignments()
        {
            return View(_mappingRepository.GetAll());
        }
    }
}
