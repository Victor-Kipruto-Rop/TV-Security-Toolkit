# Testing: current state

Phase 0 repository audit. Audited commit `93f14ca`. No functionality was modified.

## Inventory

| Suite | Framework | Count | Last observed result |
| --- | --- | --- | --- |
| `TVSecurityToolkit.UnitTests` | xUnit | 104 | 104 passed, 0 failed |
| `TVSecurityToolkit.IntegrationTests` | xUnit | 12 | 12 passed, 0 failed |
| `tests/python` | `unittest` | 10 | 10 passed, 0 failed |

The green 104/104 + 12/12 result was recorded on commit `0244d89`. Test-catalog parity is 55
definitions in the .NET engine.

## What is well covered

- **Safety policy.** `PolicyTests` asserts the read-only allowlist and the mutating-command denylist
  agree, that an unknown command is refused, and that no catalog test needs a state-changing call in a
  read-only profile. This is the behaviour that must never regress, and it is directly tested.
- **Catalog integrity.** 55/55 parity, plus a uniqueness test across catalog files.
- **Evidence integrity.** 14 tests covering seal determinism, acceptance of an untouched chain, and
  rejection of edited values, permuted sequence numbers, in-place swaps, truncation, unsealed records,
  mismatched manifest heads, archive tampering, and a deleted manifest.
- **Configuration parity.** 4 xUnit and 4 Python tests asserting a single security config file, that
  the lab key appears in exactly one place, and that both entry points resolve the same key.
- **Engine behaviour.** Dependency-aware scheduling, cycle detection, `stopOnCriticalFailure`, and
  device reset isolation between tests.
- **Test classes.** 16 PAYG, 16 update/recovery, 11 firmware, 9 network, 7 local.

## Blocking problem: the local host cannot give stable evidence

Smart App Control on the authoring host blocks unsigned managed assemblies with
`0x800711C7` (`FileLoadException`). This is intermittent and **which assemblies are blocked changes
between runs** as reputation is evaluated.

Observed across runs on the same commit:

- `TVSecurityToolkit.Tests.PayG.dll` blocked
- `TVSecurityToolkit.Engine.dll` blocked
- `TVSecurityToolkit.Security.dll` blocked
- at worst, the test host failing to load at all ("No test is available")

Rebuilding an assembly resets its reputation, so a freshly built DLL is more likely to be blocked.

**Critical detail for interpreting results:** every failure observed was an Application Control
`FileLoadException`. There were **zero assertion failures** attributable to code defects. A run showing
47 of 108 failing with 42 SAC blocks is an environment failure, not a regression.

This is the single strongest argument for clearing blocker B2 (GitHub Actions): CI would provide a
host without this policy and turn the suite into real release evidence.

## Known flaky test

`TcpTransportTests.Call_fails_promptly_when_peer_closes_connection` times out under full-suite load
(15 s) but passes in isolation in under one second. It is unchanged since the initial commit, so it
predates all recent work. It is a timing sensitivity in the test harness, not a confirmed transport
defect. It should be stabilised before being relied on as a regression guard.

## Gaps against the specification

| Required | Present | Notes |
| --- | --- | --- |
| Result statuses (Phase 7) | No | `TestStatus` lacks `Blocked`, `NotApplicable`, `NotVerified`, `Inconclusive`, `RequiresIntervention` |
| Test definition metadata (Phase 8) | Partial | `id`, `title`, `severity`, `steps` exist; no `version`, `risk`, `dependsOn`, `requiredCapabilities`, `requiredEnvironment`, `timeout`, `isolation`, `stateChanging`, `evidenceRequirements` |
| Catalog schema validation (Phase 8) | No | `TestValidator` enforces required fields at run time; no schema file, no checksum, no signing, no migration |
| .NET/Python parity tests (Phase 9) | Partial | counts match at 55, but no test compares the two trees semantically |
| Fuzz testing (Phase 22) | No | nothing |
| Fault injection (Phase 23) | Partial | simulator `flaws` parameter injects named faults deterministically; there is no scenario/seed/injection-point model |
| Performance testing (Phase 49) | No | no benchmark, no load test, no stated target |
| Security regression fixtures (Phase 58) | Partial | fixtures exist under `tests/fixtures`; no release-gated fixture suite |
| Coverage reporting (Phase 2) | No | no coverage collection in any workflow |

## Determinism

Good: simulator faults are injected by name, evidence timestamps come from a single clock, canonical
JSON gives reproducible signatures, and the evidence chain is deterministic for identical input
(covered by a test).

Poor: there is no global or per-test timeout budget, no retry classification, and no concurrency
control. The `EvidenceCollector` stamps capture time at record time, so two runs of the same
scenario produce different chain hashes by design; a test covers determinism only with an injected
fixed timestamp.
