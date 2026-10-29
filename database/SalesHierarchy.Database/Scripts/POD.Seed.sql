-- Seed POD master data from the reviewed pods.xlsx `pod` sheet.
--
-- SOURCE STATUS: `pods.xlsx` and `Sales Mapping With Dealer and POD.xlsx` were
-- reviewed on 2026-10-29. The applied POD/station rows are in
-- Import-ReviewedSalesData.sql. This template remains for future reviewed batches.
-- No POD rows or station relationships are invented here. Add verified rows to
-- @Source, review the unresolved-station report, then execute this script.
--
-- Columns:
--   OSSPODID         = the workbook PODId column (stored in POD.OSSPODID)
--   Name             = the workbook PODName column
--   OSSPODStationId  = NULL unless the authoritative source contains the POD's station.
--                      This script never guesses a station from names, numbers or row order.
--
-- Because dbo.POD.PODStationId is NOT NULL, a POD without a verified station is
-- reported as unresolved and is not inserted. The station must be supplied by the
-- authoritative data owner before those POD rows can be seeded.
SET NOCOUNT ON;
SET XACT_ABORT ON;

DECLARE @Source TABLE
(
    OSSPODID NVARCHAR(50) NOT NULL,
    Name NVARCHAR(400) NOT NULL,
    OSSPODStationId NVARCHAR(50) NULL
);

-- Add verified workbook rows here, for example:
-- INSERT INTO @Source (OSSPODID, Name, OSSPODStationId) VALUES
--     (N'...', N'...', NULL);

IF NOT EXISTS (SELECT 1 FROM @Source)
BEGIN
    PRINT 'POD.Seed.sql: no reviewed source rows supplied; nothing was changed.';
    RETURN;
END;

IF EXISTS
(
    SELECT OSSPODID
    FROM @Source
    GROUP BY OSSPODID
    HAVING COUNT(*) > 1
)
BEGIN
    THROW 51001, 'The source contains duplicate OSSPODID values. Resolve them before seeding.', 1;
END;

BEGIN TRANSACTION;

-- Resolve internal station IDs through the verified external station IDs.
DECLARE @Resolved TABLE
(
    OSSPODID NVARCHAR(50) NOT NULL,
    Name NVARCHAR(400) NOT NULL,
    PODStationId INT NOT NULL
);

INSERT INTO @Resolved (OSSPODID, Name, PODStationId)
SELECT s.OSSPODID, s.Name, st.ID
FROM @Source AS s
INNER JOIN dbo.PODStation AS st
    ON st.OSSPODStationId = s.OSSPODStationId;

-- Report PODs that cannot be inserted because the source has no verified station.
SELECT s.OSSPODID, s.Name, s.OSSPODStationId, N'Missing or unknown station' AS Reason
FROM @Source AS s
WHERE s.OSSPODStationId IS NULL
   OR NOT EXISTS
   (
       SELECT 1
       FROM dbo.PODStation AS st
       WHERE st.OSSPODStationId = s.OSSPODStationId
   );

-- Report reviewed differences instead of overwriting existing master data.
SELECT r.OSSPODID, r.Name AS SourceName, p.Name AS ExistingName,
       r.PODStationId AS SourceStationId, p.PODStationId AS ExistingStationId
FROM @Resolved AS r
INNER JOIN dbo.POD AS p
    ON p.OSSPODID = r.OSSPODID
WHERE p.Name <> r.Name
   OR p.PODStationId <> r.PODStationId;

INSERT INTO dbo.POD (OSSPODID, Name, PODStationId)
SELECT r.OSSPODID, r.Name, r.PODStationId
FROM @Resolved AS r
WHERE NOT EXISTS
(
    SELECT 1
    FROM dbo.POD AS p
    WHERE p.OSSPODID = r.OSSPODID
);

COMMIT TRANSACTION;

SELECT COUNT(*) AS InsertedOrMatchedPODs FROM dbo.POD;
