#Requires -Version 5.1
<#
.SYNOPSIS
  Builds a production-ready self-contained WGC package.

.DESCRIPTION
  - Publishes win-x64 self-contained single-file Release build
  - Embeds Desktop OAuth Client ID/Secret from %LocalAppData%\WGC\google-oauth.json
    (or credentials.json; falls back to legacy CalendarApp folder)
  - Zips the output under artifacts/
  - Optionally Authenticode-signs if $env:CALENDARAPP_SIGN_THUMBPRINT / $env:WGC_SIGN_THUMBPRINT is set

.EXAMPLE
  .\scripts\publish.ps1
#>
param(
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64",
    [string]$Version = "1.0.0"
)

$ErrorActionPreference = "Stop"
$scriptDir = $PSScriptRoot
if ([string]::IsNullOrWhiteSpace($scriptDir)) {
    $scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
}
$root = Resolve-Path (Join-Path $scriptDir "..")
$project = Join-Path $root "CalendarDesktop\CalendarDesktop.csproj"
$outDir = Join-Path $root "artifacts\WGC-$Version-$Runtime"
$zipPath = Join-Path $root "artifacts\WGC-$Version-$Runtime.zip"

function Get-OAuthPair {
    $candidates = @(
        (Join-Path $env:LOCALAPPDATA "WGC\google-oauth.json"),
        (Join-Path $env:LOCALAPPDATA "WGC\credentials.json"),
        (Join-Path $env:LOCALAPPDATA "CalendarApp\google-oauth.json"),
        (Join-Path $env:LOCALAPPDATA "CalendarApp\credentials.json")
    )

    foreach ($path in $candidates) {
        if (-not (Test-Path $path)) { continue }

        $json = Get-Content $path -Raw | ConvertFrom-Json
        if ($path -like "*credentials.json") {
            if ($json.installed.client_id) {
                return @{
                    ClientId = [string]$json.installed.client_id
                    ClientSecret = [string]$json.installed.client_secret
                    Source = $path
                }
            }
        }
        else {
            if ($json.clientId -and $json.clientSecret) {
                return @{ ClientId = [string]$json.clientId; ClientSecret = [string]$json.clientSecret; Source = $path }
            }
            if ($json.ClientId -and $json.ClientSecret) {
                return @{ ClientId = [string]$json.ClientId; ClientSecret = [string]$json.ClientSecret; Source = $path }
            }
        }
    }

    return $null
}

Write-Host "==> Publishing WGC $Version ($Runtime)" -ForegroundColor Cyan
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

$exe = Join-Path $outDir "WGC.exe"
if (-not (Test-Path $exe)) {
    throw "WGC.exe was not produced at $exe"
}

$thumbprint = $env:WGC_SIGN_THUMBPRINT
if ([string]::IsNullOrWhiteSpace($thumbprint)) {
    $thumbprint = $env:CALENDARAPP_SIGN_THUMBPRINT
}
if (-not [string]::IsNullOrWhiteSpace($thumbprint)) {
    Write-Host "==> Signing with certificate thumbprint $thumbprint" -ForegroundColor Cyan
    & signtool sign /fd SHA256 /td SHA256 /tr http://timestamp.digicert.com /sha1 $thumbprint $exe
    if ($LASTEXITCODE -ne 0) {
        throw "signtool failed"
    }
} else {
    Write-Host "==> Skipping Authenticode signing (set WGC_SIGN_THUMBPRINT to enable)" -ForegroundColor DarkYellow
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
