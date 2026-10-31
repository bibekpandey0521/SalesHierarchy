using SalesHierarchy.Core.Validation;

namespace SalesHierarchy.Tests;

public class PodAssignmentTests
{
    [Fact]
    public void Normalize_DeduplicatesAndSorts()
    {
        var result = PodAssignment.Normalize(new[] { 3, 1, 3, 2, 1 });

        Assert.Equal(new[] { 1, 2, 3 }, result);
    }

    [Fact]
    public void Diff_AddsAndRemovesOnlyTheChangedAssignments()
    {
        var (toAdd, toRemove) = PodAssignment.Diff(new[] { 1, 2, 3 }, new[] { 3, 4, 5 });

        Assert.Equal(new[] { 4, 5 }, toAdd);
        Assert.Equal(new[] { 1, 2 }, toRemove);
    }

    [Fact]
    public void Diff_WithSameSelection_ChangesNothing()
    {
        var (toAdd, toRemove) = PodAssignment.Diff(new[] { 1, 2 }, new[] { 2, 1 });

        Assert.Empty(toAdd);
        Assert.Empty(toRemove);
    }

    [Fact]
    public void Diff_EmptySelectionRemovesEverything()
    {
        var (toAdd, toRemove) = PodAssignment.Diff(new[] { 1, 2 }, Array.Empty<int>());

        Assert.Empty(toAdd);
        Assert.Equal(new[] { 1, 2 }, toRemove);
    }

    [Fact]
    public void FindStationNameConflict_AllowsDifferentNames()
    {
        var result = PodAssignment.FindStationNameConflict(
            new[] { "Baneshwor", "Chabahil" },
            new[] { "Kalanki" });

        Assert.Null(result);
    }

    [Fact]
    public void FindStationNameConflict_RejectsDuplicateNamesInSelection()
    {
        var result = PodAssignment.FindStationNameConflict(
            new[] { "Baneshwor", "baneshwor" },
            Array.Empty<string>());

        Assert.NotNull(result);
        Assert.Contains("Two selected Pods", result);
    }

    [Fact]
    public void FindStationNameConflict_RejectsNameAlreadyInStation()
    {
        var result = PodAssignment.FindStationNameConflict(
            new[] { "Chabahil" },
            new[] { "chabahil" });

        Assert.NotNull(result);
        Assert.Contains("already in this station", result);
    }

    [Fact]
    public void FindStationNameConflict_IgnoresBlankNames()
    {
        var result = PodAssignment.FindStationNameConflict(
            new[] { "  ", "Baneshwor" },
            new[] { "" });

        Assert.Null(result);
    }
}
