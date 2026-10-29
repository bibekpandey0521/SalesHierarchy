-- Idempotent upgrade for the many-to-many salesperson/POD assignment table.
-- Safe to run more than once. It never drops the legacy POD.salespersonId column.
-- Added: schema verification and invalid-legacy-row reporting (2026-10-29).
SET NOCOUNT ON;
SET XACT_ABORT ON;

BEGIN TRANSACTION;

-- ----------------------------------------------------------------
-- Schema verification: ensure expected columns/tables exist before proceeding.
-- ----------------------------------------------------------------
DECLARE @PODStationIdExists BIT, @PODSalespersonIdExists BIT, @SalesPersonPODExists BIT,
        @FKExists BIT, @LegacyRowsChecked BIT;

SELECT @PODStationIdExists = CASE WHEN COL_NAME(OBJECT_ID(N'dbo.POD'), N'PODStationId') IS NOT NULL THEN 1 ELSE 0 END;
SELECT @PODSalespersonIdExists = CASE WHEN COL_NAME(OBJECT_ID(N'dbo.POD'), N'salespersonId') IS NOT NULL THEN 1 ELSE 0 END;
SELECT @SalesPersonPODExists = CASE WHEN OBJECT_ID(N'dbo.SalesPersonPOD', N'U') IS NOT NULL THEN 1 ELSE 0 END;

IF @PODStationIdExists = 0
BEGIN
    THROW 51000, N'Schema verification failed: dbo.POD does not have column PODStationId.', 1;
END;
IF @PODSalespersonIdExists = 0
BEGIN
    THROW 51001, N'Schema verification failed: dbo.POD does not have column salespersonId.', 1;
END;
IF @SalesPersonPODExists = 1
BEGIN
    THROW 51002, N'Schema verification failed: dbo.SalesPersonPOD already exists. Run only once per migration session.', 1;
END;

-- ----------------------------------------------------------------
-- Report any legacy POD rows where salespersonId references a non‑existent SalesPerson.
-- These rows will be preserved as‑is; the migration will skip them and report them.
-- ----------------------------------------------------------------
IF @PODSalespersonIdExists = 1
BEGIN
    SELECT N'Legacy salespersonId missing from SalesPerson' AS Issue, CAST(p.ID AS NVARCHAR(20))) AS PodID, p.salespersonId AS MissingSpId
    FROM dbo.POD AS p
    WHERE p.salespersonId IS NOT NULL
      AND NOT EXISTS (SELECT 1 FROM dbo.SalesPerson AS sp WHERE sp.ID = p.salespersonId);
END;

-- ----------------------------------------------------------------
-- Create the new many‑to‑many table (if not already created by another process).
-- ----------------------------------------------------------------
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
END;
ELSE
BEGIN
    SELECT N'SalesPersonPOD table already exists – skipping creation.' AS Action;
END;

-- ----------------------------------------------------------------
-- Populate SalesPersonPOD from the legacy single‑owner column.
-- Only rows where salespersonId exists and the assignment does not already exist
-- in the new table are inserted.  Rows where salespersonId is NULL or references
-- a non‑existent SalesPerson are left untouched and reported above.
-- ----------------------------------------------------------------
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

-- Report how many legacy rows were migrated into the new table.
SELECT N'Legacy rows migrated into SalesPersonPOD' AS Summary, COUNT(*) AS MigratedCount
FROM dbo.SalesPersonPOD;

-- ----------------------------------------------------------------
-- Summary: report rows that could not be migrated because the legacy
-- salespersonId is NULL or points to a non‑existent SalesPerson.
-- These rows remain dependent on the legacy column only.
-- ----------------------------------------------------------------
SELECT N'Legacy rows NOT migrated (NULL or missing SalesPerson)' AS Summary, COUNT(*) AS SkippedCount
FROM dbo.POD
WHERE salespersonId IS NULL
   OR NOT EXISTS (SELECT 1 FROM dbo.SalesPerson AS sp WHERE sp.ID = salespersonId);

COMMIT TRANSACTION;

-- ----------------------------------------------------------------
-- Post‑migration report (run outside transaction for readability).
-- ----------------------------------------------------------------
SELECT N'Total PODs' AS Metric, COUNT(*) AS Value FROM dbo.POD
UNION ALL SELECT N'Total SalesPersons', COUNT(*) FROM dbo.SalesPerson
UNION ALL SELECT N'Total SalesPersonPOD rows', COUNT(*) FROM dbo.SalesPersonPOD
UNION ALL SELECT N'PODs with legacy salespersonId set', COUNT(*) FROM dbo.POD WHERE salespersonId IS NOT NULL;