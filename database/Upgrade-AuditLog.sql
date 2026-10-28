-- Idempotent upgrade for databases created before team-scoped activity history.
-- New databases get the column from SalesHierarchy.Database\Tables\AuditLog.sql.
SET NOCOUNT ON;

IF COL_LENGTH(N'dbo.AuditLog', N'RelatedSalesPersonId') IS NULL
BEGIN
    ALTER TABLE dbo.AuditLog ADD RelatedSalesPersonId INT NULL;
END;
