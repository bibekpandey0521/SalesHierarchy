using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SalesHierarchy.Core.Interfaces.Repositories;
using SalesHierarchy.Core.Models;
using SalesHierarchy.Core.Validation;
using SalesHierarchy.Web.Services;
using SalesHierarchy.Web.ViewModels;

namespace SalesHierarchy.Web.Controllers
{
    [Authorize(Roles = "SystemAdmin")]
    public class AdminPodController : Controller
    {
        private readonly IPodRepository _podRepository;
        private readonly IPodStationRepository _podStationRepository;
        private readonly ISalesPersonRepository _salesPersonRepository;
        private readonly AuditService _audit;
        private readonly ILogger<AdminPodController> _logger;

        public AdminPodController(
            IPodRepository podRepository,
            IPodStationRepository podStationRepository,
            ISalesPersonRepository salesPersonRepository,
            AuditService audit,
            ILogger<AdminPodController> logger)
        {
            _podRepository = podRepository;
            _podStationRepository = podStationRepository;
            _salesPersonRepository = salesPersonRepository;
            _audit = audit;
            _logger = logger;
        }

        public IActionResult Index(string? q, int page = 1)
        {
            var all = _podRepository.GetAll().ToList();
            var stations = _podStationRepository.GetAll().GroupBy(s => s.Id).ToDictionary(g => g.Key, g => g.First());
            var people = _salesPersonRepository.GetAll().GroupBy(p => p.Id).ToDictionary(g => g.Key, g => g.First());
            var owners = _salesPersonRepository.GetAllPodAssignments()
                .Where(a => people.ContainsKey(a.SalesPersonId))
                .GroupBy(a => a.PodId)
                .ToDictionary(g => g.Key, g => people[g.First().SalesPersonId]);

            if (!string.IsNullOrWhiteSpace(q))
            {
                all = all.Where(p =>
                    p.Name.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                    p.OSSPODID.ToString().Contains(q, StringComparison.OrdinalIgnoreCase) ||
                    (p.PODStationId.HasValue && stations.TryGetValue(p.PODStationId.Value, out var station) && station.Name.Contains(q, StringComparison.OrdinalIgnoreCase)) ||
                    (owners.TryGetValue(p.Id, out var owner) && owner.Name.Contains(q, StringComparison.OrdinalIgnoreCase)))
                    .ToList();
            }

            var filtered = all.OrderBy(p => p.Name).ToList();
            var (items, pager) = Paging.Paginate(filtered, page);
            pager.Action = nameof(Index);
            if (!string.IsNullOrWhiteSpace(q))
            {
                pager.RouteValues["q"] = q;
            }

            ViewBag.Query = q;
            ViewBag.Stations = stations;
            ViewBag.People = people;
            ViewBag.Owners = owners;
            ViewBag.TotalCount = filtered.Count;
            ViewBag.Pager = pager;
            return View(items);
        }

        public IActionResult Details(int id)
        {
            var pod = _podRepository.GetById(id);
            if (pod == null)
            {
                return NotFound();
            }

            ViewBag.Station = pod.PODStationId.HasValue ? _podStationRepository.GetById(pod.PODStationId.Value) : null;
            ViewBag.SalesPerson = pod.salespersonId.HasValue ? _salesPersonRepository.GetById(pod.salespersonId.Value) : null;
            var people = _salesPersonRepository.GetAll().GroupBy(p => p.Id).ToDictionary(g => g.Key, g => g.First());
            ViewBag.Owners = _salesPersonRepository.GetAllPodAssignments()
                .Where(a => a.PodId == pod.Id && people.ContainsKey(a.SalesPersonId))
                .Select(a => people[a.SalesPersonId])
                .OrderBy(p => p.Name)
                .ToList();
            return View(pod);
        }

        public IActionResult Create()
        {
            LoadPodFormLists();
            return View(new Pod());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Create(Pod pod)
        {
            pod.Name = pod.Name?.Trim() ?? string.Empty;
            pod.PODStationId = null;
            pod.salespersonId = null;
            ValidatePod(pod, null);

            if (!ModelState.IsValid)
            {
                LoadPodFormLists();
                return View(pod);
            }

            var podId = _podRepository.Add(pod);
            _audit.Record("Create", "Pod", podId.ToString(), $"Pod '{pod.Name}' created without a station assignment.", pod);
            TempData["SuccessMessage"] = $"Pod '{pod.Name}' created. Assign it to a station on the Pod assignment page.";
            return RedirectToAction(nameof(Index));
        }

        public IActionResult Edit(int id)
        {
            var pod = _podRepository.GetById(id);
            if (pod == null)
            {
                return NotFound();
            }

            ViewBag.Station = pod.PODStationId.HasValue ? _podStationRepository.GetById(pod.PODStationId.Value) : null;
            LoadPodFormLists();
            return View(pod);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Edit(Pod pod)
        {
            pod.Name = pod.Name?.Trim() ?? string.Empty;

            var existingPod = _podRepository.GetById(pod.Id);
            if (existingPod == null)
            {
                return NotFound();
            }

            ValidatePod(pod, pod.Id, existingPod.PODStationId);
            if (!ModelState.IsValid)
            {
                LoadPodFormLists();
                return View(pod);
            }

            _podRepository.Update(pod);
            _audit.Record("Update", "Pod", pod.Id.ToString(), $"Pod '{pod.Name}' updated.", new { Before = existingPod, After = pod }, relatedSalesPersonId: existingPod.salespersonId);
            TempData["SuccessMessage"] = $"Pod '{pod.Name}' updated.";
            return RedirectToAction(nameof(Index));
        }

        public IActionResult Delete(int id)
        {
            var pod = _podRepository.GetById(id);
            if (pod == null)
            {
                return NotFound();
            }

            ViewBag.Station = pod.PODStationId.HasValue ? _podStationRepository.GetById(pod.PODStationId.Value) : null;
            ViewBag.SalesPerson = pod.salespersonId.HasValue ? _salesPersonRepository.GetById(pod.salespersonId.Value) : null;
            return View(pod);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public IActionResult DeleteConfirmed(int id)
        {
            var pod = _podRepository.GetById(id);
            if (pod == null)
            {
                return NotFound();
            }

            try
            {
                _podRepository.Delete(id);
                _audit.Record("Delete", "Pod", pod.Id.ToString(), $"Pod '{pod.Name}' deleted.", pod, relatedSalesPersonId: pod.salespersonId);
                TempData["SuccessMessage"] = $"Pod '{pod.Name}' deleted.";
            }
            catch
            {
                TempData["ErrorMessage"] = $"Pod '{pod.Name}' could not be deleted because related records still reference it.";
            }

            return RedirectToAction(nameof(Index));
        }

        private void ValidatePod(Pod pod, int? editingId, int? stationId = null)
        {
            if (pod.OSSPODID > 0 && _podRepository.ExistsByOssId(pod.OSSPODID, editingId))
            {
                ModelState.AddModelError(nameof(Pod.OSSPODID), "A Pod with this OSS Id already exists.");
            }

            // The filtered unique index UX_Pod_Station_Name allows the same Pod name in
            // different stations but not inside one station. Report it here instead of
            // surfacing a raw SQL unique-key violation.
            if (stationId.HasValue && !string.IsNullOrWhiteSpace(pod.Name))
            {
                var duplicate = _podRepository.GetByStationId(stationId.Value)
                    .Any(p => p.Id != (editingId ?? 0) &&
                              string.Equals(p.Name, pod.Name, StringComparison.OrdinalIgnoreCase));
                if (duplicate)
                {
                    ModelState.AddModelError(nameof(Pod.Name), "Another Pod in the same station already uses this name.");
                }
            }
        }

        private void LoadPodFormLists()
        {
            ViewBag.PodStations = _podStationRepository.GetAll().OrderBy(s => s.Name).ToList();
        }

        public IActionResult PodAssignments(int? stationId, int page = 1, int unassignedPage = 1)
        {
            var stations = _podStationRepository.GetAll().OrderBy(s => s.Name).ToList();
            ViewBag.Stations = stations;

            if (stations.Count == 0)
            {
                ViewBag.SelectedStation = null;
                ViewBag.AllPods = Array.Empty<Pod>();
                ViewBag.AssignedPodIds = new HashSet<int>();
                ViewBag.AssignedPods = new List<Pod>();
                ViewBag.AssignedCount = 0;
                ViewBag.UnassignedPods = new List<Pod>();
                ViewBag.UnassignedCount = 0;
                ViewBag.OtherStationCount = 0;
                ViewBag.Owners = new Dictionary<int, SalesPerson>();
                ViewBag.AssignedPager = null;
                ViewBag.UnassignedPager = null;
                return View(new List<Pod>());
            }

            var selectedStation = stationId.HasValue
                ? stations.FirstOrDefault(s => s.Id == stationId.Value)
                : stations.First();
            selectedStation ??= stations.First();

            var allPods = _podRepository.GetAll().OrderBy(p => p.Name).ToList();
            var assigned = allPods.Where(p => p.PODStationId == selectedStation.Id).ToList();
            var unassigned = allPods.Where(p => p.PODStationId == null).ToList();
            var people = _salesPersonRepository.GetAll().GroupBy(p => p.Id).ToDictionary(g => g.Key, g => g.First());

            var (assignedPageItems, assignedPager) = Paging.Paginate(assigned, page);
            assignedPager.Action = nameof(PodAssignments);
            assignedPager.RouteValues["stationId"] = selectedStation.Id.ToString();
            if (unassignedPage > 1)
            {
                assignedPager.RouteValues["unassignedPage"] = unassignedPage.ToString();
            }

            var (unassignedPageItems, unassignedPager) = Paging.Paginate(unassigned, unassignedPage);
            unassignedPager.Action = nameof(PodAssignments);
            unassignedPager.RouteValues["stationId"] = selectedStation.Id.ToString();
            if (page > 1)
            {
                unassignedPager.RouteValues["page"] = page.ToString();
            }

            ViewBag.SelectedStation = selectedStation;
            ViewBag.AllPods = allPods;
            ViewBag.AssignedPodIds = assigned.Select(p => p.Id).ToHashSet();
            ViewBag.AssignedPods = assigned;
            ViewBag.AssignedCount = assigned.Count;
            ViewBag.UnassignedPods = unassignedPageItems;
            ViewBag.UnassignedCount = unassigned.Count;
            ViewBag.OtherStationCount = allPods.Count(p => p.PODStationId.HasValue && p.PODStationId.Value != selectedStation.Id);
            ViewBag.Owners = _salesPersonRepository.GetAllPodAssignments()
                .Where(a => people.ContainsKey(a.SalesPersonId))
                .GroupBy(a => a.PodId)
                .ToDictionary(g => g.Key, g => people[g.First().SalesPersonId]);
            ViewBag.AssignedPager = assignedPager;
            ViewBag.UnassignedPager = unassignedPager;

            return View(assignedPageItems);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult SavePodAssignments(int stationId, List<int>? PodIds)
        {
            var station = _podStationRepository.GetById(stationId);
            if (station == null)
            {
                return NotFound();
            }

            var submitted = PodAssignment.Normalize(PodIds).ToList();
            var invalidIds = submitted.Where(id => id <= 0).ToList();
            if (invalidIds.Count > 0)
            {
                TempData["ErrorMessage"] = "One or more selected Pods are invalid.";
                return RedirectToAction(nameof(PodAssignments), new { stationId });
            }

            var existingIds = _podRepository.GetExistingIds(submitted).ToHashSet();
            if (existingIds.Count != submitted.Count)
            {
                TempData["ErrorMessage"] = "One or more selected Pods no longer exist. Refresh the page and try again.";
                return RedirectToAction(nameof(PodAssignments), new { stationId });
            }

            var allPods = _podRepository.GetAll().ToList();
            var selectedPods = allPods.Where(p => submitted.Contains(p.Id)).ToList();

            var nameError = PodAssignment.FindStationNameConflict(
                selectedPods.Select(p => p.Name),
                allPods
                    .Where(p => p.PODStationId == stationId && !submitted.Contains(p.Id))
                    .Select(p => p.Name));
            if (nameError != null)
            {
                TempData["ErrorMessage"] = nameError;
                return RedirectToAction(nameof(PodAssignments), new { stationId });
            }

            var previousIds = _podRepository.GetByStationId(stationId).Select(p => p.Id).ToList();
            var (toAdd, toRemove) = PodAssignment.Diff(previousIds, submitted);

            try
            {
                _podRepository.AssignPodsToStation(stationId, submitted);
                _audit.Record(
                    "AssignPod",
                    "PodStation",
                    station.Id.ToString(),
                    $"Pod station '{station.Name}' assignments updated: {toAdd.Count} assigned, {toRemove.Count} unassigned.",
                    new
                    {
                        Station = station.Name,
                        AssignedPodIds = toAdd,
                        UnassignedPodIds = toRemove,
                        FinalPodIds = submitted
                    });

                TempData["SuccessMessage"] = toAdd.Count == 0 && toRemove.Count == 0
                    ? "Pod station assignments are already up to date."
                    : $"Pod station '{station.Name}' updated: {toAdd.Count} assigned, {toRemove.Count} unassigned.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Updating Pod station {StationId} assignments failed.", stationId);
                TempData["ErrorMessage"] = "The Pod assignments could not be saved. No changes were committed.";
            }

            return RedirectToAction(nameof(PodAssignments), new { stationId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult ClearPodAssignments(int stationId)
        {
            var station = _podStationRepository.GetById(stationId);
            if (station == null)
            {
                return NotFound();
            }

            var previous = _podRepository.GetByStationId(stationId).ToList();
            if (previous.Count == 0)
            {
                TempData["Message"] = $"No Pods are assigned to '{station.Name}'.";
                return RedirectToAction(nameof(PodAssignments), new { stationId });
            }

            try
            {
                _podRepository.ClearStationAssignments(stationId);
                _audit.Record(
                    "AssignPod",
                    "PodStation",
                    station.Id.ToString(),
                    $"All {previous.Count} Pod assignment(s) removed from station '{station.Name}'.",
                    new { Station = station.Name, RemovedPodIds = previous.Select(p => p.Id).ToList() });

                TempData["SuccessMessage"] = $"All {previous.Count} Pod assignment(s) removed from '{station.Name}'.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Clearing Pod station {StationId} assignments failed.", stationId);
                TempData["ErrorMessage"] = "The Pod assignments could not be cleared. No changes were committed.";
            }

            return RedirectToAction(nameof(PodAssignments), new { stationId });
        }
    }
}
