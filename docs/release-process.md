# Release process

## Preconditions

- Confirm the version in `VERSION` follows `MAJOR.MINOR.PATCH` and update `CHANGELOG.md`.
- Review and merge the changes through the normal pull-request process.
- Confirm the Windows build and test workflows pass, including the dependency and CodeQL checks.
- Verify the supported device protocol and test profile for the intended release. Simulator results do not establish TV compatibility.
- Keep production signing material in a protected signing environment. Never commit a private signing key, certificate, password, or customer data.

## Local USB package

With the .NET 10 SDK installed on Windows, create a clean self-contained package:

```powershell
./scripts/package-usb.ps1 -OutputDirectory C:\release\TV-Security-Toolkit-USB
```

The script refuses to overwrite an existing output directory. It publishes the WPF application for `win-x64`, stages configuration, profiles, test definitions/catalogs, schemas, drivers, and documentation, and creates report/evidence/log directories. In the staged package only, `appsettings.json` is configured with `outputDir: "."`, so each run writes reports to `reports/<session-id>/`, its JSON evidence archive to `evidence/<session-id>/`, and structured operational logs to `logs/` on the USB drive. It does not copy repository metadata, build intermediates, previous reports, or previous logs. Review the resulting contents before distribution; the included signing configuration and payloads are for the lab simulator only.

To start the portable app, open the USB drive in File Explorer and double-click `Start-TV-Security-Toolkit.cmd`. The launcher starts the app from the package directory so its relative configuration and data paths resolve correctly. Each completed test run writes a JSON report to `reports` and a per-session evidence archive to `evidence`; other report formats can be generated in the Reports page. `autorun.inf` only supplies the drive label and icon; it deliberately does not attempt to auto-launch anything. Current Windows versions block automatic execution from removable drives, so insertion alone cannot safely or reliably start the application.

## Signed releases

Signing is built into the packaging script so a release cannot ship unsigned binaries by accident:

```powershell
# Build, sign, timestamp, and refuse to stage anything not validly signed
./scripts/package-usb.ps1 -OutputDirectory C:\release\TV-Security-Toolkit-USB `
    -SigningCertificatePath C:\secrets\tv-toolkit.pfx -RequireSigned
```

Provide the password through the `SIGNING_CERTIFICATE_PASSWORD` environment variable or an interactive
prompt; passing `-SigningCertificatePassword` on the command line is visible in process listings. The script
resolves `signtool.exe` from `PATH` or the Windows SDK, signs every `.exe`/`.dll` with SHA-256 plus an
RFC 3161 timestamp, verifies each signature with `signtool verify /pa /all`, and then re-checks the result
with the platform verifier. It fails if any binary is unsigned or untrusted.

`signtool.exe` can be obtained without a machine-wide SDK install by referencing the official Microsoft
`Microsoft.Windows.SDK.BuildTools` package (for example `dotnet add package Microsoft.Windows.SDK.BuildTools`
in a scratch project) and pointing `-SignToolPath` at `bin/<version>/x64/signtool.exe`.

Timestamping defaults to `http://timestamp.digicert.com`. signtool requires a complete timestamp endpoint;
a bare host with an https scheme is rejected with `Invalid Timestamp URL`.

Flags:

| Flag | Effect |
|---|---|
| `-SigningCertificatePath` | `.pfx` to sign with; enables signing |
| `-RequireSigned` | Hard failure if any shipped binary is unsigned. Use for anything leaving a trusted machine |
| `-AllowUnsigned` | Acknowledge an unsigned package (local lab only). Mutually exclusive with `-RequireSigned` |
| `-SignToolPath` | Explicit `signtool.exe` when it is not on `PATH` |
| `-TimestampUrl` | Defaults to DigiCert |

Without any signing flag the script still packages, but warns that the package is unsigned and will not start
on machines with Smart App Control enabled.

To check an existing package without rebuilding:

```powershell
./scripts/verify-package-signatures.ps1 -PackagePath C:\release\TV-Security-Toolkit-USB
```

It prints a per-binary table and exits `1` if anything is unsigned or untrusted, so it can gate a deployment
script. Note that the self-contained runtime contributes many already-signed Microsoft binaries; the check
reports the whole set so gaps in first-party binaries are visible.

The certificate must be issued by a publicly trusted certificate authority with the Code Signing EKU
(`1.3.6.1.5.5.7.3.3`). Self-signed certificates are rejected by Smart App Control and will not make the
package runnable on a secured Windows machine. A self-signed certificate is useful for exercising the
pipeline locally: signing and timestamping succeed, but verification reports
`A certificate chain processed, but terminated in a root certificate which is not trusted by the trust
provider` and the run correctly refuses to stage the package. That rejection is the expected behaviour, not
a defect, and confirms the preflight is doing its job.

## Signed GitHub release

The `Release package` workflow runs on a `v*` tag or manual dispatch. It requires the protected repository secrets `SIGNING_CERTIFICATE_BASE64` (a base64-encoded PFX) and `SIGNING_CERTIFICATE_PASSWORD`. It signs every EXE and DLL with SHA-256 and a timestamp, verifies each signature, then uploads the USB archive. A tag creates a GitHub release only after those steps succeed.

Configure a protected release environment and restrict who can create release tags. If signing secrets are unavailable, the workflow intentionally fails rather than publishing an unsigned package.

## Required release evidence

Before marking the toolkit operational for a TV model, record a clean-machine installation test and an end-to-end run against an explicitly authorized development TV. Verify discovery, identity/authentication, protocol compatibility, test execution, evidence redaction, report generation, and the production-readonly policy. Do not automatically deploy firmware to production devices.
