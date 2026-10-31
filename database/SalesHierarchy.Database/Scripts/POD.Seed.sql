-- Seed POD master data from the reviewed pods.xlsx `pod` sheet.
--
-- SOURCE STATUS: `pods.xlsx` and `Sales Mapping With Dealer and POD.xlsx` were
-- reviewed on 2026-10-29. The applied POD/station/ownership rows are in
-- Import-ReviewedSalesData.sql. This template remains for future reviewed batches.
-- No POD rows or station relationships are invented here. Add verified rows to
-- @Source, review the unresolved-station report, then execute this script.
--
-- Columns:
--   OSSPODID         = the workbook PODId column (stored in POD.OSSPODID)
--   Name             = the workbook PODName column
--   OSSPODStationId  = optional external station ID. Leave NULL when the
--                      authoritative source has no verified station; station
--                      assignment is managed separately in the web application.
--
-- PODStationId is nullable in the target schema, so a POD without a verified
-- station is inserted with PODStationId = NULL and can be assigned later.
SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;

DECLARE @Source TABLE
(
    OSSPODID INT NOT NULL,
    Name NVARCHAR(150) NOT NULL,
    OSSPODStationId INT NULL
);

-- Add verified workbook rows here, for example:
-- INSERT INTO @Source (OSSPODID, Name, OSSPODStationId) VALUES
--     (1, N'Samakhusi', NULL);

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
    OSSPODID INT NOT NULL,
    Name NVARCHAR(150) NOT NULL,
    PODStationId INT NULL
);

INSERT INTO @Resolved (OSSPODID, Name, PODStationId)
SELECT s.OSSPODID, s.Name, st.ID
FROM @Source AS s
LEFT JOIN dbo.PODStation AS st
    ON st.OSSPODStationId = s.OSSPODStationId;

-- Report source rows whose provisional station ID cannot be resolved.
SELECT s.OSSPODID, s.Name, s.OSSPODStationId, N'Missing or unknown station' AS Reason
FROM @Source AS s
WHERE s.OSSPODStationId IS NOT NULL
  AND NOT EXISTS
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
   OR (p.PODStationId <> r.PODStationId AND r.PODStationId IS NOT NULL);

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
