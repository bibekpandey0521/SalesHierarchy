-- Idempotent upgrade for the many-to-many salesperson/POD assignment table.
-- Safe to run more than once. It never drops the legacy POD.salespersonId column
-- and never deletes POD or salesperson rows.
SET NOCOUNT ON;
SET XACT_ABORT ON;

IF OBJECT_ID(N'dbo.POD', N'U') IS NULL OR OBJECT_ID(N'dbo.SalesPerson', N'U') IS NULL
BEGIN
    THROW 51000, N'Required tables dbo.POD and dbo.SalesPerson are missing.', 1;
END;

IF COL_LENGTH(N'dbo.POD', N'salespersonId') IS NULL
BEGIN
    THROW 51001, N'Schema verification failed: dbo.POD does not have column salespersonId.', 1;
END;

BEGIN TRANSACTION;

BEGIN TRY

    -- Report legacy POD rows whose salespersonId does not reference a real person.
    -- These rows are preserved as-is and are not copied into SalesPersonPOD.
    SELECT N'Legacy salespersonId missing from SalesPerson' AS Issue,
           CAST(p.ID AS NVARCHAR(20)) AS PodID,
           p.salespersonId AS MissingSpId
    FROM dbo.POD AS p
    WHERE p.salespersonId IS NOT NULL
      AND NOT EXISTS (SELECT 1 FROM dbo.SalesPerson AS sp WHERE sp.ID = p.salespersonId);

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
        SELECT N'Created new SalesPersonPOD table.' AS Action;
    END
    ELSE
    BEGIN
        SELECT N'SalesPersonPOD table already exists - skipping creation.' AS Action;
    END;

    -- Populate the many-to-many table from the legacy single-owner column.
    -- Existing pairs are left untouched so the script can be re-run safely.
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
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0
        ROLLBACK TRANSACTION;

    THROW;
END CATCH;

-- Post-migration report (outside the transaction for readability).
SELECT N'Total PODs' AS Metric, COUNT(*) AS Value FROM dbo.POD
UNION ALL SELECT N'Total SalesPersons', COUNT(*) FROM dbo.SalesPerson
UNION ALL SELECT N'Total SalesPersonPOD rows', COUNT(*) FROM dbo.SalesPersonPOD
UNION ALL SELECT N'PODs with legacy salespersonId set', COUNT(*) FROM dbo.POD WHERE salespersonId IS NOT NULL;
