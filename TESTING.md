# Testing Guide

## Prerequisites

- SQL Server or LocalDB with the `SalesProdAuthDb` database (run `database/Setup-LocalDb.ps1`, see README). The setup script also applies `database/Upgrade-AuditLog.sql` to older databases.
- .NET 10 SDK
- Identity tables present (`dotnet ef database update --project src/SalesHierarchy.Web`; Development also applies migrations at startup)
- E-mail configuration only if you want to exercise real invitation delivery. Locally, the Development settings write messages to `src/SalesHierarchy.Web/App_Data/emails` instead.

## Automated tests

```powershell
dotnet test SalesHierarchy.slnx
```

Covered automatically:

| Area | Tests |
|---|---|
| Hierarchy rules | self-reporting, direct cycle, deep cycle, valid reassignment, designation-ladder compatibility, descendants, manager chain, cycle tolerance, subtree scope includes self/descendants and excludes managers/other teams |
| POD assignment rules | deduplication/sorting, add/remove diff, unchanged selection, empty selection |
| Reporting-hierarchy import review | duplicate names, conflicting designations/managers, missing managers, self-reporting, cycles, unknown designation warnings |
| Bulk row validation | required fields, e-mail formats, max lengths, manager e-mail format, self-reporting, in-file circular managers |
| Bulk upload status | valid/invalid/duplicate rows, importability, batch counts |
| Authorization | controller role attributes, POST + anti-forgery on all admin and SalesPerson team mutations |

Current result: `Passed! Failed: 0, Passed: 58, Skipped: 0, Total: 58`.

## Reviewed database import

Apply the upgrade and reviewed import, then verify the expected counts and conflicts:

```powershell
sqlcmd -S localhost -E -C -b -d SalesProdAuthDb -i .\database\Upgrade-SalesPersonPOD.sql
sqlcmd -S localhost -E -C -b -d SalesProdAuthDb -i .\database\SalesHierarchy.Database\Scripts\Import-ReviewedSalesData.sql
sqlcmd -S localhost -E -C -b -d SalesProdAuthDb -i .\database\Verify-ReviewedImport.sql
```

```sql
SELECT
    (SELECT COUNT(*) FROM dbo.PODStation) AS PODStations,
    (SELECT COUNT(*) FROM dbo.POD) AS PODs,
    (SELECT COUNT(*) FROM dbo.SalesPerson) AS SalesPeople,
    (SELECT COUNT(*) FROM dbo.SalesPersonPOD) AS SalesPersonPODs,
    (SELECT COUNT(*) FROM dbo.Dealer) AS Dealers,
    (SELECT COUNT(*) FROM dbo.salesdealermapping) AS DealerMappings;

-- Expected: 75, 211, 38, 211, 44, 71 on the local reviewed database.

SELECT COUNT(*) AS InvalidManagerLinks
FROM dbo.SalesPerson sp
LEFT JOIN dbo.SalesPerson m ON m.ID = sp.ManagerId
WHERE (sp.Designation = 'Head' AND ISNULL(m.Designation, '') <> 'CXO')
   OR (sp.Designation = 'Sales Manager/Supervisor' AND ISNULL(m.Designation, '') <> 'Head')
   OR (sp.Designation = 'Sales Officer' AND ISNULL(m.Designation, '') <> 'Sales Manager/Supervisor')
   OR (sp.Designation = 'CXO' AND sp.ManagerId IS NOT NULL);

-- Expected: 3, the documented conflicts:
-- Bipindra Neupane, Ujjwol Neupane, Hritik Mishra.
```

The import is idempotent: re-run it and the counts must remain unchanged. It creates no duplicate station names/source IDs, POD names/source IDs, dealer codes, salesperson names, or dealer mappings. The 65 PODs without a verified station are reported but not inserted.

## Initial System Admin

1. Configure `AdminBootstrap:Email` and `AdminBootstrap:Password` with user secrets (see README).
2. Run the application.
3. Sign in with the configured administrator.
4. Remove the bootstrap secrets after the first login.

There is no seed URL and no hard-coded password.

## Manual test cases

### Public access (no login)

- [ ] `GET /` loads the home page.
- [ ] `GET /Public` shows the directory overview.
- [ ] `GET /Public/SalesHierarchy` lists only approved names, designations, and manager references.
- [ ] `GET /Public/SalesHierarchy?q=...` filters results.
- [ ] `GET /Public/PODStations` and `/Public/PODs` list approved data and support search.
- [ ] Public HTML contains no e-mail addresses, dealer mappings, or account identifiers.
- [ ] Visiting `/Admin/Dashboard` anonymously redirects to `/Account/Login`.

### Authentication

- [ ] Valid credentials sign in and respect the return URL.
- [ ] Invalid credentials show a generic error.
- [ ] Sign out clears the session.
- [ ] Forgot password with a non-existent e-mail still shows the confirmation page (no account enumeration).
- [ ] Forgot password with an existing e-mail produces a reset link when SMTP is configured.
- [ ] Reset password with a valid token succeeds; an invalid or expired token fails with a clear message.
- [ ] A newly invited user cannot sign in before setting a password.
- [ ] Account is locked after 5 failed attempts and unlocks after the lockout window.
- [ ] Access denied page appears when a SalesPerson opens an admin URL.

### Admin CRUD

For each module verify list, details, create, edit, delete confirmation, validation messages, and empty states:

- [ ] POD Stations: duplicate name rejected; delete blocked while PODs reference the station.
- [ ] Sales Persons: required name/designation; self-reporting rejected; circular manager rejected; invalid manager rejected; duplicate account e-mail rejected.
- [ ] Sales Persons: manager choices are filtered by designation (CXO → none, Head → CXO, Manager/Supervisor → Head, Officer → Manager); submitting a forged incompatible manager ID is rejected server-side.
- [ ] Sales Persons: searchable POD multi-select shows removable tags, supports name/OSS ID/station search, and submits repeated `PODIds` values.
- [ ] Sales Persons: create/edit with zero, one, and many PODs; duplicates in the submitted IDs are collapsed; a deleted/invalid POD ID is rejected without writing the salesperson.
- [ ] Sales Persons: saving a POD assignment keeps legacy `POD.salespersonId` consistent and does not overwrite another salesperson's legacy owner.
- [ ] Sales Persons: delete blocked by direct reports, assigned PODs, dealer mappings, or a linked login account.
- [ ] PODs: station and salesperson dropdowns are populated from the database; invalid values rejected; create/edit/delete work.
- [ ] Dealers: duplicate code rejected; delete blocked while mappings exist.
- [ ] Sales / Dealer mappings: duplicate pair rejected on create and on edit; reassignment works; delete confirmation works.

### Individual account creation

- [ ] Creating an account creates one `SalesPerson` row and one Identity user linked through `SalesPersonId`.
- [ ] No password is chosen by the administrator.
- [ ] The invitation link is e-mailed when SMTP is configured; otherwise the UI reports pending/failed.
- [ ] With the Development pickup directory, a `.eml` file appears under `App_Data/emails`; opening its **Set my password** link lets the user choose a password and then sign in.
- [ ] The administrator cannot see the new user's password anywhere in the UI or logs.
- [ ] Existing e-mail is rejected without creating any record.
- [ ] If user creation fails, the created business record is compensated (no orphan).

### Activity log

- [ ] **Admin → Activity Log** lists create/update/delete events with timestamp, actor, entity, and summary.
- [ ] Editing a record stores before/after values; filters by entity, action, and search work.
- [ ] Creating an account records both the `SalesPerson` and `UserAccount` events; setting a password records `PasswordSet`.
- [ ] Bulk import records one `Import` event with imported/skipped/failed counts.
- [ ] Password hashes, reset tokens, and invitation links never appear in `Details`.
- [ ] If `dbo.AuditLog` is missing, the admin page shows a warning instead of failing, and normal CRUD still works.
- [ ] An older database with no `RelatedSalesPersonId` column works after `database/Upgrade-AuditLog.sql` is applied; the upgrade is idempotent.
- [ ] **Team Activity** shows only the signed-in manager's subtree: their own actions, team members' actions, and admin actions for those people.
- [ ] Team Activity action and search filters work; another team's events never appear.

### Excel bulk upload

- [ ] Template download returns a valid `.xlsx` with `Name`, `Designation`, `Email`, `ManagerEmail`.
- [ ] A non-`.xlsx` file is rejected.
- [ ] Wrong headers are rejected with a clear message.
- [ ] Missing required fields produce row-level errors.
- [ ] Invalid e-mail formats are reported.
- [ ] Duplicate e-mails inside the file and against existing accounts are marked as skipped.
- [ ] A manager appearing later in the file is resolved correctly.
- [ ] Self-reporting and circular manager references are rejected.
- [ ] Preview does not create any records; confirm does.
- [ ] Re-uploading the same file does not create duplicate accounts.
- [ ] Results show imported, failed, skipped, and invitation status.
- [ ] Results workbook downloads and contains the rejected rows.
- [ ] Rows that fail account creation are compensated and reported.

### SalesPerson workspace

- [ ] My Profile shows the manager and direct reports, and no admin links.
- [ ] My PODs shows only PODs assigned to the signed-in salesperson.
- [ ] My Dealers shows only dealers mapped to that salesperson.
- [ ] My Team and Reporting Hierarchy include direct and indirect reports only.
- [ ] Team Dealer Assignments is read-only for a SalesPerson without reports; a manager can add/remove mappings for self and descendants.
- [ ] Posting `salespersonId` for a user outside the team is rejected.
- [ ] A non-manager POSTing a mapping change is rejected.
- [ ] Removing another team's mapping ID is rejected.
- [ ] SalesPerson cannot access `/Admin/*`, cannot change their manager, and cannot manage master data.
- [ ] A SalesPerson without reports sees a read-only workspace: no add-member button, no account actions, no POD reassignment, and read-only dealer mappings.
- [ ] Posting a team-member, account, POD, or mapping change as a non-manager is rejected.

### Manager team permissions

Set up a manager with at least one direct report as a System Admin, then sign in as the manager.

- [ ] Team navigation shows My Team, Team Accounts, Team PODs, Team Mappings, Team Activity, and Hierarchy.
- [ ] My Team lists direct and indirect reports, including each member's manager, login status, POD count, and dealer count.
- [ ] Adding a team member creates the record with the signed-in manager as `ManagerId`; a spoofed `ManagerId` in the POST is ignored.
- [ ] Editing a team member changes only name/designation; a spoofed `ManagerId` in the POST is ignored.
- [ ] Editing or deleting yourself, your own manager, or somebody above/outside your subtree returns access denied.
- [ ] Deleting a member is blocked while they have reports, PODs, dealer mappings, or a linked account, and the dependency message is specific.
- [ ] After removing those dependencies, deleting the member succeeds.
- [ ] Team Accounts creates a login only for a member of the manager's own subtree; no password is chosen by the manager.
- [ ] Resend invitation, disable, enable, and delete account work; a disabled member cannot sign in until re-enabled.
- [ ] The manager cannot resend/disable/enable/delete their own account or an account outside their subtree.
- [ ] Team PODs lists only PODs already owned inside the subtree; a POD owned by another team is rejected.
- [ ] A POD can be reassigned to the manager or any team member, or unassigned; posting an outside salesperson ID is rejected.
- [ ] Team Accounts and Team Activity show a clear empty state for a non-manager.
- [ ] Non-managers cannot add/remove dealer mappings; managers can manage mappings for themselves and their subtree.
- [ ] A System Admin still sees and controls the full hierarchy, all accounts, all PODs, and the full activity log.

### Security

- [ ] All POST forms include anti-forgery tokens.
- [ ] No passwords or tokens appear in logs.
- [ ] The activity log contains no password hashes, reset tokens, or invitation URLs.
- [ ] Password recovery responses are generic.
- [ ] All ADO.NET repositories use parameterized `SqlCommand` parameters.
- [ ] Role checks exist on `AdminController` and `SalesPersonController`.
- [ ] Public endpoints return DTOs with approved fields only.
- [ ] Invalid IDs in routes return 404/forbidden rather than leaking other teams' data.
