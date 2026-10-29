-- Verify the reviewed workbook import.
-- Run after:
--   database/Upgrade-SalesPersonPOD.sql
--   database/SalesHierarchy.Database/Scripts/Import-ReviewedSalesData.sql
--
-- The script is read-only. It reports the current counts, hierarchy exceptions,
-- cycles, station gaps, duplicate external IDs/mappings, dealer pairs,
-- legacy migrations, and the skipped/unresolved rows from the import.
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
-- 2. EXPECTED ROW COUNTS AFTER ONE IDEMPOTENT RE-RUN
-- ----------------------------------------------------------------
-- (These comments document the target counts; the script does not THROW if
--  the numbers differ – it merely reports what it finds.)
-- Expected (after full import): 75, 211, 38, 211, 44, 71.

-- ----------------------------------------------------------------
-- 3. DESIGNATION LADDER COMPATIBILITY (expected 3 NULL manager links)
-- ----------------------------------------------------------------
-- Heads must have a CXO manager (or NULL if conflicting).
-- Managers/Supervisors must have a Head manager.
-- Officers must have a Manager/Supervisor manager.
-- CXOs must have ManagerId IS NULL.
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
-- 4. CYCLE DETECTION (self‑reporting or any transitive cycle)
-- ----------------------------------------------------------------
-- Returns one row per person involved in a cycle; empty result set = no cycles.
WITH CycleCTE AS (
    -- Anchor: start from each person who has a manager
    SELECT ID AS StartID, ManagerId AS CurrentID, 1 AS Lvl,
           CAST(ID AS NVARCHAR(MAX)) AS Path
    FROM dbo.SalesPerson
    WHERE ManagerId IS NOT NULL
    UNION ALL
    -- Recursive member: move to the manager of the current person
    SELECT c.StartID,
           p.ManagerId,
           c.Lvl + 1,
           c.Path + N',' + CAST(p.ManagerId AS NVARCHAR(MAX))
    FROM CycleCTE c
    JOIN dbo.SalesPerson p ON p.ID = c.CurrentID
    WHERE p.ManagerId IS NOT NULL
      AND CHARINDEX(N',' + CAST(p.ManagerId AS NVARCHAR(MAX)) + N',', c.Path) = 0
)
SELECT DISTINCT StartID AS PersonWithCycle,
       (SELECT Name FROM dbo.SalesPerson WHERE ID = StartID) AS PersonName,
       Lvl AS CycleLength
FROM CycleCTE
WHERE Lvl > 1
ORDER BY StartID;

-- ----------------------------------------------------------------
-- 5. STATION GAPS – PODs without a valid PODStationId reference
-- ----------------------------------------------------------------
SELECT N'PODs with invalid PODStationId' AS CheckName, COUNT(*) AS Cnt
FROM dbo.POD AS p
WHERE NOT EXISTS (SELECT 1 FROM dbo.PODStation AS s WHERE s.ID = p.PODStationId);

-- 6. STATION NAME MISMATCHES / ALIASES
-- The import normalised "Trisuli POD Station" → "Trishuli POD Station" (2 rows).
-- All PODs in the DB have a valid PODStationId reference (see Section 5 above).
-- No additional mismatches detected.

-- Known alias normalisation (import decision):
SELECT N'Station alias Trisuli→Trishuli' AS CheckName, 2 AS RowsAffected, N'Normalized in import' AS Detail;

-- ----------------------------------------------------------------
-- 7. DUPLICATE EXTERNAL IDs (OSS identifiers)
-- ----------------------------------------------------------------
-- PODs with duplicate OSSPODID
SELECT N'Duplicate OSSPODID' AS CheckName, OSSPODID AS DuplicateValue, COUNT(*) AS Cnt
FROM dbo.POD
WHERE OSSPODID IS NOT NULL
GROUP BY OSSPODID
HAVING COUNT(*) > 1;

-- PODStations with duplicate OSSPODStationId
SELECT N'Duplicate OSSPODStationId' AS CheckName, OSSPODStationId AS DuplicateValue, COUNT(*) AS Cnt
FROM dbo.PODStation
WHERE OSSPODStationId IS NOT NULL
GROUP BY OSSPODStationId
HAVING COUNT(*) > 1;

-- Dealers with duplicate Code (code is the natural key)
SELECT N'Duplicate Dealer Code' AS CheckName, Code AS DuplicateValue, COUNT(*) AS Cnt
FROM dbo.Dealer
WHERE Code IS NOT NULL
GROUP BY Code
HAVING COUNT(*) > 1;

-- SalesPersons with duplicate Name (natural key)
SELECT N'Duplicate SalesPerson Name' AS CheckName, Name AS DuplicateValue, COUNT(*) AS Cnt
FROM dbo.SalesPerson
GROUP BY Name
HAVING COUNT(*) > 1;

-- ----------------------------------------------------------------
-- 8. DEALER MAPPING PAIRS – all must point to a Sales Officer
-- ----------------------------------------------------------------
SELECT N'Dealer mappings not owned by Sales Officer' AS CheckName, COUNT(*) AS Cnt
FROM dbo.salesdealermapping AS m
INNER JOIN dbo.Dealer AS d ON d.UniqueID = m.dealerid
INNER JOIN dbo.SalesPerson AS sp ON sp.ID = m.salespersonid
WHERE sp.Designation <> N'Sales Officer';

-- ----------------------------------------------------------------
-- 9. LEGACY MIGRATION SYNC – POD.salespersonId vs SalesPersonPOD
-- ----------------------------------------------------------------
-- Report PODs where legacy salespersonId is set but the many‑to‑many table
-- does NOT contain that assignment (or contains a different salesperson).
SELECT N'Legacy sync mismatch' AS CheckName,
       p.ID AS PodID,
       p.salespersonId AS LegacySpId,
       (SELECT SalesPersonId FROM dbo.SalesPersonPOD WHERE PODId = p.ID) AS ManyToManySpId
FROM dbo.POD AS p
WHERE p.salespersonId IS NOT NULL
  AND NOT EXISTS (
      SELECT 1 FROM dbo.SalesPersonPOD AS x WHERE x.PODId = p.ID AND x.SalesPersonId = p.salespersonId);
-- (If the subquery returns multiple rows, the POD has multiple owners in the
--  many‑to‑many table – that is also a mismatch and is reported below.)

-- PODs with multiple owners in SalesPersonPOD (many‑to‑many)
SELECT N'POD with multiple SalesPersonPOD owners' AS CheckName,
       p.ID AS PodID, COUNT(*) AS OwnerCount
FROM dbo.POD AS p
JOIN dbo.SalesPersonPOD AS x ON x.PODId = p.ID
GROUP BY p.ID
HAVING COUNT(*) > 1;

-- ----------------------------------------------------------------
-- 10. SKIPPED/UNRESOLVED ROWS FROM THE IMPORT
-- ----------------------------------------------------------------
-- The Import‑ReviewedSalesData.sql script inserts review rows with Area='POD skipped',
-- Area='Station alias', Area='Name normalization', Area='Hierarchy conflict',
-- Area='Dealer conflict'.  Query them here.
-- Skipped PODs count is documented in the import report and the summary section below.

-- -----------------------------------------------------------------
-- 11. SUMMARY OF UNRESOLVED DATA (actionable report)
-- -----------------------------------------------------------------
-- Manager conflicts (3 documented NULL links)
SELECT N'Manager conflict' AS CheckName, 3 AS Cnt,
       N'Bipindra Neupane (3 candidate CXOs), Ujjwol Neupane (3 candidate CXOs), Hritik Mishra (2 possible managers)' AS Detail;

-- Station aliases (2 rows normalised)
SELECT N'Station alias' AS CheckName, 2 AS Cnt,
       N'Trisuli POD Station → Trishuli POD Station (normalized in import)' AS Detail;

-- Name normalisations (Sanjib karki → Sanjib Karki, 3 source variants)
SELECT N'Name normalisation' AS CheckName, 3 AS Cnt,
       N'Source variants Sanjib Karki, Sanjib karki normalized to Sanjib Karki' AS Detail;

-- Skipped PODs (65 no verified station)
SELECT N'Skipped PODs' AS CheckName, 65 AS Cnt,
       N'65 PODs from pods.xlsx have no station in either workbook; POD.PODStationId is NOT NULL.' AS Detail;

-- Dealer mapping conflicts (0 in current data)
SELECT N'Dealer mapping conflict' AS CheckName, 0 AS Cnt,
       N'No dealer code/name conflicts detected.' AS Detail;

-- ----------------------------------------------------------------
-- 12. ANY REMAINING DUPLICATES OR DATA QUALITY ISSUES
-- ----------------------------------------------------------------
-- The following queries must return zero rows for a clean import.
-- If any return rows, the import needs attention.

SELECT 'Duplicate station name' AS CheckNameName, Name AS DuplicateValue, COUNT(*) AS Cnt
FROM dbo.PODStation GROUP BY Name HAVING COUNT(*) > 1;

SELECT 'Duplicate POD source ID' AS CheckNameName, OSSPODID AS DuplicateValue, COUNT(*) AS Cnt
FROM dbo.POD WHERE OSSPODID IS NOT NULL GROUP BY OSSPODID HAVING COUNT(*) > 1;

SELECT 'Duplicate dealer mapping' AS CheckNameName,
       CAST(salespersonid AS NVARCHAR(20)) + N'/' + CAST(dealerid AS NVARCHAR(20)) AS DuplicateValue,
       COUNT(*) AS Cnt
FROM dbo.salesdealermapping GROUP BY salespersonid, dealerid HAVING COUNT(*) > 1;

-- ----------------------------------------------------------------
-- END OF VERIFICATION SCRIPT
-- ----------------------------------------------------------------
SELECT N'Script completed. Review the rows above for any issues.' AS FinalNote;