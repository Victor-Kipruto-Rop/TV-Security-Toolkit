# Security: current state

Phase 0 repository audit. What the repository actually enforces today, and where it does not.
Audited commit: `93f14ca`. No functionality was modified to produce this document.

## Controls that are present and working

### State-change restriction (strongest area)

`SecurityConstants.ReadOnlyCommands` is an authoritative **allowlist**. `IsStateChanging` returns true
for anything not on it, so a newly added command is refused rather than silently permitted. This is
the fail-closed orientation the specification requires, and it is the correct design.

`TestPolicyEngine` applies it across catalog steps, and `PolicyTests` asserts that the allowlist and
the `MutatingCommands` denylist agree, and that no catalog test depends on a state-changing call
under a read-only profile. The `production-readonly` profile sets `allow_state_changing: false` and
`enforceReadOnlyCommandAllowlist`.

**Gap (Phase 36):** the gate is a **catalog-time** policy decision. Nothing re-checks the allowlist at
the transport boundary, so a `CallAsync` issued outside catalog evaluation is not re-validated. A
defence-in-depth check in `ProtocolClient`/`RpcDeviceAdapter` does not exist.

### Redaction and log hygiene

- `SecurityConstants.RedactedKeys` = `sig`, `key`, `secret`, `password`, `mac`; applied during evidence
  summarisation before anything is written.
- `SecurityConstants.IdentityKeys` masks serial, MAC, model, region, firmware, host and IP in
  operational logs, case-insensitively.
- `EvidenceCollector.Summarize` replaces binary payloads with `<N bytes sha256=...>` rather than
  inlining them.

### Cryptographic primitives

Primitives are competent: HMAC-SHA256, SHA-256, CRC-32 (zlib-compatible), constant-time comparison via
`CryptographicOperations.FixedTimeEquals`, and a canonical-JSON serialiser with ordinal key ordering
so signatures are reproducible.

A repository-wide scan found **no** use of MD5, SHA-1, DES, RC4, `ssl://`, `ServicePointManager`, or
`new Random()`. No `ServerCertificateCustomValidationCallback` or
`RemoteCertificateValidationCallback` overrides exist, so certificate validation is not disabled
anywhere. `RokuEcpAdapter` explicitly sets `AllowAutoRedirect = false`, which matches the documented
behaviour.

### Transport hardening

`TcpTransport` returns 0 on timeout so `ReadExactAsync` can distinguish timeout from closure, and
`ProtocolClient` applies a read deadline, a single-call gate, and header/CRC validation. Broader
`catch (Exception)` sites are almost all exception-filtered (`when (e is ...)`) rather than blanket.

## Critical gaps

### 1. No device authentication (Phase 13)

There is no authentication anywhere in the adapter contract. `IDeviceAdapter` has no authentication
member; `GetIdentityAsync` reads a device ID string, caches it, and writes it into evidence, but it is
never verified against a trust anchor or an expected identity.

Consequence: the toolkit will send assessment traffic to, and accept results from, **any** device that
answers on the expected VID/PID, serial port, or socket. There is no mutual authentication, no
certificate chain handling, no nonce or challenge/response, no revocation checking, and no fail-closed
path, because there is no authentication path at all.

This is the single largest functional gap in the product and it cannot be closed without the vendor
trust model (blocker B3).

### 2. Symmetric-only cryptography used as a stand-in for vendor signatures (Phase 14)

`SignatureVerifier` implements HMAC-SHA256 over canonical JSON. Its own comment states it is a stand-in
for a vendor's asymmetric signature. `FirmwareSignatureChecker.IsValid(package, byte[] key)` takes a
**symmetric** key and verifies the same HMAC.

As written, a "signature" only proves that the party holding the key produced the bytes. Anyone with
the key can both sign and verify. There is no asymmetric verification, no key derivation, no key
rotation, no key management, and no algorithm policy. The same limitation applies to entitlement
verification and to update package signatures.

Because the repository is public, the lab key (`labSigningKeyHex`, in `config/security-policy.json`)
is world-readable. That is acceptable **only** while it signs simulator payloads. The production
profile does not currently refuse to load a lab key, so that separation is not enforced in code.

### 3. Environment model cannot express the simulator/hardware distinction (Phase 5)

`EnvironmentType` has three values. There is no `lab` and no `authorized-hardware` environment, and no
field anywhere that records whether results came from a simulator or a physical device.

The distinction currently exists only in documentation, in the device adapter name, and in the
`README`. **A result produced against the simulator is structurally indistinguishable from one produced
against a real television.** This directly conflicts with the requirement that simulator results must
never be represented as real-device validation, and with the requirement to clearly distinguish
SIMULATOR / LAB / STAGING / PRODUCTION-READONLY / AUTHORIZED-HARDWARE.

This is the highest-priority correctness gap, because every other security statement in the product
depends on being able to say which environment produced a result.

### 4. Result vocabulary is too small (Phase 7)

`TestStatus` = `Pass, Fail, Error, Skipped, Cancelled, Running, Ready`.

Missing: `Blocked`, `NotApplicable`, `NotVerified`, `Inconclusive`, `RequiresIntervention`. `Running`
and `Ready` are lifecycle states mixed into a result enum. A test that cannot run because the device
lacks a capability currently has no correct status to return, and `Error` is the only option.

### 5. Undocumented warning suppression (Phase 1)

`NU1701` is suppressed in `Device`, `UnitTests` and `IntegrationTests` with no reason, no affected-code
note, and no issue reference.

## Placeholder scan (Phase 10, Phase 71)

Repository-wide across all tracked source:

| Pattern | Count | Assessment |
| --- | --- | --- |
| `TODO` | 0 | clean |
| `FIXME` | 0 | clean |
| `NotImplementedException` | 0 | clean |
| `XXX` / `HACK` | 0 | clean |
| `placeholder` | 5 | all legitimate: XAML `PlaceholderBrush` resource names, and prose in XML doc comments |
| `NotSupportedException` | 0 | the one match is a caught `PlatformNotSupportedException` in discovery, a legitimate filter |

The only functional skeleton is `toolkit/device/generic.py`, which raises on construction by design,
is documented in `README` as not implemented, and fails closed rather than returning fabricated
results. It is still a selectable adapter name, which is the one thing the specification forbids.

## Secret scanning (Phase 40)

No credentials, API keys, passwords, private keys, or bearer tokens were found in tracked source,
scripts, or configuration. The only key material in the repository is the deliberate, labelled
simulator key.

The release workflow reads `SIGNING_CERTIFICATE_BASE64` and `SIGNING_CERTIFICATE_PASSWORD` from
GitHub secrets, writes the certificate to a file, and explicitly clears both environment variables
afterwards. Workflow permissions are least-privilege (`contents: read`, plus `security-events: write`
only for the CodeQL workflow).

**Gap:** there is no automated secret scanning in CI, and no scan of Git history.

## Missing security capabilities

Certificate/revocation infrastructure, plugin sandboxing, operator roles and identity, an append-only
audit trail, and a health-check surface are all absent. There is no fuzzing, no fault injection, and
no security regression fixture set (Phase 58) that is executed on every release.

