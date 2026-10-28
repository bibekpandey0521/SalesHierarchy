# Database Project - SalesHierarchy.Database

## Purpose
SQL Server Database Project for schema management and DACPAC deployment.

## Structure
- Tables/ - Table definitions (PODStation, SalesPerson, POD, Dealer, salesdealermapping)
- StoredProcedures/ - Stored procedure definitions (if added)
- Scripts/ - Post-deployment scripts (if needed)

## Schema Notes
- Preserves existing primary keys, foreign keys, unique constraints
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
