using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace SalesHierarchy.Web.Controllers
{
    public class SeedController : Controller
    {
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly UserManager<Data.ApplicationUser> _userManager;

        public SeedController(RoleManager<IdentityRole> roleManager, UserManager<Data.ApplicationUser> userManager)
        {
            _roleManager = roleManager;
            _userManager = userManager;
        }

        public async Task<IActionResult> CreateAdmin()
        {
            string[] roles = { "SystemAdmin", "SalesPerson" };
            foreach (var r in roles)
            {
                if (!await _roleManager.RoleExistsAsync(r))
                    await _roleManager.CreateAsync(new IdentityRole(r));
            }
            var email = "admin@saleshierarchy.local";
            var user = await _userManager.FindByEmailAsync(email);
            if (user == null)
            {
                user = new Data.ApplicationUser { UserName = email, Email = email, EmailConfirmed = true };
                await _userManager.CreateAsync(user, "Admin@123");
            }
            if (!await _userManager.IsInRoleAsync(user, "SystemAdmin"))
                await _userManager.AddToRoleAsync(user, "SystemAdmin");
            return Content("Admin seeded: admin@saleshierarchy.local / Admin@123");
        }
    }
}
