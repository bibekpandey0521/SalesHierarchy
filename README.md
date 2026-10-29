# SalesHierarchy

ASP.NET Core MVC application for managing a sales organization: salespersons and their reporting hierarchy, POD stations, PODs, dealers, and dealer mappings. Business data is accessed through ADO.NET repositories (`Microsoft.Data.SqlClient`), while authentication uses ASP.NET Core Identity.

## Stack

- .NET 10 / ASP.NET Core MVC with Razor views
- SQL Server (`SalesProdAuthDb`)
- ADO.NET + Repository pattern for the five business tables
- ASP.NET Core Identity for login, roles, password hashing, lockout, and password reset tokens
- Bootstrap 5.3 with a custom design system (`wwwroot/css/site.css`)
- xUnit tests in `SalesHierarchy.Tests`

## Structure

```
SalesHierarchy/
├── SalesHierarchy.slnx              # solution (created by the .NET 10 SDK; .slnx format)
├── src/
│   ├── SalesHierarchy.Web/          # controllers, Razor views, view models, Identity, DI
│   ├── SalesHierarchy.Core/         # models, DTOs, repository interfaces, validation rules
│   ├── SalesHierarchy.Infrastructure/# SqlConnectionFactory + ADO.NET repositories
│   └── SalesHierarchy.Tests/        # xUnit tests for business rules and authorization
└── database/
    └── SalesHierarchy.Database/     # SQL Server Database Project (DACPAC)
```

> Build with `dotnet build SalesHierarchy.slnx`. The SDK emits the `.slnx` solution format by default in .NET 10.

## Business tables

| Table | Key | Notes |
|---|---|---|
| `PODStation` | `ID` | Optional `OSSPODStationId`, required `Name` |
| `SalesPerson` | `ID` | `ManagerId` self-reference is the source of truth for hierarchy |
| `POD` | `ID` | Belongs to a `PODStation`; legacy `salespersonId` is kept for compatibility |
| `SalesPersonPOD` | `SalesPersonId` + `PODId` | Many-to-many POD ownership; source of truth for multi-POD assignment |
| `Dealer` | `UniqueID` | `Code` is unique |
| `salesdealermapping` | `UniqueID` | Links a salesperson to a dealer; duplicates blocked in the application |
| `AuditLog` | `ID` | Append-only history of business/account changes (added by this application); optional `RelatedSalesPersonId` scopes an event to a team |

No tables are recreated and no indexes/clustering are redesigned. Delete operations are blocked with a clear message while dependent records exist; no cascading deletes were added.

## Local development (localhost / LocalDB)

The default connection string points at `localhost` and the `SalesProdAuthDb` database. A setup script creates the database, business tables, and (optionally) demo data:

```powershell
# Local SQL Server instance (default)
powershell -ExecutionPolicy Bypass -File .\database\Setup-LocalDb.ps1 -SeedDemoData

# Or SQL Server LocalDB
powershell -ExecutionPolicy Bypass -File .\database\Setup-LocalDb.ps1 -Server "(localdb)\MSSQLLocalDB" -SeedDemoData
```

The setup script also runs `database/Upgrade-AuditLog.sql`, which safely adds the `RelatedSalesPersonId` column to an existing `dbo.AuditLog`, and `database/Upgrade-SalesPersonPOD.sql`, which creates `dbo.SalesPersonPOD` and copies verified legacy `POD.salespersonId` assignments. To upgrade a database created by an older version manually, run:

```powershell
sqlcmd -S localhost -E -C -b -d SalesProdAuthDb -i .\database\Upgrade-AuditLog.sql
sqlcmd -S localhost -E -C -b -d SalesProdAuthDb -i .\database\Upgrade-SalesPersonPOD.sql
```

Both scripts are idempotent. The POD migration never drops the legacy `POD.salespersonId` column.

### Reviewed workbook import

The supplied `pods.xlsx` and `Sales Mapping With Dealer and POD.xlsx` rows were reviewed and applied to the local `SalesProdAuthDb` with:

```powershell
sqlcmd -S localhost -E -C -b -d SalesProdAuthDb -i .\database\SalesHierarchy.Database\Scripts\Import-ReviewedSalesData.sql
```

The import added 73 stations, 208 PODs, 34 salespeople, 208 POD-to-officer assignments, 40 dealers, and 67 dealer-to-officer mappings. It is idempotent and never overwrites existing names, designations, or owners.

Verify it with the read-only script:

```powershell
sqlcmd -S localhost -E -C -b -d SalesProdAuthDb -i .\database\Verify-ReviewedImport.sql
```

Review exceptions are documented in `docs/REPORTING-HIERARCHY-AND-POD.md`:

- 3 manager links are intentionally `NULL` because the workbook gives conflicting parents.
- 65 PODs are not imported because neither workbook supplies a station for them and `POD.PODStationId` is `NOT NULL`.
- Two `Trisuli POD Station` rows were normalized to the station master's `Trishuli POD Station`.
- Dealer records use the dealer code as both `Name` and `Code` because the workbook contains no dealer names.

The source workbooks are local data files and are excluded from Git; do not commit them.

Then apply the committed Identity migration and configure the first administrator:

```powershell
dotnet tool restore
dotnet ef database update --project src/SalesHierarchy.Web

dotnet user-secrets init --project src/SalesHierarchy.Web
dotnet user-secrets set "AdminBootstrap:Email" "admin@saleshierarchy.local" --project src/SalesHierarchy.Web
dotnet user-secrets set "AdminBootstrap:Password" "Admin#12345" --project src/SalesHierarchy.Web

dotnet run --project src/SalesHierarchy.Web
```

Open <http://localhost:5283> and sign in with the bootstrapped administrator. In Development the app also applies pending Identity migrations automatically at startup.

If you use LocalDB, also point the app at it:

```powershell
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Server=(localdb)\MSSQLLocalDB;Database=SalesProdAuthDb;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=True" --project src/SalesHierarchy.Web
```

## Configuration

All secrets must come from user secrets (development) or environment variables (production). Nothing sensitive is committed.

```powershell
cd src/SalesHierarchy.Web
dotnet user-secrets init
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Server=localhost;Database=SalesProdAuthDb;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=True"
dotnet user-secrets set "AdminBootstrap:Email" "admin@yourcompany.com"
dotnet user-secrets set "AdminBootstrap:Password" "Use-A-Strong-Password1"
```

### First System Admin

On startup the application:

1. Creates the `SystemAdmin` and `SalesPerson` roles if they are missing.
2. If no `SystemAdmin` exists **and** `AdminBootstrap:Email` + `AdminBootstrap:Password` are configured, creates that account.

There is no public self-registration and no hard-coded seed password. Remove the `AdminBootstrap` secrets after the first successful login. The bootstrap is skipped when an administrator already exists.

> If the database is not reachable at startup, the bootstrap logs a warning instead of crashing the application. Deploy the database first, then restart.

### E-mail delivery

Password setup and reset links are sent through SMTP:

```powershell
dotnet user-secrets set "Email:Enabled" "true"
dotnet user-secrets set "Email:SmtpHost" "smtp.yourprovider.com"
dotnet user-secrets set "Email:SmtpPort" "587"
dotnet user-secrets set "Email:UseSsl" "true"
dotnet user-secrets set "Email:UserName" "smtp-user"
dotnet user-secrets set "Email:Password" "smtp-password"
dotnet user-secrets set "Email:FromAddress" "no-reply@yourcompany.com"
dotnet user-secrets set "Email:FromName" "SalesHierarchy"
```

If e-mail is not configured or delivery fails, the UI reports the invitation as **pending/failed** instead of pretending it was sent. Invitations can be re-sent by an administrator from **User Accounts**, or by a manager from **Team Accounts** for their own team. Passwords and tokens are never logged.

#### Invitation flow

1. An administrator creates a salesperson account (individually or via bulk upload). No password is set by the administrator.
2. The application creates the Identity account without a password and generates a one-time password-reset token.
3. The salesperson receives an e-mail containing a **Set my password** link.
4. They choose their own password; only the hash is stored. The link expires according to Identity's token lifespan and becomes invalid after use.

#### Local e-mail without an SMTP server

`appsettings.Development.json` enables a local pickup directory:

```json
"Email": {
  "Enabled": true,
  "PickupDirectory": "App_Data/emails",
  "FromAddress": "no-reply@saleshierarchy.local"
}
```

Invitation messages are written as `.eml` files under `src/SalesHierarchy.Web/App_Data/emails` instead of being sent. Open the file and copy the **Set my password** URL into the browser. This folder is git-ignored and contains sensitive one-time links — delete it after testing. For real delivery, remove `Email:PickupDirectory` and configure the SMTP settings above (user secrets or environment variables).

### Identity schema

The application uses the existing database connection for both business data and Identity. The Identity tables (`AspNetUsers`, `AspNetRoles`, …) are created by the committed migration in `src/SalesHierarchy.Web/Data/Migrations`:

```powershell
dotnet tool restore
dotnet ef database update --project src/SalesHierarchy.Web
```

In Development the application also applies pending Identity migrations automatically at startup. In production, run `dotnet ef database update` (or generate an idempotent SQL script with `dotnet ef migrations script --idempotent`) during deployment.

Inspect the existing `SalesProdAuthDb` schema first and do not recreate tables that already exist. `ApplicationUser` adds only `SalesPersonId` (nullable) to link an account to a `SalesPerson.ID`.

## Running

```powershell
dotnet build SalesHierarchy.slnx
dotnet run --project src/SalesHierarchy.Web
```

Then open the HTTPS URL shown in the console and sign in with the bootstrapped administrator.

## Features

### Public (anonymous)

- Home and public directory
- Approved sales hierarchy (names and designations only)
- Approved POD station and POD lists
- Search on each public list

Public pages use read-only DTOs (`PublicSalesPersonDto`, `PublicPODStationDto`, `PublicPODDto`). Emails, dealer mappings, account data, and all write operations are unavailable.

### System Admin

- Full CRUD for all five business tables (list, details, create, edit, delete confirmation)
- Search on list pages and database-populated dropdowns
- Reporting-manager assignment with self-reporting and circular-hierarchy rejection. This is the only place where a salesperson's manager can be created or changed. The manager list follows the verified CXO → Head → Manager/Supervisor → Officer ladder; custom/legacy designations remain unrestricted.
- Create/edit a salesperson and assign any number of PODs in one transaction, using a searchable multi-select with removable tags. Duplicate, missing, and non-existent POD IDs are rejected before any write, and the legacy `POD.salespersonId` column is kept in sync where it is safe to do so.
- Delete guards: subordinates, assigned PODs, dealer mappings, and linked accounts block salesperson deletion; PODs block station deletion; mappings block dealer deletion
- Individual salesperson account creation (business record + Identity account + invitation in one flow, with compensating rollback)
- Excel bulk upload with validation, preview, confirmation, result summary, and downloadable results
- Account management: invitation status, resend invitation, disable/enable, delete account
- Complete reporting hierarchy view per salesperson
- Activity log: filterable, read-only history of business and account changes stored in `dbo.AuditLog` (before/after values for edits; passwords and tokens excluded)

### SalesPerson

Every SalesPerson has a personal workspace: dashboard, profile, reporting chain, assigned PODs, mapped dealers, and a read-only view of their hierarchy. A SalesPerson who has direct reports also acts as a **manager** for their entire subtree (themselves plus direct and indirect reports). Managers can:

- Add, edit, and delete team member business records. New members automatically report to the manager who created them.
- Create a login for a team member, resend the invitation, disable/enable the account, or delete the account while keeping the business record.
- Reassign PODs **that are already owned within their subtree** to themselves or another team member, or unassign them.
- Add and remove dealer mappings for themselves and their team.
- View a team-scoped activity history covering their own actions, their team's actions, and admin actions related to their team.

Managers cannot edit or delete themselves, edit their own manager, change any reporting line, manage their own login from team screens, manage master data, or reach another team's records by changing an ID in a URL. Those actions remain System Admin–only. Non-managers retain a read-only workspace for team members, accounts, PODs, and dealer mappings; an administrator must assign their first report before team management and dealer changes open up.

## Excel bulk upload

1. Download the template from **Admin → Bulk Upload** (`Name`, `Designation`, `Email`, `ManagerEmail`).
2. Fill in rows and upload the `.xlsx` file (maximum 1,000 rows / 5 MB).
3. Every row is validated: required fields, e-mail format, duplicate e-mails (in file and existing accounts), unknown managers, self-reporting, and circular relationships.
4. Review the preview — nothing is written yet.
5. Confirm the import. Salesperson records and Identity accounts are created without passwords, links are resolved in a second pass (so managers can appear later in the file), and invitations are sent.
6. Results show imported, failed, skipped, and invitation status. Download the results workbook for failed rows.

Re-uploading the same file is safe: existing e-mails are detected as duplicates and skipped. Failed rows are compensated (created business records and identity accounts are removed) and reported.

## Testing

```powershell
dotnet test SalesHierarchy.slnx
```

Automated tests cover:

- Hierarchy rules: self-reporting, direct/deep cycles, valid reassignment, designation-ladder compatibility, descendants, manager chain, cycle tolerance, subtree scope
- POD assignment rules: deduplication, add/remove diff, empty selection, transactional persistence expectations
- Reporting-hierarchy import review: duplicates, conflicting designations/managers, missing managers, self-reporting, cycles
- Bulk row rules: required fields, e-mail formats, length limits, self-reporting, in-file circular managers
- Bulk upload status logic
- Authorization: controller role requirements and POST/anti-forgery protection of admin and manager mutations

See [TESTING.md](TESTING.md) for the manual test checklist (public access, authentication, CRUD, delete guards, bulk upload, invitations, team-scope enforcement).

## Database deployment

The SQL Server Database Project lives in `database/SalesHierarchy.Database`. See its [README](database/SalesHierarchy.Database/README.md) for DACPAC build/publish steps. Always take a backup before publishing and never drop production data.

## Security notes

- Parameterized SQL only — see [SQL_INJECTION_PREVENTION.md](SQL_INJECTION_PREVENTION.md).
- Anti-forgery tokens on all state-changing forms.
- Password rules: minimum 8 characters with upper/lower/digit; lockout after 5 failed attempts for 5 minutes.
- Authentication cookie: HTTP-only, sliding expiration, 2-hour lifetime.
- Generic forgot-password response so account existence is not revealed.
- No shared passwords; invitations use Identity reset tokens.
- Activity history is append-only in `dbo.AuditLog`; secrets, password hashes, and invitation tokens are never written there.
- Administrative authorization is enforced on controllers, not only by hiding navigation.
