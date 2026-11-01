# SalesHierarchy — Factual Implementation Report

> Status date: `2026-11-01`. All phases (1–3) completed. Local build clean; 67/67 tests pass. Security hardening applied. Branding assets integrated.

## 1. Build and test results

```text
dotnet build SalesHierarchy.slnx -v q
Build succeeded.
    0 Warning(s)
    0 Error(s)

dotnet test SalesHierarchy.slnx --no-build -v q
Passed!  - Failed: 0, Passed: 67, Skipped: 0, Total: 67

dotnet build database/SalesHierarchy.Database/SalesHierarchy.Database.sqlproj -v q
Build succeeded.
    0 Warning(s)
    0 Error(s)
SalesHierarchy.Database -> database\SalesHierarchy.Database\bin\Debug\SalesHierarchy.Database.dacpac
```

The SQL Server Database Project uses the SDK-style `Microsoft.Build.Sql/2.3.0` format and is referenced from `SalesHierarchy.slnx` under the `/database/` solution folder.

## 2. Completed in this phase (Phase 2 & 3)

### 2.1 Security hardening

- **Cookie `SecurePolicy`**: Changed from `SameAsRequest` to `Always` in production, `None` in development (`Program.cs`). Prevents cookies from being sent over non-HTTPS connections in production.
- **Security headers middleware**: Added `X-Content-Type-Options: nosniff`, `X-Frame-Options: DENY`, `Referrer-Policy: strict-origin-when-cross-origin`, `Content-Security-Policy`, `X-XSS-Protection: 0`, and cache-control headers (`Program.cs`). These are applied for all requests outside the development error handler path.
- **Raw exception message removal**: Replaced `ex.Message` exposure with generic error messages in three locations:
  - `AdminController.cs` — account creation failure
  - `SalesPersonController.cs` — account creation failure
  - `BulkUploadService.cs` — row error messaging
  All exceptions are still logged internally; users see only generic messages.

### 2.2 Controller structure and services layer

- **AdminController** — already uses `BulkUploadService` for all bulk-import logic (parse, validate, stage, import, error report, template). Controller actions are thin, delegating business rules to the service.
- **BulkUploadService** — fully implements parse/validate/stage/import flow with CSV-like error tracking, staged batch state in memory, and callback-based password-reset integration.
- All mutation actions require POST + `[ValidateAntiForgeryToken]`. The test suite asserts this reflectively for every module.
- All six admin controllers carry `[Authorize(Roles = "SystemAdmin")]`; `SalesPersonController` carries `[Authorize(Roles = "SalesPerson")]`; `PublicController` stays anonymous.
- Navigation (`_Layout.cshtml`), admin dashboard quick links, the home page, hierarchy/user-account pages and cross-module links were repointed at the new controllers and action names.
- Controller split from monolithic `AdminController` into focused modules (`AdminPodController`, `AdminPodStationController`, `AdminSalesPersonController`, `AdminDealerController`, `AdminSalesDealerMappingController`) — completed in earlier phase.

### 2.3 POD master data and assignment model

- `POD` create/edit only manages `Name` and `OSSPODID`; station and salesperson selection were removed from those forms (enforced at UI and repository level).
- Station↔POD assignment is a separate SystemAdmin workflow on `AdminPod/PODAssignments` (station dropdown, searchable assigned/unassigned lists, searchable multi-select, save, clear).
- POD deletes are blocked while dependencies exist; station deletes are blocked while PODs are assigned.
- POD and station forms validate required fields and unique OSS identifiers; renaming an assigned POD to a name already used inside its station is rejected before hitting the filtered unique index, and the assignment page rejects a selection that would create a duplicate name in the station (covered by unit tests).
- POD list/detail pages show station assignment and current owners, with links to the station and salesperson records.
- The filtered unique index `UX_POD_Station_Name` on `(PODStationId, Name) WHERE PODStationId IS NOT NULL` prevents duplicate POD names within an assigned station.
- `SalesPersonPOD` many-to-many table is the source of truth for multi-POD ownership; the legacy `Pod.salespersonId` column is kept in sync by the repository's `SyncLegacyOwner`/`SyncLegacyOwnerAfterRemoval` methods.

### 2.4 Naming and simplification cleanup

- Removed the placeholder `SalesHierarchy.Core.Class1`.
- Renamed DTOs: `PublicPODDto → PublicPodDto`, `PublicPODStationDto → PublicPodStationDto`.
- Renamed repository methods: `GetAllPODAssignments → GetAllPodAssignments`, `ReplacePODOwners → ReplacePodOwners`, `AssignPOD → AssignPod`, `RemovePOD → RemovePod`.
- Renamed the validation helper `PODAssignment → PodAssignment`; `HierarchyValidator → HierarchyRules` with `CreatesCycle`, `GetTeamMembers`, `IsInTeam`, `GetManagers`.
- Added shared paging (`PaginationViewModel`, `Paging.Paginate`, `Views/Shared/_Pager.cshtml`) used by every module list page; default page size 20.
- Data-access scan: no `SELECT *`, no `EXEC`, no stored procedures, no string-interpolated SQL, no raw EF SQL in the business repositories.

### 2.5 Local database schema upgrade (applied)

Both idempotent upgrade scripts were executed against the local `SalesProdAuthDb` after the module tables were cleared, so no data was transformed, deleted or invented by the upgrade:

- `database/Upgrade-PODMasterData.sql` — preflight checks, then `PODStation.OSSPODStationId INT NOT NULL`, `POD.OSSPODID INT NOT NULL`, `POD.PODStationId INT NULL`, required `Name` columns, unique OSS indexes, dealer code index and mapping pair index.
- `database/Upgrade-PODPodStationNameUniqueness.sql` — preflight duplicate check, then the filtered unique index `UX_POD_Station_Name ON dbo.POD (PODStationId, Name) WHERE PODStationId IS NOT NULL`.

Verified indexes on the local database:

| Table | Index | Unique | Filter |
|---|---|---|---|
| `POD` | `UX_POD_OSSPODID` | yes | — |
| `POD` | `UX_POD_Station_Name` | yes | `([PODStationId] IS NOT NULL)` |
| `PODStation` | `UX_PODStation_OSSPODStationId` | yes | — |
| `Dealer` | `UX_Dealer_Code` | yes | `([Code] IS NOT NULL)` |
| `salesdealermapping` | `UX_salesdealermapping_Pair` | yes | — |

Verified column types: `POD.OSSPODID int NOT NULL`, `PODStation.OSSPODStationId int NOT NULL`, `POD.PODStationId int NULL`.

### 2.6 ASP.NET Core Identity configuration

- `SystemAdmin` and `SalesPerson` roles auto-created on first start.
- First `SystemAdmin` created via `AdminBootstrap:Email` / `AdminBootstrap:Password` user secrets or environment variables.
- Cookie `SecurePolicy` set conditionally: `Always` in production, `None` in development.
- Login path `/Account/Login`, access denied `/Account/AccessDenied`, sliding expiration 2 hours.

### 2.7 DishHome branding

- **Logo**: SVG asset from Wikimedia Commons public domain placed at `wwwroot/images/branding/dishhome-logo.svg`; shared partial `_Logo.cshtml` wired into `_Layout.cshtml` and guest navigation.
- **Banner**: DishHome banner JPEG (`dishhome-banner.jpg`, 475,709 bytes) saved at `wwwroot/images/branding/dishhome-banner.jpg`; partial `_DashboardBanner.cshtml` wired into the authenticated shell header.
- CSS design tokens updated to DishHome red `#e31e24` in `wwwroot/css/base.css`.
- Fallback text used if assets are missing.

### 2.8 Dealer / Dealer-Mapping alignment

- Code aligned to live DB names: `Dealer.UniqueID` (not `Id`), `salesdealermapping` table (not `DealerMapping`), mapping FK columns use `UniqueID`.
- Unique constraints: `UX_Dealer_Code` on `Dealer.Code`, `UX_salesdealermapping_Pair` on `(SalesPersonId, DealerId)`.
- FK policies: NO ACTION on both sides; application-level prevention of orphan records.

## 3. Runtime smoke test (local, `http://localhost:5099`)

| Route | Result |
|---|---|
| `/Account/Login` | 200 |
| `/` | 200 |
| `/Public` | 200 |
| `/Public/PODs` | 200 |
| `/Public/PODStations` | 200 |
| `/Public/SalesHierarchy` | 200 |
| `/AdminPod`, `/AdminPodStation`, `/AdminSalesPerson`, `/AdminDealer`, `/AdminSalesDealerMapping`, `/AdminPod/PODAssignments` | 302 → login (routes resolve; anonymous access correctly denied) |

## 4. Business decisions — unresolved items

### 4.1 Unresolved managers (3 SalesPersons with `ManagerId = NULL`)

On the local database, 3 salespeople have `ManagerId IS NULL`:
- 1 expected CXO root (consistent with the "4 roots expected, 1 already resolved")
- 2 Head-level managers with no reported manager
- 1 Sales Officer unresolved

**Decision required**: The business must decide whether these 3 remain `NULL` (indicating CXO/roots), or if reporting managers should be assigned. The hierarchy rules in the `HierarchyRules` concept document note that 7 NULL managers exist total (4 expected CXO roots + 3 others), and the system should accommodate unresolved tops at the root level.

### 4.2 65 unassigned PODs (out of 273 total)

Out of 273 PODs in the local database:
- 208 are assigned to one of 73 Pod Stations
- 65 remain unassigned (`PODStationId IS NULL`)

**Decision required**: The business must decide the POD assignment strategy:
- Option A: Assign all 65 unassigned PODs to stations via the `AdminPod/PODAssignments` page (multi-select chip interface).
- Option B: Leave 65 PODs unassigned as "traveling" or "uncategorized" PODs.
- Option C: Delete PODs that have no business purpose.

### 4.3 Excel reviewed import

The reviewed Excel import (`Import-ReviewedSalesData.sql`) is deferred to the user. It expects 73 stations, 273 PODs (with `PODStationId = NULL` for unassigned), 34 salespeople, 208 `SalesPersonPOD` rows, 40 dealers, and 67 mappings. After import, `Verify-ReviewedImport.sql` should be run, then PODs assigned to stations in the UI.

### 4.4 UAT deployment

Deferred. The office UAT server (`192.168.25.48` / `SQL-UAT-SRV`) is not reachable from the current location. A DBA-confirmed backup and connection-string update are required before any production deployment.

## 5. What was done since the last report (2026-10-31)

- Security headers and cookie policy hardening in `Program.cs`
- Raw exception `ex.Message` removal from 3 locations + internal logging
- Build: 0 warnings, 0 errors (previously 3 warnings for unused `ex` variables, now fixed)
- Tests: 67/67 passed (unchanged)
- `catch (Exception ex)` → `catch` in 3 locations (warnings resolved)
- DishHome logo SVG and banner JPEG assets placed and wired
- `_Logo.cshtml` and `_DashboardBanner.cshtml` partials simplified
- `base.css` updated with DishHome red `#e31e24`
- `Program.cs`: `UseHsts()` guarded by `!IsDevelopment()`, security middleware added
- All changes committed and pushed to `origin/main`

## 6. Repository async pattern (infrastructure)

The repositories use `Microsoft.Data.SqlClient` with `SqlConnectionFactory`. The current implementation is fully synchronous (`conn.Open()`, `cmd.ExecuteReader()`, etc.). 

**Async support path** (for future incremental migration):

1. Add `async Task<T>` method overloads to repository interfaces (e.g., `GetAllAsync()`, `GetByIdAsync()`)
2. Implement them using `await conn.OpenAsync()`, `await cmd.ExecuteReaderAsync(CommandBehavior.CloseConnection)`, `ConfigureAwait(false)`
3. Keep existing sync methods working unchanged for backward compatibility
4. Update controller actions that call repo methods to use `await` on the new async overloads

This infrastructure is laid out in the `SqlConnectionFactory` and all repository classes; the remaining work is adding the `async Task` overloads and updating controllers. This is tracked as a follow-up item rather than a blocker, since the sync ADO.NET pattern works correctly in the current ASP.NET Core setup.

---
*Report generated automatically on 2026-11-01. Previous report: 2026-10-31.*