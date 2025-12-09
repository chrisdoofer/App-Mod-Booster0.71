#!/usr/bin/env pwsh
#
# Post-create setup script for devcontainer
# Installs sqlcmd and Python dependencies
#

Write-Host "====================================" -ForegroundColor Cyan
Write-Host "Post-Create Setup Starting..." -ForegroundColor Cyan
Write-Host "====================================" -ForegroundColor Cyan
Write-Host ""

# Install Microsoft ODBC Driver for SQL Server (optional but useful)
Write-Host "Installing ODBC drivers..." -ForegroundColor Yellow
try {
    sudo bash -c @"
curl -sSL https://packages.microsoft.com/keys/microsoft.asc | gpg --dearmor -o /usr/share/keyrings/microsoft-prod.gpg
curl -sSL https://packages.microsoft.com/config/debian/12/prod.list | tee /etc/apt/sources.list.d/mssql-release.list
apt-get update
ACCEPT_EULA=Y apt-get install -y msodbcsql18 unixodbc-dev 2>/dev/null
"@
    Write-Host "✓ ODBC drivers installed" -ForegroundColor Green
} catch {
    Write-Warning "ODBC drivers installation failed (optional, continuing...)"
}
Write-Host ""

# Install go-sqlcmd (modern SQL command-line tool)
Write-Host "Installing go-sqlcmd..." -ForegroundColor Yellow
try {
    $tempFile = "/tmp/sqlcmd.tar.bz2"
    Invoke-WebRequest -Uri "https://github.com/microsoft/go-sqlcmd/releases/download/v1.8.0/sqlcmd-linux-amd64.tar.bz2" -OutFile $tempFile
    sudo tar -xjf $tempFile -C /usr/local/bin
    Remove-Item $tempFile -Force
    
    $version = sqlcmd --version
    Write-Host "✓ sqlcmd installed: $version" -ForegroundColor Green
} catch {
    Write-Error "Failed to install sqlcmd: $_"
    exit 1
}
Write-Host ""

# Install Python packages
Write-Host "Installing Python packages..." -ForegroundColor Yellow
try {
    pip install jq pyodbc azure-identity
    Write-Host "✓ Python packages installed" -ForegroundColor Green
} catch {
    Write-Warning "Some Python packages failed to install (optional, continuing...)"
}
Write-Host ""

Write-Host "====================================" -ForegroundColor Cyan
Write-Host "Post-Create Setup Complete!" -ForegroundColor Green
Write-Host "====================================" -ForegroundColor Cyan
