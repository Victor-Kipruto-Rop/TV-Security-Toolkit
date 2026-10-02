# Implementation Audit

**Audit date:** 2026-10-01  
**Scope:** Repository source, project/build files, application UI, test and catalog data, configuration, documentation, dependencies, and existing automation/package surfaces. This is an implementation-status audit, not a penetration test or proof of compatibility with a production TV.

## Executive summary

The repository is a dual-implementation toolkit: a .NET 10/WPF application and a Python CLI/simulator. It contains substantial implementations of the UI, test execution, simulated device, policy evaluation, protocol framing, transports, validation, evidence, and reporting. A project or class existing does not, by itself, establish the end-to-end feature.

Real-device readiness is **not established**. The repository documents the hardware wire protocol as an assumption, and no authorized development TV or clean-machine execution was available for this audit. Production vendor authentication/signature verification, firmware-side code, and release signing/package validation are not evidenced as complete.

## Current implementation status

| Area | Status | Evidence and limits |
|---|---|---|
| Windows application | **Implemented; Release build verified** | WPF application with MVVM views/view models for device, test selection/execution, results, findings, reports, diagnostics, settings, and about. Release build succeeds on .NET 10.0.401 with no warnings/errors. Report-format defaults now follow `appsettings.json`. |
| Python CLI | **Implemented; simulator path verified** | `launcher.py` and `toolkit/` provide commands, configuration, device adapters, a test runner, policy evaluation, payload builders, and reporting. The checked-in default CLI configuration was previously mismatched; the CLI defaults were corrected and its Python regression tests passed. A simulator run completed 55 tests with 55 passed. This does not validate hardware behavior. |
| Test engine | **Implemented for current catalog model** | .NET and Python implementations load definitions, apply policy, execute steps, evaluate expected results, and aggregate results/evidence. Existing tests cover selected engine and simulator behavior. Advanced dependency scheduling, safe parallel execution, full timeout/precondition/isolation semantics, and cross-engine catalog parity are not demonstrated as complete. |
| Device discovery and transports | **Partially implemented; hardware tests fail closed** | .NET contains USB, serial, TCP, and HTTP communication/discovery code; Python has USB and loopback support. The WPF Device page can perform read-only connected-device discovery through Android/Google TV `adb`, Samsung Tizen `sdb`, and LG webOS `ares-device` tools, plus an explicit-host Roku ECP device-info probe. Tool output and reachable devices are not authenticated identity or target compatibility. The WPF test path continues to block execution on non-simulator transports pending vendor authentication and a verified target protocol. |
| Diagnostic protocol | **Partially implemented; target compatibility unverified** | The .NET TVSP frame encoder/decoder, request/response handling, and protocol validation exist. The README describes the wire protocol as an assumption. Protocol compatibility, version negotiation, and authorization against a real diagnostic agent have not been established. |
| Device authentication | **Incomplete for production identity** | A simulator and lab signing flow exist. The .NET `SignatureVerifier` uses the lab HMAC scheme, which is not evidence of vendor asymmetric-signature or certificate-chain authentication for production devices. |
| PAYG, firmware, and update checks | **Implemented in the simulator/test harness** | Test definitions and simulator logic exercise entitlement validity, expiration, device binding, replay, firmware integrity/signature/compatibility, and update/recovery behavior. They are not verified against actual TV firmware or production entitlements. Tests must continue to use dedicated lab fixtures/devices. |
| Network and local security checks | **Partially implemented; primarily simulated** | Catalog entries and simulator probes cover TLS, authentication, authorization, storage, logging, and debug checks. Real device/backend measurement and independent validation are not established. |
| Evidence and reports | **Implemented with limits** | Evidence is attached to results, automatically archived as per-session JSON under `evidence/<session-id>/`, and included in the automatically generated HTML/JSON/PDF reports under `reports/<session-id>/`. Findings include test/device/firmware context and recommendations. The PDF implementation documents an ASCII-only limitation; data redaction/output behavior still needs validation against real-device sources. |
| Embedded firmware | **Not present as an implementation** | `firmware/` contains manifests/schemas and fixtures, and `runtime/native/` has documentation. No TV firmware, bootloader, diagnostic-agent source, or C/C++ build target was found. |
| CI, packaging, and signing | **Partially implemented** | Windows build/test/dependency/CodeQL workflows, a self-contained USB package script, structured Serilog JSON logs, and a tag release workflow are present. Package staging and app initialization were exercised locally. Tagged release signing requires protected certificate secrets; no signed release or clean-machine verification was available. |
| Documentation | **Partially complete** | User, technician, architecture, protocol, security, troubleshooting, test-catalog, driver, and release-process documentation exist. The technician guide distinguishes PASS/FAIL/SKIPPED/ERROR and warns that simulator results are not hardware evidence. |

## Verified gaps and broken behavior

1. **Environment and profile configuration are separate sources.** The .NET application loads `config/environments/`; the Python CLI loads `profiles/`. Their profile/environment counts and schemas differ, so policy parity cannot be presumed.
2. **TLS minimum configuration is wired only to the Diagnostics probe.** The Diagnostics page checks an explicitly entered host/port against the configured minimum TLS version. This is a read-only endpoint probe, not integration with a discovered TV or production device workflow; authorized real-endpoint coverage remains to be validated.
3. **The Python generic hardware adapter is a placeholder.** Selecting `--device generic` reaches [`toolkit/device/generic.py`](../toolkit/device/generic.py), whose connection behavior is not implemented and raises `NotImplementedError`.
4. **Production authentication is not demonstrated.** The lab HMAC key is explicitly for the simulator. A production vendor trust root, device certificate-validation configuration, and verified challenge/response exchange are not established in the repository.
5. **The hardware wire protocol remains an assumption.** No authorized development TV or vendor diagnostic-agent specification was available, so frame conformance, version negotiation, and authorization against a real agent are unverified.

### Resolved since the initial audit

- **Catalog parity (previously gap 1).** The .NET catalog now contains 55 definitions, matching the 55 Python test files. Three `recovery` and four `usb` definitions were ported into `test-catalog/`, each with a matching `CatalogTest` class, and the new categories were added to every shipped environment. Regression tests assert the inventory, unique ids, protocol-command validity, and that every definition has an implementation, so the inventories cannot silently drift apart again.
- **Read-only environments previously failed open.** The state-change guard was a blocklist, so any command missing from it was permitted against a production device. `SecurityConstants.ReadOnlyCommands` is now an authoritative allowlist; an unclassified or newly introduced command is refused. The legacy blocklist remains as defence in depth. Regression coverage includes fail-closed behaviour, ordinal matching, catalog drift, and the property that `production-readonly` cannot run any state-changing catalog test.
- **Operational logs recorded device identity.** An `IdentityRedactingSink` masks device identifiers, firmware versions, and host values before any log event reaches disk.
- **`stopOnCriticalFailure` was configured but ignored by the .NET engine.** It is now honored, and remaining tests are recorded as `Cancelled` with a reason so the report still accounts for every selected test.
- **Engine scheduling lacked dependency and isolation support.** `TestDefinition` now carries `DependsOn` and `Isolated`. The scheduler performs a deterministic topological pass (Kahn's algorithm) before the existing severity ordering, and reports unknown dependencies, dependencies outside the current selection, and cycles rather than reordering silently. A cycle stops the run; other dependency problems are recorded without failing otherwise-runnable tests. `IDeviceAdapter` gained `ResetAsync`, which isolated tests call before and after running so a state-changing test cannot leak into later results. Adapters that cannot guarantee a baseline throw rather than silently sharing state, and the simulator implements a full reset.

### Audit follow-up completed

After the initial audit, the Python CLI was corrected to return exit code 2 and an explicit error when a requested test/category filter matches nothing. The runner now honors the checked-in `stopOnCriticalFailure` setting while retaining support for the snake-case key. Regression coverage verifies both behaviors.

The .NET and Python policy engines now fail closed when the state-change permission is omitted: mutating tests are blocked unless the active policy explicitly opts in. Explicit development configuration remains opt-in. Regression tests cover the omitted-setting default.

The portable package now writes generated reports and per-session evidence archives under the package root, and the Reports view can open the evidence directory. The package output path is verified during staging. This proves package-side persistence on the current host, not USB media durability or real-device evidence quality.

The Device page now probes the fixed, read-only device-list commands for `adb`, `sdb`, and `ares-device`, with bounded execution time and explicit missing-tool/failure results. It also offers a single-host, read-only Roku ECP device-info probe that validates the host, avoids redirects, bounds XML parsing, and excludes device identifiers from output and operational logs. Device-discovery driver and permission failures are visible. No feature performs range scanning or changes the hardware test guard. Output/response interpretation has regression tests; these discovery surfaces are not vendor authentication or proof of compatibility.

Assessment cancellation now returns a report with explicit `Cancelled` results for interrupted and not-started tests, retaining completed step evidence; incomplete sessions receive a non-success exit code. The test view reports the active test as well as completed results. Configured HTML, JSON, and PDF formats are generated automatically after runs. Findings carry IDs, expected/actual behavior, device/firmware context, timestamp, evidence references, and a remediation recommendation; the Findings view exposes those details, and the dashboard summarizes cancelled tests and finding severities. The app writes structured JSON operational logs to `logs/`, and connection setup disposes failed candidate sessions. A hardware-execution guard now blocks non-simulator tests pending verified vendor authentication and target compatibility. The latest Release build and USB package startup smoke test succeeded. The most recent full Debug test pass predates the Roku additions; the latest test invocation was blocked by Windows Application Control.

The WPF presentation layer was refreshed without changing view-model bindings or safety behavior. The shell now has branded navigation, clearer content spacing, persistent status treatment, selected/hover navigation states, and a responsive content surface. Shared resources provide a consistent palette, semantic status colors, accessible button states, improved form/list/grid styling, and reusable card treatments. The dashboard now presents target readiness, run-summary metrics, and a safety-first workflow panel.

The build foundation now targets .NET 10.0.401 with analyzers and deterministic builds enabled. A simulator test exposed and fixed a JSON integer conversion bug. Legacy TLS checks avoid obsolete enum warnings; the checker enforces the configured minimum version in the Diagnostics TLS probe and propagates caller cancellation. The WPF reports view initializes its options from configured formats. Windows build/test/security workflows, release packaging/signing workflow, USB package script, and release-process documentation were added.

## Build, tests, dependencies, and automation

- The shared build properties target **.NET 10**; `global.json` selects SDK 10.0.401 with latest-patch roll-forward.
- Nullable references and implicit usings are enabled; package versions are centrally declared in [`Directory.Packages.props`](../Directory.Packages.props). The device and unit-test projects suppress `NU1701`; that compatibility warning should be reviewed rather than assumed harmless.
- The repository includes xUnit unit/integration projects and a Python `unittest` suite. The unit suite contains 90 tests and the integration suite 12. The most recent Release run on the authoring host reported **87 passing and 3 failing of 90**; the blocking Application Control policy on that host makes results vary between runs, so a stable figure requires a host without that policy, or CI. The 12 integration tests have not been re-executed since the device-reset isolation work. The Release solution build succeeds with 0 warnings and 0 errors. See [GitHub Actions status](#github-actions-status) for the CI position.
- The WPF design system is verified statically. `XamlResourceTests` parses every view and resource dictionary and asserts that each `StaticResource`/`BasedOn` reference resolves, that each keyed style is applied to a matching element type, that the required brushes exist, and that no view hardcodes a colour. Those checks are part of the blocked suite above; the same assertions were also executed directly against the source files and passed. Running the application confirmed the dashboard, test-execution, and reports pages render, and exposed a `TextBox` style applied to a `ListBox` that compile-time checks had not caught.
- `dotnet list package --vulnerable --include-transitive` reported no vulnerable NuGet packages from the current sources.

- The expanded adapter-enabled USB package was staged and copied to the user-confirmed `D:\TV-Security-Toolkit` folder; all 599 package files matched their staged SHA-256 hashes. The redesigned Release package was subsequently staged at `artifacts/TV-Security-Toolkit-USB-Redesigned` and is ready to copy to removable media. The packaged application previously launched from D: and remained responsive while its startup log confirmed toolkit initialization. The package includes the launcher, package-relative output path, runtime/config/catalog data, Android/Tizen/webOS tool discovery, and Roku ECP probe UI. One pre-existing destination file was preserved. A clean-machine launch, code-signing, USB-media durability, and real-TV workflow remain unverified.
- `cmake` and the Visual C++ compiler were unavailable; no native firmware source was identified.

## GitHub Actions status

The repository is published at `github.com/Victor-Kipruto-Rop/TV-Security-Toolkit` and the workflows have now been triggered. **Every job fails before a runner is assigned**, so there is no CI build or test result in either direction:

| Workflow | Job | Duration | Steps executed | Runner |
| --- | --- | --- | --- | --- |
| Test | `python` | 2 s | 0 | none (`runner_id: 0`) |
| Test | `dotnet` | 1 s | 0 | none |
| Security | `dependency-audit` | 4 s | 0 | none |
| Security | `codeql` | 4 s | 0 | none |
| Build | `build` | 5 s | 0 | none |

A 1–5 second failure with an empty `steps` array and no assigned runner is the signature of a job rejected during scheduling, not a build or test failure. This points to an account or organisation setting (Actions enablement, spending limit, or pending billing) rather than a defect in this repository, and it cannot be resolved from the working tree. Enabling Actions for the account and re-running is the next action, and it is a human step.

One genuine repository defect was found and fixed while reproducing the `python` job locally. A fresh clone has no `logs/tests/` directory, so `toolkit/cli.py` raised `FileNotFoundError` from `logging.basicConfig` before any test could run. The directory is now created on demand, with a stderr fallback for read-only removable media, and the `.gitkeep` placeholders for `logs/`, `reports/`, and `evidence/` are tracked. In a fresh clone the Python suite reports `Ran 6 tests ... OK`, and the .NET solution builds with 0 warnings and 0 errors.

On the authoring host, a Release `dotnet test` reported **87 passing and 3 failing of 90**. Two were Application Control blocks (`0x800711C7`) of `TVSecurityToolkit.Tests.PayG.dll`; which tests are blocked varies between runs as reputation is evaluated. The third, `TcpTransportTests.Call_fails_promptly_when_peer_closes_connection`, is a **pre-existing flaky test that predates this work** (unchanged since the initial commit): it times out under full-suite load and passes in isolation in under one second. That is a timing sensitivity in the test harness, not a confirmed transport defect, and it should be stabilised before being relied on as a regression guard. The 12 integration tests have still not been re-executed since `ResetAsync` was introduced.

## Security and release considerations

- The repository states that payloads are lab-signed and the configured key is simulator-only. Keep it out of any production trust configuration; do not treat possession of the lab key as device authorization.
- **The lab key is now public.** `config/security-policy.json` and `config/security.json` are tracked and the repository is public, so `labSigningKeyHex` is world-readable. This is acceptable only while it signs simulator payloads exclusively, and it must never be reused for evidence attestation, package signing, or device authentication. The production profile does not currently refuse to load a lab key, so this separation is a code-level control that is **not** yet enforced; treat "lab key present" as a hard stop for any production run.
- Never use simulator PASS results as evidence that a production TV rejects the same condition.
- Do not run state-changing checks against production devices. The policy default is now read-only when the setting is absent; confirm the selected profile and policy behavior at the engine boundary before any authorized hardware assessment.
- Avoid collecting customer or payment data. Validate redaction against actual report/evidence contents before release.
- No independent security review, code signing, clean-machine test, or authorized-TV end-to-end assessment was completed as part of this implementation-status audit.

## Recommended implementation order

1. **Obtain a publicly trusted code-signing certificate** and configure the protected signing secrets. This is the gating item: unsigned packages do not start on machines with Smart App Control enabled, so it blocks both distribution and clean-machine testing. The packaging script supports `-SigningCertificatePath -RequireSigned` and was exercised end to end against a locally generated certificate.
2. **Enable GitHub Actions for the account and re-run the workflows.** The repository is now published and the workflows execute, but all five jobs are rejected during scheduling with no runner assigned (1–5 s, zero steps). This is an account or organisation setting rather than a repository defect, and it must be fixed before CI can be used as evidence. CI also provides a host without the authoring machine's Application Control policy, which is currently preventing part of the unit and integration suites from running.
3. **Execute the full suites on a host without Application Control** (the CI host once Actions is enabled). The latest authoring-host run was 87 passing and 3 failing of 90, and 2 of the 3 failures are policy blocks rather than logic failures; the third is a pre-existing flaky TCP test. Re-run all 90 unit and 12 integration tests there and record the result.
4. Define the supported TV diagnostic protocol and production device-authentication/trust model with the device/platform owner. Validate them on an explicitly authorized development TV before claiming hardware coverage.
5. Decide whether to complete or remove the Python `generic` adapter placeholder, and document the Python CLI as simulator-only until then.
6. Complete authorized-device end-to-end verification and finalize production-readiness evidence. Do not deploy firmware automatically.

## Audit limitations

The .NET 10 SDK became available during implementation follow-up; .NET build and test results are recorded above. CMake and Visual C++ were unavailable, and no firmware source exists in the repository. No production TV, diagnostic agent, vendor trust material, clean Windows machine, or backend was available. No firmware binary or tagged package fixture was executed. This document reports repository evidence and observed validation only; it is not certification or a security-review report.
