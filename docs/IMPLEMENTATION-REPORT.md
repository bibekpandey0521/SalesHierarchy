# SalesHierarchy — Factual Implementation Report

> Status date: 2026-10-31. Local-only work; no deployment to the office UAT server was attempted in this phase.

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

## 2. Completed in this phase

### 2.1 Controller split (module controllers with conventional actions)

`AdminController` was split into focused controllers. Every module now uses the same conventional action names, and its views live in `Views/<Controller>/`.

| Controller | Actions | Views |
|---|---|---|
| `AdminPodStationController` | `Index`, `Details`, `Create`, `Edit`, `Delete`, `DeleteConfirmed` | `Views/AdminPodStation/` |
| `AdminPodController` | `Index`, `Details`, `Create`, `Edit`, `Delete`, `DeleteConfirmed`, `PODAssignments`, `SavePODAssignments`, `ClearPODAssignments` | `Views/AdminPod/` |
| `AdminSalesPersonController` | `Index`, `Details`, `Create`, `Edit`, `Delete`, `DeleteConfirmed` | `Views/AdminSalesPerson/` |
| `AdminDealerController` | `Index`, `Details`, `Create`, `Edit`, `Delete`, `DeleteConfirmed` | `Views/AdminDealer/` |
| `AdminSalesDealerMappingController` | `Index`, `Details`, `Create`, `Edit`, `Delete`, `DeleteConfirmed` | `Views/AdminSalesDealerMapping/` |
| `AdminController` | `Index` (redirect to dashboard), `Dashboard`, `Hierarchy`, `AuditLog`, `UserAccountManagement`, `ResendInvitation`, `DisableAccount`, `EnableAccount`, `DeleteAccount`, `CreateSalesPersonAccount`, `BulkUpload`, `BulkPreview`, `ConfirmBulkUpload`, `BulkResult`, `DownloadErrorReport`, `DownloadTemplate` | `Views/Admin/` |

- Delete confirmation posts target `Delete`; the POST handler is `DeleteConfirmed` with `[HttpPost, ActionName("Delete")]` in every module, matching the standard MVC scaffold pattern.
- All mutation actions require POST and `[ValidateAntiForgeryToken]`; the test suite asserts this reflectively for every module.
- All six admin controllers carry `[Authorize(Roles = "SystemAdmin")]`; `SalesPersonController` keeps `[Authorize(Roles = "SalesPerson")]`; `PublicController` stays anonymous.
- Navigation (`_Layout.cshtml`), the admin dashboard quick links, the home page, hierarchy/user-account pages and cross-module links were repointed at the new controllers and action names.

### 2.2 POD master data and assignment model

- `POD` create/edit only manages `Name` and `OSSPODID`; station and salesperson selection were removed from those forms.
- Station↔POD assignment is a separate SystemAdmin workflow on `AdminPod/PODAssignments` (station dropdown, searchable assigned/unassigned lists, searchable multi-select, save, clear).
- POD deletes are blocked while dependencies exist; station deletes are blocked while PODs are assigned.
- POD and station forms validate required fields and unique OSS identifiers; renaming an assigned POD to a name already used inside its station is rejected before hitting the filtered unique index, and the assignment page rejects a selection that would create a duplicate name in the station (covered by unit tests).
- POD list/detail pages show station assignment and current owners, with links to the station and salesperson records.

### 2.3 Naming and simplification cleanup

- Removed the placeholder `SalesHierarchy.Core.Class1`.
- Renamed DTOs: `PublicPODDto → PublicPodDto`, `PublicPODStationDto → PublicPodStationDto`.
- Renamed repository methods: `GetAllPODAssignments → GetAllPodAssignments`, `ReplacePODOwners → ReplacePodOwners`, `AssignPOD → AssignPod`, `RemovePOD → RemovePod`.
- Renamed the validation helper `PODAssignment → PodAssignment`; `HierarchyValidator → HierarchyRules` with `CreatesCycle`, `GetTeamMembers`, `IsInTeam`, `GetManagers`.
- Added shared paging (`PaginationViewModel`, `Paging.Paginate`, `Views/Shared/_Pager.cshtml`) used by every module list page; default page size 20.
- Data-access scan: no `SELECT *`, no `EXEC`, no stored procedures, no string-interpolated SQL, no raw EF SQL in the business repositories.

### 2.4 Local database schema upgrade (applied)

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

### 2.5 Local data state

On explicit instruction, module data in the local `SalesProdAuthDb` was deleted in one transaction (`AuditLog`, `salesdealermapping`, `SalesPersonPOD`, `Dealer`, `POD`, `SalesPerson`, `PODStation`) and verified at 0 rows each. ASP.NET Core Identity users/roles were untouched (1 user, 2 roles). The reviewed Excel import is deferred to the user; the schema is already upgraded and ready for it.

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

An additional row-level check inserted one station and one POD with `OSSPODStationId`/`OSSPODID` = 999001, loaded `/Public/PODs` and `/Public/PODStations` (both 200, both rendered the test rows), then deleted exactly those two rows. Tables are back to 0 rows.

The earlier `InvalidCastException` on `/Public/*` was caused by `nvarchar` OSS columns; those columns are now `int`, so the mapping matches the schema. Manual inserts through `sqlcmd` must run with `QUOTED_IDENTIFIER ON` because of the filtered unique index; the seed/import scripts now set `ANSI_NULLS` and `QUOTED_IDENTIFIER` explicitly.

## 4. Still pending (not done in this phase)

1. **Reviewed Excel import** — the user will run `database/SalesHierarchy.Database/Scripts/Import-ReviewedSalesData.sql` (expects 73 stations, 273 PODs with `PODStationId = NULL`, 34 salespeople, 208 `SalesPersonPOD` rows, 40 dealers, 67 mappings), then `database/Verify-ReviewedImport.sql`, then assign PODs to stations in the UI.
2. **UAT deployment** — deferred; the office server `192.168.25.48` (`SQL-UAT-SRV`) is not reachable from the current location. Do not deploy without a DBA-confirmed backup.
3. **Legacy `POD.salespersonId`** — retained temporarily and kept in sync from `SalesPersonPOD`; it can be dropped in a later cleanup once consumers are confirmed.
4. **Nothing has been committed or pushed** — the working tree holds all changes described above.
