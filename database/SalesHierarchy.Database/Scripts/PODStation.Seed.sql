-- Seed PODStation master data from the reviewed pods.xlsx `station` sheet.
--
-- SOURCE STATUS: `pods.xlsx` was reviewed on 2026-10-29. The applied station rows
-- are in Import-ReviewedSalesData.sql. This template remains for future reviewed
-- batches. No station rows are invented here. Add verified rows to @Source, review
-- the conflict report, then execute this script. The script is idempotent and never
-- overwrites an existing station.
--
-- Columns:
--   OSSPODStationId = the workbook ID column (stored in PODStation.OSSPODStationId)
--   Name            = the workbook station name
SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;

DECLARE @Source TABLE
(
    OSSPODStationId INT NOT NULL,
    Name NVARCHAR(150) NOT NULL
);

-- Add verified workbook rows here, for example:
-- INSERT INTO @Source (OSSPODStationId, Name) VALUES
--     (1, N'Dharan POD Station');

IF NOT EXISTS (SELECT 1 FROM @Source)
BEGIN
    PRINT 'PODStation.Seed.sql: no reviewed source rows supplied; nothing was changed.';
    RETURN;
END;

IF EXISTS
(
    SELECT OSSPODStationId
    FROM @Source
    GROUP BY OSSPODStationId
    HAVING COUNT(*) > 1
)
BEGIN
    THROW 51000, 'The source contains duplicate OSSPODStationId values. Resolve them before seeding.', 1;
END;

BEGIN TRANSACTION;

-- Existing rows whose external ID is present but whose name differs are reported
-- and left untouched so reviewed production names are never silently replaced.
SELECT s.OSSPODStationId, s.Name AS SourceName, d.Name AS ExistingName
FROM @Source AS s
INNER JOIN dbo.PODStation AS d
    ON d.OSSPODStationId = s.OSSPODStationId
WHERE d.Name <> s.Name;

INSERT INTO dbo.PODStation (OSSPODStationId, Name)
SELECT s.OSSPODStationId, s.Name
FROM @Source AS s
WHERE NOT EXISTS
(
    SELECT 1
    FROM dbo.PODStation AS d
    WHERE d.OSSPODStationId = s.OSSPODStationId
);

COMMIT TRANSACTION;

SELECT COUNT(*) AS InsertedOrMatchedStations FROM dbo.PODStation;
