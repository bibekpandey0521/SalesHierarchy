namespace SalesHierarchy.Web.Services
{
    public enum BulkRowStatus
    {
        Valid,
        Invalid,
        Duplicate,
        Imported,
        Failed,
        Skipped
    }

    public class BulkRow
    {
        public int RowNumber { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Designation { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string ManagerEmail { get; set; } = string.Empty;
        public BulkRowStatus Status { get; set; } = BulkRowStatus.Valid;
        public List<string> Errors { get; set; } = new();
        public List<string> Warnings { get; set; } = new();
        public string? InvitationStatus { get; set; }
        public int? CreatedSalesPersonId { get; set; }

        public bool IsImportable => Status == BulkRowStatus.Valid && Errors.Count == 0;
    }

    public class BulkUploadBatch
    {
        public string Key { get; set; } = Guid.NewGuid().ToString("N");
        public string FileName { get; set; } = string.Empty;
        public List<BulkRow> Rows { get; set; } = new();
        public int ValidCount => Rows.Count(r => r.Status == BulkRowStatus.Valid);
        public int InvalidCount => Rows.Count(r => r.Status == BulkRowStatus.Invalid);
        public int DuplicateCount => Rows.Count(r => r.Status == BulkRowStatus.Duplicate);
        public bool IsImported { get; set; }
        public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
    }

    public class BulkUploadParseResult
    {
        public bool Success { get; set; }
        public string? FileError { get; set; }
        public BulkUploadBatch? Batch { get; set; }
    }
}
