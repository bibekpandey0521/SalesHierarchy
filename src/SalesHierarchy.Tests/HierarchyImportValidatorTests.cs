using SalesHierarchy.Core.Models;
using SalesHierarchy.Core.Validation;

namespace SalesHierarchy.Tests;

public class HierarchyImportValidatorTests
{
    [Fact]
    public void ValidHierarchy_HasNoErrors()
    {
        var rows = new[]
        {
            new ReportingHierarchyImportRow { RowNumber = 2, Name = "A CXO", Designation = "CXO" },
            new ReportingHierarchyImportRow { RowNumber = 3, Name = "B Head", Designation = "Head", ManagerName = "A CXO" },
            new ReportingHierarchyImportRow { RowNumber = 4, Name = "C Manager", Designation = "Sales Manager", ManagerName = "B Head" },
            new ReportingHierarchyImportRow { RowNumber = 5, Name = "D Officer", Designation = "Sales Officer", ManagerName = "C Manager" }
        };

        var report = HierarchyImportValidator.Analyze(rows);

        Assert.False(report.HasErrors);
        Assert.Empty(report.Errors);
    }

    [Fact]
    public void MissingManager_IsReported()
    {
        var report = HierarchyImportValidator.Analyze(new[]
        {
            new ReportingHierarchyImportRow { RowNumber = 2, Name = "B Head", Designation = "Head" }
        });

        Assert.True(report.HasErrors);
        Assert.Contains(report.Errors, e => e.Message.Contains("blank"));
    }

    [Fact]
    public void DuplicateNameWithConflictingDesignation_IsReported()
    {
        var report = HierarchyImportValidator.Analyze(new[]
        {
            new ReportingHierarchyImportRow { RowNumber = 2, Name = "Same Name", Designation = "Head", ManagerName = "A CXO" },
            new ReportingHierarchyImportRow { RowNumber = 3, Name = " same   name ", Designation = "Sales Officer", ManagerName = "C Manager" }
        });

        Assert.True(report.HasErrors);
        Assert.Contains(report.Errors, e => e.Message.Contains("conflicting designations"));
    }

    [Fact]
    public void DuplicateNameWithConflictingManager_IsReported()
    {
        var report = HierarchyImportValidator.Analyze(new[]
        {
            new ReportingHierarchyImportRow { RowNumber = 2, Name = "A CXO", Designation = "CXO" },
            new ReportingHierarchyImportRow { RowNumber = 3, Name = "B Head", Designation = "Head", ManagerName = "A CXO" },
            new ReportingHierarchyImportRow { RowNumber = 4, Name = "C Head", Designation = "Head", ManagerName = "A CXO" },
            new ReportingHierarchyImportRow { RowNumber = 5, Name = "D Officer", Designation = "Sales Officer", ManagerName = "B Head" },
            new ReportingHierarchyImportRow { RowNumber = 6, Name = "D Officer", Designation = "Sales Officer", ManagerName = "C Head" }
        });

        Assert.True(report.HasErrors);
        Assert.Contains(report.Errors, e => e.Message.Contains("conflicting managers"));
    }

    [Fact]
    public void DuplicateIdenticalRows_AreFlaggedForReviewWithoutInventingAConflict()
    {
        var report = HierarchyImportValidator.Analyze(new[]
        {
            new ReportingHierarchyImportRow { RowNumber = 2, Name = "A CXO", Designation = "CXO" },
            new ReportingHierarchyImportRow { RowNumber = 3, Name = "A CXO", Designation = "CXO" }
        });

        Assert.Contains(report.Issues, i => i.Message.Contains("appears 2 times"));
        Assert.DoesNotContain(report.Errors, e => e.Message.Contains("conflicting"));
    }

    [Fact]
    public void SelfReporting_IsReported()
    {
        var report = HierarchyImportValidator.Analyze(new[]
        {
            new ReportingHierarchyImportRow { RowNumber = 2, Name = "A Head", Designation = "Head", ManagerName = "A Head" }
        });

        Assert.True(report.HasErrors);
        Assert.Contains(report.Errors, e => e.Message.Contains("themselves"));
    }

    [Fact]
    public void Cycle_IsReported()
    {
        var report = HierarchyImportValidator.Analyze(new[]
        {
            new ReportingHierarchyImportRow { RowNumber = 2, Name = "A Head", Designation = "Head", ManagerName = "B Manager" },
            new ReportingHierarchyImportRow { RowNumber = 3, Name = "B Manager", Designation = "Sales Manager", ManagerName = "A Head" }
        });

        Assert.True(report.HasErrors);
        Assert.Contains(report.Errors, e => e.Message.Contains("Circular"));
    }
}
