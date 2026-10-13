# TV Security Toolkit

Defensive verification toolkit for pay-as-you-go (PAYG) TV devices. It checks that a device correctly **rejects**
invalid, replayed, expired, tampered or incompatible entitlements, firmware updates and protocol messages, and that
secure boot, local storage and debug hardening are in place. Use only on devices you own or are authorised to test.

## Status
The .NET solution targets .NET 10. On Windows, use the .NET 10 SDK with Windows desktop targeting support:

```
dotnet restore
dotnet build
dotnet test            # unit + integration tests (run against the built-in simulated TV)
dotnet run --project src/TVSecurityToolkit.App
```

The current repository passes its Debug unit and integration suites and builds in Release. These results exercise the
simulator and protocol loopback; they do not establish compatibility with a production TV or its vendor authentication.
See [the implementation audit](docs/implementation-audit.md) for validation boundaries and [the release process](docs/release-process.md)
for packaging and signing requirements. See the [TV platform compatibility matrix](docs/tv-platform-compatibility.md) for the current evidence-based support status across Android/Google TV, Samsung Tizen, LG webOS, Roku TV, Hisense VIDAA, TCL, Sony BRAVIA, and other platforms. See [USB deployment](docs/usb-deployment.md) for portable packaging, removable-drive installation, and safe discovery.

## How it works
- `test-catalog/**.json` define tests as steps (`call`, `args`, `expect`). `src/TVSecurityToolkit.Tests.*` hold one small
  class per test (`CatalogTest` subclass keyed by id), discovered by reflection.
- `Engine` validates, applies the environment policy, provisions the device, runs the steps and collects redacted evidence.
- `Device` talks to hardware through a framed protocol (see docs/protocol.md) over USB bulk endpoints (LibUsbDotNet),
  serial, TCP or HTTP. `SimulatedTvAdapter` is a reference device whose protections can be switched off to prove the
  tests catch them (Device page -> "Disabled protections", e.g. `accept_replay,skip_signature`).
- `config/environments/production-readonly.json` blocks every state-changing test before it reaches the device.
- The Device page can check for the official Android/Google TV (`adb`), Samsung Tizen (`sdb`), and LG webOS
  (`ares-device`) command-line tools and perform read-only attached-device discovery. Install and authorize these
  tools using the corresponding vendor's supported process. Missing drivers, permissions, or platform dependencies
  are reported in the discovery list. Discovery is not device authentication or compatibility verification, and does
  not enable test execution; the hardware test guard remains active.
- For Roku devices, the Device page offers an optional read-only ECP device-info request to one explicitly entered
  IP address or hostname on port 8060. It does not scan networks, follow redirects, or collect device identifiers.
  The returned model string is unverified, and this does not authenticate or authorize test execution.

## Python CLI
The Python launcher can list the catalog or run it against the built-in simulator:

```
python launcher.py list
python launcher.py run --device sim --report json
python -m unittest discover -s tests/python -v
```

The CLI uses `config/toolkit.json`'s `defaultEnvironment` when no profile is specified. Hardware runs should
explicitly select the appropriate profile and device.

### Device adapters and their maturity

The Python CLI is **simulator-first and is not validated against real hardware**. Check the adapter you select
before relying on any result:

| `--device` | Status | Notes |
| --- | --- | --- |
| `sim` | Usable | Built-in simulator. Results are synthetic and prove nothing about a real TV. |
| `usb-loopback` | Usable | Exercises the full USB protocol path against the simulator. No hardware required. |
| `usb` | **Blocked by default** | Real USB security-test execution is refused until the exact model's vendor protocol and device authentication are implemented and verified. |
| `generic` | **Blocked / not implemented** | The generic hardware adapter is a skeleton and hardware runs are refused by the CLI. |

`generic` deliberately fails closed rather than returning fabricated results. It is the correct extension point
for a real serial/network adapter, but it must be written against the vendor's actual protocol specification and
the approved device authentication model; neither is available yet. No `generic` result may be reported as
hardware evidence, and a `PASS` from the simulator is not evidence that a production TV rejects the same condition.

## USB devices and automatic discovery

The Device page also has **Start USB watcher / Stop watcher** controls that poll for attach/removal events while the app is open. The watcher is discovery-only and never connects to a device or starts tests automatically. The CLI can also list USB descriptors or watch for connection/removal events:

```powershell
python launcher.py devices
python launcher.py devices --watch --interval 1
```

The watcher polls the USB bus and reports additions/removals. It is discovery-only: it never starts tests automatically. The interval must be between 0.5 and 60 seconds. Install the optional dependency with `python -m pip install -r requirements-usb.txt`; on Windows, the native libusb backend/driver must also be available. Device VID/PID and descriptor strings are identification hints only, not authentication.

The Windows application can use supported vendor discovery tools, but a USB cable does not guarantee a TV exposes a supported data interface. For real TV security-test execution, the exact model's vendor protocol and device authentication must be implemented and verified first. The CLI intentionally refuses `--device usb` and `--device generic` until that safety gate can be satisfied. Use `--device sim` or `--device usb-loopback` for simulator-only test execution.

## Portable USB launcher

Build the Windows x64 portable package on a Windows machine with the .NET 10 SDK:

```powershell
./scripts/package-usb.ps1 -OutputDirectory C:\release\TV-Security-Toolkit-USB
```

Copy and SHA-256-verify the completed package to a removable drive (replace `E` with its drive letter):

```powershell
./scripts/install-usb.ps1 -PackageDirectory C:\release\TV-Security-Toolkit-USB -DriveLetter E
```

The install script refuses to overwrite an existing destination, checks that Windows identifies the target as removable, checks free space, asks for confirmation, and verifies every copied file hash. To start it, open the USB drive and double-click `TV-Security-Toolkit-USB\Start-TV-Security-Toolkit.cmd`. Windows does not permit reliable automatic execution on USB insertion; `autorun.inf` only sets the drive label/icon. Reports from the portable package are written to its reports/evidence/logs folders. The USB storage stick carries the Windows app and its output; the TV must communicate through a separately supported and authorized interface.

## Layout
See docs/architecture.md. Payloads in `payloads/` are lab-signed with the key in `config/security-policy.json`
(simulator only; never put production keys there).
