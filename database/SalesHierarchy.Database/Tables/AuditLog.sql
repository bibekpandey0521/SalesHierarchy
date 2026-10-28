CREATE TABLE [dbo].[AuditLog] (
    [ID] BIGINT IDENTITY(1,1) NOT NULL,
    [OccurredUtc] DATETIME2(3) NOT NULL CONSTRAINT [DF_AuditLog_OccurredUtc] DEFAULT (SYSUTCDATETIME()),
    [UserEmail] NVARCHAR(256) NULL,
    [Action] NVARCHAR(32) NOT NULL,
    [EntityName] NVARCHAR(64) NOT NULL,
    [EntityId] NVARCHAR(64) NULL,
    [Summary] NVARCHAR(400) NULL,
    [Details] NVARCHAR(MAX) NULL,
    [RelatedSalesPersonId] INT NULL,
    CONSTRAINT [PK_AuditLog] PRIMARY KEY CLUSTERED ([ID] ASC)
);
