param(
    [Parameter(Mandatory = $true)]
    [string]$PackagePath,

    # Exit 0 when every binary is validly signed, 1 when any are not.
    [switch]$Quiet
)

$ErrorActionPreference = "Stop"

if (-not (Test-Path -LiteralPath $PackagePath -PathType Container)) {
    throw "Package directory not found: $PackagePath"
}

$binaries = @(Get-ChildItem -LiteralPath $PackagePath -Recurse -File |
    Where-Object { $_.Extension -in ".exe", ".dll" })

if ($binaries.Count -eq 0) {
    throw "No .exe or .dll binaries were found under $PackagePath"
}

$results = foreach ($binary in $binaries) {
    $signature = Get-AuthenticodeSignature -LiteralPath $binary.FullName
    [pscustomobject]@{
        File    = $binary.FullName.Substring((Resolve-Path $PackagePath).Path.Length).TrimStart("\", "/")
        Status  = [string]$signature.Status
        Signer  = if ($signature.SignerCertificate) { $signature.SignerCertificate.Subject } else { "" }
        Valid   = ($signature.Status -eq "Valid" -and $null -ne $signature.SignerCertificate)
    }
}

$invalid = @($results | Where-Object { -not $_.Valid })

if (-not $Quiet) {
    $results | Format-Table File, Status, Signer -AutoSize
    Write-Host ""
    Write-Host ("Signed: {0}/{1}" -f ($results.Count - $invalid.Count), $results.Count)
    if ($invalid.Count -gt 0) {
        Write-Warning "Unsigned or untrusted binaries prevent this package from running on machines with Smart App Control enabled:"
        foreach ($item in $invalid) { Write-Host ("  {0} -> {1}" -f $item.File, $item.Status) -ForegroundColor Yellow }
    }
}

if ($invalid.Count -gt 0) { exit 1 }
exit 0