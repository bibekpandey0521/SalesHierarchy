using SalesHierarchy.Web.Services;

namespace SalesHierarchy.Tests;

public class BulkUploadModelTests
{
    [Fact]
    public void ValidRows_AreImportable()
    {
        var row = new BulkRow { Status = BulkRowStatus.Valid };
        Assert.True(row.IsImportable);
    }

    [Fact]
    public void RowWithErrors_IsNotImportable()
    {
        var row = new BulkRow { Status = BulkRowStatus.Valid };
        row.Errors.Add("Name is required.");
        Assert.False(row.IsImportable);
    }

    [Fact]
    public void DuplicateRow_IsNotImportable()
    {
        var row = new BulkRow { Status = BulkRowStatus.Duplicate };
        Assert.False(row.IsImportable);
    }

    [Fact]
    public void BatchCounts_AreCalculatedFromRowStatuses()
    {
        var batch = new BulkUploadBatch
        {
            Rows =
            {
                new BulkRow { Status = BulkRowStatus.Valid },
                new BulkRow { Status = BulkRowStatus.Valid },
                new BulkRow { Status = BulkRowStatus.Invalid },
                new BulkRow { Status = BulkRowStatus.Duplicate }
            }
        };

        Assert.Equal(2, batch.ValidCount);
        Assert.Equal(1, batch.InvalidCount);
        Assert.Equal(1, batch.DuplicateCount);
        Assert.False(batch.IsImported);
    }
}
