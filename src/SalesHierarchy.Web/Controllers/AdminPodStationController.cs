using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SalesHierarchy.Core.Interfaces.Repositories;
using SalesHierarchy.Core.Models;
using SalesHierarchy.Web.Services;
using SalesHierarchy.Web.ViewModels;

namespace SalesHierarchy.Web.Controllers
{
    [Authorize(Roles = "SystemAdmin")]
    public class AdminPodStationController : Controller
    {
        private readonly IPodStationRepository _podStationRepository;
        private readonly IPodRepository _podRepository;
        private readonly AuditService _audit;
        private readonly IAuditLogRepository _auditLogRepository;

        public AdminPodStationController(
            IPodStationRepository podStationRepository,
            IPodRepository podRepository,
            AuditService audit,
            IAuditLogRepository auditLogRepository)
        {
            _podStationRepository = podStationRepository;
            _podRepository = podRepository;
            _audit = audit;
            _auditLogRepository = auditLogRepository;
        }

        public IActionResult Index(string? q, int page = 1)
        {
            var stations = _podStationRepository.GetAll().AsEnumerable();
            if (!string.IsNullOrWhiteSpace(q))
            {
                stations = stations.Where(s =>
                    s.Name.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                    s.OSSPODStationId.ToString().Contains(q, StringComparison.OrdinalIgnoreCase));
            }

            var filtered = stations.OrderBy(s => s.Name).ToList();
            var (items, pager) = Paging.Paginate(filtered, page);
            pager.Action = nameof(Index);
            if (!string.IsNullOrWhiteSpace(q))
            {
                pager.RouteValues["q"] = q;
            }

            ViewBag.Query = q;
            ViewBag.TotalCount = filtered.Count;
            ViewBag.Pager = pager;
            return View(items);
        }

        public IActionResult Details(int id)
        {
            var station = _podStationRepository.GetById(id);
            if (station == null)
            {
                return NotFound();
            }

            ViewBag.Pods = _podRepository.GetAll().Where(p => p.PODStationId == id).ToList();
            return View(station);
        }

        public IActionResult Create()
        {
            return View(new PodStation());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Create(PodStation station)
        {
            station.Name = station.Name?.Trim() ?? string.Empty;

            if (_podStationRepository.ExistsByOssId(station.OSSPODStationId))
            {
                ModelState.AddModelError(nameof(PodStation.OSSPODStationId), "A Pod station with this OSS Id already exists.");
            }

            if (!ModelState.IsValid)
            {
                return View(station);
            }

            var stationId = _podStationRepository.Add(station);
            _audit.Record("Create", "PodStation", stationId.ToString(), $"Pod station '{station.Name}' created.", station);
            TempData["SuccessMessage"] = $"Pod station '{station.Name}' created.";
            return RedirectToAction(nameof(Index));
        }

        public IActionResult Edit(int id)
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
        public IActionResult Edit(PodStation station)
        {
            station.Name = station.Name?.Trim() ?? string.Empty;

            var existingStation = _podStationRepository.GetById(station.Id);
            if (existingStation == null)
            {
                return NotFound();
            }

            if (_podStationRepository.ExistsByOssId(station.OSSPODStationId, station.Id))
            {
                ModelState.AddModelError(nameof(PodStation.OSSPODStationId), "A Pod station with this OSS Id already exists.");
            }

            if (!ModelState.IsValid)
            {
                return View(station);
            }

            _podStationRepository.Update(station);
            _audit.Record("Update", "PodStation", station.Id.ToString(), $"Pod station '{station.Name}' updated.", new { Before = existingStation, After = station });
            TempData["SuccessMessage"] = $"Pod station '{station.Name}' updated.";
            return RedirectToAction(nameof(Index));
        }

        public IActionResult Delete(int id)
        {
            var station = _podStationRepository.GetById(id);
            if (station == null)
            {
                return NotFound();
            }

            ViewBag.Pods = _podRepository.GetAll().Where(p => p.PODStationId == id).ToList();
            return View(station);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public IActionResult DeleteConfirmed(int id)
        {
            var station = _podStationRepository.GetById(id);
            if (station == null)
            {
                return NotFound();
            }

            if (_podRepository.HasPodsForStation(id))
            {
                TempData["ErrorMessage"] = $"Pod station '{station.Name}' cannot be deleted because Pods are still assigned to it. Reassign or delete those Pods first.";
                return RedirectToAction(nameof(Index));
            }

            try
            {
                _podStationRepository.Delete(id);
                _audit.Record("Delete", "PodStation", station.Id.ToString(), $"Pod station '{station.Name}' deleted.", station);
                TempData["SuccessMessage"] = $"Pod station '{station.Name}' deleted.";
            }
            catch
            {
                TempData["ErrorMessage"] = $"Pod station '{station.Name}' could not be deleted because related records still reference it.";
            }

            return RedirectToAction(nameof(Index));
        }
    }
}
