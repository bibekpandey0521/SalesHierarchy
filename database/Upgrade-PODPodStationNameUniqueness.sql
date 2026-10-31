-- ============================================================================
-- Upgrade-PODPodStationNameUniqueness.sql
-- Replaces global POD.Name uniqueness with a filtered unique index that applies
-- only to assigned PODs: (PODStationId, Name) must be unique when PODStationId IS NOT NULL.
--
-- Business rules:
--   * One POD belongs to one station at a time.
--   * Assigned POD names must be unique within that station.
--   * Multiple unassigned PODs may share the same name (index is filtered).
--
-- Safety rules:
--   * NO rows are deleted, truncated or recreated.
--   * The script refuses to run while duplicate assigned (PODStationId, Name)
--     pairs exist and reports them; it never auto-fixes data.
-- ============================================================================
SET NOCOUNT ON;
SET XACT_ABORT ON;

IF OBJECT_ID(N'dbo.POD', N'U') IS NULL
BEGIN
    THROW 51100, 'Required table is missing. Run the table creation scripts first.', 1;
END;

BEGIN TRANSACTION;
BEGIN TRY
    IF EXISTS
    (
        SELECT 1
        FROM dbo.POD AS p
        GROUP BY p.PODStationId, p.Name
        HAVING COUNT(*) > 1 AND MIN(p.PODStationId) IS NOT NULL
    )
    BEGIN
        SELECT p.PODStationId, p.Name, COUNT(*) AS DuplicateCount
        FROM dbo.POD AS p
        GROUP BY p.PODStationId, p.Name
        HAVING COUNT(*) > 1 AND MIN(p.PODStationId) IS NOT NULL;

        THROW 51103, 'Assigned POD duplicates (PODStationId, Name) exist. Resolve them before creating the filtered unique index. No changes were committed.', 1;
    END;

    -- Replace the legacy global unique index/constraint on POD.Name with the
    -- filtered unique index for assigned PODs. Safe because the filtered index
    -- permits shared names only for unassigned PODs.
    IF EXISTS
    (
        SELECT 1
        FROM sys.indexes
        WHERE object_id = OBJECT_ID(N'dbo.POD')
          AND name = N'UQ_POD_Name'
    )
    BEGIN
        DROP INDEX UQ_POD_Name ON dbo.POD;
    END;

    IF NOT EXISTS
    (
        SELECT 1
        FROM sys.indexes
        WHERE object_id = OBJECT_ID(N'dbo.POD')
          AND name = N'UX_POD_Station_Name'
    )
    BEGIN
        CREATE UNIQUE NONCLUSTERED INDEX UX_POD_Station_Name
        ON dbo.POD (PODStationId, Name)
        WHERE PODStationId IS NOT NULL;
    END;

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0
        ROLLBACK TRANSACTION;

    THROW;
END CATCH;

-- Verification queries (read-only).
SELECT COUNT(*) AS AssignedPodCount FROM dbo.POD WHERE PODStationId IS NOT NULL;
SELECT COUNT(*) AS UnassignedPodCount FROM dbo.POD WHERE PODStationId IS NULL;
SELECT COUNT(*) AS AssignedDuplicateStationNamePairs
FROM (
    SELECT PODStationId, Name
    FROM dbo.POD
    WHERE PODStationId IS NOT NULL
    GROUP BY PODStationId, Name
    HAVING COUNT(*) > 1
) AS duplicates;
