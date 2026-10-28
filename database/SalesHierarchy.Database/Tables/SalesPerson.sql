CREATE TABLE [dbo].[SalesPerson] (
    [ID] INT IDENTITY(1,1) NOT NULL,
    [Name] NVARCHAR(200) NOT NULL,
    [Designation] NVARCHAR(100) NULL,
    [ManagerId] INT NULL,
    CONSTRAINT [PK_SalesPerson] PRIMARY KEY CLUSTERED ([ID] ASC),
    CONSTRAINT [FK_SalesPerson_Manager] FOREIGN KEY ([ManagerId]) REFERENCES [dbo].[SalesPerson] ([ID])
);
