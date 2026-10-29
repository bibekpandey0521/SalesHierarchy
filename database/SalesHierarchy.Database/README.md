# Database Project - SalesHierarchy.Database

## Purpose
SQL Server Database Project for schema management and DACPAC deployment.

## Structure
- Tables/ - Table definitions (PODStation, SalesPerson, POD, SalesPersonPOD, Dealer, salesdealermapping, AuditLog)
- StoredProcedures/ - Stored procedure definitions (if added)
- Scripts/ - Post-deployment, reviewed seed scripts, and the generated reviewed workbook import (`Import-ReviewedSalesData.sql`). `PODStation.Seed.sql` and `POD.Seed.sql` remain no-op templates for future reviewed batches.

Identity tables (`AspNetUsers`, `AspNetRoles`, ...) are managed by EF Core migrations in `src/SalesHierarchy.Web/Data/Migrations`, not by this project.

## Local setup
From the repository root:

```powershell
powershell -ExecutionPolicy Bypass -File .\database\Setup-LocalDb.ps1 -SeedDemoData
dotnet ef database update --project src/SalesHierarchy.Web
```

Use `-Server "(localdb)\MSSQLLocalDB"` for LocalDB. The script is idempotent and never drops data. `Seed-DemoData.sql` only inserts into empty tables.

## Reviewed workbook import

After `database/Upgrade-SalesPersonPOD.sql` has created `dbo.SalesPersonPOD`, apply the reviewed workbook rows with:

```powershell
sqlcmd -S <server> -E -C -b -d SalesProdAuthDb -i .\database\SalesHierarchy.Database\Scripts\Import-ReviewedSalesData.sql
sqlcmd -S <server> -E -C -b -d SalesProdAuthDb -i .\database\Verify-ReviewedImport.sql
```

The script is idempotent and never overwrites an existing station/POD/dealer name, designation, or owner. It imports PODs only when a verified station exists, uses the Sales Officer as the POD/dealer owner, and prints a review report. Three conflicting manager links are intentionally left `NULL`; see `docs/REPORTING-HIERARCHY-AND-POD.md`.

## Schema Notes
- Preserves existing primary keys, foreign keys, unique constraints
- `SalesPersonPOD(SalesPersonId, PODId)` is the source of truth for multi-POD ownership and is added by `database/Upgrade-SalesPersonPOD.sql`
- Legacy `POD.salespersonId` is retained and back-filled into `SalesPersonPOD`; it is never dropped by the migration
- No unnecessary indexes added
- Foreign key delete behavior preserved as-is
- Cascading deletes not added indiscriminately

## Deployment
1. Build the project to generate DACPAC
2. Use SQLPackage or Visual Studio to publish
3. Always backup database before deployment
4. Review schema changes carefully
5. Never drop tables or data

## Safe Publishing
```bash
SqlPackage /Action:Publish /SourceFile:SalesHierarchy.Database.dacpac /TargetServerName:server /TargetDatabaseName:SalesProdAuthDb /TargetTrustServerCertificate:True
```

## Important
- Do not create Indexes folder (as per requirements)
- Do not redesign clustering
- Make minimal schema changes only when necessary
