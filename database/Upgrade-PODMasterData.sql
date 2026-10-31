-- ============================================================================
-- Upgrade-PODMasterData.sql
-- Target schema for reviewed POD master data and assignments.
--
-- Changes made (only when needed, so the script is idempotent):
--   * PODStation.OSSPODStationId  -> INT NOT NULL + unique index
--   * PODStation.Name             -> NVARCHAR(150) NOT NULL
--   * POD.OSSPODID                -> INT NOT NULL + unique index
--   * POD.Name                    -> NVARCHAR(150) NOT NULL
--   * POD.PODStationId            -> INT NULL (station assignment is optional)
--   * Dealer.Name                 -> NVARCHAR(150) NOT NULL
--   * Dealer.Code                 -> NVARCHAR(50) NULL + unique filtered index
--   * DealerMapping          -> unique (SalesPersonId, DealerId) index
--
-- Safety rules:
--   * NO rows are deleted, truncated, or recreated.
--   * Identity users/roles are untouched.
--   * The script refuses to run and reports the exact rows when existing data
--     cannot be converted safely (NULL, non-numeric, <= 0, duplicates, blanks).
--     It never invents or silently rewrites OSS identifiers.
--   * Back up the database before running this script against live data.
-- ============================================================================
SET NOCOUNT ON;
SET XACT_ABORT ON;

IF OBJECT_ID(N'dbo.PODStation', N'U') IS NULL
   OR OBJECT_ID(N'dbo.POD', N'U') IS NULL
   OR OBJECT_ID(N'dbo.Dealer', N'U') IS NULL
   OR OBJECT_ID(N'dbo.salesdealermapping', N'U') IS NULL
BEGIN
    THROW 51100, 'Required tables are missing. Run the table creation scripts first.', 1;
END;

IF COL_LENGTH(N'dbo.PODStation', N'OSSPODStationId') IS NULL
   OR COL_LENGTH(N'dbo.PODStation', N'Name') IS NULL
   OR COL_LENGTH(N'dbo.POD', N'OSSPODID') IS NULL
   OR COL_LENGTH(N'dbo.POD', N'Name') IS NULL
   OR COL_LENGTH(N'dbo.POD', N'PODStationId') IS NULL
   OR COL_LENGTH(N'dbo.POD', N'salespersonId') IS NULL
BEGIN
    THROW 51101, 'Required columns are missing. Review the table definitions before upgrading.', 1;
END;

-- ---------------------------------------------------------------- preflight
DECLARE @Issues TABLE
(
    Issue NVARCHAR(200) NOT NULL,
    Detail NVARCHAR(800) NOT NULL
);

INSERT INTO @Issues (Issue, Detail)
SELECT N'PODStation has a NULL OSSPODStationId',
       N'ID ' + CAST(ID AS NVARCHAR(20)) + N', Name ''' + ISNULL(Name, N'') + N''''
FROM dbo.PODStation
WHERE OSSPODStationId IS NULL;

INSERT INTO @Issues (Issue, Detail)
SELECT N'PODStation has a non-numeric OSSPODStationId',
       N'ID ' + CAST(ID AS NVARCHAR(20)) + N', OSSPODStationId ''' + ISNULL(CAST(OSSPODStationId AS NVARCHAR(100)), N'') + N''''
FROM dbo.PODStation
WHERE OSSPODStationId IS NOT NULL
  AND TRY_CONVERT(INT, OSSPODStationId) IS NULL;

INSERT INTO @Issues (Issue, Detail)
SELECT N'PODStation OSSPODStationId must be positive',
       N'ID ' + CAST(ID AS NVARCHAR(20)) + N', OSSPODStationId ' + CAST(OSSPODStationId AS NVARCHAR(50))
FROM dbo.PODStation
WHERE TRY_CONVERT(INT, OSSPODStationId) IS NOT NULL
  AND TRY_CONVERT(INT, OSSPODStationId) <= 0;

INSERT INTO @Issues (Issue, Detail)
SELECT N'Duplicate PODStation OSSPODStationId',
       N'OSSPODStationId ' + CAST(OSSPODStationId AS NVARCHAR(50)) + N' is used by ' + CAST(COUNT(*) AS NVARCHAR(20)) + N' rows'
FROM dbo.PODStation
WHERE OSSPODStationId IS NOT NULL
GROUP BY OSSPODStationId
HAVING COUNT(*) > 1;

INSERT INTO @Issues (Issue, Detail)
SELECT N'PODStation has a blank name',
       N'ID ' + CAST(ID AS NVARCHAR(20))
FROM dbo.PODStation
WHERE Name IS NULL OR LTRIM(RTRIM(Name)) = N'';

INSERT INTO @Issues (Issue, Detail)
SELECT N'PODStation name exceeds 150 characters',
       N'ID ' + CAST(ID AS NVARCHAR(20)) + N', length ' + CAST(LEN(Name) AS NVARCHAR(20))
FROM dbo.PODStation
WHERE LEN(Name) > 150;

INSERT INTO @Issues (Issue, Detail)
SELECT N'POD has a NULL OSSPODID',
       N'ID ' + CAST(ID AS NVARCHAR(20)) + N', Name ''' + ISNULL(Name, N'') + N''''
FROM dbo.POD
WHERE OSSPODID IS NULL;

INSERT INTO @Issues (Issue, Detail)
SELECT N'POD has a non-numeric OSSPODID',
       N'ID ' + CAST(ID AS NVARCHAR(20)) + N', OSSPODID ''' + ISNULL(CAST(OSSPODID AS NVARCHAR(100)), N'') + N''', Name ''' + ISNULL(Name, N'') + N''''
FROM dbo.POD
WHERE OSSPODID IS NOT NULL
  AND TRY_CONVERT(INT, OSSPODID) IS NULL;

INSERT INTO @Issues (Issue, Detail)
SELECT N'POD OSSPODID must be positive',
       N'ID ' + CAST(ID AS NVARCHAR(20)) + N', OSSPODID ' + CAST(OSSPODID AS NVARCHAR(50))
FROM dbo.POD
WHERE TRY_CONVERT(INT, OSSPODID) IS NOT NULL
  AND TRY_CONVERT(INT, OSSPODID) <= 0;

INSERT INTO @Issues (Issue, Detail)
SELECT N'Duplicate POD OSSPODID',
       N'OSSPODID ' + CAST(OSSPODID AS NVARCHAR(50)) + N' is used by ' + CAST(COUNT(*) AS NVARCHAR(20)) + N' rows'
FROM dbo.POD
WHERE OSSPODID IS NOT NULL
GROUP BY OSSPODID
HAVING COUNT(*) > 1;

INSERT INTO @Issues (Issue, Detail)
SELECT N'POD has a blank name',
       N'ID ' + CAST(ID AS NVARCHAR(20))
FROM dbo.POD
WHERE Name IS NULL OR LTRIM(RTRIM(Name)) = N'';

INSERT INTO @Issues (Issue, Detail)
SELECT N'POD name exceeds 150 characters',
       N'ID ' + CAST(ID AS NVARCHAR(20)) + N', length ' + CAST(LEN(Name) AS NVARCHAR(20))
FROM dbo.POD
WHERE LEN(Name) > 150;

INSERT INTO @Issues (Issue, Detail)
SELECT N'POD references a missing PODStation',
       N'POD ID ' + CAST(p.ID AS NVARCHAR(20)) + N', PODStationId ' + CAST(p.PODStationId AS NVARCHAR(20))
FROM dbo.POD AS p
WHERE p.PODStationId IS NOT NULL
  AND NOT EXISTS (SELECT 1 FROM dbo.PODStation AS s WHERE s.ID = p.PODStationId);

INSERT INTO @Issues (Issue, Detail)
SELECT N'POD references a missing legacy SalesPerson',
       N'POD ID ' + CAST(p.ID AS NVARCHAR(20)) + N', salespersonId ' + CAST(p.salespersonId AS NVARCHAR(20))
FROM dbo.POD AS p
WHERE p.salespersonId IS NOT NULL
  AND NOT EXISTS (SELECT 1 FROM dbo.SalesPerson AS sp WHERE sp.ID = p.salespersonId);

INSERT INTO @Issues (Issue, Detail)
SELECT N'Dealer has a blank name and no code to use as a fallback',
       N'UniqueID ' + CAST(UniqueID AS NVARCHAR(20))
FROM dbo.Dealer
WHERE (Name IS NULL OR LTRIM(RTRIM(Name)) = N'')
  AND (Code IS NULL OR LTRIM(RTRIM(Code)) = N'');

INSERT INTO @Issues (Issue, Detail)
SELECT N'Duplicate Dealer Code',
       N'Code ''' + Code + N''' is used by ' + CAST(COUNT(*) AS NVARCHAR(20)) + N' rows'
FROM dbo.Dealer
WHERE Code IS NOT NULL
GROUP BY Code
HAVING COUNT(*) > 1;

INSERT INTO @Issues (Issue, Detail)
SELECT N'Duplicate dealer mapping pair',
       N'SalesPersonId ' + CAST(SalesPersonId AS NVARCHAR(20)) + N', DealerId ' + CAST(DealerId AS NVARCHAR(20)) + N' has ' + CAST(COUNT(*) AS NVARCHAR(20)) + N' rows'
FROM dbo.salesdealermapping
GROUP BY SalesPersonId, DealerId
HAVING COUNT(*) > 1;

IF EXISTS (SELECT 1 FROM @Issues)
BEGIN
    SELECT Issue, Detail
    FROM @Issues
    ORDER BY Issue, Detail;

    THROW 51102, 'POD master data upgrade stopped: existing rows cannot be converted safely. Review the Issue/Detail rows above, correct the data, take a backup, and run this script again. No changes were committed.', 1;
END;

-- ---------------------------------------------------------------- upgrade
BEGIN TRANSACTION;

BEGIN TRY

    -- Dealer names must fall back to the dealer code when the name is blank.
    UPDATE dbo.Dealer
    SET Name = Code
    WHERE (Name IS NULL OR LTRIM(RTRIM(Name)) = N'')
      AND Code IS NOT NULL
      AND LTRIM(RTRIM(Code)) <> N'';

    -- PODStation.OSSPODStationId -> INT NOT NULL
    IF EXISTS
    (
        SELECT 1
        FROM sys.columns AS c
        INNER JOIN sys.types AS t ON t.user_type_id = c.user_type_id
        WHERE c.object_id = OBJECT_ID(N'dbo.PODStation')
          AND c.name = N'OSSPODStationId'
          AND (t.name <> N'int' OR c.is_nullable = 1)
    )
    BEGIN
        IF EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.PODStation') AND name = N'UX_PODStation_OSSPODStationId')
            DROP INDEX UX_PODStation_OSSPODStationId ON dbo.PODStation;

        ALTER TABLE dbo.PODStation ALTER COLUMN OSSPODStationId INT NOT NULL;
    END;

    IF EXISTS
    (
        SELECT 1
        FROM sys.columns AS c
        INNER JOIN sys.types AS t ON t.user_type_id = c.user_type_id
        WHERE c.object_id = OBJECT_ID(N'dbo.PODStation')
          AND c.name = N'Name'
          AND (t.name <> N'nvarchar' OR c.max_length <> 300 OR c.is_nullable = 1)
    )
    BEGIN
        ALTER TABLE dbo.PODStation ALTER COLUMN Name NVARCHAR(150) NOT NULL;
    END;

    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.PODStation') AND name = N'UX_PODStation_OSSPODStationId')
    BEGIN
        CREATE UNIQUE INDEX UX_PODStation_OSSPODStationId ON dbo.PODStation (OSSPODStationId);
    END;

    -- POD.PODStationId -> INT NULL. Drop/re-add the FK when the column changes.
    IF EXISTS
    (
        SELECT 1
        FROM sys.columns AS c
        INNER JOIN sys.types AS t ON t.user_type_id = c.user_type_id
        WHERE c.object_id = OBJECT_ID(N'dbo.POD')
          AND c.name = N'PODStationId'
          AND (t.name <> N'int' OR c.is_nullable = 0)
    )
    BEGIN
        IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE parent_object_id = OBJECT_ID(N'dbo.POD') AND name = N'FK_POD_PODStation')
            ALTER TABLE dbo.POD DROP CONSTRAINT FK_POD_PODStation;

        ALTER TABLE dbo.POD ALTER COLUMN PODStationId INT NULL;

        ALTER TABLE dbo.POD WITH CHECK
            ADD CONSTRAINT FK_POD_PODStation FOREIGN KEY (PODStationId) REFERENCES dbo.PODStation (ID);
    END;

    -- POD.OSSPODID -> INT NOT NULL
    IF EXISTS
    (
        SELECT 1
        FROM sys.columns AS c
        INNER JOIN sys.types AS t ON t.user_type_id = c.user_type_id
        WHERE c.object_id = OBJECT_ID(N'dbo.POD')
          AND c.name = N'OSSPODID'
          AND (t.name <> N'int' OR c.is_nullable = 1)
    )
    BEGIN
        IF EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.POD') AND name = N'UX_POD_OSSPODID')
            DROP INDEX UX_POD_OSSPODID ON dbo.POD;

        ALTER TABLE dbo.POD ALTER COLUMN OSSPODID INT NOT NULL;
    END;

    IF EXISTS
    (
        SELECT 1
        FROM sys.columns AS c
        INNER JOIN sys.types AS t ON t.user_type_id = c.user_type_id
        WHERE c.object_id = OBJECT_ID(N'dbo.POD')
          AND c.name = N'Name'
          AND (t.name <> N'nvarchar' OR c.max_length <> 300 OR c.is_nullable = 1)
    )
    BEGIN
        ALTER TABLE dbo.POD ALTER COLUMN Name NVARCHAR(150) NOT NULL;
    END;

    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.POD') AND name = N'UX_POD_OSSPODID')
    BEGIN
        CREATE UNIQUE INDEX UX_POD_OSSPODID ON dbo.POD (OSSPODID);
    END;

    -- Dealer column widths
    IF EXISTS
    (
        SELECT 1
        FROM sys.columns AS c
        INNER JOIN sys.types AS t ON t.user_type_id = c.user_type_id
        WHERE c.object_id = OBJECT_ID(N'dbo.Dealer')
          AND c.name = N'Name'
          AND (t.name <> N'nvarchar' OR c.max_length <> 300 OR c.is_nullable = 1)
    )
    BEGIN
        ALTER TABLE dbo.Dealer ALTER COLUMN Name NVARCHAR(150) NOT NULL;
    END;

    IF EXISTS
    (
        SELECT 1
        FROM sys.columns AS c
        INNER JOIN sys.types AS t ON t.user_type_id = c.user_type_id
        WHERE c.object_id = OBJECT_ID(N'dbo.Dealer')
          AND c.name = N'Code'
          AND (t.name <> N'nvarchar' OR c.max_length <> 100)
    )
    BEGIN
        ALTER TABLE dbo.Dealer ALTER COLUMN Code NVARCHAR(50) NULL;
    END;

    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.Dealer') AND name = N'UX_Dealer_Code')
    BEGIN
        CREATE UNIQUE INDEX UX_Dealer_Code ON dbo.Dealer (Code) WHERE Code IS NOT NULL;
    END;

    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.salesdealermapping') AND name = N'UX_salesdealermapping_Pair')
    BEGIN
        CREATE UNIQUE INDEX UX_salesdealermapping_Pair ON dbo.salesdealermapping (SalesPersonId, DealerId);
    END;

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0
        ROLLBACK TRANSACTION;

    THROW;
END CATCH;

-- ---------------------------------------------------------------- verification
SELECT N'PODStation.OSSPODStationId type' AS CheckName,
       t.name + N'(' + CAST(c.max_length AS NVARCHAR(10)) + N'), nullable=' + CASE WHEN c.is_nullable = 1 THEN N'YES' ELSE N'NO' END AS Detail
FROM sys.columns AS c
INNER JOIN sys.types AS t ON t.user_type_id = c.user_type_id
WHERE c.object_id = OBJECT_ID(N'dbo.PODStation') AND c.name = N'OSSPODStationId'
UNION ALL
SELECT N'POD.OSSPODID type',
       t.name + N'(' + CAST(c.max_length AS NVARCHAR(10)) + N'), nullable=' + CASE WHEN c.is_nullable = 1 THEN N'YES' ELSE N'NO' END
FROM sys.columns AS c
INNER JOIN sys.types AS t ON t.user_type_id = c.user_type_id
WHERE c.object_id = OBJECT_ID(N'dbo.POD') AND c.name = N'OSSPODID'
UNION ALL
SELECT N'POD.PODStationId type',
       t.name + N'(' + CAST(c.max_length AS NVARCHAR(10)) + N'), nullable=' + CASE WHEN c.is_nullable = 1 THEN N'YES' ELSE N'NO' END
FROM sys.columns AS c
INNER JOIN sys.types AS t ON t.user_type_id = c.user_type_id
WHERE c.object_id = OBJECT_ID(N'dbo.POD') AND c.name = N'PODStationId'
UNION ALL
SELECT N'Unique POD OSS IDs', CAST(COUNT(*) AS NVARCHAR(20))
FROM (SELECT OSSPODID FROM dbo.POD GROUP BY OSSPODID HAVING COUNT(*) > 1) AS d
UNION ALL
SELECT N'Unique PODStation OSS IDs', CAST(COUNT(*) AS NVARCHAR(20))
FROM (SELECT OSSPODStationId FROM dbo.PODStation GROUP BY OSSPODStationId HAVING COUNT(*) > 1) AS d
UNION ALL
SELECT N'PODs without a station (expected before assignment)', CAST(COUNT(*) AS NVARCHAR(20))
FROM dbo.POD WHERE PODStationId IS NULL;
