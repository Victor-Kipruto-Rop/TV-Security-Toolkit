# Architecture: current state

Phase 0 repository audit. This records what the repository **is today**, verified by inspection. It is
not a target-state design. No functionality was modified to produce this document.

Repository: `github.com/Victor-Kipruto-Rop/TV-Security-Toolkit`
Audited commit: `93f14ca`

## Solution and projects

Thirteen projects, all .NET 10, SDK pinned by `global.json` to `10.0.401` with `latestPatch` roll-forward.

| Project | Role | Files |
| --- | --- | --- |
| `TVSecurityToolkit.Core` | Contracts, models, enums, constants, exceptions. No infrastructure dependency. | 31 |
| `TVSecurityToolkit.Engine` | Test registry, scheduling, execution, evidence collection, validation | 21 |
| `TVSecurityToolkit.Device` | Transports (USB/serial/TCP/HTTP/loopback), protocol, discovery, platform adapters, simulator | 29 |
| `TVSecurityToolkit.Security` | Policy engines, cryptography, firmware and network checkers | 16 |
| `TVSecurityToolkit.Reporting` | JSON/HTML/PDF reports, evidence archive, integrity manifest | 11 |
| `TVSecurityToolkit.App` | WPF desktop application (MVVM, views, resources) | 56 |
| `TVSecurityToolkit.Tests.PayG` | 16 PAYG security tests | 17 |
| `TVSecurityToolkit.Tests.Firmware` | Firmware security tests | 11 |
| `TVSecurityToolkit.Tests.Update` | Update/rollback/recovery tests | 16 |
| `TVSecurityToolkit.Tests.Network` | Network security tests | 9 |
| `TVSecurityToolkit.Tests.Local` | Local/device security tests | 7 |
| `TVSecurityToolkit.UnitTests` | xUnit unit suite (104 tests) | 13 |
| `TVSecurityToolkit.IntegrationTests` | xUnit integration suite (12 tests) | 7 |

## Dependency direction

The layering is sound and flows one way:

```
App  ->  Engine  ->  Core
          |
          v
      Device, Security, Reporting  ->  Core
```

`Core` contains no reference to any adapter, transport, or UI type. Engine depends on `Core`; `Device`,
`Security` and `Reporting` depend on `Core`; the WPF `App` sits on top. There are no circular project
references and no test-catalog project references another.

**Gap (Phase 4):** this is enforced only by convention. There is no architecture test asserting the
edges, so a future `using` can invert a layer without any build failure.

## Test catalog: two physical copies, one semantic set

The 55 test definitions exist **twice in two different shapes**:

- `test-catalog/<category>/<group>.json` - grouped, each test carries an explicit `id`
  (e.g. `payg.entitlement.valid-entitlement`). Read by the .NET engine.
- `tests/<category>/<group>/<name>.test.json` - one file per test, `id` implied by the path. Read by
  the Python CLI.

The content and ID scheme are equivalent and the two currently agree at 55, but:

- `tools/generate_assets.py` writes **only** the Python copy.
- The .NET `test-catalog/` tree is therefore maintained by hand.

**Gap (Phase 8, Phase 9):** there is no catalog checksum, no schema validation at load, no migration
path, and no parity test that compares the two trees. Nothing would catch a test being added to one
side and not the other. The parity test that exists today checks .NET catalogue uniqueness only.

## Environments

`EnvironmentType` declares three values: `Development`, `Staging`, `ProductionReadonly`.
Configuration exists for `development`, `staging`, `production-readonly` and an extra `factory` profile.

The required model distinguishes **SIMULATOR / LAB / STAGING / PRODUCTION-READONLY / AUTHORIZED-HARDWARE**.

**Gaps (Phase 5):**

- There is no `lab` environment and no `authorized-hardware` environment, so the toolkit cannot
  express "an authorized physical device under test" as a distinct, enforceable state.
- `development` and `factory` both permit state-changing operations and are not distinguished by any
  property other than the allowed-category list. An operator cannot tell from configuration whether
  the target is a simulator or a real television.

This is the structural reason the simulator/real-device distinction is currently carried by
documentation and by the device adapter name, rather than by an enforced configuration state.

## Versioning

`1.0.0` is declared in three independent places:

- `VERSION` (file)
- `Directory.Build.props` -> `<Version>1.0.0</Version>`
- `toolkit/__init__.py` -> `VERSION = "1.0.0"`

They currently agree. **Gap (Phase 44):** there is no single authoritative source and no check that
they agree, so they can drift silently. The CLI, the assembly informational version, the catalog and
the report are all versioned independently.

## Parallel .NET and Python implementation

The two implementations are genuinely separate: .NET is a WPF application with a full engine, and
Python is a CLI over its own simulator. They share the test catalog semantics and the security policy
file, but they do not share code.

- Python has no dependency manifest at all (no `requirements.txt`, `pyproject.toml`, or equivalent).
  It currently runs on the standard library alone, but this is undeclared and unenforced.
- Python has no equivalent of `EvidenceChain`, so an archive written by the Python CLI is not covered
  by the integrity work landed in commit `0244d89`.

**Gaps (Phase 9, Phase 24):** no semantic-equivalence harness, and the integrity guarantees are
asymmetric between the two entry points.

## Device layer

Transports present: USB (LibUsbDotNet), serial (`System.IO.Ports`), TCP, HTTP, and a loopback
transport for protocol testing. Discovery is split across `DeviceDiscovery` (USB/serial enumeration)
and `NetworkDeviceDiscovery`, plus platform adapters for Roku ECP.

`IDeviceAdapter` exposes `ProvisionAsync`, `GetIdentityAsync`, `GetNowAsync`, `CallAsync`, `ResetAsync`.
`RpcDeviceAdapter` expresses identity, clock, and provisioning as protocol commands so the simulator
and hardware share one shape. Adapters that cannot guarantee a clean baseline throw from `ResetAsync`
rather than silently sharing device state between tests.

**Gaps (Phase 11, Phase 12, Phase 13):**

- There is no `IDeviceAuthentication`, `IDeviceCapabilities`, or `IDeviceProtocol` seam. Identity is
  *read* but never *verified*.
- There is no capability negotiation, so a test needing an unavailable capability cannot return
  `NOT_APPLICABLE`; the status does not exist.
- `toolkit/device/generic.py` is a deliberate skeleton that raises on construction. It is documented
  and fails closed, but it is a selectable adapter name that cannot work.

## Reporting and evidence

`JsonReportGenerator`, `HtmlReportGenerator`, `PdfReportGenerator` and `EvidenceArchiveWriter`. The
archive writer writes atomically and now seals a SHA-256 evidence chain with a sibling manifest
(commit `0244d89`).

**Gaps (Phase 31, Phase 33):** no CSV, SARIF or JUnit output; no report integrity or provenance
header; the evidence manifest is unsigned.

## Dead code and duplication

No dead code was positively identified. The three declared NuGet packages (`LibUsbDotNet`,
`System.IO.Ports`, `Serilog` + `Serilog.Sinks.File`) are each referenced by source, so there are no
orphaned dependencies.

The only structural duplication found is the catalog described above.

## Build state

`dotnet build --configuration Release` succeeds with **0 warnings and 0 errors**, verified both in the
working tree and in a clean clone of the published repository.

`NU1701` (package compatibility) is suppressed in three project files
(`Device`, `UnitTests`, `IntegrationTests`) with no comment, no documented reason, and no issue
reference. This does not currently produce a warning, but it violates the requirement that every
suppression carry a reason and a justification.

