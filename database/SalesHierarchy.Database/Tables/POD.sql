CREATE TABLE [dbo].[POD] (
    [ID] INT IDENTITY(1,1) NOT NULL,
    [OSSPODID] INT NOT NULL,
    [Name] NVARCHAR(150) NOT NULL,
    [PODStationId] INT NULL,
    [salespersonId] INT NULL,
    CONSTRAINT [PK_POD] PRIMARY KEY CLUSTERED ([ID] ASC),
    CONSTRAINT [FK_POD_PODStation] FOREIGN KEY ([PODStationId]) REFERENCES [dbo].[PODStation] ([ID]),
    CONSTRAINT [FK_POD_SalesPerson] FOREIGN KEY ([salespersonId]) REFERENCES [dbo].[SalesPerson] ([ID])
);

GO

CREATE UNIQUE INDEX [UX_POD_OSSPODID] ON [dbo].[POD] ([OSSPODID] ASC);

GO

-- One POD belongs to one station at a time; assigned POD names must be unique
-- within that station. Unassigned PODs may share names, so the index is filtered.
CREATE UNIQUE NONCLUSTERED INDEX [UX_POD_Station_Name]
ON [dbo].[POD] ([PODStationId], [Name])
WHERE [PODStationId] IS NOT NULL;
