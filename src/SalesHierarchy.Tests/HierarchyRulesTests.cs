using SalesHierarchy.Core.Models;
using SalesHierarchy.Core.Validation;

namespace SalesHierarchy.Tests;

public class HierarchyRulesTests
{
    private static List<SalesPerson> People() => new()
    {
        new SalesPerson { Id = 1, Name = "CXO" },
        new SalesPerson { Id = 2, Name = "Head of Sales", ManagerId = 1 },
        new SalesPerson { Id = 3, Name = "Sales Manager", ManagerId = 2 },
        new SalesPerson { Id = 4, Name = "Sales Officer", ManagerId = 3 }
    };

    [Fact]
    public void SelfReporting_IsRejected()
    {
        Assert.True(HierarchyRules.CreatesCycle(3, 3, People()));
    }

    [Fact]
    public void DirectCycle_IsRejected()
    {
        // 1 already manages 2; making 1 report to 2 creates a cycle.
        Assert.True(HierarchyRules.CreatesCycle(1, 2, People()));
    }

    [Fact]
    public void DeepCycle_IsRejected()
    {
        // 4 reports to 3 -> 2 -> 1; making 1 report to 4 creates a cycle.
        Assert.True(HierarchyRules.CreatesCycle(1, 4, People()));
    }

    [Fact]
    public void ValidManagerAssignment_IsAllowed()
    {
        Assert.False(HierarchyRules.CreatesCycle(4, 1, People()));
        Assert.False(HierarchyRules.CreatesCycle(3, null, People()));
    }

    [Fact]
    public void Descendants_IncludeDirectAndIndirectReports()
    {
        var descendants = HierarchyRules.GetTeamMembers(2, People());

        Assert.Equal(new[] { 3, 4 }, descendants.Select(p => p.Id).OrderBy(id => id));
    }

    [Fact]
    public void Descendants_TolerateExistingCycles()
    {
        var people = new List<SalesPerson>
        {
            new() { Id = 1, Name = "A", ManagerId = 2 },
            new() { Id = 2, Name = "B", ManagerId = 1 }
        };

        var descendants = HierarchyRules.GetTeamMembers(1, people);

        Assert.Single(descendants);
        Assert.Equal(2, descendants[0].Id);
    }

    [Fact]
    public void SubtreeScope_IncludesSelfAndAllDescendants()
    {
        var people = People();

        Assert.True(HierarchyRules.IsInTeam(2, 2, people));
        Assert.True(HierarchyRules.IsInTeam(2, 3, people));
        Assert.True(HierarchyRules.IsInTeam(2, 4, people));
    }

    [Fact]
    public void SubtreeScope_ExcludesManagersAndOtherTeams()
    {
        var people = People();
        people.Add(new SalesPerson { Id = 5, Name = "Other Manager", ManagerId = 1 });
        people.Add(new SalesPerson { Id = 6, Name = "Other Team Member", ManagerId = 5 });

        Assert.False(HierarchyRules.IsInTeam(2, 1, people));
        Assert.False(HierarchyRules.IsInTeam(2, 5, people));
        Assert.False(HierarchyRules.IsInTeam(2, 6, people));
    }

    [Fact]
    public void ManagerChain_IsOrderedNearestFirst()
    {
        var chain = HierarchyRules.GetManagers(4, People());

        Assert.Equal(new[] { 3, 2, 1 }, chain.Select(p => p.Id));
    }

    [Fact]
    public void ManagerChain_ForTopLevel_IsEmpty()
    {
        Assert.Empty(HierarchyRules.GetManagers(1, People()));
    }

    private static List<SalesPerson> Ladder() => new()
    {
        new SalesPerson { Id = 1, Name = "CXO", Designation = "CXO" },
        new SalesPerson { Id = 2, Name = "Head", Designation = "Head", ManagerId = 1 },
        new SalesPerson { Id = 3, Name = "Manager", Designation = "Sales Manager", ManagerId = 2 },
        new SalesPerson { Id = 4, Name = "Officer", Designation = "Sales Officer", ManagerId = 3 }
    };

    [Fact]
    public void Cxo_WithNoManager_IsValid()
    {
        Assert.Null(HierarchyRules.ValidateAssignment(1, "CXO", null, Ladder()));
    }

    [Fact]
    public void Cxo_WithManager_IsRejected()
    {
        var error = HierarchyRules.ValidateAssignment(1, "CXO", 2, Ladder());

        Assert.NotNull(error);
        Assert.Contains("top-level", error);
    }

    [Theory]
    [InlineData("Head", 1)]
    [InlineData("Sales Manager", 2)]
    [InlineData("Supervisor", 2)]
    [InlineData("Sales Officer", 3)]
    public void ValidManagerForEachDesignation_IsAccepted(string designation, int managerId)
    {
        Assert.Null(HierarchyRules.ValidateAssignment(0, designation, managerId, Ladder()));
    }

    [Theory]
    [InlineData("Head", 2)]
    [InlineData("Head", 3)]
    [InlineData("Sales Manager", 3)]
    [InlineData("Sales Officer", 1)]
    [InlineData("Sales Officer", 2)]
    public void InvalidManagerDesignationCombination_IsRejected(string designation, int managerId)
    {
        Assert.NotNull(HierarchyRules.ValidateAssignment(0, designation, managerId, Ladder()));
    }

    [Fact]
    public void KnownNonTopLevelDesignation_RequiresManager()
    {
        var error = HierarchyRules.ValidateAssignment(0, "Sales Officer", null, Ladder());

        Assert.NotNull(error);
        Assert.Contains("must report", error);
    }

    [Fact]
    public void CustomDesignation_KeepsLegacyFlexibility()
    {
        Assert.Null(HierarchyRules.ValidateAssignment(0, "Regional Manager - North", 1, Ladder()));
    }

    [Fact]
    public void SelfReporting_IsRejectedThroughAssignmentValidation()
    {
        var error = HierarchyRules.ValidateAssignment(3, "Sales Manager", 3, Ladder());

        Assert.NotNull(error);
        Assert.Contains("themselves", error);
    }

    [Fact]
    public void CircularAssignment_IsRejectedThroughAssignmentValidation()
    {
        var error = HierarchyRules.ValidateAssignment(2, "Head", 4, Ladder());

        Assert.NotNull(error);
        Assert.Contains("circular", error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void NonexistentManager_IsRejected()
    {
        var error = HierarchyRules.ValidateAssignment(0, "Head", 999, Ladder());

        Assert.NotNull(error);
        Assert.Contains("does not exist", error);
    }

    [Fact]
    public void DesignationLevels_AreNormalizedAndVerified()
    {
        Assert.Equal(HierarchyRules.CxoLevel, HierarchyRules.GetDesignationLevel("  cxo "));
        Assert.Equal(HierarchyRules.HeadLevel, HierarchyRules.GetDesignationLevel("HEAD"));
        Assert.Equal(HierarchyRules.ManagerLevel, HierarchyRules.GetDesignationLevel("sales  manager"));
        Assert.Equal(HierarchyRules.ManagerLevel, HierarchyRules.GetDesignationLevel("Sales Manager/Supervisor"));
        Assert.Equal(HierarchyRules.OfficerLevel, HierarchyRules.GetDesignationLevel("Sales Officer"));
        Assert.Null(HierarchyRules.GetDesignationLevel("Regional Manager"));
    }
}
