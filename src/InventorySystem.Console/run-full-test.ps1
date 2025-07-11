# Full Test Automation Script for Inventory Management System
# This script runs the complete test scenario with interactive date selection

param(
    [string]$Department = "DeptA",
    [switch]$SkipDatabaseInit = $false,
    [switch]$VerboseOutput = $false,
    [switch]$NonInteractive = $false,
    [string]$StartDate = "",
    [string]$EndDate = ""
)

# Color functions for output
function Write-Success($message) { Write-Host $message -ForegroundColor Green }
function Write-Info($message) { Write-Host $message -ForegroundColor Cyan }
function Write-Warning($message) { Write-Host $message -ForegroundColor Yellow }
function Write-Error($message) { Write-Host $message -ForegroundColor Red }
function Write-Header($message) { 
    Write-Host "`n========================================" -ForegroundColor Magenta
    Write-Host $message -ForegroundColor Magenta
    Write-Host "========================================`n" -ForegroundColor Magenta
}

# Function to get date with validation
function Get-DateInput($prompt, $defaultValue) {
    while ($true) {
        $input = Read-Host "$prompt (Default: $defaultValue)"
        if ([string]::IsNullOrWhiteSpace($input)) {
            $input = $defaultValue
        }
        
        try {
            $date = [DateTime]::Parse($input)
            return $date.ToString("yyyy-MM-dd")
        }
        catch {
            Write-Warning "Invalid date format. Please enter date in YYYY-MM-DD format."
        }
    }
}

# Interactive date selection
if (-not $NonInteractive -and ([string]::IsNullOrWhiteSpace($StartDate) -or [string]::IsNullOrWhiteSpace($EndDate))) {
    Write-Header "Test Execution Date Range Setup"
    Write-Info "This script will process inventory data for the specified date range."
    Write-Info "Default range: June 1, 2025 to June 27, 2025`n"
    
    # Get start date
    if ([string]::IsNullOrWhiteSpace($StartDate)) {
        $StartDate = Get-DateInput "Enter start date" "2025-06-01"
    }
    
    # Get end date
    if ([string]::IsNullOrWhiteSpace($EndDate)) {
        $EndDate = Get-DateInput "Enter end date" "2025-06-27"
    }
    
    # Validate date range
    $startDateTime = [DateTime]::Parse($StartDate)
    $endDateTime = [DateTime]::Parse($EndDate)
    
    if ($startDateTime -gt $endDateTime) {
        Write-Error "Start date cannot be after end date!"
        exit 1
    }
    
    $totalDays = ($endDateTime - $startDateTime).Days + 1
    Write-Info "`nSelected date range: $StartDate to $EndDate ($totalDays days)"
    
    # Confirmation
    $confirm = Read-Host "`nDo you want to proceed with this date range? (Y/N)"
    if ($confirm -ne 'Y' -and $confirm -ne 'y') {
        Write-Warning "Execution cancelled by user."
        exit 0
    }
} else {
    # Use default values if not provided
    if ([string]::IsNullOrWhiteSpace($StartDate)) { $StartDate = "2025-06-01" }
    if ([string]::IsNullOrWhiteSpace($EndDate)) { $EndDate = "2025-06-27" }
}

# Start time measurement
$startTime = Get-Date

# Change to console project directory
$projectPath = "C:\Development\InventoryManagementSystem\src\InventorySystem.Console"
if (-not (Test-Path $projectPath)) {
    Write-Error "Project path not found: $projectPath"
    exit 1
}
Set-Location $projectPath

# Step 1: Database initialization
if (-not $SkipDatabaseInit) {
    Write-Header "STEP 1: Database Initialization"
    Write-Info "Initializing database with force option..."
    dotnet run init-database --force
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Database initialization failed!"
        exit 1
    }
    Write-Success "Database initialized successfully"
    
    # Test database connection
    Write-Info "Testing database connection..."
    dotnet run test-connection
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Database connection test failed!"
        exit 1
    }
    Write-Success "Database connection verified"
    Start-Sleep -Seconds 2
} else {
    Write-Warning "Skipping database initialization (--SkipDatabaseInit flag set)"
    
    # Still test connection even if skipping init
    Write-Info "Testing database connection..."
    dotnet run test-connection
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Database connection test failed!"
        exit 1
    }
}

# Step 2: Initial inventory setup
Write-Header "STEP 2: Initial Inventory Setup"
Write-Info "Importing previous month inventory..."
dotnet run init-inventory $Department
if ($LASTEXITCODE -ne 0) {
    Write-Error "Initial inventory import failed!"
    exit 1
}
Write-Success "Initial inventory imported successfully"
Start-Sleep -Seconds 2

# Step 3: Daily processing loop
$currentDate = [DateTime]::Parse($StartDate)
$endDateTime = [DateTime]::Parse($EndDate)
$dayCount = 0

while ($currentDate -le $endDateTime) {
    $dayCount++
    $dateString = $currentDate.ToString("yyyy-MM-dd")
    $displayDate = $currentDate.ToString("MMM dd, yyyy")
    
    Write-Header "Processing Day $dayCount : $displayDate"
    
    # 3.1 Import voucher data
    Write-Info "[1/5] Importing voucher data for $dateString..."
    dotnet run import-folder $Department $dateString
    if ($LASTEXITCODE -ne 0) {
        Write-Warning "Import folder had warnings for $dateString"
    } else {
        Write-Success "Voucher data imported"
    }
    Start-Sleep -Seconds 1
    
    # 3.2 Create unmatch list
    Write-Info "[2/5] Creating unmatch list for $dateString..."
    dotnet run unmatch-list $dateString
    if ($LASTEXITCODE -ne 0) {
        Write-Warning "Unmatch list creation had issues for $dateString"
    } else {
        Write-Success "Unmatch list created"
    }
    Start-Sleep -Seconds 1
    
    # 3.3 Create daily report (using dev command)
    Write-Info "[3/5] Creating daily report for $dateString..."
    if (Test-Path ".\dev.ps1") {
        .\dev.ps1 dev-daily-report $dateString
    } else {
        dotnet run dev-daily-report $dateString
    }
    if ($LASTEXITCODE -ne 0) {
        Write-Warning "Daily report creation had issues for $dateString"
    } else {
        Write-Success "Daily report created"
    }
    Start-Sleep -Seconds 1
    
    # 3.4 Check daily close status
    Write-Info "[4/5] Checking daily close status for $dateString..."
    if (Test-Path ".\dev.ps1") {
        .\dev.ps1 dev-check-daily-close $dateString
    } else {
        dotnet run dev-check-daily-close $dateString
    }
    Start-Sleep -Seconds 1
    
    # 3.5 Execute daily close process (IMPORTANT)
    Write-Info "[5/5] Executing daily close process for $dateString..."
    dotnet run dev-daily-close $dateString --skip-validation
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Daily close process failed for $dateString!"
        Write-Warning "Continuing with next date despite error..."
    } else {
        Write-Success "Daily close completed successfully"
    }
    
    # Progress summary
    if ($VerboseOutput) {
        Write-Info "Completed processing for $displayDate"
    }
    
    # Move to next date
    $currentDate = $currentDate.AddDays(1)
    
    # Brief pause between days
    if ($currentDate -le $endDateTime) {
        Start-Sleep -Seconds 2
    }
}

# Final summary
Write-Header "Test Execution Complete"
$endTime = Get-Date
$duration = $endTime - $startTime

Write-Success "Processed $dayCount days from $StartDate to $EndDate"
Write-Info "Total execution time: $($duration.TotalMinutes.ToString('F2')) minutes"
Write-Info "Average time per day: $([Math]::Round($duration.TotalSeconds / $dayCount, 2)) seconds"

# Final verification
Write-Header "Running Final Verification"

# Check data status for the last date
Write-Info "Checking data status for last date: $EndDate"
dotnet run check-data-status $EndDate
if ($LASTEXITCODE -ne 0) {
    Write-Warning "Data status check returned with warnings or errors"
}

# Check master data integrity
Write-Info "`nChecking master data integrity..."
dotnet run check-masters
if ($LASTEXITCODE -ne 0) {
    Write-Warning "Master data check returned with warnings or errors"
}

# Summary
Write-Success "`nAll processing completed!"
Write-Info "Please review the data status output above to verify:"
Write-Info " - Voucher counts (Sales, Purchase, Inventory Adjustment)"
Write-Info " - Master data integrity"
Write-Info " - Daily processing status"