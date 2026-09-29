<#
.SYNOPSIS
    Sets up a private, portable copy of Python for the Ash Below updater.
.DESCRIPTION
    Downloads the official embeddable Python ZIP from python.org, checks it against a pinned SHA-256 checksum,
    and unpacks it into -Destination. No installer runs, no admin rights are needed, and PATH is not changed.
    Delete the destination folder to remove it.
#>
param([Parameter(Mandatory = $true)][string]$Destination)

$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'
$Version = '3.14.7'
# SHA-256 of the embeddable ZIPs, as published in python.org's SBOM files for this release.
$Hashes = @{
    amd64 = 'd297e5ff019966817ad8502465176139f2d3d840fa4ed84b13bed399a6ab1f15'
    arm64 = 'f6773983c8959d4281e48c4540cb0bdd23e42391e4e951ce17e7ceb52658f21c'
}
$Marker = 'ashbelow-python.txt'

$arch = if ($env:PROCESSOR_ARCHITEW6432) { $env:PROCESSOR_ARCHITEW6432 } else { $env:PROCESSOR_ARCHITECTURE }
$flavor = switch ($arch) { 'AMD64' { 'amd64' } 'ARM64' { 'arm64' } default { $null } }
if (-not $flavor) {
    Write-Host "Automatic Python setup does not support this PC ($arch). Install Python 3.10+ from https://www.python.org/downloads/"
    exit 1
}

$Destination = [IO.Path]::GetFullPath($Destination)
$parent = Split-Path -Parent $Destination
New-Item -ItemType Directory -Force -Path $parent | Out-Null
$staging = Join-Path $parent ('.python-setup-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $staging | Out-Null
try {
    [Net.ServicePointManager]::SecurityProtocol = [Net.ServicePointManager]::SecurityProtocol -bor [Net.SecurityProtocolType]::Tls12
    $zip = Join-Path $staging 'python.zip'
    Write-Host "Downloading Python $Version from python.org..."
    Invoke-WebRequest -UseBasicParsing -Uri "https://www.python.org/ftp/python/$Version/python-$Version-embed-$flavor.zip" -OutFile $zip
    if ((Get-FileHash -Algorithm SHA256 -Path $zip).Hash.ToLowerInvariant() -ne $Hashes[$flavor]) {
        throw 'The Python download did not match its checksum and was discarded.'
    }
    $unpacked = Join-Path $staging 'python'
    Expand-Archive -Path $zip -DestinationPath $unpacked
    & (Join-Path $unpacked 'python.exe') -c 'import ssl, sys; sys.exit(0 if sys.version_info >= (3, 10) else 1)'
    if ($LASTEXITCODE -ne 0) { throw 'The downloaded Python did not start.' }
    Set-Content -Path (Join-Path $unpacked $Marker) -Value $Version
    if (Test-Path -LiteralPath $Destination) {
        # Only replace a broken copy this script made earlier; never delete an unrelated folder.
        if (-not (Test-Path -LiteralPath (Join-Path $Destination $Marker))) { throw "$Destination already exists and was not created by Ash Below." }
        Remove-Item -LiteralPath $Destination -Recurse -Force
    }
    Move-Item -LiteralPath $unpacked -Destination $Destination
    Write-Host "Python is ready (private copy for the updater in $Destination)."
    $code = 0
}
catch {
    Write-Host "Python setup failed: $($_.Exception.Message)"
    $code = 1
}
finally {
    Remove-Item -LiteralPath $staging -Recurse -Force -ErrorAction SilentlyContinue
}
exit $code
