[CmdletBinding(SupportsShouldProcess)]
param(
    [string]$OutputDirectory = (Join-Path $PSScriptRoot "..\artifacts\TV-Security-Toolkit-USB"),
    [string]$DotNetPath = "",

    # Path to a .pfx containing a code-signing certificate (EKU 1.3.6.1.5.5.7.3.3).
    # When supplied, every .exe/.dll in the staged package is signed and timestamped.
    [string]$SigningCertificatePath = "",

    # Password for the .pfx. Prefer the SIGNING_CERTIFICATE_PASSWORD environment variable or an
    # interactive prompt over passing this on the command line, which is visible in process listings.
    [string]$SigningCertificatePassword = "",

    # RFC 3161 timestamp endpoint. signtool requires a full endpoint path; DigiCert's TSA responds at
    # http://timestamp.digicert.com (its / path 404s, which is normal for a TSA service).
    [string]$TimestampUrl = "http://timestamp.digicert.com",
    [string]$SignToolPath = "",

    # Fail the run unless every shipped binary carries a valid signature. Use for anything that
    # leaves a trusted machine. Without this, an unsigned package is produced but clearly marked.
    [switch]$RequireSigned,

    # Escape hatch for local lab use only. Never use for a distributed package.
    [switch]$AllowUnsigned,

    # Re-sign every binary, including ones that already carry a valid signature. Normally only
    # unsigned binaries are stamped, because the .NET runtime ships several hundred already signed.
    [switch]$ForceResign
)

$ErrorActionPreference = "Stop"
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$outputPath = if ([IO.Path]::IsPathRooted($OutputDirectory)) {
    $OutputDirectory
} else {
    Join-Path $repoRoot $OutputDirectory
}
$outputFullPath = [IO.Path]::GetFullPath($outputPath)
$dotnetCommand = if (-not [string]::IsNullOrWhiteSpace($DotNetPath)) {
    $DotNetPath
} else {
    $command = Get-Command dotnet -ErrorAction SilentlyContinue
    if ($null -ne $command) {
        $command.Source
    } else {
        Join-Path $env:ProgramFiles "dotnet\dotnet.exe"
    }
}

if (-not (Test-Path -LiteralPath $dotnetCommand -PathType Leaf)) {
    throw "Could not find dotnet. Install the .NET 10 SDK or pass -DotNetPath."
}

if (Test-Path -LiteralPath $outputFullPath) {
    throw "Output directory already exists; choose a new empty destination: $outputFullPath"
}

# --- Signing preflight -------------------------------------------------------
# Fail before spending minutes on a publish that cannot produce a shippable package.
if ($RequireSigned -and $AllowUnsigned) {
    throw "-RequireSigned and -AllowUnsigned are mutually exclusive."
}

function Resolve-SignTool {
    param([string]$ExplicitPath)
    if (-not [string]::IsNullOrWhiteSpace($ExplicitPath)) {
        if (-not (Test-Path -LiteralPath $ExplicitPath -PathType Leaf)) {
            throw "signtool.exe not found at the supplied -SignToolPath: $ExplicitPath"
        }
        return $ExplicitPath
    }
    $command = Get-Command signtool.exe -ErrorAction SilentlyContinue
    if ($null -ne $command) { return $command.Source }
    $sdkBin = Join-Path ${env:ProgramFiles(x86)} "Windows Kits\10\bin"
    if (Test-Path -LiteralPath $sdkBin) {
        $found = Get-ChildItem -LiteralPath $sdkBin -Filter signtool.exe -Recurse -File -ErrorAction SilentlyContinue |
            Where-Object { $_.FullName -match "[\\/]x64[\\/]signtool\.exe$" } |
            Sort-Object FullName -Descending | Select-Object -First 1 -ExpandProperty FullName
        if (-not [string]::IsNullOrWhiteSpace($found)) { return $found }
    }
    return $null
}

$certificatePassword = $SigningCertificatePassword
if ([string]::IsNullOrWhiteSpace($certificatePassword)) {
    $certificatePassword = $env:SIGNING_CERTIFICATE_PASSWORD
}

$signTool = $null
$willSign = -not [string]::IsNullOrWhiteSpace($SigningCertificatePath)

if ($willSign) {
    if (-not (Test-Path -LiteralPath $SigningCertificatePath -PathType Leaf)) {
        throw "Signing certificate not found: $SigningCertificatePath"
    }
    if ([string]::IsNullOrWhiteSpace($certificatePassword)) {
        $secure = Read-Host -Prompt "Password for $SigningCertificatePath" -AsSecureString
        $bstr = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($secure)
        try {
            $certificatePassword = [Runtime.InteropServices.Marshal]::PtrToStringBSTR($bstr)
        }
        finally {
            [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($bstr)
        }
    }
    $signTool = Resolve-SignTool -ExplicitPath $SignToolPath
    if ([string]::IsNullOrWhiteSpace($signTool)) {
        throw "signtool.exe was not found. Install the Windows SDK, put signtool on PATH, or pass -SignToolPath."
    }
}
elseif ($RequireSigned) {
    throw "-RequireSigned was requested but no -SigningCertificatePath was supplied."
}
elseif (-not $AllowUnsigned) {
    Write-Warning "No -SigningCertificatePath supplied: the package will be UNSIGNED and will not start on machines with Smart App Control enabled. Use -RequireSigned to make this a hard failure, or -AllowUnsigned to acknowledge it."
}

# --- Package signing / verification ------------------------------------------
function Invoke-PackagePreflight {
    param([string]$Root, [string]$Tool, [string]$Certificate, [string]$Password, [string]$Timestamp, [bool]$Force)

    $binaries = @(Get-ChildItem -LiteralPath $Root -Recurse -File |
        Where-Object { $_.Extension -in ".exe", ".dll" })
    if ($binaries.Count -eq 0) {
        throw "No .exe or .dll binaries were found to sign under $Root."
    }

    # The self-contained runtime ships several hundred binaries that Microsoft already signed.
    # Re-signing them costs a network timestamp round-trip each and buys nothing, so only sign what is
    # not yet validly signed unless -ForceResign was requested.
    $toSign = if ($Force) {
        $binaries
    }
    else {
        @($binaries | Where-Object {
            $existing = Get-AuthenticodeSignature -LiteralPath $_.FullName
            $existing.Status -ne "Valid" -or $null -eq $existing.SignerCertificate
        })
    }

    Write-Host "Preflight: $($toSign.Count) of $($binaries.Count) binaries need signing."
    foreach ($binary in $toSign) {
        # /td must accompany /tr, and the timestamp authority must be a full endpoint. A bare host
        # is rejected by signtool with "Invalid Timestamp URL".
        & $Tool sign /fd SHA256 /td SHA256 /tr $Timestamp /f $Certificate /p $Password $binary.FullName
        if ($LASTEXITCODE -ne 0) { throw "Signing failed for $($binary.FullName)." }
    }
    foreach ($binary in $toSign) {
        & $Tool verify /pa /all $binary.FullName
        if ($LASTEXITCODE -ne 0) { throw "Signature verification failed for $($binary.FullName)." }
    }

    # Independent check using the platform verifier, not just signtool's exit code.
    $untrusted = @()
    foreach ($binary in $binaries) {
        $signature = Get-AuthenticodeSignature -LiteralPath $binary.FullName
        if ($signature.Status -ne "Valid") {
            $untrusted += "$($binary.Name): $($signature.Status)"
        }
        elseif (-not $signature.SignerCertificate) {
            $untrusted += "$($binary.Name): no signer certificate present"
        }
    }
    if ($untrusted.Count -gt 0) {
        throw "Preflight found binaries that are not validly signed:`n  " + ($untrusted -join "`n  ")
    }
    Write-Host "Preflight passed: $($binaries.Count) binaries signed and verified ($($toSign.Count) signed in this run)."
}

function Test-PackageSigned {
    param([string]$Root)
    $binaries = @(Get-ChildItem -LiteralPath $Root -Recurse -File |
        Where-Object { $_.Extension -in ".exe", ".dll" })
    if ($binaries.Count -eq 0) {
        throw "No .exe or .dll binaries were found under $Root."
    }
    $unsigned = @()
    foreach ($binary in $binaries) {
        $signature = Get-AuthenticodeSignature -LiteralPath $binary.FullName
        if ($signature.Status -ne "Valid" -or -not $signature.SignerCertificate) {
            $unsigned += "$($binary.Name): $($signature.Status)"
        }
    }
    return [pscustomobject]@{
        Total    = $binaries.Count
        Unsigned = $unsigned
        Signed   = $binaries.Count - $unsigned.Count
    }
}

$workDirectory = Join-Path ([IO.Path]::GetTempPath()) ("tvst-package-" + [guid]::NewGuid().ToString("N"))
$publishDirectory = Join-Path $workDirectory "publish"
$stageDirectory = Join-Path $workDirectory "TV-Security-Toolkit-USB"

try {
    New-Item -ItemType Directory -Path $publishDirectory, $stageDirectory -Force | Out-Null

    & $dotnetCommand publish (Join-Path $repoRoot "src\TVSecurityToolkit.App\TVSecurityToolkit.App.csproj") `
        --configuration Release --runtime win-x64 --self-contained true --output $publishDirectory
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet publish failed with exit code $LASTEXITCODE"
    }

    Copy-Item -Path (Join-Path $publishDirectory "*") -Destination $stageDirectory -Recurse
    foreach ($directory in @("config", "profiles", "tests", "test-catalog", "payloads", "schemas", "drivers", "docs")) {
        $sourceDirectory = Join-Path $repoRoot $directory
        $packageDirectory = Join-Path $stageDirectory $directory
        New-Item -ItemType Directory -Path $packageDirectory -Force | Out-Null
        foreach ($file in Get-ChildItem -LiteralPath $sourceDirectory -File -Recurse -Force) {
            $relativePath = $file.FullName.Substring($sourceDirectory.Length).TrimStart("\", "/")
            if ($relativePath -match '(^|[\\/])(bin|obj|\.git|\.venv|__pycache__)([\\/]|$)' -or
                $relativePath.EndsWith(".pyc", [StringComparison]::OrdinalIgnoreCase)) {
                continue
            }
            $destination = Join-Path $packageDirectory $relativePath
            New-Item -ItemType Directory -Path (Split-Path -Parent $destination) -Force | Out-Null
            Copy-Item -LiteralPath $file.FullName -Destination $destination
        }
    }
    Copy-Item -LiteralPath (Join-Path $repoRoot "VERSION") -Destination $stageDirectory
    $packageSettingsPath = Join-Path $stageDirectory "appsettings.json"
    if (-not (Test-Path -LiteralPath $packageSettingsPath -PathType Leaf)) {
        throw "Published appsettings.json is missing from the package."
    }
    $packageSettings = Get-Content -LiteralPath $packageSettingsPath -Raw | ConvertFrom-Json
    $packageSettings.outputDir = "."
    $packageSettings | ConvertTo-Json -Depth 32 | Set-Content -LiteralPath $packageSettingsPath -Encoding UTF8
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot "Start-TV-Security-Toolkit.cmd") -Destination $stageDirectory
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot "autorun.inf") -Destination $stageDirectory

    foreach ($directory in @("runtime", "reports\html", "reports\json", "reports\pdf", "reports\raw", "evidence", "logs")) {
        New-Item -ItemType Directory -Path (Join-Path $stageDirectory $directory) -Force | Out-Null
    }

    foreach ($requiredFile in @(
        "TV-Security-Toolkit.exe",
        "Start-TV-Security-Toolkit.cmd",
        "autorun.inf",
        "TV-Security-Toolkit.runtimeconfig.json",
        "appsettings.json",
        "VERSION",
        "config\security-policy.json",
        "profiles\production-readonly\device-policy.json",
        "tests\payg\replay\replay-protection.test.json",
        "test-catalog\payg\replay.json"
    )) {
        if (-not (Test-Path -LiteralPath (Join-Path $stageDirectory $requiredFile) -PathType Leaf)) {
            throw "Required package file is missing: $requiredFile"
        }
    }

    $stagedSettings = Get-Content -LiteralPath (Join-Path $stageDirectory "appsettings.json") -Raw | ConvertFrom-Json
    if ($stagedSettings.outputDir -ne ".") {
        throw "USB package outputDir must be relative to the package root."
    }

    $excludedContent = Get-ChildItem -LiteralPath $stageDirectory -Recurse -Force |
        Where-Object { $_.FullName -match '[\\/](bin|obj|\.git|\.venv|__pycache__)([\\/]|$)' -or $_.Extension -eq ".pyc" }
    if ($excludedContent) {
        throw "Package contains excluded build/cache content: $($excludedContent[0].FullName)"
    }

    if ($willSign) {
        Invoke-PackagePreflight -Root $stageDirectory -Tool $signTool -Certificate $SigningCertificatePath `
            -Password $certificatePassword -Timestamp $TimestampUrl -Force $ForceResign.IsPresent
    }
    elseif ($RequireSigned) {
        throw "Refusing to stage an unsigned package because -RequireSigned was requested."
    }
    else {
        $state = Test-PackageSigned -Root $stageDirectory
        Write-Warning "Package is UNSIGNED: $($state.Signed)/$($state.Total) binaries validly signed."
    }

    New-Item -ItemType Directory -Path (Split-Path -Parent $outputFullPath) -Force | Out-Null
    Move-Item -LiteralPath $stageDirectory -Destination $outputFullPath
    Write-Host "USB package staged at $outputFullPath"

    if ($willSign) {
        $final = Test-PackageSigned -Root $outputFullPath
        if ($final.Unsigned.Count -gt 0) {
            throw "Post-move verification failed; the staged package is not fully signed:`n  " + ($final.Unsigned -join "`n  ")
        }
        Write-Host "Signed package verified in place: $($final.Signed)/$($final.Total) binaries validly signed."
    }
}
finally {
    # Never leave signing material behind in the temp staging area.
    if ([string]::IsNullOrWhiteSpace($certificatePassword)) {
        # nothing to scrub
    }
    else {
        $certificatePassword = $null
    }
    if (Test-Path -LiteralPath $workDirectory) {
        Remove-Item -LiteralPath $workDirectory -Recurse -Force
    }
}
