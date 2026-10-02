# Production-readiness program

This document organises the remaining work into the thirty programme areas. Every area states what the
repository **actually shows today**, the gap, and whether the next action is unblocked or waiting on a
human. Nothing here is a claim of production readiness.

Status values: **DONE** (evidence recorded), **PARTIAL** (works, known gap), **GAP** (missing), **BLOCKED** (waiting on a person or a purchase).

## Programme blockers

Three things gate most of the remaining areas. Until they are cleared, no distribution or hardware claim is supportable.

| # | Blocker | Blocks | Owner |
| --- | --- | --- | --- |
| B1 | No publicly trusted code-signing certificate | Packaging, release, clean-machine install, USB distribution | Human: obtain certificate, set `SIGNING_CERTIFICATE_BASE64` / `SIGNING_CERTIFICATE_PASSWORD` |
| B2 | GitHub Actions jobs rejected during scheduling (1-5 s, zero steps, no runner) | Build, test, supply-chain, compliance evidence | Human: account/org Actions enablement, spending limit, or billing |
| B3 | No authorized development TV and no vendor protocol specification | Real-device communication, device authentication, hardware compatibility, firmware, update, PAYG validation | Human: arrange authorized device, obtain protocol spec |

B2 is the cheapest to clear and immediately restores CI evidence for this document.

## 1. Build and CI reliability - BLOCKED

- Release solution build succeeds with 0 warnings and 0 errors (verified in a clean clone).
- Four workflows exist (`build`, `test`, `security`, `release`) and are well formed; all five jobs fail at scheduling with no runner. See [GitHub Actions status](implementation-audit.md#github-actions-status).
- **Next:** clear B2, then confirm the `dotnet list package --vulnerable` and CodeQL steps actually pass rather than being assumed.

## 2. Application architecture - PARTIAL

- Eleven projects with a defensible split: `Core` (contracts/models), `Device` (transports), `Engine` (execution), `Security` (policy/crypto), `Reporting`, `App` (WPF), plus six test-catalog projects. Dependencies flow one way; `Core` holds no dependency on adapters.
- **Gap:** no architecture tests, so a future `using` can invert a layer silently.
- **Next:** add a test asserting `Core` does not reference `Device`/`App`, and that test-catalog projects do not reference each other.

## 3. Test-engine reliability - PARTIAL

- Dependency-aware scheduling with cycle detection, `stopOnCriticalFailure`, and per-test `ResetAsync` isolation are implemented. Adapters that cannot guarantee a baseline throw rather than silently sharing state, which is the correct fail-closed behaviour.
- Python suite: `Ran 6 tests ... OK` in a fresh clone.
- **Gap:** the 12 integration tests have not been re-executed since `ResetAsync` was introduced. 90 unit tests last reported 87 passing / 3 failing (2 Application Control blocks, 1 pre-existing flaky TCP test).
- **Next:** run both suites on a host without Application Control (the CI host once B2 clears) and stabilise `TcpTransportTests.Call_fails_promptly_when_peer_closes_connection`, which times out under load but passes in isolation in under a second.

## 4. Device abstraction - PARTIAL

- `IDeviceAdapter` is a clean seam: `ProvisionAsync`, `GetIdentityAsync`, `GetNowAsync`, `CallAsync`, `ResetAsync`. `RpcDeviceAdapter` expresses identity, clock, and provisioning as protocol commands so simulator and hardware share one shape.
- **Gap:** the interface models *identity retrieval* but not *identity verification*. Nothing requires a device to prove who it is before commands are sent.
- **Next:** extend the contract once B3 supplies a trust model. Do not invent an authentication scheme now.

## 5. Real-device communication - BLOCKED

- `ProtocolClient` implements framed request/response with a read deadline, a single-call gate, and header/CRC validation. `TcpTransport` returns 0 on timeout so `ReadExactAsync` can distinguish timeout from closure. `UsbTvAdapter` supports VID/PID/endpoint selection.
- **Gap:** the wire protocol is an explicit assumption (see `README` and `docs/protocol-reference.md`). Nothing has been validated against a real TV. `usb-loopback` exercises the transport against the simulator, which proves the code path and not the device.
- **Next:** B3.

## 6. Device authentication - GAP

**This is the largest genuine hole in the product.** There is no authentication anywhere in the adapter contract: no mutual authentication, no device identity verification, no authorisation decision before a command is issued, and no credential storage or rotation. `GetIdentityAsync` reads a device ID string and caches it; the value is recorded in evidence but never checked against anything.

Consequence: the toolkit will send assessment traffic to, and accept results from, any device that answers on the expected VID/PID or socket. For a tool intended to run against production televisions this is not acceptable, and it is why results must not be presented as trustworthy until a trust model exists.

- **Next:** blocked on B3. Requires a documented vendor trust model before any implementation; an invented scheme would be worse than an honest gap.

## 7. PAYG security - PARTIAL

- Sixteen test classes across entitlement validity, malformed input, clock integrity, time validation, device binding and mismatch, expiration boundaries, factory reset, offline grace and reconnection, and nonce/replay protection. Catalog parity is 55/55.
- **Gap:** entitlement signatures are lab HMAC, not vendor asymmetric signatures, so expiry and device-binding assertions are only self-consistent under the lab key.
- **Next:** B3 for vendor signature verification. Strengthen boundary coverage with the existing harness in the meantime.

## 8. Firmware security - PARTIAL

- `FirmwareIntegrityChecker`, `FirmwareSignatureChecker`, `FirmwareCompatibilityChecker` and `FirmwareValidator` are separate concerns with a dedicated test project and catalog (`firmware/compatibility`, `integrity`, `rollback`, `secure-boot`, `version`).
- **Gap:** `FirmwareSignatureChecker.IsValid(package, key)` takes a **symmetric** `byte[]` and verifies HMAC-SHA256. A vendor firmware signature must be asymmetric and anchored in a trust root; as written, anyone holding the key can both sign and verify, and the "signature" proves only that the same party produced the bytes. No firmware binaries or native source exist in the repository (`cmake`/MSVC unavailable).
- **Next:** B3. Replace the symmetric path with vendor public-key verification when the scheme is known.

## 9. Secure update security - PARTIAL

- Sixteen test classes cover downgrade protection, interrupted update, hash mismatch, package integrity, package signature, compatibility, valid update, and recovery (boot/factory/rollback) with a matching catalog.
- **Gap:** same symmetric-signature limitation as firmware. A downgrade test proves the rule is implemented, not that the chain is anchored.
- **Next:** B3, plus an explicit negative test proving recovery cannot be reached without a valid signature.

## 10. Network security testing - PARTIAL

- `Tests.Network` covers TLS, authentication, authorisation, integrity and replay, supported by `TlsValidator`, `CertificateChecker` and `TransportSecurityChecker`.
- The Roku ECP probe is constrained to a single port (8060) with a configured host; it does not scan, follow redirects, or harvest identifiers, and the README states the returned model string is unverified and does not authorise execution.
- **Gap:** no IPv6 handling, no proxy/TLS-interception scenario, and no evidence of testing against a deliberately hostile endpoint.
- **Next:** add explicit negative-path tests once a stable fixture endpoint exists.

## 11. Local/device security - PARTIAL

- `Tests.Local` covers credential storage, debug interface exposure, log access, and storage auditing, with a matching catalog.
- **Gap:** assertions run against the lab simulator; no evidence of a scan against a real device image.
- **Next:** B3.

## 12. Cryptography - PARTIAL

- Competent primitives: HMAC-SHA256, SHA-256, CRC-32 (zlib-compatible), constant-time comparison via `CryptographicOperations.FixedTimeEquals`, and a canonical-JSON serialiser with ordinal key ordering so signatures are reproducible.
- **Gap:** the entire scheme is symmetric. There is no asymmetric verification, no key derivation, no key rotation, and no key management. `SecurityConstants.MinNonceLength` is declared but the replay protection it governs is simulator-side.
- **Next:** B3. Do not describe any of this as vendor-grade cryptography; it is a documented lab stand-in.

## 13. Evidence and forensic integrity - DONE (first pass)

Evidence was previously plain JSON with no integrity metadata, which was a correctness failure for a forensic tool. Now:

- `Evidence` carries a `Seq`, a `RecordedUtc` capture time, and a `ChainHash`.
- `Core.Integrity.EvidenceChain` builds a SHA-256 hash chain over the session's records and verifies it. Sealing happens immediately before the archive is serialised, so the chain covers exactly the records that are written.
- `EvidenceArchiveWriter` writes a sibling `<archive>.manifest.json` holding the chain head, record count, session identity, profile, and toolkit version, and exposes `VerifyAsync` to re-check an archive.
- Verification **fails closed**: a missing, unreadable, or head-less manifest is reported as a failure rather than treated as acceptable.

This is **tamper evidence, not tamper proofing**. The chain is an unkeyed hash, so an attacker who can rewrite an archive can also recompute it; what it does detect is accidental corruption and unauthorised edits, reordering, and truncation after the fact. It is not a signature, not proof of origin, and not legal-grade non-repudiation. The manifest states this in its own `guarantees` field so it cannot be read as a stronger claim than it is.

- **Verified:** 14 unit tests in `EvidenceIntegrityTests` covering seal determinism, acceptance of an untouched chain, and rejection of edited values, permuted sequence numbers, in-place swaps, truncation, unsealed records, mismatched manifest heads, archive tampering, and a deleted manifest.
- **Remaining:** the manifest is unsigned. Sealing it with the code-signing certificate (B1) is what makes it attributable, and remains outstanding.

## 14. Reporting - PARTIAL

- JSON, HTML and PDF generators with external templates; `SeverityEngine` and the finding model give consistent severity. The redaction list is applied during evidence summarisation.
- **Gap:** no report integrity and no provenance header; the PDF path warrants a check that it does not omit fields the HTML path includes.
- **Next:** inherit the integrity work from area 13; verify HTML/PDF field parity.

## 15. Configuration management - PARTIAL

- Configuration is externalised under `config/` with per-environment files (`development`, `staging`, `production-readonly`) and a Python-side profile system; mandatory configuration is validated at startup with actionable errors.
- **Gap:** no schema validation for the config files themselves, and no test asserting a production profile actually resolves to read-only.
- **Next:** add a config schema plus a test that the production profile fails closed.

## 16. Safety controls - PARTIAL (strong)

- The strongest area of the codebase. The read-only `ReadOnlyCommands` allowlist is authoritative and fails closed: an unknown command is refused, so a newly added command cannot silently become permitted. `TestPolicyEngine` enforces it over catalog steps, and `PolicyTests` asserts the allowlist and the mutating-command denylist agree and that no catalog test depends on a state-changing call in a read-only profile.
- **Gap:** the gate is a **catalog-time** policy decision. Nothing re-checks the allowlist at the transport boundary, so a call constructed outside catalog evaluation is not re-validated.
- **Next:** add a defence-in-depth assertion in `ProtocolClient`/`RpcDeviceAdapter` for production profiles, and a test proving a direct non-catalog call is refused.

## 17. Secrets management - DONE (with one accepted exposure)

- No credentials in source, scripts, or config beyond the deliberate lab key. The release workflow reads `SIGNING_CERTIFICATE_BASE64` / `SIGNING_CERTIFICATE_PASSWORD` from GitHub secrets, writes the certificate to a file, and explicitly clears both environment variables afterwards. Workflow permissions are least-privilege (`contents: read`, plus `security-events: write` only for the CodeQL workflow).
- **Accepted exposure:** `labSigningKeyHex` is now world-readable in a public repository. Safe only while it signs simulator payloads. The production profile does **not** currently refuse to load a lab key, so that separation is unenforced.
- **Next:** make the production profile fail closed when a lab key is configured, and rotate the lab key if it is ever reused for anything real.

## 18. Logging/observability - PARTIAL

- Device identity data (serial, MAC, model, region, firmware, host/IP) is masked from operational logs via `SecurityConstants.IdentityKeys`; evidence redaction covers `sig`, `key`, `secret`, `password`, `mac`; the Python CLI configures structured logging to a file and now degrades to stderr rather than crashing.
- **Gap:** no metrics, no traces, no health check, and no correlation/session ID threaded through the .NET logs. An operator cannot answer "which run, which device, what failed" from logs alone.
- **Next:** add a correlation ID and structured .NET logging; log a session/device summary at run start and end.

## 19. Error handling - PARTIAL

- Purpose-specific exceptions (`DeviceException`, `ProtocolException`, `SecurityTestException`, `ValidationException`) keep failures typed; adapter timeouts return a sentinel rather than an exception so the protocol reader can distinguish timeout from closure; error text avoids leaking internals.
- **Gap:** no consistent structured error contract across the Python and .NET paths, and the Python `SystemExit` usage in `make_device` is blunt for a user-facing error.
- **Next:** align user-facing error text and exit codes between both entry points.

## 20. Performance - GAP

- No measurement of any kind. No profiling, no benchmark, no load test, and no stated target for session duration or device-command latency.
- **Next:** set a target, then measure. Do not optimise before a baseline exists.

## 21. Reliability - PARTIAL

- Bounded timeouts, cooperative cancellation, read deadlines, and reset isolation between tests. `TcpTransport` correctly handles peer closure.
- **Gap:** one known flaky test (area 3) and no retry/backoff policy for transient device or transport faults.
- **Next:** fix the flake, then decide whether transient faults warrant bounded retry, and only for idempotent reads.

## 22. Packaging - PARTIAL

- `scripts/package-usb.ps1` produces a self-contained USB package with a launcher, package-relative output, catalog/config data, and platform discovery. Staging is SHA-256 verified: all 599 package files matched their staged hashes.
- `scripts/verify-package-signatures.ps1` verifies an already-produced package independently of the build.
- **Gap:** packaging cannot be proven installable on a clean machine while B1 stands.
- **Next:** B1, then a clean-VM install test with Smart App Control enabled.

## 23. Code signing - PARTIAL (proven, blocked)

- SHA-256 Authenticode with RFC 3161 timestamping, signature verification, and `-RequireSigned` / `-AllowUnsigned` / `-ForceResign` are integrated. Authentic `signtool.exe` is installed. Signing and timestamping were **proven** with a temporary certificate, and a self-signed chain was **correctly rejected** by verification, proving the release gate does not ship an untrusted package. Signing already-valid binaries reduced the workload from 411 files to 15, a 96.4% reduction in timestamp calls.
- **Blocker:** no publicly trusted Code Signing EKU certificate exists locally, so the distributed package remains unusable on Smart App Control systems.
- **Next:** B1.

## 24. Update mechanism - PARTIAL

- Recovery, rollback, interrupted-update and factory-reset paths have dedicated tests and a catalog; the packaging script supports resigning for an updated package.
- **Gap:** the toolkit does not update itself, and there is no defined update channel, rollback-of-the-toolkit policy, or update-signing trust root.
- **Next:** define the channel and trust root with B3's input; keep self-update out of scope until then.

## 25. Documentation - DONE (current)

- Eleven documents including architecture, protocol reference, security model, technician guide, test catalog, troubleshooting, and release process, plus this programme. The implementation audit records the true CI position and the public-lab-key exposure rather than claiming success.
- **Next:** keep the audit honest as evidence changes; do not let it drift back to aspirational claims.

## 26. Supply-chain security - BLOCKED

- CodeQL (C# and Python) and `dotnet list package --vulnerable --include-transitive` are configured; package versions are centrally declared in `Directory.Packages.props`; GitHub Actions are referenced by major version tag.
- **Gap:** none of it has ever executed, because of B2. No lock file, no action pinning to commit SHA, no Siggy, and no SBOM.
- **Next:** B2, then pin actions to commit SHAs and add a lock file.

## 27. Accessibility/UI - GAP

**Zero `AutomationProperties` across all ten views and `MainWindow`.** No screen-reader names, no `AutomationProperties.Name` on interactive elements, no live-region announcements for test progress, and no evidence of contrast or focus-order verification. The design system was modernised and visually verified, but visual polish is not accessibility.

- **Next (unblocked):** add `AutomationProperties.Name` to interactive controls, expose test progress as an accessible status, and verify keyboard traversal. Contained and additive.

## 28. Compliance/auditability - PARTIAL

- Sessions, findings, severity, and an evidence archive give an audit trail of what was run and observed. Redaction is applied before evidence is written.
- **Gap:** no mapping to any control framework, no tamper-evident record (blocked on area 13), and no data-retention or deletion policy for assessment output that contains device identity data.
- **Next:** land area 13, then write a retention policy.

## 29. Hardware compatibility - BLOCKED

- Discovery exists for Android, Tizen, webOS and Roku, with a simulator and a loopback path.
- **Gap:** nothing has run against real hardware; the protocol is assumed; no device matrix, no firmware-version compatibility data, and no record of which models were tested.
- **Next:** B3. Publish a compatibility matrix only from actual runs.

## 30. Release engineering - BLOCKED

- `release.yml` performs signing with secrets, timestamping, and verification, with `-RequireSigned` gating; `docs/release-process.md` documents it; the package signature verification script is standalone.
- **Gap:** no release has been produced, because B1 stands. No release branch strategy, no changelog, no tag/signature verification story, and no rollback procedure for a published package.
- **Next:** B1, then a full dry-run release on a clean VM.

## Recommended order

1. **B2** enable Actions. Cheapest blocker; immediately restores build, test, and supply-chain evidence for this document.
2. **Area 27** accessibility. Unblocked, contained, and currently at zero.
3. **Area 3** stabilise the flaky TCP test, then run the full 90 + 12 suites on the CI host.
4. **Areas 16 and 17** re-check the read-only allowlist at the transport boundary; make the production profile refuse a lab key.
5. **B1** obtain the code-signing certificate, then a clean-VM install test. This also unblocks signing the evidence manifest, which is the remaining half of area 13.
6. **B3** authorized device and protocol specification; the long pole for areas 5-11, 24, 29 and the device-authentication gap in area 6.

Area 13 was the first item taken from this list and its first pass is complete; the residual work is signing the manifest, which needs B1.

## Limitations

This assessment is based on repository contents, executed builds and tests, and GitHub API responses. Hardware, vendor documentation, a clean Windows machine, and independent review were not available. Areas 5-11, 24 and 29 are therefore described as intended behaviour, not verified behaviour. This document is a plan and a gap analysis; it is not a certification, an audit, or a security review.
