# Testing Guide

## Prerequisites
- SQL Server with database `SalesProdAuthDb` (or update connection string)
- .NET 10 SDK
- Run migrations/seed for Identity if needed

## Seed Initial Admin
1. Run the app
2. Navigate to `/Seed/CreateAdmin` 
3. Login with `admin@saleshierarchy.local` / `Admin@123`
4. Change password immediately

## Manual Test Cases

### Public Access (No Login)
- [ ] GET `/` - Home loads
- [ ] GET `/Public` - Public index
- [ ] GET `/Public/SalesHierarchy` - Lists approved salesperson names/designations only
- [ ] GET `/Public/PODStations` - Approved stations
- [ ] GET `/Public/PODs` - Approved PODs
- [ ] Try to access `/Admin/Dashboard` while unauthenticated - should redirect to login

### Authentication
- [ ] Login with valid credentials
- [ ] Login with invalid credentials - generic error
- [ ] Logout works
- [ ] Forgot Password with non-existent email - still shows confirmation (no info leak)
- [ ] Forgot Password with existing email - generates token, emails link (check EmailSender config)
- [ ] Reset Password with valid token - can set new password
- [ ] Reset Password with invalid/expired token - fails
- [ ] Access Denied for unauthorized roles

### Admin Functions
- [ ] Only SystemAdmin can access `/Admin/*`
- [ ] Create POD Station (CRUD) - deletion blocked if PODs exist
- [ ] Create individual SalesPerson via `/Admin/CreateSalesPersonAccount` - creates Identity user linked to SalesPersonId, sends invitation
- [ ] Resend invitation from User Account Management
- [ ] Disable account from User Account Management
- [ ] Bulk upload page loads with template download
- [ ] Manage Dealers, Mappings, PODs

### SalesPerson Functions
- [ ] SalesPerson login works only after password set via invitation
- [ ] Can view My Profile, My PODs (only assigned), My Dealers (mapped)
- [ ] Can view My Team (direct + indirect reports)
- [ ] Cannot access `/Admin/*` - Access Denied
- [ ] Cannot modify other teams data via URL manipulation - should be denied/filtered
- [ ] Cannot change reporting manager

### Security
- [ ] Anti-forgery tokens on POST forms
- [ ] No passwords/tokens in logs
- [ ] Generic responses for password recovery
- [ ] Parameterized queries used (all ADO.NET repos use SqlCommand with parameters)
- [ ] Role checks on controllers
- [ ] Public endpoints only expose DTOs with approved fields
