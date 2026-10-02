TV Security Toolkit 1.0.0
=========================
Defensive verification toolkit for pay-as-you-go (PAYG) TV devices: checks that a
device correctly REJECTS invalid, replayed, expired, tampered or incompatible
entitlements, firmware updates and messages, and that secure-boot, local-storage
and debug hardening are in place. Use only on devices you own or are authorised to test.

Requirements: Python 3.9+, no third-party packages.

Quick start
  python launcher.py gen-payloads                # (re)build payloads/
  python launcher.py list                        # show tests and policy decisions
  python launcher.py run                         # run all tests on the simulated TV
  python launcher.py run --category payg,update --profile staging
  python launcher.py run --flaw accept_replay,skip_signature   # prove detection works
  python launcher.py run --profile production-readonly         # read-only tests only
Reports: reports/html and reports/json. Exit code: 0 ok, 1 findings >= threshold, 2 errors.

Layout
  toolkit/engine      test loading, assertions, runner
  toolkit/security    crypto helpers, PolicyEngine (profiles)
  toolkit/device      adapter contract, simulated TV (sim.py), real-device skeleton (generic.py)
  toolkit/evidence    evidence collector with secret redaction
  toolkit/reporting   HTML/JSON reports (templates/)
  tests/              declarative *.test.json (steps: call, args, expect)
  payloads/           fixtures built by gen-payloads; "@path" in tests loads them
  config/ profiles/   policy, severity threshold, per-environment allowed tests
  tools/generate_assets.py  regenerates config, profiles, schemas and all tests

Test format
  {"title": "...", "severity": "critical", "steps": [
     {"call": "apply_entitlement", "args": {"entitlement": {"$entitlement": {"nonce": "abc12345"}}},
      "expect": {"accepted": true}}]}
  Args: "@valid/x.json" loads a payload; {"$entitlement":{}} / {"$package":{}} build signed
  lab artifacts. Expect uses subset matching plus {"in":[..]}, {"ge":n}, {"le":n}, {"not":x}.

Real devices: implement toolkit/device/generic.py (see docs/protocol-reference.md).
Not included: .exe/.dll binaries and PDF reports from the original tree; their roles are
Python modules. The lab signing key in config/security.json is for the simulator only.

USB devices
  pip install pyusb          (libusb must also be installed; on Windows use a WinUSB/libusb driver, e.g. via Zadig)
  python launcher.py devices                                   # list attached USB devices
  python launcher.py run --device usb --vid 0x1234 --pid 0x5678 --profile production-readonly
  python launcher.py run --device usb-loopback                 # full USB protocol path, no hardware
Defaults (endpoints, timeout) live in device/discovery/discovery.config.json.
The wire format (TVSP framing, JSON commands) is defined in toolkit/device/protocol.py; if your
device speaks something else, edit encode/decode there and the command mapping in usb_adapter.py.
Start with production-readonly: it blocks every state-changing command before it reaches the device.
