# ==============================================================================
# HAWASSA UNIFIED CAMPUS EVENT MANAGEMENT SYSTEM (HUCEMS)
# AUTOMATED WINDOWS / IIS PRODUCTION DEPLOYMENT SCRIPT
# ==============================================================================

Write-Host "======================================================================" -ForegroundColor Cyan
Write-Host "      HUCEMS AUTOMATED PRODUCTION PUBLISH & DEPLOYMENT SCRIPT         " -ForegroundColor Cyan
Write-Host "======================================================================" -ForegroundColor Cyan

$PublishDir = ".\publish"

Write-Host "`n[STEP 1/4] Stopping any existing running HUCEMS instance..." -ForegroundColor Yellow
Stop-Process -Name "HawassaUnifiedCampusEventManagementSystem" -Force -ErrorAction SilentlyContinue

Write-Host "`n[STEP 2/4] Restoring NuGet Packages & Verifying Dependencies..." -ForegroundColor Yellow
dotnet restore

Write-Host "`n[STEP 3/4] Compiling Release Build & Bundling Static Assets..." -ForegroundColor Yellow
dotnet publish HawassaUnifiedCampusEventManagementSystem.csproj -c Release -o $PublishDir --nologo

if ($LASTEXITCODE -eq 0) {
    Write-Host "`n[STEP 4/4] Production Bundle Successfully Created at: $PublishDir" -ForegroundColor Green
    Write-Host "======================================================================" -ForegroundColor Green
    Write-Host "  DEPLOYMENT READY! To run the production server directly:" -ForegroundColor White
    Write-Host "  cd .\publish ; .\HawassaUnifiedCampusEventManagementSystem.exe --urls=http://localhost:5000" -ForegroundColor Yellow
    Write-Host "======================================================================" -ForegroundColor Green
} else {
    Write-Host "`n[ERROR] Production Publish Failed. Please review build errors above." -ForegroundColor Red
}
