CREATE TABLE [dbo].[Dealer] (
    [UniqueID] INT IDENTITY(1,1) NOT NULL,
    [Name] NVARCHAR(150) NOT NULL,
    [Code] NVARCHAR(50) NULL,
    CONSTRAINT [PK_Dealer] PRIMARY KEY CLUSTERED ([UniqueID] ASC)
);

GO

CREATE UNIQUE INDEX [UX_Dealer_Code] ON [dbo].[Dealer] ([Code] ASC) WHERE [Code] IS NOT NULL;
