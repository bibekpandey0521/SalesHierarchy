using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;
using SalesHierarchy.Core.Interfaces.Repositories;
using SalesHierarchy.Core.Models;
using SalesHierarchy.Web.Data;
using SalesHierarchy.Web.ViewModels;
using System.Transactions;

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
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IEmailSender _emailSender;

        public AdminController(IPODStationRepository podStationRepository, ISalesPersonRepository salesPersonRepository, IPODRepository podRepository, IDealerRepository dealerRepository, ISalesDealerMappingRepository mappingRepository, UserManager<ApplicationUser> userManager, IEmailSender emailSender)
        {
            _podStationRepository = podStationRepository;
            _salesPersonRepository = salesPersonRepository;
            _podRepository = podRepository;
            _dealerRepository = dealerRepository;
            _mappingRepository = mappingRepository;
            _userManager = userManager;
            _emailSender = emailSender;
        }

        public IActionResult Dashboard()
        {
            return View();
        }

        public IActionResult UserAccountManagement()
        {
            var users = _userManager.Users.ToList();
            return View(users);
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
        public IActionResult CreatePODStation(PODStation station)
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
        public IActionResult EditPODStation(PODStation station)
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
            var pods = _podRepository.GetAll().Where(p => p.PODStationId == id).ToList();
            if (pods.Count > 0)
            {
                TempData["Error"] = "Cannot delete POD Station due to existing related records.";
                return RedirectToAction("PODStations");
            }
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
            return View(new CreateSalesPersonAccountViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateSalesPersonAccount(CreateSalesPersonAccountViewModel model)
        {
            ViewBag.Managers = _salesPersonRepository.GetAll();
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var existingUser = await _userManager.FindByEmailAsync(model.Email);
            if (existingUser != null)
            {
                ModelState.AddModelError(string.Empty, "Email already registered.");
                return View(model);
            }

            if (model.ManagerId.HasValue)
            {
                var mgr = _salesPersonRepository.GetById(model.ManagerId.Value);
                if (mgr == null)
                {
                    ModelState.AddModelError(string.Empty, "Invalid manager.");
                    return View(model);
                }
                if (mgr.ID == 0) { }
            }

            SalesPerson? personCreated = null;
            ApplicationUser? userCreated = null;
            try
            {
                using (var scope = new TransactionScope(TransactionScopeAsyncFlowOption.Enabled))
                {
                    var person = new SalesPerson
                    {
                        Name = model.Name,
                        Designation = model.Designation,
                        ManagerId = model.ManagerId
                    };
                    var personId = _salesPersonRepository.Add(person);
                    personCreated = new SalesPerson { ID = personId, Name = person.Name, Designation = person.Designation, ManagerId = person.ManagerId };

                    var user = new ApplicationUser { UserName = model.Email, Email = model.Email, EmailConfirmed = true, SalesPersonId = personId };
                    var createResult = await _userManager.CreateAsync(user);
                    if (!createResult.Succeeded)
                    {
                        throw new Exception(string.Join(",", createResult.Errors.Select(e => e.Description)));
                    }
                    await _userManager.AddToRoleAsync(user, "SalesPerson");
                    userCreated = user;

                    // Generate password setup token and send invitation
                    var token = await _userManager.GeneratePasswordResetTokenAsync(user);
                    var callbackUrl = Url.Action("ResetPassword", "Account", new { token, email = user.Email }, Request.Scheme);
                    try
                    {
                        await _emailSender.SendEmailAsync(user.Email!, "Set your password - SalesHierarchy", $"Set your password by clicking here: {callbackUrl}");
                    }
                    catch
                    {
                        // Report as failed/pending; do not log token
                    }

                    TempData["Message"] = $"SalesPerson created. Password setup invitation sent to {model.Email} (if email configured).";
                    scope.Complete();
                    return RedirectToAction("SalesPersons");
                }
            }
            catch
            {
                if (userCreated != null)
                {
                    try { await _userManager.DeleteAsync(userCreated); } catch { }
                }
                if (personCreated != null)
                {
                    try { _salesPersonRepository.Delete(personCreated.ID); } catch { }
                }
                ModelState.AddModelError(string.Empty, "Account creation failed. No orphaned records should remain.");
                return View(model);
            }
        }

        public IActionResult BulkUpload()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> BulkUpload(IFormFile file)
        {
            if (file == null || file.Length == 0)
            {
                ModelState.AddModelError(string.Empty, "Please select a valid .xlsx file.");
                return View();
            }
            if (!Path.GetExtension(file.FileName).Equals(".xlsx", StringComparison.OrdinalIgnoreCase))
            {
                ModelState.AddModelError(string.Empty, "Only .xlsx files are supported.");
                return View();
            }
            TempData["Message"] = "File uploaded. Validation, preview, and import require full implementation per requirements.";
            return View();
        }

        public IActionResult DownloadTemplate()
        {
            var content = "Name,Designation,Email,ManagerEmail\n";
            return File(System.Text.Encoding.UTF8.GetBytes(content), "text/csv", "SalesPersonTemplate.csv");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResendInvitation(string userId)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null) return NotFound();
            var token = await _userManager.GeneratePasswordResetTokenAsync(user);
            var callbackUrl = Url.Action("ResetPassword", "Account", new { token, email = user.Email }, Request.Scheme);
            try
            {
                await _emailSender.SendEmailAsync(user.Email!, "Set your password - SalesHierarchy", $"Set your password by clicking here: {callbackUrl}");
                TempData["Message"] = $"Invitation resent to {user.Email}.";
            }
            catch
            {
                TempData["Message"] = $"Invitation failed to send for {user.Email} (pending).";
            }
            return RedirectToAction("UserAccountManagement");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DisableAccount(string userId)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null) return NotFound();
            user.LockoutEnabled = true;
            user.LockoutEnd = DateTimeOffset.UtcNow.AddYears(100);
            await _userManager.UpdateAsync(user);
            TempData["Message"] = $"Account {user.Email} disabled.";
            return RedirectToAction("UserAccountManagement");
        }
    }
}
