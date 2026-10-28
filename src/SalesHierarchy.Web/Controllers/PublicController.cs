using Microsoft.AspNetCore.Mvc;
using SalesHierarchy.Core.Interfaces.Repositories;
using SalesHierarchy.Core.DTOs;

namespace SalesHierarchy.Web.Controllers
{
    /// <summary>
    /// Read-only, anonymous access to approved directory information.
    /// Only DTOs are returned so internal fields (emails, mappings, account data)
    /// can never leak into public pages.
    /// </summary>
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
            ViewBag.SalesPersonCount = _salesPersonRepository.GetAll().Count();
            ViewBag.PODCount = _podRepository.GetAll().Count();
            ViewBag.PODStationCount = _podStationRepository.GetAll().Count();
            return View();
        }

        public IActionResult SalesHierarchy(string? q)
        {
            var people = _salesPersonRepository.GetAll()
                .Select(p => new PublicSalesPersonDto { ID = p.ID, Name = p.Name, Designation = p.Designation, ManagerId = p.ManagerId })
                .ToList();

            if (!string.IsNullOrWhiteSpace(q))
            {
                people = people.Where(p =>
                    p.Name.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                    (p.Designation?.Contains(q, StringComparison.OrdinalIgnoreCase) ?? false))
                    .ToList();
            }

            ViewBag.Query = q;
            return View(people);
        }

        public IActionResult PODStations(string? q)
        {
            var stations = _podStationRepository.GetAll()
                .Select(s => new PublicPODStationDto { ID = s.ID, Name = s.Name })
                .ToList();

            if (!string.IsNullOrWhiteSpace(q))
            {
                stations = stations.Where(s => s.Name.Contains(q, StringComparison.OrdinalIgnoreCase)).ToList();
            }

            ViewBag.Query = q;
            return View(stations);
        }

        public IActionResult PODs(string? q)
        {
            var pods = _podRepository.GetAll()
                .Select(p => new PublicPODDto { ID = p.ID, Name = p.Name, OSSPODID = p.OSSPODID })
                .ToList();

            if (!string.IsNullOrWhiteSpace(q))
            {
                pods = pods.Where(p =>
                    p.Name.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                    (p.OSSPODID?.Contains(q, StringComparison.OrdinalIgnoreCase) ?? false))
                    .ToList();
            }

            ViewBag.Query = q;
            return View(pods);
        }
    }
}
