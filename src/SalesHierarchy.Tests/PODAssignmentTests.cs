using SalesHierarchy.Core.Validation;

namespace SalesHierarchy.Tests;

public class PODAssignmentTests
{
    [Fact]
    public void Normalize_DeduplicatesAndSorts()
    {
        var result = PODAssignment.Normalize(new[] { 3, 1, 3, 2, 1 });

        Assert.Equal(new[] { 1, 2, 3 }, result);
    }

    [Fact]
    public void Diff_AddsAndRemovesOnlyTheChangedAssignments()
    {
        var (toAdd, toRemove) = PODAssignment.Diff(new[] { 1, 2, 3 }, new[] { 3, 4, 5 });

        Assert.Equal(new[] { 4, 5 }, toAdd);
        Assert.Equal(new[] { 1, 2 }, toRemove);
    }

    [Fact]
    public void Diff_WithSameSelection_ChangesNothing()
    {
        var (toAdd, toRemove) = PODAssignment.Diff(new[] { 1, 2 }, new[] { 2, 1 });

        Assert.Empty(toAdd);
        Assert.Empty(toRemove);
    }

    [Fact]
    public void Diff_EmptySelectionRemovesEverything()
    {
        var (toAdd, toRemove) = PODAssignment.Diff(new[] { 1, 2 }, Array.Empty<int>());

        Assert.Empty(toAdd);
        Assert.Equal(new[] { 1, 2 }, toRemove);
    }
}
