<#
.SYNOPSIS
    Automated Production Build & Packaging Script for Smart Power Scheduler.
    Reads Single Source of Truth (SSOT) from DevConfig.json.
    Produces standard release installer artifacts:
      1. {AppName}-v{Version}-win-x64-Setup.exe (Self-contained, recommended)
      2. {AppName}-v{Version}-win-x64-Setup-ReqDotNet10.exe (Framework-dependent)

.PARAMETER VersionTag
    Optional git tag override (e.g. "v0.9.0.1" or "0.9.0.1").
    If omitted, the version defined in DevConfig.json is used.
#>
[CmdletBinding()]
param(
    [string]$VersionTag = ""
)

$ErrorActionPreference = "Stop"
$rootDir = $PSScriptRoot
$srcDir = Join-Path $rootDir "src\Smart-Power-Scheduler-Win"
$configPath = Join-Path $srcDir "Config\DevConfig.json"
$artifactsDir = Join-Path $rootDir "artifacts"
$tempPublishDir = Join-Path $rootDir ".temp_publish"

Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host "  Smart Power Scheduler - Production Release Pipeline" -ForegroundColor Cyan
Write-Host "==========================================================" -ForegroundColor Cyan

# 1. Read Single Source of Truth (SSOT)
if (!(Test-Path $configPath)) {
    throw "Configuration file not found at: $configPath"
}
$cfg = Get-Content -Raw $configPath | ConvertFrom-Json

$appName = $cfg.App.Name
$appDisplayName = $cfg.App.DisplayName
$appVendor = $cfg.App.Vendor
$appId = $cfg.App.AppId
$appUrl = $cfg.App.RepositoryUrl
$appVersion = $cfg.App.Version

# Override version if tag passed
if (![string]::IsNullOrWhiteSpace($VersionTag)) {
    $cleanVersion = $VersionTag.Trim().TrimStart('v','V')
    if ($cleanVersion -match '^[0-9]+(\.[0-9]+)*$') {
        $appVersion = $cleanVersion
        Write-Host "-> Overriding version from tag: $appVersion" -ForegroundColor Yellow
    }
}

# 2. Synchronize Icon Pipeline from SSOT master-icon.png
$iconPipeline = Join-Path $srcDir "Assets\Update-Icons.ps1"
if (Test-Path $iconPipeline) {
    & $iconPipeline
}

Write-Host "App Name:        $appName ($appDisplayName)" -ForegroundColor Green
Write-Host "App Version:     $appVersion" -ForegroundColor Green
Write-Host "Vendor:          $appVendor" -ForegroundColor Green

# 2. Clean and prepare output directories
if (Test-Path $artifactsDir) {
    Get-ChildItem -Path $artifactsDir -Recurse | Remove-Item -Recurse -Force -ErrorAction SilentlyContinue
} else {
    New-Item -ItemType Directory -Path $artifactsDir -Force | Out-Null
}
if (Test-Path $tempPublishDir) {
    Get-ChildItem -Path $tempPublishDir -Recurse | Remove-Item -Recurse -Force -ErrorAction SilentlyContinue
} else {
    New-Item -ItemType Directory -Path $tempPublishDir -Force | Out-Null
}

$selfContainedDir = Join-Path $tempPublishDir "SelfContained"
$frameworkDepDir = Join-Path $tempPublishDir "FrameworkDependent"

$streamDeckProj = Join-Path $srcDir "StreamDeck.Plugin\SmartPowerScheduler.StreamDeck.csproj"
$streamDeckPluginSrc = Join-Path $srcDir "StreamDeck.Plugin\com.appsphinx.smartpowerscheduler.sdPlugin"

$selfContainedSDDir = Join-Path $tempPublishDir "StreamDeckSelfContained"
$frameworkDepSDDir = Join-Path $tempPublishDir "StreamDeckFrameworkDep"

# Synchronize Stream Deck manifest.json with SSOT
$manifestFile = Join-Path $streamDeckPluginSrc "manifest.json"
if (Test-Path $manifestFile) {
    $manifestObj = Get-Content -Raw $manifestFile | ConvertFrom-Json
    $manifestObj.Version = $appVersion
    $manifestObj.URL = $appUrl
    $manifestObj.Name = $appDisplayName
    $manifestObj.Description = $cfg.App.Description
    $manifestObj | ConvertTo-Json -Depth 10 | Set-Content -Path $manifestFile -Encoding UTF8
    Write-Host "-> Synchronized Stream Deck manifest.json with SSOT (v$appVersion)" -ForegroundColor Green
}

# Copy base plugin templates to staging directories
Copy-Item -Path $streamDeckPluginSrc -Destination $selfContainedSDDir -Recurse -Force
Copy-Item -Path $streamDeckPluginSrc -Destination $frameworkDepSDDir -Recurse -Force

# 3. Publish Self-Contained Win-x64
Write-Host "`n[1/3] Publishing Self-Contained Win-x64 build..." -ForegroundColor Magenta
dotnet publish "$srcDir\SmartPowerScheduler.csproj" `
    -c Release `
    -r win-x64 `
    --self-contained true `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:EnableCompressionInSingleFile=true `
    -p:Version=$appVersion `
    -o $selfContainedDir

Write-Host "-> Publishing Stream Deck Plugin (Self-Contained)..."
dotnet publish $streamDeckProj `
    -c Release `
    -r win-x64 `
    --self-contained true `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:EnableCompressionInSingleFile=true `
    -p:Version=$appVersion `
    -o $selfContainedSDDir

# 4. Publish Framework-Dependent Win-x64 (Requires .NET 10)
Write-Host "`n[2/3] Publishing Framework-Dependent Win-x64 build..." -ForegroundColor Magenta
dotnet publish "$srcDir\SmartPowerScheduler.csproj" `
    -c Release `
    -r win-x64 `
    --no-self-contained `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:EnableCompressionInSingleFile=false `
    -p:Version=$appVersion `
    -o $frameworkDepDir

Write-Host "-> Publishing Stream Deck Plugin (Framework-Dependent)..."
dotnet publish $streamDeckProj `
    -c Release `
    -r win-x64 `
    --no-self-contained `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:EnableCompressionInSingleFile=false `
    -p:Version=$appVersion `
    -o $frameworkDepSDDir

# 5. Build Inno Setup Installers
Write-Host "`n[3/3] Building Inno Setup Installers..." -ForegroundColor Magenta

$isccPath = ""

# 1. Check PATH
$cmd = Get-Command iscc.exe -ErrorAction SilentlyContinue
if ($cmd) {
    $isccPath = $cmd.Source
}

# 2. Check Windows Registry (App Paths & Uninstall keys for 32-bit and 64-bit)
if (-not $isccPath) {
    $regKeys = @(
        "HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\ISCC.exe",
        "HKCU:\SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\ISCC.exe",
        "HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\Inno Setup 6_is1",
        "HKLM:\SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\Inno Setup 6_is1"
    )
    foreach ($rk in $regKeys) {
        if (Test-Path $rk) {
            $regItem = Get-ItemProperty -Path $rk -ErrorAction SilentlyContinue
            if ($regItem -and $regItem.'(default)' -and (Test-Path $regItem.'(default)')) {
                $isccPath = $regItem.'(default)'
                break
            }
            if ($regItem -and $regItem.InstallLocation) {
                $candidate = Join-Path $regItem.InstallLocation "ISCC.exe"
                if (Test-Path $candidate) {
                    $isccPath = $candidate
                    break
                }
            }
        }
    }
}

# 3. Fallback to standard environment directories
if (-not $isccPath) {
    $envCandidates = @(
        (Join-Path ${env:ProgramFiles(x86)} "Inno Setup 6\ISCC.exe"),
        (Join-Path ${env:ProgramFiles} "Inno Setup 6\ISCC.exe"),
        (Join-Path ${env:LOCALAPPDATA} "Programs\Inno Setup 6\ISCC.exe")
    )
    foreach ($cand in $envCandidates) {
        if ($cand -and (Test-Path $cand)) {
            $isccPath = $cand
            break
        }
    }
}

if (-not $isccPath) {
    throw "Inno Setup Compiler (ISCC.exe) was not found on this system. Please install Inno Setup 6 or add ISCC.exe to PATH."
}

$issPath = Join-Path $srcDir "Packaging\installer.iss"
Write-Host "Found Inno Setup Compiler: $isccPath" -ForegroundColor Green

# Installer 1: Self-contained installer
$setupBaseName = "$appName-v$appVersion-win-x64-Setup"
Write-Host "Compiling: $setupBaseName.exe"
& $isccPath `
    "/DMyAppName=$appDisplayName" `
    "/DMyAppExeName=$appName.exe" `
    "/DMyAppVersion=$appVersion" `
    "/DMyPublisher=$appVendor" `
    "/DMyAppURL=$appUrl" `
    "/DMyAppId=$appId" `
    "/DSourceDir=$selfContainedDir" `
    "/DStreamDeckPluginDir=$selfContainedSDDir" `
    "/DOutputDir=$artifactsDir" `
    "/DOutputBaseFilename=$setupBaseName" `
    $issPath | Out-Null

# Installer 2: Framework-dependent installer
$setupReqDotNetBaseName = "$appName-v$appVersion-win-x64-Setup-ReqDotNet10"
Write-Host "Compiling: $setupReqDotNetBaseName.exe"
& $isccPath `
    "/DMyAppName=$appDisplayName" `
    "/DMyAppExeName=$appName.exe" `
    "/DMyAppVersion=$appVersion" `
    "/DMyPublisher=$appVendor" `
    "/DMyAppURL=$appUrl" `
    "/DMyAppId=$appId" `
    "/DSourceDir=$frameworkDepDir" `
    "/DStreamDeckPluginDir=$frameworkDepSDDir" `
    "/DOutputDir=$artifactsDir" `
    "/DOutputBaseFilename=$setupReqDotNetBaseName" `
    $issPath | Out-Null

# 6. Cleanup temp build directory
if (Test-Path $tempPublishDir) {
    Remove-Item -Recurse -Force $tempPublishDir -ErrorAction SilentlyContinue
}

# 7. Display Summary
Write-Host "`n==========================================================" -ForegroundColor Cyan
Write-Host "  Artifacts Generated Successfully in: artifacts\" -ForegroundColor Green
Write-Host "==========================================================" -ForegroundColor Cyan

Get-ChildItem -Path $artifactsDir | ForEach-Object {
    $hash = (Get-FileHash -Path $_.FullName -Algorithm SHA256).Hash.Substring(0, 16)
    $mb = [math]::Round($_.Length / 1MB, 2)
    Write-Host ("{0,-58} {1,7} MB  [SHA256: {2}...]" -f $_.Name, $mb, $hash) -ForegroundColor White
}
Write-Host ""
