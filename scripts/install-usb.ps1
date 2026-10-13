[CmdletBinding(SupportsShouldProcess, ConfirmImpact = 'Medium')]
param(
    [Parameter(Mandatory = $true)]
    [ValidateNotNullOrEmpty()]
    [string]$PackageDirectory,

    [Parameter(Mandatory = $true)]
    [ValidatePattern('^[A-Za-z]$')]
    [string]$DriveLetter,

    # Some USB flash drives report as Fixed on Windows. This override is explicit and
    # does not bypass the confirmation prompt, free-space check, or overwrite protection.
    [switch]$AllowFixedDrive,

    # Skip the interactive COPY prompt for controlled automation. Does not overwrite.
    [switch]$Force
)

$ErrorActionPreference = "Stop"

$packageItem = Get-Item -LiteralPath $PackageDirectory -ErrorAction Stop
if (-not $packageItem.PSIsContainer) {
    throw "PackageDirectory must be a directory: $PackageDirectory"
}
$package = $packageItem.FullName
$launcher = Join-Path $package "Start-TV-Security-Toolkit.cmd"
$app = Join-Path $package "TV-Security-Toolkit.exe"

if (-not (Test-Path -LiteralPath $launcher -PathType Leaf) -or
    -not (Test-Path -LiteralPath $app -PathType Leaf)) {
    throw "Package is incomplete. Expected TV-Security-Toolkit.exe and Start-TV-Security-Toolkit.cmd in: $package"
}

$sourceFiles = @(Get-ChildItem -LiteralPath $package -Recurse -File -Force)
if ($sourceFiles.Count -eq 0) {
    throw "Package contains no files: $package"
}
$sourceBytes = ($sourceFiles | Measure-Object -Property Length -Sum).Sum
if ($null -eq $sourceBytes) { $sourceBytes = 0 }

$drive = $DriveLetter.ToUpperInvariant()
$volume = Get-Volume -DriveLetter $drive -ErrorAction Stop
if ($volume.HealthStatus -ne "Healthy") {
    throw "Drive $($drive): is not healthy (status: $($volume.HealthStatus)). Refusing to copy."
}
if ($volume.OperationalStatus -notcontains "OK") {
    throw "Drive $($drive): is not ready (status: $($volume.OperationalStatus -join ', '))."
}
if ($volume.DriveType -ne "Removable") {
    if (-not $AllowFixedDrive) {
        throw "$($drive): is reported as '$($volume.DriveType)', not Removable. If this is the intended USB drive, rerun with -AllowFixedDrive after checking the drive letter."
    }
    Write-Warning "$($drive): is reported as '$($volume.DriveType)'. Confirm the drive letter carefully; the override does not disable other safety checks."
}

$destinationRoot = $drive + ":\"
$destination = Join-Path $destinationRoot "TV-Security-Toolkit-USB"
$freeBytes = (Get-PSDrive -Name $drive -ErrorAction Stop).Free
if ($sourceBytes -gt $freeBytes) {
    throw ("USB drive needs {0:N0} bytes but only {1:N0} bytes are free." -f $sourceBytes, $freeBytes)
}
if (Test-Path -LiteralPath $destination) {
    throw "Destination already exists. Back it up or remove it intentionally before retrying: $destination"
}
if (-not $PSCmdlet.ShouldProcess($destination, "Copy and verify portable TV Security Toolkit package")) {
    return
}
if (-not $Force) {
    $answer = Read-Host "Copy $($sourceFiles.Count) files ($('{0:N0}' -f $sourceBytes) bytes) to $destination? Type COPY to continue"
    if ($answer -cne "COPY") {
        Write-Host "Cancelled; no files were copied."
        return
    }
}

$createdDestination = $false
try {
    New-Item -ItemType Directory -Path $destination -ErrorAction Stop | Out-Null
    $createdDestination = $true
    Copy-Item -Path (Join-Path $package "*") -Destination $destination -Recurse -Force:$false

    $copiedFiles = @(Get-ChildItem -LiteralPath $destination -Recurse -File -Force)
    if ($copiedFiles.Count -ne $sourceFiles.Count) {
        throw "Copy verification failed: source has $($sourceFiles.Count) files but destination has $($copiedFiles.Count)."
    }

    foreach ($sourceFile in $sourceFiles) {
        $relative = $sourceFile.FullName.Substring($package.Length).TrimStart("\", "/")
        $targetFile = Join-Path $destination $relative
        if (-not (Test-Path -LiteralPath $targetFile -PathType Leaf)) {
            throw "Copy verification failed; missing file: $relative"
        }
        $sourceHash = (Get-FileHash -LiteralPath $sourceFile.FullName -Algorithm SHA256).Hash
        $targetHash = (Get-FileHash -LiteralPath $targetFile -Algorithm SHA256).Hash
        if ($sourceHash -ne $targetHash) {
            throw "Copy verification failed; SHA-256 mismatch: $relative"
        }
    }

    Write-Host "USB package copied and verified: $destination"
    Write-Host "Files verified: $($sourceFiles.Count); SHA-256 algorithm: SHA256."
    Write-Host "To launch, open the USB drive and double-click TV-Security-Toolkit-USB\Start-TV-Security-Toolkit.cmd"
    Write-Host "Insertion will not auto-launch the application. This package runs on Windows, not on the TV."
}
catch {
    if ($createdDestination -and (Test-Path -LiteralPath $destination)) {
        # Remove only the folder created by this run; existing destinations are refused above.
        Remove-Item -LiteralPath $destination -Recurse -Force -ErrorAction SilentlyContinue
    }
    throw
}
