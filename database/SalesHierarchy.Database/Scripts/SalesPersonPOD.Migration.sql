-- Reviewed migration for existing databases. Mirrors database\Upgrade-SalesPersonPOD.sql.
-- Safe to execute repeatedly; it only creates the join table if missing and copies
-- legacy POD.salespersonId assignments that can be verified.
SET NOCOUNT ON;
SET XACT_ABORT ON;

BEGIN TRANSACTION;

IF OBJECT_ID(N'dbo.SalesPersonPOD', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.SalesPersonPOD
    (
        SalesPersonId INT NOT NULL,
        PODId INT NOT NULL,
        CONSTRAINT PK_SalesPersonPOD PRIMARY KEY CLUSTERED (SalesPersonId ASC, PODId ASC),
        CONSTRAINT FK_SalesPersonPOD_SalesPerson FOREIGN KEY (SalesPersonId) REFERENCES dbo.SalesPerson (ID),
        CONSTRAINT FK_SalesPersonPOD_POD FOREIGN KEY (PODId) REFERENCES dbo.POD (ID)
    );
END;

INSERT INTO dbo.SalesPersonPOD (SalesPersonId, PODId)
SELECT p.salespersonId, p.ID
FROM dbo.POD AS p
WHERE p.salespersonId IS NOT NULL
  AND EXISTS (SELECT 1 FROM dbo.SalesPerson AS sp WHERE sp.ID = p.salespersonId)
  AND NOT EXISTS
  (
      SELECT 1
      FROM dbo.SalesPersonPOD AS x
      WHERE x.SalesPersonId = p.salespersonId
        AND x.PODId = p.ID
  );

COMMIT TRANSACTION;
