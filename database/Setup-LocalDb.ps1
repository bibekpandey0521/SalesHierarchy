#requires -Version 5.1
<#
.SYNOPSIS
    Creates the SalesHierarchy SQL Server database and business tables on a local instance.

.DESCRIPTION
    Works with SQL Server Developer/Express (default "localhost"), LocalDB
    ("(localdb)\MSSQLLocalDB"), or any other reachable instance. The script is
    idempotent: existing databases and tables are left untouched. Identity tables
    (AspNetUsers, AspNetRoles, ...) are created separately by EF Core migrations:

        dotnet ef database update --project src/SalesHierarchy.Web

.PARAMETER Server
    SQL Server instance. Defaults to "localhost". Use "(localdb)\MSSQLLocalDB"
    for SQL Server LocalDB.

.PARAMETER Database
    Database name. Defaults to "SalesProdAuthDb" (matches appsettings.json).

.PARAMETER SeedDemoData
    Also inserts a small demo hierarchy (3 salespeople, 2 stations, 3 PODs,
    4 dealers) so the public pages and admin screens have data to show.
    Existing rows are never overwritten; rows are only added when the tables
    are empty.

.EXAMPLE
    .\Setup-LocalDb.ps1

.EXAMPLE
    .\Setup-LocalDb.ps1 -Server "(localdb)\MSSQLLocalDB" -SeedDemoData
#>
[CmdletBinding()]
param(
    [string]$Server = "localhost",
    [string]$Database = "SalesProdAuthDb",
    [switch]$SeedDemoData
)

$ErrorActionPreference = "Stop"

if ($Database -notmatch '^[A-Za-z0-9_]+$') {
    throw "Database name may only contain letters, digits, and underscores."
}

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$tableDir = Join-Path $scriptDir "SalesHierarchy.Database\Tables"

if (-not (Get-Command sqlcmd -ErrorAction SilentlyContinue)) {
    throw "sqlcmd was not found on PATH. Install SQL Server command-line tools or run the scripts from SQL Server Management Studio."
}

function Invoke-SqlQuery {
    param(
        [Parameter(Mandatory = $true)][string]$Query,
        [string]$DatabaseName = "master"
    )

    $output = & sqlcmd -S $Server -E -C -b -r 1 -d $DatabaseName -Q $Query
    if ($LASTEXITCODE -ne 0) {
        throw "sqlcmd failed against '$Server' / '$DatabaseName' (exit code $LASTEXITCODE)."
    }

    return $output
}

function Invoke-SqlFile {
    param(
        [Parameter(Mandatory = $true)][string]$Path,
        [Parameter(Mandatory = $true)][string]$DatabaseName
    )

    & sqlcmd -S $Server -E -C -b -r 1 -d $DatabaseName -i $Path
    if ($LASTEXITCODE -ne 0) {
        throw "Failed to run $Path (exit code $LASTEXITCODE)."
    }
}

Write-Host "Connecting to SQL Server instance '$Server'..."
Invoke-SqlQuery -Query "SELECT 1 AS ok;" | Out-Null

Write-Host "Creating database '$Database' if it does not exist..."
Invoke-SqlQuery -Query "IF DB_ID(N'$Database') IS NULL CREATE DATABASE [$Database];" | Out-Null

# Parent tables must exist before the tables that reference them.
$tables = @(
    @{ Name = "PODStation"; File = "PODStation.sql" },
    @{ Name = "SalesPerson"; File = "SalesPerson.sql" },
    @{ Name = "Dealer"; File = "Dealer.sql" },
    @{ Name = "POD"; File = "POD.sql" },
    @{ Name = "salesdealermapping"; File = "salesdealermapping.sql" },
    @{ Name = "AuditLog"; File = "AuditLog.sql" }
)

foreach ($table in $tables) {
    $exists = Invoke-SqlQuery -DatabaseName $Database `
        -Query "SET NOCOUNT ON; SELECT CASE WHEN OBJECT_ID(N'dbo.$($table.Name)', N'U') IS NULL THEN 0 ELSE 1 END;"

    if (($exists | Select-Object -Last 1).Trim() -eq "1") {
        Write-Host "  Table dbo.$($table.Name) already exists - skipped."
        continue
    }

    $file = Join-Path $tableDir $table.File
    if (-not (Test-Path $file)) {
        throw "Table script not found: $file"
    }

    Write-Host "  Creating dbo.$($table.Name)..."
    Invoke-SqlFile -Path $file -DatabaseName $Database
}

$upgradeFile = Join-Path $scriptDir "Upgrade-AuditLog.sql"
if (Test-Path $upgradeFile) {
    Write-Host "Applying idempotent schema upgrades..."
    Invoke-SqlFile -Path $upgradeFile -DatabaseName $Database
}

if ($SeedDemoData) {
    $seedFile = Join-Path $scriptDir "Seed-DemoData.sql"
    if (-not (Test-Path $seedFile)) {
        throw "Seed script not found: $seedFile"
    }

    Write-Host "Seeding demo data (only empty tables are populated)..."
    Invoke-SqlFile -Path $seedFile -DatabaseName $Database
}

Write-Host ""
Write-Host "Business schema is ready on '$Server' / '$Database'."
Write-Host "Next: apply the Identity tables with"
Write-Host "  dotnet ef database update --project src/SalesHierarchy.Web"
Write-Host "Then run the web app:"
Write-Host "  dotnet run --project src/SalesHierarchy.Web"
