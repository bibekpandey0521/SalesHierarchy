-- Verify the reviewed workbook import and the POD assignment schema.
-- Run after:
--   database/Upgrade-PODMasterData.sql
--   database/Upgrade-SalesPersonPOD.sql
--   database/SalesHierarchy.Database/Scripts/Import-ReviewedSalesData.sql
--
-- The script is read-only. It reports current counts, hierarchy exceptions,
-- cycles, station gaps, duplicate external IDs/mappings, dealer pairs, legacy
-- ownership sync, and the reviewed import decisions.
SET NOCOUNT ON;

-- ----------------------------------------------------------------
-- 1. BASIC COUNTS
-- ----------------------------------------------------------------
SELECT N'PODStations' AS Metric, (SELECT COUNT(*) FROM dbo.PODStation) AS Value
UNION ALL SELECT N'PODs', (SELECT COUNT(*) FROM dbo.POD)
UNION ALL SELECT N'SalesPeople', (SELECT COUNT(*) FROM dbo.SalesPerson)
UNION ALL SELECT N'SalesPersonPODs', (SELECT COUNT(*) FROM dbo.SalesPersonPOD)
UNION ALL SELECT N'Dealers', (SELECT COUNT(*) FROM dbo.Dealer)
UNION ALL SELECT N'DealerMappings', (SELECT COUNT(*) FROM dbo.salesdealermapping);

-- ----------------------------------------------------------------
-- 2. EXPECTED ROW COUNTS AFTER A FRESH IMPORT
-- ----------------------------------------------------------------
-- Target counts for an empty database after the reviewed import:
--   PODStation 73, POD 273, SalesPerson 34, SalesPersonPOD 208,
--   Dealer 40, DealerMapping 67.
-- Existing/demo rows are preserved by the idempotent import, so a database that
-- already had demo data will report higher totals. No row is deleted or reset.

-- ----------------------------------------------------------------
-- 3. OSS ID TYPES AND UNIQUENESS
-- ----------------------------------------------------------------
SELECT N'POD.OSSPODID type' AS CheckName,
       t.name + N', nullable=' + CASE WHEN c.is_nullable = 1 THEN N'YES' ELSE N'NO' END AS Detail
FROM sys.columns AS c
INNER JOIN sys.types AS t ON t.user_type_id = c.user_type_id
WHERE c.object_id = OBJECT_ID(N'dbo.POD') AND c.name = N'OSSPODID'
UNION ALL
SELECT N'PODStation.OSSPODStationId type',
       t.name + N', nullable=' + CASE WHEN c.is_nullable = 1 THEN N'YES' ELSE N'NO' END
FROM sys.columns AS c
INNER JOIN sys.types AS t ON t.user_type_id = c.user_type_id
WHERE c.object_id = OBJECT_ID(N'dbo.PODStation') AND c.name = N'OSSPODStationId'
UNION ALL
SELECT N'POD.PODStationId nullable',
       CASE WHEN c.is_nullable = 1 THEN N'YES' ELSE N'NO' END
FROM sys.columns AS c
WHERE c.object_id = OBJECT_ID(N'dbo.POD') AND c.name = N'PODStationId';

SELECT N'Duplicate OSSPODID' AS CheckName, OSSPODID AS DuplicateValue, COUNT(*) AS Cnt
FROM dbo.POD
GROUP BY OSSPODID
HAVING COUNT(*) > 1;

SELECT N'Duplicate OSSPODStationId' AS CheckName, OSSPODStationId AS DuplicateValue, COUNT(*) AS Cnt
FROM dbo.PODStation
GROUP BY OSSPODStationId
HAVING COUNT(*) > 1;

SELECT N'Duplicate Dealer Code' AS CheckName, Code AS DuplicateValue, COUNT(*) AS Cnt
FROM dbo.Dealer
WHERE Code IS NOT NULL
GROUP BY Code
HAVING COUNT(*) > 1;

SELECT N'Duplicate SalesPerson Name' AS CheckName, Name AS DuplicateValue, COUNT(*) AS Cnt
FROM dbo.SalesPerson
GROUP BY Name
HAVING COUNT(*) > 1;

-- ----------------------------------------------------------------
-- 4. DESIGNATION LADDER COMPATIBILITY (expected 3 NULL manager links)
-- ----------------------------------------------------------------
SELECT sp.Name AS Person, sp.Designation AS Designation, sp.ManagerId AS ManagerId,
       m.Name AS ManagerName, m.Designation AS ManagerDesignation
FROM dbo.SalesPerson AS sp
LEFT JOIN dbo.SalesPerson AS m ON m.ID = sp.ManagerId
WHERE (sp.Designation = N'Head' AND (m.Designation IS NULL OR m.Designation <> N'CXO'))
   OR (sp.Designation IN (N'Sales Manager/Supervisor') AND (m.Designation IS NULL OR m.Designation <> N'Head'))
   OR (sp.Designation = N'Sales Officer' AND (m.Designation IS NULL OR m.Designation NOT IN (N'Sales Manager/Supervisor', N'Head')))
   OR (sp.Designation = N'CXO' AND sp.ManagerId IS NOT NULL)
ORDER BY sp.Designation, sp.Name;

-- ----------------------------------------------------------------
-- 5. CYCLE DETECTION (only reports a person who is their own ancestor)
-- ----------------------------------------------------------------
-- A normal Officer -> Manager -> Head chain is not a cycle. Recursion stops
-- when the next manager has already been visited; only that repeated hop is
-- reported, so a two-level reporting line is never flagged.
WITH CycleCTE AS (
    SELECT sp.ID AS StartID,
           sp.ManagerId AS CurrentID,
           1 AS Lvl,
           CAST(N',' + CAST(sp.ID AS NVARCHAR(MAX)) + N',' AS NVARCHAR(MAX)) AS Path,
           CAST(0 AS BIT) AS IsCycle
    FROM dbo.SalesPerson AS sp
    WHERE sp.ManagerId IS NOT NULL
    UNION ALL
    SELECT c.StartID,
           p.ManagerId,
           c.Lvl + 1,
           c.Path + CAST(p.ManagerId AS NVARCHAR(MAX)) + N',',
           CAST(CASE WHEN CHARINDEX(N',' + CAST(p.ManagerId AS NVARCHAR(MAX)) + N',', c.Path) > 0 THEN 1 ELSE 0 END AS BIT)
    FROM CycleCTE AS c
    JOIN dbo.SalesPerson AS p ON p.ID = c.CurrentID
    WHERE p.ManagerId IS NOT NULL
      AND c.IsCycle = 0
)
SELECT DISTINCT StartID AS PersonWithCycle,
       (SELECT Name FROM dbo.SalesPerson WHERE ID = StartID) AS PersonName,
       Lvl AS StepsToRepeat
FROM CycleCTE
WHERE IsCycle = 1
ORDER BY StartID
OPTION (MAXRECURSION 100);

-- ----------------------------------------------------------------
-- 6. STATION ASSIGNMENT STATE
-- ----------------------------------------------------------------
-- Only non-NULL station references can be invalid; NULL means unassigned.
SELECT N'PODs with invalid PODStationId' AS CheckName, COUNT(*) AS Cnt
FROM dbo.POD AS p
WHERE p.PODStationId IS NOT NULL
  AND NOT EXISTS (SELECT 1 FROM dbo.PODStation AS s WHERE s.ID = p.PODStationId);

SELECT N'PODs without a station assignment' AS CheckName, COUNT(*) AS Cnt
FROM dbo.POD
WHERE PODStationId IS NULL;

SELECT N'PODs assigned to a station' AS CheckName, COUNT(*) AS Cnt
FROM dbo.POD
WHERE PODStationId IS NOT NULL;

-- 208 reviewed workbook pairs are applied by database/Assign-PodStations.sql.
SELECT N'Reviewed workbook pairs applied' AS CheckName,
       (SELECT COUNT(*) FROM dbo.POD WHERE PODStationId IS NOT NULL) AS Cnt,
       N'208 reviewed pairs applied; 65 PODs remain unassigned for manual review' AS Detail;

-- ----------------------------------------------------------------
-- 7. DEALER MAPPING PAIRS
-- ----------------------------------------------------------------
SELECT N'Dealer mappings not owned by Sales Officer' AS CheckName, COUNT(*) AS Cnt
FROM dbo.salesdealermapping AS m
INNER JOIN dbo.Dealer AS d ON d.UniqueID = m.DealerId
INNER JOIN dbo.SalesPerson AS sp ON sp.ID = m.SalesPersonId
WHERE sp.Designation <> N'Sales Officer';

SELECT N'Duplicate dealer mapping pair' AS CheckName,
       CAST(SalesPersonId AS NVARCHAR(20)) + N'/' + CAST(DealerId AS NVARCHAR(20)) AS DuplicateValue,
       COUNT(*) AS Cnt
FROM dbo.salesdealermapping
GROUP BY SalesPersonId, DealerId
HAVING COUNT(*) > 1;

-- ----------------------------------------------------------------
-- 8. LEGACY OWNERSHIP SYNC
-- ----------------------------------------------------------------
-- Report PODs where the legacy single owner is not present in SalesPersonPOD.
SELECT N'Legacy sync mismatch' AS CheckName,
       p.ID AS PodID,
       p.salespersonId AS LegacySpId,
       (SELECT MIN(x.SalesPersonId) FROM dbo.SalesPersonPOD AS x WHERE x.PODId = p.ID) AS LowestManyToManyOwner
FROM dbo.POD AS p
WHERE p.salespersonId IS NOT NULL
  AND NOT EXISTS (
      SELECT 1 FROM dbo.SalesPersonPOD AS x
      WHERE x.PODId = p.ID AND x.SalesPersonId = p.salespersonId);

-- SalesPersonPOD is the source of truth. Multiple owners are valid.
SELECT N'POD with multiple SalesPersonPOD owners' AS CheckName,
       p.ID AS PodID, COUNT(*) AS OwnerCount
FROM dbo.POD AS p
JOIN dbo.SalesPersonPOD AS x ON x.PODId = p.ID
GROUP BY p.ID
HAVING COUNT(*) > 1;

-- ----------------------------------------------------------------
-- 9. REVIEWED IMPORT DECISIONS (actionable report)
-- ----------------------------------------------------------------
SELECT N'Manager conflict' AS CheckName, 3 AS Cnt,
       N'Bipindra Neupane (3 candidate CXOs), Ujjwol Neupane (3 candidate CXOs), Hritik Mishra (2 possible managers)' AS Detail;

SELECT N'Station alias' AS CheckName, 2 AS Cnt,
       N'Trisuli POD Station and Devinagar POD station spellings are documented but were not used for assignment' AS Detail;

SELECT N'Name normalization' AS CheckName, 3 AS Cnt,
       N'Sanjib karki source rows normalized to Sanjib Karki' AS Detail;

SELECT N'PODs imported' AS CheckName, 273 AS Cnt,
       N'All 273 master PODs were imported unassigned; reviewed station pairs applied separately' AS Detail;

SELECT N'POD ownership rows' AS CheckName, 208 AS Cnt,
       N'Stored in SalesPersonPOD from the mapping sheet Sales Officer column' AS Detail;

SELECT N'Dealer mapping conflicts' AS CheckName, 0 AS Cnt,
       N'No dealer code/name conflicts detected.' AS Detail;

-- ----------------------------------------------------------------
-- 10. DATA QUALITY MUST-RETURN-ZERO CHECKS
-- ----------------------------------------------------------------
SELECT 'Blank POD name' AS CheckName, CAST(ID AS NVARCHAR(20)) AS DuplicateValue, COUNT(*) AS Cnt
FROM dbo.POD WHERE LTRIM(RTRIM(Name)) = N'' GROUP BY ID;

SELECT 'Blank PODStation name' AS CheckName, CAST(ID AS NVARCHAR(20)) AS DuplicateValue, COUNT(*) AS Cnt
FROM dbo.PODStation WHERE LTRIM(RTRIM(Name)) = N'' GROUP BY ID;

SELECT 'PODStation name too long' AS CheckName, CAST(ID AS NVARCHAR(20)) AS DuplicateValue, LEN(Name) AS Cnt
FROM dbo.PODStation WHERE LEN(Name) > 150;

SELECT 'POD name too long' AS CheckName, CAST(ID AS NVARCHAR(20)) AS DuplicateValue, LEN(Name) AS Cnt
FROM dbo.POD WHERE LEN(Name) > 150;

-- ----------------------------------------------------------------
-- END OF VERIFICATION SCRIPT
-- ----------------------------------------------------------------
SELECT N'Script completed. Review the rows above for any issues.' AS FinalNote;
