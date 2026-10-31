using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SalesHierarchy.Core.Interfaces.Repositories;
using SalesHierarchy.Core.Models;
using SalesHierarchy.Web.Services;
using SalesHierarchy.Web.ViewModels;

namespace SalesHierarchy.Web.Controllers
{
    [Authorize(Roles = "SystemAdmin")]
    public class AdminDealerMappingController : Controller
    {
        private readonly IDealerMappingRepository _mappingRepository;
        private readonly ISalesPersonRepository _salesPersonRepository;
        private readonly IDealerRepository _dealerRepository;
        private readonly AuditService _audit;

        public AdminDealerMappingController(
            IDealerMappingRepository mappingRepository,
            ISalesPersonRepository salesPersonRepository,
            IDealerRepository dealerRepository,
            AuditService audit)
        {
            _mappingRepository = mappingRepository;
            _salesPersonRepository = salesPersonRepository;
            _dealerRepository = dealerRepository;
            _audit = audit;
        }

        public IActionResult Index(string? q, int page = 1)
        {
            var mappings = _mappingRepository.GetAll().ToList();
            var people = _salesPersonRepository.GetAll().GroupBy(p => p.Id).ToDictionary(g => g.Key, g => g.First());
            var dealers = _dealerRepository.GetAll().GroupBy(d => d.UniqueID).ToDictionary(g => g.Key, g => g.First());

            if (!string.IsNullOrWhiteSpace(q))
            {
                mappings = mappings.Where(m =>
                    (people.TryGetValue(m.SalesPersonId, out var person) && person.Name.Contains(q, StringComparison.OrdinalIgnoreCase)) ||
                    (dealers.TryGetValue(m.DealerId, out var dealer) && dealer.Name.Contains(q, StringComparison.OrdinalIgnoreCase)) ||
                    (dealers.TryGetValue(m.DealerId, out var dealerCode) && (dealerCode.Code?.Contains(q, StringComparison.OrdinalIgnoreCase) ?? false)))
                    .ToList();
            }

            var (items, pager) = Paging.Paginate(mappings, page);
            pager.Action = nameof(Index);
            if (!string.IsNullOrWhiteSpace(q))
            {
                pager.RouteValues["q"] = q;
            }

            ViewBag.Query = q;
            ViewBag.People = people;
            ViewBag.Dealers = dealers;
            ViewBag.TotalCount = mappings.Count;
            ViewBag.Pager = pager;
            return View(items);
        }

        public IActionResult Details(int id)
        {
            var mapping = _mappingRepository.GetById(id);
            if (mapping == null)
            {
                return NotFound();
            }

            ViewBag.SalesPerson = _salesPersonRepository.GetById(mapping.SalesPersonId);
            ViewBag.Dealer = _dealerRepository.GetById(mapping.DealerId);
            return View(mapping);
        }

        public IActionResult Create()
        {
            LoadMappingFormLists();
            return View(new DealerMapping());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Create(DealerMapping mapping)
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

            var mappingPerson = _salesPersonRepository.GetById(mapping.SalesPersonId)?.Name ?? mapping.SalesPersonId.ToString();
            var mappingDealer = _dealerRepository.GetById(mapping.DealerId)?.Name ?? mapping.DealerId.ToString();
            _audit.Record("Create", "DealerMapping", id.ToString(), $"Dealer mapping created: '{mappingPerson}' to '{mappingDealer}'.", mapping, relatedSalesPersonId: mapping.SalesPersonId);
            TempData["SuccessMessage"] = "Dealer mapping created.";
            return RedirectToAction(nameof(Index));
        }

        public IActionResult Edit(int id)
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
        public IActionResult Edit(DealerMapping mapping)
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

            _audit.Record("Update", "DealerMapping", mapping.UniqueID.ToString(), "Dealer mapping updated.", new { Before = existingMapping, After = mapping }, relatedSalesPersonId: mapping.SalesPersonId);
            TempData["SuccessMessage"] = "Dealer mapping updated.";
            return RedirectToAction(nameof(Index));
        }

        public IActionResult Delete(int id)
        {
            var mapping = _mappingRepository.GetById(id);
            if (mapping == null)
            {
                return NotFound();
            }

            ViewBag.SalesPerson = _salesPersonRepository.GetById(mapping.SalesPersonId);
            ViewBag.Dealer = _dealerRepository.GetById(mapping.DealerId);
            return View(mapping);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public IActionResult DeleteConfirmed(int id)
        {
            var mapping = _mappingRepository.GetById(id);
            if (mapping == null)
            {
                return NotFound();
            }

            _mappingRepository.Delete(id);
            var deletedPerson = _salesPersonRepository.GetById(mapping.SalesPersonId)?.Name ?? mapping.SalesPersonId.ToString();
            var deletedDealer = _dealerRepository.GetById(mapping.DealerId)?.Name ?? mapping.DealerId.ToString();
            _audit.Record("Delete", "DealerMapping", mapping.UniqueID.ToString(), $"Dealer mapping deleted: '{deletedPerson}' from '{deletedDealer}'.", mapping, relatedSalesPersonId: mapping.SalesPersonId);
            TempData["SuccessMessage"] = "Dealer mapping deleted.";
            return RedirectToAction(nameof(Index));
        }

        private void ValidateMapping(DealerMapping mapping, int? excludeId)
        {
            if (_salesPersonRepository.GetById(mapping.SalesPersonId) == null)
            {
                ModelState.AddModelError(nameof(DealerMapping.SalesPersonId), "Select a valid salesperson.");
            }

            if (_dealerRepository.GetById(mapping.DealerId) == null)
            {
                ModelState.AddModelError(nameof(DealerMapping.DealerId), "Select a valid dealer.");
            }

            if (ModelState.IsValid && _mappingRepository.Exists(mapping.SalesPersonId, mapping.DealerId, excludeId))
            {
                ModelState.AddModelError(string.Empty, "This salesperson is already mapped to this dealer.");
            }
        }

        private void LoadMappingFormLists()
        {
            ViewBag.SalesPersons = _salesPersonRepository.GetAll().OrderBy(p => p.Name).ToList();
            ViewBag.Dealers = _dealerRepository.GetAll().OrderBy(d => d.Name).ToList();
        }
    }
}
