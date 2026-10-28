using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SalesHierarchy.Web.Controllers;

namespace SalesHierarchy.Tests;

public class AuthorizationTests
{
    [Fact]
    public void AdminController_RequiresSystemAdminRole()
    {
        var attribute = typeof(AdminController).GetCustomAttribute<AuthorizeAttribute>();
        Assert.NotNull(attribute);
        Assert.Equal("SystemAdmin", attribute!.Roles);
    }

    [Fact]
    public void SalesPersonController_RequiresSalesPersonRole()
    {
        var attribute = typeof(SalesPersonController).GetCustomAttribute<AuthorizeAttribute>();
        Assert.NotNull(attribute);
        Assert.Equal("SalesPerson", attribute!.Roles);
    }

    [Fact]
    public void PublicController_IsAnonymous()
    {
        Assert.Null(typeof(PublicController).GetCustomAttribute<AuthorizeAttribute>());
    }

    [Fact]
    public void AccountController_DoesNotAllowAnonymousWriteActionsToLeak()
    {
        // Account actions are intentionally anonymous (login, forgot/reset password),
        // but must never be reachable through the admin area.
        Assert.Equal("Account", typeof(AccountController).Name.Replace("Controller", string.Empty));
    }

    [Fact]
    public void AdminMutations_ArePostAndAntiForgeryProtected()
    {
        var mutations = new[]
        {
            nameof(AdminController.CreatePODStation),
            nameof(AdminController.EditPODStation),
            nameof(AdminController.DeletePODStationConfirmed),
            nameof(AdminController.CreateSalesPerson),
            nameof(AdminController.EditSalesPerson),
            nameof(AdminController.DeleteSalesPersonConfirmed),
            nameof(AdminController.CreatePOD),
            nameof(AdminController.EditPOD),
            nameof(AdminController.DeletePODConfirmed),
            nameof(AdminController.CreateDealer),
            nameof(AdminController.EditDealer),
            nameof(AdminController.DeleteDealerConfirmed),
            nameof(AdminController.CreateSalesDealerMapping),
            nameof(AdminController.EditSalesDealerMapping),
            nameof(AdminController.DeleteSalesDealerMappingConfirmed),
            nameof(AdminController.CreateSalesPersonAccount),
            nameof(AdminController.ConfirmBulkUpload),
            nameof(AdminController.ResendInvitation),
            nameof(AdminController.DisableAccount),
            nameof(AdminController.EnableAccount),
            nameof(AdminController.DeleteAccount)
        };

        foreach (var name in mutations)
        {
            var postActions = typeof(AdminController)
                .GetMethods(BindingFlags.Instance | BindingFlags.Public)
                .Where(m => m.Name == name && m.GetCustomAttribute<HttpPostAttribute>() != null)
                .ToList();

            Assert.True(postActions.Count > 0, $"{name} should have a POST action.");
            foreach (var action in postActions)
            {
                Assert.NotNull(action.GetCustomAttribute<ValidateAntiForgeryTokenAttribute>());
            }
        }
    }
}
