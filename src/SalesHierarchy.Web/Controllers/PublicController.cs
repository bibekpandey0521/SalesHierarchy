using Microsoft.AspNetCore.Mvc;
using SalesHierarchy.Core.Interfaces.Repositories;
using SalesHierarchy.Core.DTOs;

namespace SalesHierarchy.Web.Controllers
{
    public class PublicController : Controller
    {
        private readonly ISalesPersonRepository _salesPersonRepository;
        private readonly IPODRepository _podRepository;
        private readonly IPODStationRepository _podStationRepository;

        public PublicController(ISalesPersonRepository salesPersonRepository, IPODRepository podRepository, IPODStationRepository podStationRepository)
        {
            _salesPersonRepository = salesPersonRepository;
            _podRepository = podRepository;
            _podStationRepository = podStationRepository;
        }

        public IActionResult Index()
        {
            return View();
        }

        public IActionResult SalesHierarchy()
        {
            var people = _salesPersonRepository.GetAll()
                .Select(p => new PublicSalesPersonDto { ID = p.ID, Name = p.Name, Designation = p.Designation, ManagerId = p.ManagerId })
                .ToList();
            return View(people);
        }

        public IActionResult PODStations()
        {
            var stations = _podStationRepository.GetAll()
                .Select(s => new PublicPODStationDto { ID = s.ID, Name = s.Name })
                .ToList();
            return View(stations);
        }

        public IActionResult PODs()
        {
            var pods = _podRepository.GetAll()
                .Select(p => new PublicPODDto { ID = p.ID, Name = p.Name, OSSPODID = p.OSSPODID })
                .ToList();
            return View(pods);
        }
    }
}
