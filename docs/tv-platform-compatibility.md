# TV platform compatibility matrix

## Scope and guarantee

This registry is a compatibility-planning and discovery inventory, not a certification list. A brand appearing here does not mean its TVs can execute security tests. Model, region, OS version, firmware, enabled developer mode, cable/network path, vendor permissions, and the available diagnostic agent can all change what is possible.

A simulator pass is evidence about the simulator only. A successful discovery command is evidence that a tool returned a candidate only.

## Current platform inventory

| Brand / platform | Discovery in this repository | Current claim | Real-device security tests |
|---|---|---|---|
| Android TV / Google TV | Android Debug Bridge (ADB) device listing | Discovery only; identity and diagnostic protocol are unverified | Blocked |
| Samsung Tizen | Samsung Smart Development Bridge (SDB) listing | Discovery only; identity and diagnostic protocol are unverified | Blocked |
| LG webOS | LG webOS CLI (ares-device) listing | Discovery only; output format and identity are unverified | Blocked |
| Roku TV | Optional read-only ECP device-info request to one explicitly entered host | Reported model is unverified; request does not authorize testing | Blocked |
| Hisense VIDAA | No adapter implemented | Unsupported pending model-specific vendor documentation and authorized test access | Blocked |
| TCL | Depends on the exact model and OS; no brand-wide adapter | Identify the OS/model first; a TCL label is not a protocol | Blocked |
| Sony BRAVIA | Depends on model/year and OS; Android/Google TV tools may discover some models | OS discovery does not establish BRAVIA compatibility | Blocked |
| Other brands | No generic adapter assumed | Unsupported until assessed | Blocked |

## Safety gates

The compatibility registry fails closed for all shipped profiles. Real-device security tests require all of the following to be established for the exact model/firmware and adapter:

1. Written authorization and a defined test scope.
2. Vendor-documented protocol and supported commands, with version and framing verified against an authorized device.
3. Device identity/authentication that is cryptographically or vendor-appropriately verified before commands are sent.
4. An explicit capability negotiation result; unsupported checks must be reported as NOT_APPLICABLE or UNSUPPORTED, never as PASS.
5. A default read-only profile, with state-changing checks disabled unless a dedicated lab policy explicitly permits them.
6. Hardware-backed positive and negative fixtures, redacted evidence, timeouts, and tests for malformed responses, unexpected commands, and connection loss.
7. Model/OS/firmware-specific validation and a release note that states the exact tested combinations.

Do not invent vendor protocols or credentials. Do not treat a device ID string, VID/PID, IP address, ADB/SDB listing, Roku model response, or simulator response as authenticated identity.

## Adding a supported model

For each model family, record manufacturer, model numbers, region, OS version, firmware range, vendor documentation version, discovery mechanism, protocol version, authentication mechanism, supported capabilities, test fixture, and test evidence. Add an adapter only after the protocol and authorization model are reviewed. Add regression tests proving unauthorized, unknown, unsupported, and malformed cases fail closed.

## What this change does not claim

This change does not add new vendor transports, certify any hardware, enable security tests against real TVs, or prove production readiness. It makes the supported-target inventory explicit and testable while keeping unverified hardware gated.


## USB connection discovery

The Python CLI now supports `python launcher.py devices` to list attached USB descriptors and `python launcher.py devices --watch --interval 1` to poll for device additions/removals. This watcher is host-side discovery only; it does not identify a TV's operating system, authenticate the device, choose a vendor protocol, or run tests. Polling is not guaranteed to observe very short-lived connections.

Portable package creation and verified copying to a removable drive are documented in [USB deployment](usb-deployment.md). This Windows x64 package is a host application, not an application that executes on a TV.

## Evidence record required before claiming support

A model-specific support claim must be backed by a reviewable record with these fields:

- Manufacturer, exact model identifier, region, OS version, and firmware version.
- Vendor documentation URL/version and approved diagnostic/developer-access process.
- Transport and protocol framing version.
- Authentication/authorization mechanism and how device identity is validated.
- Discovery, capability-negotiation, read-only, timeout, disconnect, and malformed-response test results.
- Hardware fixture and test date; redacted logs/report reference.
- Explicit supported test categories, unsupported categories, and known limitations.
- Reviewer and toolkit release version.

Until this evidence exists, the platform remains `Discovery only`, `Model/OS identification required`, or `Unsupported` as listed above. Never infer compatibility solely from manufacturer name, VID/PID, a device listing, or a simulator result.
