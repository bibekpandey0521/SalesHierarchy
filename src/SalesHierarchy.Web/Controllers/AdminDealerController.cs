using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SalesHierarchy.Core.Interfaces.Repositories;
using SalesHierarchy.Core.Models;
using SalesHierarchy.Web.Services;
using SalesHierarchy.Web.ViewModels;

namespace SalesHierarchy.Web.Controllers
{
    [Authorize(Roles = "SystemAdmin")]
    public class AdminDealerController : Controller
    {
        private readonly IDealerRepository _dealerRepository;
        private readonly IDealerMappingRepository _mappingRepository;
        private readonly ISalesPersonRepository _salesPersonRepository;
        private readonly AuditService _audit;

        public AdminDealerController(
            IDealerRepository dealerRepository,
            IDealerMappingRepository mappingRepository,
            ISalesPersonRepository salesPersonRepository,
            AuditService audit)
        {
            _dealerRepository = dealerRepository;
            _mappingRepository = mappingRepository;
            _salesPersonRepository = salesPersonRepository;
            _audit = audit;
        }

        public IActionResult Index(string? q, int page = 1)
        {
            var dealers = _dealerRepository.GetAll().AsEnumerable();
            if (!string.IsNullOrWhiteSpace(q))
            {
                dealers = dealers.Where(d =>
                    d.Name.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                    (d.Code?.Contains(q, StringComparison.OrdinalIgnoreCase) ?? false));
            }

            var filtered = dealers.OrderBy(d => d.Name).ToList();
            var (items, pager) = Paging.Paginate(filtered, page);
            pager.Action = nameof(Index);
            if (!string.IsNullOrWhiteSpace(q))
            {
                pager.RouteValues["q"] = q;
            }

            ViewBag.Query = q;
            ViewBag.MappingCounts = _mappingRepository.GetAll()
                .GroupBy(m => m.DealerId)
                .ToDictionary(g => g.Key, g => g.Count());
            ViewBag.TotalCount = filtered.Count;
            ViewBag.Pager = pager;
            return View(items);
        }

        public IActionResult Details(int id)
        {
            var dealer = _dealerRepository.GetById(id);
            if (dealer == null)
            {
                return NotFound();
            }

            var people = _salesPersonRepository.GetAll().GroupBy(p => p.Id).ToDictionary(g => g.Key, g => g.First());
            ViewBag.People = people;
            ViewBag.Mappings = _mappingRepository.GetAll().Where(m => m.DealerId == id).ToList();
            return View(dealer);
        }

        public IActionResult Create()
        {
            return View(new Dealer());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Create(Dealer dealer)
        {
            NormalizeDealer(dealer);

            if (!string.IsNullOrWhiteSpace(dealer.Code) && _dealerRepository.ExistsByCode(dealer.Code))
            {
                ModelState.AddModelError(nameof(Dealer.Code), "A dealer with this code already exists.");
            }

            if (!ModelState.IsValid)
            {
                return View(dealer);
            }

            var dealerId = _dealerRepository.Add(dealer);
            _audit.Record("Create", "Dealer", dealerId.ToString(), $"Dealer '{dealer.Name}' created.", dealer);
            TempData["SuccessMessage"] = $"Dealer '{dealer.Name}' created.";
            return RedirectToAction(nameof(Index));
        }

        public IActionResult Edit(int id)
        {
            var dealer = _dealerRepository.GetById(id);
            if (dealer == null)
            {
                return NotFound();
            }

            return View(dealer);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Edit(Dealer dealer)
        {
            NormalizeDealer(dealer);

            var existingDealer = _dealerRepository.GetById(dealer.UniqueID);
            if (existingDealer == null)
            {
                return NotFound();
            }

            if (!string.IsNullOrWhiteSpace(dealer.Code) && _dealerRepository.ExistsByCode(dealer.Code, dealer.UniqueID))
            {
                ModelState.AddModelError(nameof(Dealer.Code), "A dealer with this code already exists.");
            }

            if (!ModelState.IsValid)
            {
                return View(dealer);
            }

            _dealerRepository.Update(dealer);
            _audit.Record("Update", "Dealer", dealer.UniqueID.ToString(), $"Dealer '{dealer.Name}' updated.", new { Before = existingDealer, After = dealer });
            TempData["SuccessMessage"] = $"Dealer '{dealer.Name}' updated.";
            return RedirectToAction(nameof(Index));
        }

        private static void NormalizeDealer(Dealer dealer)
        {
            dealer.Code = string.IsNullOrWhiteSpace(dealer.Code) ? null : dealer.Code.Trim();
            dealer.Name = string.IsNullOrWhiteSpace(dealer.Name)
                ? dealer.Code ?? string.Empty
                : dealer.Name.Trim();
        }

        public IActionResult Delete(int id)
        {
            var dealer = _dealerRepository.GetById(id);
            if (dealer == null)
            {
                return NotFound();
            }

            ViewBag.MappingCount = _mappingRepository.GetAll().Count(m => m.DealerId == id);
            return View(dealer);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public IActionResult DeleteConfirmed(int id)
        {
            var dealer = _dealerRepository.GetById(id);
            if (dealer == null)
            {
                return NotFound();
            }

            if (_dealerRepository.HasMappings(id))
            {
                TempData["ErrorMessage"] = $"Dealer '{dealer.Name}' cannot be deleted while dealer mappings reference it. Remove those mappings first.";
                return RedirectToAction(nameof(Index));
            }

            try
            {
                _dealerRepository.Delete(id);
                _audit.Record("Delete", "Dealer", dealer.UniqueID.ToString(), $"Dealer '{dealer.Name}' deleted.", dealer);
                TempData["SuccessMessage"] = $"Dealer '{dealer.Name}' deleted.";
            }
            catch
            {
                TempData["ErrorMessage"] = $"Dealer '{dealer.Name}' could not be deleted because related records still reference it.";
            }

            return RedirectToAction(nameof(Index));
        }
    }
}
