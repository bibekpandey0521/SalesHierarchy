# Testing Guide

## Prerequisites

- SQL Server or LocalDB with the `SalesProdAuthDb` database (run `database/Setup-LocalDb.ps1`, see README)
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
| Hierarchy rules | self-reporting, direct cycle, deep cycle, valid reassignment, descendants, manager chain, cycle tolerance |
| Bulk row validation | required fields, e-mail formats, max lengths, manager e-mail format, self-reporting, in-file circular managers |
| Bulk upload status | valid/invalid/duplicate rows, importability, batch counts |
| Authorization | controller role attributes, POST + anti-forgery on all admin mutations |

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
- [ ] Team Dealer Assignments allows adding/removing mappings for self and descendants.
- [ ] Posting `salespersonId` for a user outside the team is rejected.
- [ ] Removing another team's mapping ID is rejected.
- [ ] SalesPerson cannot access `/Admin/*`, cannot change their manager, and cannot manage master data.

### Security

- [ ] All POST forms include anti-forgery tokens.
- [ ] No passwords or tokens appear in logs.
- [ ] The activity log contains no password hashes, reset tokens, or invitation URLs.
- [ ] Password recovery responses are generic.
- [ ] All ADO.NET repositories use parameterized `SqlCommand` parameters.
- [ ] Role checks exist on `AdminController` and `SalesPersonController`.
- [ ] Public endpoints return DTOs with approved fields only.
- [ ] Invalid IDs in routes return 404/forbidden rather than leaking other teams' data.
