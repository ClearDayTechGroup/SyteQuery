<#
.SYNOPSIS
    Builds the SyteQuery installer (Setup.exe) and update packages with Velopack.

.EXAMPLE
    ./build/Build-Installer.ps1 -Version 0.2.0

.NOTES
    Output goes to artifacts/releases:
      ClearDay.SyteQuery-win-Setup.exe   what users run to install
      *.nupkg                            full + delta packages the in-app updater downloads
      releases.win.json                  the update feed
    To publish an update, upload everything in that folder to a GitHub Release (tag v<Version>).
    The app is installed per-user to %LOCALAPPDATA%\ClearDay.SyteQuery. Your data lives separately in
    %LOCALAPPDATA%\SyteQuery (app.db) and is never touched by install, update or uninstall.
#>
param(
    [string]$Version = "0.1.0",
    [switch]$NoReadyToRun,
    # Wipe artifacts/releases first. Normally leave it alone: Velopack builds small delta updates by
    # comparing against the previous releases sitting there, and refuses to re-pack a version that exists.
    [switch]$Clean
)

$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$publishDir = Join-Path $root 'artifacts\publish'
$releaseDir = Join-Path $root 'artifacts\releases'

Push-Location $root
try {
    dotnet tool restore | Out-Null

    if (Test-Path $publishDir) { Remove-Item $publishDir -Recurse -Force }
    if ($Clean -and (Test-Path $releaseDir)) { Remove-Item $releaseDir -Recurse -Force }

    # Self-contained: the .NET runtime ships inside the installer, so users need nothing pre-installed.
    # ReadyToRun pre-compiles the code for a noticeably faster first launch (bigger files).
    $r2r = if ($NoReadyToRun) { 'false' } else { 'true' }
    dotnet publish SyteQuery.Desktop/SyteQuery.Desktop.csproj -c Release -r win-x64 --self-contained `
        -p:Version=$Version -p:PublishReadyToRun=$r2r -o $publishDir
    if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed" }

    dotnet vpk pack `
        --packId ClearDay.SyteQuery `
        --packVersion $Version `
        --packDir $publishDir `
        --mainExe SyteQuery.exe `
        --packTitle SyteQuery `
        --runtime win-x64 `
        --packAuthors "ClearDay Tech Group" `
        --icon SyteQuery.Desktop/Assets/SyteQuery.ico `
        --outputDir $releaseDir
    if ($LASTEXITCODE -ne 0) { throw "vpk pack failed" }

    Write-Host "`nDone. Installer and update packages are in: $releaseDir" -ForegroundColor Green
}
finally {
    Pop-Location
}
