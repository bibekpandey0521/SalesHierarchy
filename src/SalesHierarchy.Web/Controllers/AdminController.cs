using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SalesHierarchy.Core.Interfaces.Repositories;

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

        public AdminController(IPODStationRepository podStationRepository, ISalesPersonRepository salesPersonRepository, IPODRepository podRepository, IDealerRepository dealerRepository, ISalesDealerMappingRepository mappingRepository)
        {
            _podStationRepository = podStationRepository;
            _salesPersonRepository = salesPersonRepository;
            _podRepository = podRepository;
            _dealerRepository = dealerRepository;
            _mappingRepository = mappingRepository;
        }

        public IActionResult Dashboard()
        {
            return View();
        }

        public IActionResult PODStations()
        {
            return View(_podStationRepository.GetAll());
        }

        public IActionResult CreatePODStation()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult CreatePODStation(Core.Models.PODStation station)
        {
            if (ModelState.IsValid)
            {
                _podStationRepository.Add(station);
                return RedirectToAction("PODStations");
            }
            return View(station);
        }

        public IActionResult EditPODStation(int id)
        {
            var station = _podStationRepository.GetById(id);
            if (station == null) return NotFound();
            return View(station);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult EditPODStation(Core.Models.PODStation station)
        {
            if (ModelState.IsValid)
            {
                _podStationRepository.Update(station);
                return RedirectToAction("PODStations");
            }
            return View(station);
        }

        public IActionResult DeletePODStation(int id)
        {
            var station = _podStationRepository.GetById(id);
            if (station == null) return NotFound();
            return View(station);
        }

        [HttpPost, ActionName("DeletePODStation")]
        [ValidateAntiForgeryToken]
        public IActionResult DeletePODStationConfirmed(int id)
        {
            try
            {
                _podStationRepository.Delete(id);
            }
            catch
            {
                TempData["Error"] = "Cannot delete POD Station due to existing related records.";
                return RedirectToAction("PODStations");
            }
            return RedirectToAction("PODStations");
        }

        public IActionResult SalesPersons()
        {
            return View(_salesPersonRepository.GetAll());
        }

        public IActionResult PODs()
        {
            return View(_podRepository.GetAll());
        }

        public IActionResult Dealers()
        {
            return View(_dealerRepository.GetAll());
        }

        public IActionResult SalesDealerMappings()
        {
            return View(_mappingRepository.GetAll());
        }

        public IActionResult CreateSalesPersonAccount()
        {
            ViewBag.Managers = _salesPersonRepository.GetAll();
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult CreateSalesPersonAccount(Core.Models.SalesPerson person)
        {
            if (ModelState.IsValid)
            {
                if (person.ManagerId.HasValue && person.ManagerId.Value == 0) person.ManagerId = null;
                // basic validation
                _salesPersonRepository.Add(person);
                TempData["Message"] = "SalesPerson created.";
                return RedirectToAction("SalesPersons");
            }
            ViewBag.Managers = _salesPersonRepository.GetAll();
            return View(person);
        }

        public IActionResult BulkUpload()
        {
            return View();
        }
    }
}
