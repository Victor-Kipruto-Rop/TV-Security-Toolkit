# TV platform compatibility roadmap

## Scope and safety contract

This toolkit supports defensive testing only on TVs the operator owns or is explicitly authorized to assess. The compatibility command performs local, read-only vendor-tool discovery. It does not scan the network, authenticate a device, prove a model/firmware combination is supported, or authorize a security test.

## Current compatibility surface

| Platform | Discovery path | What discovery proves | Real-device security tests |
|---|---|---|---|
| Android TV / Google TV | Official Android Debug Bridge (ADB), `adb devices -l` | ADB may report a connected candidate and its ADB state | Blocked until a supported diagnostic protocol and device-authentication model are verified |
| Samsung Tizen | Samsung Smart Development Bridge (SDB), `sdb devices` | SDB may report a connected candidate | Blocked until a supported diagnostic protocol and device-authentication model are verified |
| LG webOS | LG webOS CLI, `ares-device --list` | Vendor CLI returned a result; output is not treated as authenticated identity | Blocked until a supported diagnostic protocol and device-authentication model are verified |
| Roku TV | Explicit-host read-only ECP device-info probe in the .NET app | The endpoint may return device-info XML; reported model is unverified | Blocked; ECP device-info is not diagnostic-agent authentication |

## Compatibility result rules

- **Tool missing:** actionable setup result, not a claim the TV is unsupported.
- **Discovered:** candidate reported by a vendor tool, not a verified device identity.
- **Timeout / tool error:** inconclusive result, not a compatibility failure.
- **Not verified:** model, region, OS version, firmware, and diagnostic-protocol support have not been proven.
- **Hardware security tests:** remain disabled unless the device is authenticated and the protocol is explicitly supported by a tested adapter.

The Python command `python launcher.py compatibility` emits JSON with the discovery scope and an explicit `hardware_security_tests_authorized: false` guard. It uses fixed command arguments, does not invoke a shell, limits each probe to a timeout, and does not include raw vendor output in its report.

## Requirements before marking a TV combination supported

A support entry must identify at least:

1. Brand, model number, region where relevant, TV OS/platform version, and firmware/build version.
2. Officially supported connection and diagnostic interfaces, including required user authorization.
3. A vendor-documented protocol or an approved vendor SDK; no guessed wire protocol.
4. Device identity verification and an authorization decision before any diagnostic or mutating command.
5. Capability negotiation so unsupported checks report `NOT_APPLICABLE`, not a false pass.
6. Read-only default execution and an explicit, auditable opt-in for authorized state-changing lab tests.
7. Tests on the named physical device/firmware combination, including disconnects, timeouts, malformed responses, and negative authentication cases.
8. A compatibility report that distinguishes simulated, discovered, authenticated, and hardware-validated evidence.

## Rollout

1. **Discovery layer (implemented):** detect vendor tools for Android/Google TV, Samsung Tizen, and LG webOS; retain explicit-host read-only Roku ECP probing in the .NET app.
2. **Adapter contracts:** add platform identity, capabilities, protocol version, authentication status, and safe unsupported-capability outcomes.
3. **Vendor-approved integrations:** implement only after the appropriate SDK/protocol and authorization model are available.
4. **Hardware validation:** test on named devices/firmware with documented operator authorization.
5. **Support matrix:** publish only the model/firmware combinations with repeatable evidence.

Universal compatibility is not a realistic acceptance criterion. The target is a growing, evidence-backed matrix with safe, explicit unsupported results.
