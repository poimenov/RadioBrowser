<#
.SYNOPSIS
Build script for RadioBrowser project (https://github.com/poimenov/RadioBrowser)

.DESCRIPTION
This script performs:
1. Clean solution
2. Create source code archive
4. Publish for specified platforms
5. Final report

.PARAMETER RuntimeIdentifiers
Platforms to build for (default: "win-x64,linux-x64")

.EXAMPLE
./build.ps1
#>

param (
    [string[]]$RuntimeIdentifiers = @("win-x64", "linux-x64")
)

$ErrorActionPreference = "Stop"

# Project configuration
$projectName = "RadioBrowser"
$projectPath = "./src/RadioBrowser.fsproj"
$dateString = Get-Date -Format "yyyy-MM-dd"
$artifactsDir = "./artifacts"
$publishDir = "$artifactsDir/publish"

# 1. Clean solution
Write-Host "Cleaning solution..." -ForegroundColor Cyan
Get-ChildItem ./ -include bin, obj -Recurse | ForEach-Object ($_) { remove-item $_.fullname -Force -Recurse }
if (Test-Path $artifactsDir) {
    Remove-Item $artifactsDir -Recurse -Force
}

# Create directories
New-Item -ItemType Directory -Path $artifactsDir -Force | Out-Null
New-Item -ItemType Directory -Path $publishDir -Force | Out-Null

# 2. Create source code archive
Write-Host "`nCreating source code archive..." -ForegroundColor Magenta
$sourceArchivePath = "$artifactsDir/$projectName-src-$dateString.zip"

# Create archive
Compress-Archive -Path ./ -DestinationPath $sourceArchivePath -CompressionLevel Optimal -Force
Write-Host "Source archive created: $sourceArchivePath" -ForegroundColor Green

# 4. Build and publish for each platform
foreach ($rid in $RuntimeIdentifiers) {
    Write-Host "`nProcessing platform: $rid" -ForegroundColor Yellow

    $sc = "self-contained"
    $platformPublishDir = "$publishDir\$rid"
    $archiveName = "$projectName-$rid-$dateString.zip"
    $archivePath = "$artifactsDir/$archiveName"

    # Publish project
    Write-Host "Publishing for $rid..." -ForegroundColor Cyan
    dotnet publish $projectPath -c Release -r $rid -o $platformPublishDir `
        /p:DebugType=None /p:DebugSymbols=false

    # Archive results
    Write-Host "Creating archive for $rid..." -ForegroundColor Cyan
    Compress-Archive -Path "$platformPublishDir/*" -DestinationPath $archivePath -CompressionLevel Optimal

    # Clean Publishing folder
    Write-Host "Cleaning publishing folder..." -ForegroundColor Cyan
    Remove-Item  $platformPublishDir -Recurse -Force

    $archiveName = "$projectName-$sc-$rid-$dateString.zip"
    $archivePath = "$artifactsDir/$archiveName"

    # Publish self-contained project
    Write-Host "Publishing for $sc $rid..." -ForegroundColor Cyan
    dotnet publish $projectPath -c Release -r $rid -o $platformPublishDir --sc `
        /p:DebugType=None /p:DebugSymbols=false

    # Archive results
    Write-Host "Creating archive for $sc $rid..." -ForegroundColor Cyan
    Compress-Archive -Path "$platformPublishDir/*" -DestinationPath $archivePath -CompressionLevel Optimal

    Write-Host "Platform $rid completed. Archive: $archivePath" -ForegroundColor Green
}

# 5. Final report
Write-Host "`nBuild completed successfully!" -ForegroundColor Green
Write-Host "Artifacts location: $(Resolve-Path $artifactsDir)" -ForegroundColor Green
Write-Host "Generated files:" -ForegroundColor Green
Get-ChildItem $artifactsDir | Select-Object Name, Length, LastWriteTime | Format-Table -AutoSize
