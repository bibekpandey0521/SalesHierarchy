# SalesHierarchy

ASP.NET Core MVC (.NET 10) SalesHierarchy system with ADO.NET repositories, ASP.NET Core Identity, role-based access, public read-only views, admin CRUD, individual/bulk SalesPerson account creation with secure password setup, and team-scoped access.

## Structure
- Solution: SalesHierarchy.sln
- src/SalesHierarchy.Web
- src/SalesHierarchy.Core
- src/SalesHierarchy.Infrastructure
- tests/SalesHierarchy.Tests
- database/SalesHierarchy.Database

## Setup
1. Update connection string in src/SalesHierarchy.Web/appsettings.json
2. Run migrations/seed via /Seed/CreateAdmin
3. Build and run

## Features
- Two roles: SystemAdmin, SalesPerson
- Public read-only pages
- Admin CRUD for all business tables
- Individual & bulk SalesPerson registration
- Password setup invitations (time-limited tokens)
- Forgot/Reset password
- Team-scoped access for SalesPerson

