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
