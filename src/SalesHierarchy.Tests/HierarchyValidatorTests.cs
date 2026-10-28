using SalesHierarchy.Core.Models;
using SalesHierarchy.Core.Validation;

namespace SalesHierarchy.Tests;

public class HierarchyValidatorTests
{
    private static List<SalesPerson> People() => new()
    {
        new SalesPerson { ID = 1, Name = "CXO" },
        new SalesPerson { ID = 2, Name = "Head of Sales", ManagerId = 1 },
        new SalesPerson { ID = 3, Name = "Sales Manager", ManagerId = 2 },
        new SalesPerson { ID = 4, Name = "Sales Officer", ManagerId = 3 }
    };

    [Fact]
    public void SelfReporting_IsRejected()
    {
        Assert.True(HierarchyValidator.WouldCreateCycle(3, 3, People()));
    }

    [Fact]
    public void DirectCycle_IsRejected()
    {
        // 1 already manages 2; making 1 report to 2 creates a cycle.
        Assert.True(HierarchyValidator.WouldCreateCycle(1, 2, People()));
    }

    [Fact]
    public void DeepCycle_IsRejected()
    {
        // 4 reports to 3 -> 2 -> 1; making 1 report to 4 creates a cycle.
        Assert.True(HierarchyValidator.WouldCreateCycle(1, 4, People()));
    }

    [Fact]
    public void ValidManagerAssignment_IsAllowed()
    {
        Assert.False(HierarchyValidator.WouldCreateCycle(4, 1, People()));
        Assert.False(HierarchyValidator.WouldCreateCycle(3, null, People()));
    }

    [Fact]
    public void Descendants_IncludeDirectAndIndirectReports()
    {
        var descendants = HierarchyValidator.GetDescendants(2, People());

        Assert.Equal(new[] { 3, 4 }, descendants.Select(p => p.ID).OrderBy(id => id));
    }

    [Fact]
    public void Descendants_TolerateExistingCycles()
    {
        var people = new List<SalesPerson>
        {
            new() { ID = 1, Name = "A", ManagerId = 2 },
            new() { ID = 2, Name = "B", ManagerId = 1 }
        };

        var descendants = HierarchyValidator.GetDescendants(1, people);

        Assert.Single(descendants);
        Assert.Equal(2, descendants[0].ID);
    }

    [Fact]
    public void ManagerChain_IsOrderedNearestFirst()
    {
        var chain = HierarchyValidator.GetManagerChain(4, People());

        Assert.Equal(new[] { 3, 2, 1 }, chain.Select(p => p.ID));
    }

    [Fact]
    public void ManagerChain_ForTopLevel_IsEmpty()
    {
        Assert.Empty(HierarchyValidator.GetManagerChain(1, People()));
    }
}
