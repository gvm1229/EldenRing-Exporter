param(
    [switch]$Force
)

$ErrorActionPreference = "Stop"

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$repoRoot = Resolve-Path (Join-Path $scriptDir "..")

function Require-Command($name) {
    if (-not (Get-Command $name -ErrorAction SilentlyContinue)) {
        throw "Required command not found on PATH: $name"
    }
}

Require-Command git

Push-Location $repoRoot
try {
    Write-Host "Updating git submodules..."
    git submodule update --init --recursive

    $soulstructVersion = "2.6.0"
    $soulstructZipUrl = "https://github.com/Grimrukh/soulstruct-blender/releases/download/v$soulstructVersion/io_soulstruct-$soulstructVersion.zip"
    $soulstructInstallRoot = Join-Path $repoRoot "external\soulstruct-blender-release"
    $soulstructAddonRoot = Join-Path $soulstructInstallRoot "io_soulstruct-$soulstructVersion"
    $soulstructZip = Join-Path $soulstructInstallRoot "io_soulstruct-$soulstructVersion.zip"

    if ($Force -and (Test-Path $soulstructAddonRoot)) {
        Remove-Item -LiteralPath $soulstructAddonRoot -Recurse -Force
    }

    if (-not (Test-Path (Join-Path $soulstructAddonRoot "io_soulstruct_lib"))) {
        New-Item -ItemType Directory -Force -Path $soulstructInstallRoot | Out-Null
        Write-Host "Downloading Soulstruct for Blender $soulstructVersion release..."
        Invoke-WebRequest -Uri $soulstructZipUrl -OutFile $soulstructZip
        Write-Host "Extracting Soulstruct release..."
        Expand-Archive -LiteralPath $soulstructZip -DestinationPath $soulstructAddonRoot -Force
    }

    $witchyVersion = "3.0.0.1"
    $witchyZipUrl = "https://github.com/ividyon/WitchyBND/releases/download/v$witchyVersion/WitchyBND-v$witchyVersion-win-x64.zip"
    $witchyInstallRoot = Join-Path $repoRoot "external\WitchyBND-release"
    $witchyRoot = Join-Path $witchyInstallRoot "WitchyBND-v$witchyVersion-win-x64"
    $witchyExe = Join-Path $witchyRoot "WitchyBND.exe"
    $witchyZip = Join-Path $witchyInstallRoot "WitchyBND-v$witchyVersion-win-x64.zip"

    if ($Force -and (Test-Path $witchyRoot)) {
        Remove-Item -LiteralPath $witchyRoot -Recurse -Force
    }

    if (-not (Test-Path $witchyExe)) {
        New-Item -ItemType Directory -Force -Path $witchyRoot | Out-Null
        Write-Host "Downloading WitchyBND $witchyVersion release..."
        Invoke-WebRequest -Uri $witchyZipUrl -OutFile $witchyZip
        Write-Host "Extracting WitchyBND release..."
        Expand-Archive -LiteralPath $witchyZip -DestinationPath $witchyRoot -Force
    }

    $nuxeRes = Join-Path $repoRoot "external\Nuxe\dist\res"
    if (-not (Test-Path (Join-Path $nuxeRes "BinderKeys"))) {
        throw "Nuxe resources were not initialized: $nuxeRes"
    }

    Write-Host ""
    Write-Host "Dependency setup complete."
    Write-Host "Soulstruct: $soulstructAddonRoot"
    Write-Host "WitchyBND:   $witchyExe"
    Write-Host "Nuxe res:    $nuxeRes"
}
finally {
    Pop-Location
}
