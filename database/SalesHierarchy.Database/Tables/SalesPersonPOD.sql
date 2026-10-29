
CREATE TABLE [dbo].[SalesPersonPOD] (
    [SalesPersonId] INT NOT NULL,
    [PODId] INT NOT NULL,
    CONSTRAINT [PK_SalesPersonPOD] PRIMARY KEY CLUSTERED ([SalesPersonId] ASC, [PODId] ASC),
    CONSTRAINT [FK_SalesPersonPOD_SalesPerson] FOREIGN KEY ([SalesPersonId]) REFERENCES [dbo].[SalesPerson] ([ID]),
    CONSTRAINT [FK_SalesPersonPOD_POD] FOREIGN KEY ([PODId]) REFERENCES [dbo].[POD] ([ID])
);

