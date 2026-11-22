<#
.SYNOPSIS
    Automated Production Build & Packaging Script for Smart Power Scheduler.
    Reads Single Source of Truth (SSOT) from DevConfig.json.
    Produces standard release installer artifacts:
      1. {AppName}-v{Version}-{Arch}-Setup.exe (Self-contained, recommended)
      2. {AppName}-v{Version}-{Arch}-Setup-ReqDotNet10.exe (Framework-dependent)

.PARAMETER VersionTag
    Optional git tag override (e.g. "v0.9.0.1" or "0.9.0.1").
    If omitted, the version defined in DevConfig.json is used.

.PARAMETER Architecture
    Target architecture ("all", "win-x64", "win-arm64").
    Defaults to "all" (reads from DevConfig.json SSOT).
#>
[CmdletBinding()]
param(
    [string]$VersionTag = "",
    [ValidateSet("all", "win-x64", "win-arm64")]
    [string]$Architecture = "all"
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

# Determine target architectures from SSOT
$targetArchitectures = if ($Architecture -eq "all") {
    if ($cfg.App.SupportedArchitectures) { @($cfg.App.SupportedArchitectures) } else { @("win-x64", "win-arm64") }
} else {
    @($Architecture)
}

# 2. Synchronize Icon Pipeline from SSOT master-icon.png
$iconPipeline = Join-Path $srcDir "Assets\Update-Icons.ps1"
if (Test-Path $iconPipeline) {
    & $iconPipeline
}

# Locate Inno Setup Compiler upfront
$isccPath = ""
$cmd = Get-Command iscc.exe -ErrorAction SilentlyContinue
if ($cmd) {
    $isccPath = $cmd.Source
}
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
Write-Host "App Name:        $appName ($appDisplayName)" -ForegroundColor Green
Write-Host "App Version:     $appVersion" -ForegroundColor Green
Write-Host "Vendor:          $appVendor" -ForegroundColor Green
Write-Host "Architectures:   $($targetArchitectures -join ', ')" -ForegroundColor Green
Write-Host "Inno Compiler:   $isccPath" -ForegroundColor Green

# 3. Clean and prepare output directories
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

$streamDeckProj = Join-Path $srcDir "StreamDeck.Plugin\SmartPowerScheduler.StreamDeck.csproj"
$streamDeckPluginSrc = Join-Path $srcDir "StreamDeck.Plugin\com.appsphinx.smartpowerscheduler.sdPlugin"

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

# 4. Process each architecture target
$step = 1
$totalSteps = $targetArchitectures.Count
foreach ($targetArch in $targetArchitectures) {
    Write-Host "`n==========================================================" -ForegroundColor Yellow
    Write-Host "  Target [$step/$totalSteps]: $targetArch" -ForegroundColor Yellow
    Write-Host "==========================================================" -ForegroundColor Yellow

    $innoArch = if ($targetArch -eq "win-arm64") { "arm64" } else { "x64" }
    $archStagingDir = Join-Path $tempPublishDir $targetArch
    $selfContainedDir = Join-Path $archStagingDir "SelfContained"
    $frameworkDepDir = Join-Path $archStagingDir "FrameworkDependent"
    $selfContainedSDDir = Join-Path $archStagingDir "StreamDeckSelfContained"
    $frameworkDepSDDir = Join-Path $archStagingDir "StreamDeckFrameworkDep"

    New-Item -ItemType Directory -Path $selfContainedDir -Force | Out-Null
    New-Item -ItemType Directory -Path $frameworkDepDir -Force | Out-Null
    New-Item -ItemType Directory -Path $selfContainedSDDir -Force | Out-Null
    New-Item -ItemType Directory -Path $frameworkDepSDDir -Force | Out-Null

    # Copy base plugin templates to staging directories
    Copy-Item -Path "$streamDeckPluginSrc\*" -Destination $selfContainedSDDir -Recurse -Force
    Copy-Item -Path "$streamDeckPluginSrc\*" -Destination $frameworkDepSDDir -Recurse -Force

    # 4a. Publish Self-Contained build
    Write-Host "-> Publishing Self-Contained ($targetArch)..." -ForegroundColor Magenta
    dotnet publish "$srcDir\SmartPowerScheduler.csproj" `
        -c Release `
        -r $targetArch `
        --self-contained true `
        -p:PublishSingleFile=true `
        -p:IncludeNativeLibrariesForSelfExtract=true `
        -p:EnableCompressionInSingleFile=true `
        -p:Version=$appVersion `
        -o $selfContainedDir

    Write-Host "-> Publishing Stream Deck Plugin (Self-Contained - $targetArch)..."
    dotnet publish $streamDeckProj `
        -c Release `
        -r $targetArch `
        --self-contained true `
        -p:PublishSingleFile=true `
        -p:IncludeNativeLibrariesForSelfExtract=true `
        -p:EnableCompressionInSingleFile=true `
        -p:Version=$appVersion `
        -o $selfContainedSDDir

    # 4b. Publish Framework-Dependent build (Requires .NET 10)
    Write-Host "-> Publishing Framework-Dependent ($targetArch)..." -ForegroundColor Magenta
    dotnet publish "$srcDir\SmartPowerScheduler.csproj" `
        -c Release `
        -r $targetArch `
        --no-self-contained `
        -p:PublishSingleFile=true `
        -p:IncludeNativeLibrariesForSelfExtract=true `
        -p:EnableCompressionInSingleFile=false `
        -p:Version=$appVersion `
        -o $frameworkDepDir

    Write-Host "-> Publishing Stream Deck Plugin (Framework-Dependent - $targetArch)..."
    dotnet publish $streamDeckProj `
        -c Release `
        -r $targetArch `
        --no-self-contained `
        -p:PublishSingleFile=true `
        -p:IncludeNativeLibrariesForSelfExtract=true `
        -p:EnableCompressionInSingleFile=false `
        -p:Version=$appVersion `
        -o $frameworkDepSDDir

    # 4c. Compile Inno Setup Installers
    Write-Host "-> Compiling Inno Setup Installers ($targetArch)..." -ForegroundColor Magenta

    # Installer 1: Self-contained installer
    $setupBaseName = "$appName-v$appVersion-$targetArch-Setup"
    Write-Host "   Compiling: $setupBaseName.exe"
    & $isccPath `
        "/DMyAppName=$appDisplayName" `
        "/DMyAppExeName=$appName.exe" `
        "/DMyAppVersion=$appVersion" `
        "/DMyPublisher=$appVendor" `
        "/DMyAppURL=$appUrl" `
        "/DMyAppId=$appId" `
        "/DAppArch=$innoArch" `
        "/DSourceDir=$selfContainedDir" `
        "/DStreamDeckPluginDir=$selfContainedSDDir" `
        "/DOutputDir=$artifactsDir" `
        "/DOutputBaseFilename=$setupBaseName" `
        $issPath | Out-Null

    # Installer 2: Framework-dependent installer
    $setupReqDotNetBaseName = "$appName-v$appVersion-$targetArch-Setup-ReqDotNet10"
    Write-Host "   Compiling: $setupReqDotNetBaseName.exe"
    & $isccPath `
        "/DMyAppName=$appDisplayName" `
        "/DMyAppExeName=$appName.exe" `
        "/DMyAppVersion=$appVersion" `
        "/DMyPublisher=$appVendor" `
        "/DMyAppURL=$appUrl" `
        "/DMyAppId=$appId" `
        "/DAppArch=$innoArch" `
        "/DSourceDir=$frameworkDepDir" `
        "/DStreamDeckPluginDir=$frameworkDepSDDir" `
        "/DOutputDir=$artifactsDir" `
        "/DOutputBaseFilename=$setupReqDotNetBaseName" `
        $issPath | Out-Null

    $step++
}

# 5. Cleanup temp build directory
if (Test-Path $tempPublishDir) {
    Remove-Item -Recurse -Force $tempPublishDir -ErrorAction SilentlyContinue
}

# 6. Display Summary
Write-Host "`n==========================================================" -ForegroundColor Cyan
Write-Host "  Artifacts Generated Successfully in: artifacts\" -ForegroundColor Green
Write-Host "==========================================================" -ForegroundColor Cyan

Get-ChildItem -Path $artifactsDir | ForEach-Object {
    $hash = (Get-FileHash -Path $_.FullName -Algorithm SHA256).Hash.Substring(0, 16)
    $mb = [math]::Round($_.Length / 1MB, 2)
    Write-Host ("{0,-58} {1,7} MB  [SHA256: {2}...]" -f $_.Name, $mb, $hash) -ForegroundColor White
}
Write-Host ""
