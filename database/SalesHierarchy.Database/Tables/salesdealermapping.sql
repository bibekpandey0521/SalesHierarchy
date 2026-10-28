CREATE TABLE [dbo].[salesdealermapping] (
    [UniqueID] INT IDENTITY(1,1) NOT NULL,
    [salespersonid] INT NOT NULL,
    [dealerid] INT NOT NULL,
    CONSTRAINT [PK_salesdealermapping] PRIMARY KEY CLUSTERED ([UniqueID] ASC),
    CONSTRAINT [FK_salesdealermapping_SalesPerson] FOREIGN KEY ([salespersonid]) REFERENCES [dbo].[SalesPerson] ([ID]),
    CONSTRAINT [FK_salesdealermapping_Dealer] FOREIGN KEY ([dealerid]) REFERENCES [dbo].[Dealer] ([UniqueID])
);
