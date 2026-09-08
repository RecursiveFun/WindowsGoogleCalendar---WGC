#Requires -Version 5.1
<#
.SYNOPSIS
  Builds a production-ready self-contained CalendarApp package.

.DESCRIPTION
  - Publishes win-x64 self-contained single-file Release build
  - Embeds Desktop OAuth Client ID/Secret from %LocalAppData%\CalendarApp\google-oauth.json
    (or credentials.json) into the published appsettings.json
  - Zips the output under artifacts/
  - Optionally Authenticode-signs if $env:CALENDARAPP_SIGN_THUMBPRINT is set

.EXAMPLE
  .\scripts\publish.ps1

.EXAMPLE
  $env:CALENDARAPP_SIGN_THUMBPRINT = "YOURCERTTHUMBPRINT"
  .\scripts\publish.ps1
#>
param(
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64",
    [string]$Version = "1.0.0"
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
if (-not $PSScriptRoot) {
    $root = Split-Path -Parent $MyInvocation.MyCommand.Path
    if (-not $root) { $root = Get-Location }
}
# Prefer repo root relative to this script.
$scriptDir = $PSScriptRoot
if ([string]::IsNullOrWhiteSpace($scriptDir)) {
    $scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
}
$root = Resolve-Path (Join-Path $scriptDir "..")
$project = Join-Path $root "CalendarDesktop\CalendarDesktop.csproj"
$outDir = Join-Path $root "artifacts\CalendarApp-$Version-$Runtime"
$zipPath = Join-Path $root "artifacts\CalendarApp-$Version-$Runtime.zip"

function Get-OAuthPair {
    $oauthPath = Join-Path $env:LOCALAPPDATA "CalendarApp\google-oauth.json"
    $credPath = Join-Path $env:LOCALAPPDATA "CalendarApp\credentials.json"

    if (Test-Path $oauthPath) {
        $json = Get-Content $oauthPath -Raw | ConvertFrom-Json
        if ($json.clientId -and $json.clientSecret) {
            return @{ ClientId = [string]$json.clientId; ClientSecret = [string]$json.clientSecret; Source = "google-oauth.json" }
        }
        if ($json.ClientId -and $json.ClientSecret) {
            return @{ ClientId = [string]$json.ClientId; ClientSecret = [string]$json.ClientSecret; Source = "google-oauth.json" }
        }
    }

    if (Test-Path $credPath) {
        $json = Get-Content $credPath -Raw | ConvertFrom-Json
        if ($json.installed.client_id) {
            return @{
                ClientId = [string]$json.installed.client_id
                ClientSecret = [string]$json.installed.client_secret
                Source = "credentials.json"
            }
        }
    }

    return $null
}

Write-Host "==> Publishing CalendarApp $Version ($Runtime)" -ForegroundColor Cyan
if (Test-Path $outDir) { Remove-Item $outDir -Recurse -Force }
New-Item -ItemType Directory -Force -Path $outDir | Out-Null

dotnet publish $project `
    -c $Configuration `
    -r $Runtime `
    --self-contained true `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:Version=$Version `
    -p:AssemblyVersion=$Version.0 `
    -p:FileVersion=$Version.0 `
    -o $outDir

if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish failed"
}

$oauth = Get-OAuthPair
$settingsPath = Join-Path $outDir "appsettings.json"
if ($null -eq $oauth) {
    Write-Host "WARNING: No local OAuth credentials found. Published build will require Advanced Google setup." -ForegroundColor Yellow
} else {
    $payload = @{
        GoogleCalendar = @{
            ClientId = $oauth.ClientId
            ClientSecret = $oauth.ClientSecret
        }
    } | ConvertTo-Json -Depth 5
    Set-Content -Path $settingsPath -Value $payload -Encoding UTF8
    Write-Host "==> Embedded OAuth from $($oauth.Source) into published appsettings.json" -ForegroundColor Green
}

$exe = Join-Path $outDir "CalendarApp.exe"
if (-not (Test-Path $exe)) {
    throw "CalendarApp.exe was not produced at $exe"
}

$thumbprint = $env:CALENDARAPP_SIGN_THUMBPRINT
if (-not [string]::IsNullOrWhiteSpace($thumbprint)) {
    Write-Host "==> Signing with certificate thumbprint $thumbprint" -ForegroundColor Cyan
    & signtool sign /fd SHA256 /td SHA256 /tr http://timestamp.digicert.com /sha1 $thumbprint $exe
    if ($LASTEXITCODE -ne 0) {
        throw "signtool failed"
    }
} else {
    Write-Host "==> Skipping Authenticode signing (set CALENDARAPP_SIGN_THUMBPRINT to enable)" -ForegroundColor DarkYellow
}

if (Test-Path $zipPath) { Remove-Item $zipPath -Force }
Compress-Archive -Path (Join-Path $outDir "*") -DestinationPath $zipPath -Force

Write-Host ""
Write-Host "Production package ready:" -ForegroundColor Green
Write-Host "  Folder: $outDir"
Write-Host "  Zip:    $zipPath"
Write-Host "  Run:    $exe"
Write-Host ""
Write-Host "Google Console reminder: publish the OAuth consent screen (or keep Testing + test users)." -ForegroundColor DarkCyan
