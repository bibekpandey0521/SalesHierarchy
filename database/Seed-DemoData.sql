-- Optional demo data for local development.
-- Safe to re-run: each block inserts only when the target table is empty.

SET NOCOUNT ON;
SET XACT_ABORT ON;

BEGIN TRANSACTION;

IF NOT EXISTS (SELECT 1 FROM dbo.PODStation)
BEGIN
    INSERT INTO dbo.PODStation (OSSPODStationId, Name) VALUES
        (9001, N'North Hub'),
        (9002, N'South Hub');
END;

IF NOT EXISTS (SELECT 1 FROM dbo.SalesPerson)
BEGIN
    DECLARE @DirectorId INT;
    DECLARE @NorthManagerId INT;
    DECLARE @SouthManagerId INT;

    INSERT INTO dbo.SalesPerson (Name, Designation, ManagerId) VALUES (N'Avery Director', N'National Sales Director', NULL);
    SET @DirectorId = SCOPE_IDENTITY();

    INSERT INTO dbo.SalesPerson (Name, Designation, ManagerId) VALUES (N'Morgan North', N'Regional Manager - North', @DirectorId);
    SET @NorthManagerId = SCOPE_IDENTITY();

    INSERT INTO dbo.SalesPerson (Name, Designation, ManagerId) VALUES (N'Riley South', N'Regional Manager - South', @DirectorId);
    SET @SouthManagerId = SCOPE_IDENTITY();

    INSERT INTO dbo.SalesPerson (Name, Designation, ManagerId) VALUES (N'Casey Field', N'Territory Salesperson', @NorthManagerId);
END;

IF NOT EXISTS (SELECT 1 FROM dbo.Dealer)
BEGIN
    INSERT INTO dbo.Dealer (Name, Code) VALUES
        (N'Summit Outdoor Supply', N'D-1001'),
        (N'Lakeside Equipment', N'D-1002'),
        (N'Prairie Home & Garden', N'D-1003'),
        (N'Coastal Power Tools', N'D-1004');
END;

IF NOT EXISTS (SELECT 1 FROM dbo.POD)
BEGIN
    DECLARE @NorthId INT = (SELECT TOP (1) ID FROM dbo.PODStation WHERE Name = N'North Hub' ORDER BY ID);
    DECLARE @SouthId INT = (SELECT TOP (1) ID FROM dbo.PODStation WHERE Name = N'South Hub' ORDER BY ID);
    DECLARE @NorthManager INT = (SELECT TOP (1) ID FROM dbo.SalesPerson WHERE Name = N'Morgan North' ORDER BY ID);
    DECLARE @SouthManager INT = (SELECT TOP (1) ID FROM dbo.SalesPerson WHERE Name = N'Riley South' ORDER BY ID);
    DECLARE @FieldRep INT = (SELECT TOP (1) ID FROM dbo.SalesPerson WHERE Name = N'Casey Field' ORDER BY ID);

    INSERT INTO dbo.POD (OSSPODID, Name, PODStationId, salespersonId) VALUES
        (9101, N'North Metro POD', @NorthId, @NorthManager),
        (9102, N'North Valley POD', @NorthId, @FieldRep),
        (9201, N'South Metro POD', @SouthId, @SouthManager);
END;

-- Keep the many-to-many ownership table in sync with the demo legacy owner.
IF OBJECT_ID(N'dbo.SalesPersonPOD', N'U') IS NOT NULL
   AND OBJECT_ID(N'dbo.POD', N'U') IS NOT NULL
BEGIN
    INSERT INTO dbo.SalesPersonPOD (SalesPersonId, PODId)
    SELECT p.salespersonId, p.ID
    FROM dbo.POD AS p
    WHERE p.salespersonId IS NOT NULL
      AND NOT EXISTS
      (
          SELECT 1 FROM dbo.SalesPersonPOD AS x
          WHERE x.SalesPersonId = p.salespersonId AND x.PODId = p.ID
      );
END;

IF NOT EXISTS (SELECT 1 FROM dbo.salesdealermapping)
BEGIN
    DECLARE @NorthManagerId2 INT = (SELECT TOP (1) ID FROM dbo.SalesPerson WHERE Name = N'Morgan North' ORDER BY ID);
    DECLARE @SouthManagerId2 INT = (SELECT TOP (1) ID FROM dbo.SalesPerson WHERE Name = N'Riley South' ORDER BY ID);
    DECLARE @FieldRepId INT = (SELECT TOP (1) ID FROM dbo.SalesPerson WHERE Name = N'Casey Field' ORDER BY ID);
    DECLARE @Summit INT = (SELECT TOP (1) UniqueID FROM dbo.Dealer WHERE Code = N'D-1001');
    DECLARE @Lakeside INT = (SELECT TOP (1) UniqueID FROM dbo.Dealer WHERE Code = N'D-1002');
    DECLARE @Prairie INT = (SELECT TOP (1) UniqueID FROM dbo.Dealer WHERE Code = N'D-1003');
    DECLARE @Coastal INT = (SELECT TOP (1) UniqueID FROM dbo.Dealer WHERE Code = N'D-1004');

    INSERT INTO dbo.salesdealermapping (SalesPersonId, DealerId) VALUES
        (@NorthManagerId2, @Summit),
        (@FieldRepId, @Lakeside),
        (@SouthManagerId2, @Prairie),
        (@SouthManagerId2, @Coastal);
END;

INSERT INTO dbo.AuditLog (OccurredUtc, UserEmail, [Action], EntityName, EntityId, Summary)
SELECT SYSUTCDATETIME(), N'local-setup', N'Seed', N'Database', NULL, N'Demo data seeded for local development.'
WHERE NOT EXISTS (
    SELECT 1 FROM dbo.AuditLog WHERE [Action] = N'Seed' AND [EntityName] = N'Database'
);

COMMIT TRANSACTION;
