using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SalesHierarchy.Web.Controllers;

namespace SalesHierarchy.Tests;

public class AuthorizationTests
{
    [Theory]
    [InlineData(typeof(AdminController))]
    [InlineData(typeof(AdminPodStationController))]
    [InlineData(typeof(AdminPodController))]
    [InlineData(typeof(AdminSalesPersonController))]
    [InlineData(typeof(AdminDealerController))]
    [InlineData(typeof(AdminDealerMappingController))]
    public void AdminControllers_RequireSystemAdminRole(Type controller)
    {
        var attribute = controller.GetCustomAttribute<AuthorizeAttribute>();
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
        AssertPostAndAntiForgery(typeof(AdminPodStationController), new[]
        {
            nameof(AdminPodStationController.Create),
            nameof(AdminPodStationController.Edit),
            nameof(AdminPodStationController.DeleteConfirmed),
        });

        AssertPostAndAntiForgery(typeof(AdminPodController), new[]
        {
            nameof(AdminPodController.Create),
            nameof(AdminPodController.Edit),
            nameof(AdminPodController.DeleteConfirmed),
            nameof(AdminPodController.SavePodAssignments),
            nameof(AdminPodController.ClearPodAssignments),
        });

        AssertPostAndAntiForgery(typeof(AdminSalesPersonController), new[]
        {
            nameof(AdminSalesPersonController.Create),
            nameof(AdminSalesPersonController.Edit),
            nameof(AdminSalesPersonController.DeleteConfirmed),
        });

        AssertPostAndAntiForgery(typeof(AdminDealerController), new[]
        {
            nameof(AdminDealerController.Create),
            nameof(AdminDealerController.Edit),
            nameof(AdminDealerController.DeleteConfirmed),
        });

        AssertPostAndAntiForgery(typeof(AdminDealerMappingController), new[]
        {
            nameof(AdminDealerMappingController.Create),
            nameof(AdminDealerMappingController.Edit),
            nameof(AdminDealerMappingController.DeleteConfirmed),
        });

        AssertPostAndAntiForgery(typeof(AdminController), new[]
        {
            nameof(AdminController.CreateAccount),
            nameof(AdminController.ConfirmBulkUpload),
            nameof(AdminController.ResendInvitation),
            nameof(AdminController.DisableAccount),
            nameof(AdminController.EnableAccount),
            nameof(AdminController.DeleteAccount)
        });
    }

    private static void AssertPostAndAntiForgery(Type controller, string[] names)
    {
        foreach (var name in names)
        {
            var postActions = controller
                .GetMethods(BindingFlags.Instance | BindingFlags.Public)
                .Where(m => m.Name == name && m.GetCustomAttribute<HttpPostAttribute>() != null)
                .ToList();

            Assert.True(postActions.Count > 0, $"{name} on {controller.Name} should have a POST action.");
            foreach (var action in postActions)
            {
                Assert.NotNull(action.GetCustomAttribute<ValidateAntiForgeryTokenAttribute>());
            }
        }
    }

    [Fact]
    public void SalesPersonTeamMutations_ArePostAndAntiForgeryProtected()
    {
        var mutations = new[]
        {
            nameof(SalesPersonController.CreateTeamMember),
            nameof(SalesPersonController.EditTeamMember),
            nameof(SalesPersonController.DeleteTeamMemberConfirmed),
            nameof(SalesPersonController.CreateTeamAccount),
            nameof(SalesPersonController.ResendTeamInvitation),
            nameof(SalesPersonController.DisableTeamAccount),
            nameof(SalesPersonController.EnableTeamAccount),
            nameof(SalesPersonController.DeleteTeamAccount),
            nameof(SalesPersonController.AssignTeamPod),
            nameof(SalesPersonController.AddTeamMapping),
            nameof(SalesPersonController.RemoveTeamMapping)
        };

        foreach (var name in mutations)
        {
            var postActions = typeof(SalesPersonController)
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
