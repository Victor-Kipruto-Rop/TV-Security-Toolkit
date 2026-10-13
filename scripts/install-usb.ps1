[CmdletBinding(SupportsShouldProcess)]
param(
    [Parameter(Mandatory = $true)]
    [string]$PackageDirectory,
    [Parameter(Mandatory = $true)]
    [ValidatePattern('^[A-Za-z]$')]
    [string]$DriveLetter,
    [switch]$Force
)

$ErrorActionPreference = "Stop"
$package = (Resolve-Path -LiteralPath $PackageDirectory).Path
$launcher = Join-Path $package "Start-TV-Security-Toolkit.cmd"
$app = Join-Path $package "TV-Security-Toolkit.exe"
if (-not (Test-Path -LiteralPath $launcher -PathType Leaf) -or
    -not (Test-Path -LiteralPath $app -PathType Leaf)) {
    throw "Package is incomplete. Expected TV-Security-Toolkit.exe and Start-TV-Security-Toolkit.cmd in: $package"
}

$drive = $DriveLetter.ToUpperInvariant()
$volume = Get-Volume -DriveLetter $drive -ErrorAction Stop
if ($volume.DriveType -ne "Removable") {
    throw "$($drive): is not reported by Windows as a removable drive. Refusing to copy to a potentially incorrect disk."
}
$destinationRoot = $drive + ":\"
$destination = Join-Path $destinationRoot "TV-Security-Toolkit-USB"
$sourceBytes = (Get-ChildItem -LiteralPath $package -Recurse -File | Measure-Object -Property Length -Sum).Sum
$freeBytes = (Get-PSDrive -Name $drive).Free
if ($sourceBytes -gt $freeBytes) {
    throw ("USB drive needs {0:N0} bytes but only {1:N0} bytes are free." -f $sourceBytes, $freeBytes)
}
if (Test-Path -LiteralPath $destination) {
    throw "Destination already exists. Back it up or remove it intentionally before retrying: $destination"
}
if (-not $Force) {
    $answer = Read-Host "Copy the complete toolkit package to $destination? Type COPY to continue"
    if ($answer -cne "COPY") { Write-Host "Cancelled."; exit 1 }
}
if (-not $PSCmdlet.ShouldProcess($destination, "Copy portable TV Security Toolkit package")) {
    return
}

New-Item -ItemType Directory -Path $destination -Force | Out-Null
try {
    Copy-Item -Path (Join-Path $package "*") -Destination $destination -Recurse
    $sourceFiles = @(Get-ChildItem -LiteralPath $package -Recurse -File)
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
    Write-Host "To launch, open the USB drive and double-click TV-Security-Toolkit-USB\Start-TV-Security-Toolkit.cmd"
    Write-Host "Insertion will not auto-launch the application. This package runs on Windows, not on the TV."
}
catch {
    Write-Error $_
    throw
}
