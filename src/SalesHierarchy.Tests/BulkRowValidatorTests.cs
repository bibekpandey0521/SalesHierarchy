using SalesHierarchy.Core.Validation;

namespace SalesHierarchy.Tests;

public class BulkRowValidatorTests
{
    [Fact]
    public void ValidRow_HasNoErrors()
    {
        var errors = BulkRowValidator.ValidateFields("Asha Verma", "Sales Officer", "asha@example.com", "manager@example.com");
        Assert.Empty(errors);
    }

    [Fact]
    public void RequiredFields_AreEnforced()
    {
        var errors = BulkRowValidator.ValidateFields("", "", "", null);
        Assert.Contains(errors, e => e.Contains("Name"));
        Assert.Contains(errors, e => e.Contains("Designation"));
        Assert.Contains(errors, e => e.Contains("Email"));
    }

    [Theory]
    [InlineData("not-an-email")]
    [InlineData("missing@tld")]
    [InlineData("@example.com")]
    public void InvalidEmail_IsRejected(string email)
    {
        Assert.False(BulkRowValidator.IsValidEmail(email));
    }

    [Fact]
    public void InvalidManagerEmail_IsRejected()
    {
        var errors = BulkRowValidator.ValidateFields("Asha", "Officer", "asha@example.com", "bad-manager");
        Assert.Contains(errors, e => e.Contains("Manager email"));
    }

    [Fact]
    public void LongFields_AreRejected()
    {
        var errors = BulkRowValidator.ValidateFields(new string('a', 201), new string('b', 101), "asha@example.com", null);
        Assert.Contains(errors, e => e.Contains("200"));
        Assert.Contains(errors, e => e.Contains("100"));
    }

    [Fact]
    public void SelfReporting_IsDetectedCaseInsensitively()
    {
        Assert.True(BulkRowValidator.IsSelfReport("Asha@Example.com", " asha@example.com "));
        Assert.False(BulkRowValidator.IsSelfReport("asha@example.com", "manager@example.com"));
    }

    [Fact]
    public void Cycle_InFileManagerGraph_IsDetected()
    {
        var managerByEmail = new Dictionary<string, string>
        {
            ["a@example.com"] = "b@example.com",
            ["b@example.com"] = "c@example.com",
            ["c@example.com"] = "a@example.com"
        };

        Assert.True(BulkRowValidator.WouldCreateCycle("a@example.com", "b@example.com", managerByEmail));
        Assert.True(BulkRowValidator.WouldCreateCycle("b@example.com", "c@example.com", managerByEmail));
    }

    [Fact]
    public void AcyclicManagerGraph_IsAllowed()
    {
        var managerByEmail = new Dictionary<string, string>
        {
            ["a@example.com"] = "b@example.com",
            ["b@example.com"] = "c@example.com"
        };

        Assert.False(BulkRowValidator.WouldCreateCycle("a@example.com", "b@example.com", managerByEmail));
        Assert.False(BulkRowValidator.WouldCreateCycle("c@example.com", "", managerByEmail));
    }
}
