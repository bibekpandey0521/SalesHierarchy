CREATE TABLE [dbo].[POD] (
    [ID] INT IDENTITY(1,1) NOT NULL,
    [OSSPODID] NVARCHAR(50) NULL,
    [Name] NVARCHAR(200) NOT NULL,
    [PODStationId] INT NOT NULL,
    [salespersonId] INT NULL,
    CONSTRAINT [PK_POD] PRIMARY KEY CLUSTERED ([ID] ASC),
    CONSTRAINT [FK_POD_PODStation] FOREIGN KEY ([PODStationId]) REFERENCES [dbo].[PODStation] ([ID]),
    CONSTRAINT [FK_POD_SalesPerson] FOREIGN KEY ([salespersonId]) REFERENCES [dbo].[SalesPerson] ([ID])
);
