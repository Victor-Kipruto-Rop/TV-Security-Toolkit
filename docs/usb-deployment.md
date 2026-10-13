# USB deployment and device discovery

## Two different USB roles

1. **Portable storage drive:** contains the Windows x64 toolkit package, configuration, documentation, and reports. It does not execute on the TV.
2. **TV/diagnostic connection:** a separate cable or supported diagnostic interface between the Windows host and a TV. Plugging in a cable is not proof that the TV exposes a supported protocol.

The portable launcher is deliberately manual. Windows removable-media autorun is not a reliable or safe application launch mechanism; `autorun.inf` only supplies drive presentation metadata.

## Build a portable package

On Windows, install the .NET 10 SDK compatible with `global.json`, clone the repository, and run tests before packaging:

```powershell
dotnet restore TV-Security-Toolkit.sln
dotnet build TV-Security-Toolkit.sln --configuration Release
dotnet test TV-Security-Toolkit.sln --configuration Release
python -m unittest discover -s tests/python -v
./scripts/package-usb.ps1 -OutputDirectory C:\release\TV-Security-Toolkit-USB
```

The packaging script publishes the WPF application as a self-contained `win-x64` application. The output directory must not already exist. For distribution, use a trusted code-signing certificate and require signing; an unsigned local package is not a production release.

## Copy to a removable drive and verify

Replace `E` with the drive letter assigned to the USB flash drive:

```powershell
./scripts/install-usb.ps1 -PackageDirectory C:\release\TV-Security-Toolkit-USB -DriveLetter E
```

The installer verifies the launcher and executable, checks drive health/readiness and free space, refuses to overwrite an existing toolkit folder, asks for confirmation, copies the package, and verifies the file count plus SHA-256 hashes for every copied file. If copying or verification fails, it removes only the destination folder created by that run so a partial install does not block a retry. Some flash drives are reported as Fixed by Windows; only after verifying the drive letter, rerun with `-AllowFixedDrive` to explicitly override that classification. This does not bypass the confirmation, free-space check, hash verification, or overwrite protection. Review the destination before running the launcher. The hash comparison verifies the copy operation, not the publisher's identity; use package signature verification for that.

Launch by opening `E:\TV-Security-Toolkit-USB` and double-clicking `Start-TV-Security-Toolkit.cmd`. Reports are written under the package's configured reports/evidence/logs folders. Keep the drive physically secure and encrypt it if reports may contain sensitive information.

## USB discovery in the Windows application

Open the Device page and choose **Start USB watcher**. The app polls the host USB inventory approximately once per second and records present, attached, and removed descriptors in the watcher panel. Choose **Stop watcher** when finished. This does not auto-connect, authenticate the TV, or launch a test. Polling can miss very short-lived connections; the app must remain open for the watcher to run.

## USB discovery from the Python CLI

Install the optional Python USB dependency:

```powershell
python -m pip install -r requirements-usb.txt
python launcher.py devices
python launcher.py devices --watch --interval 1
```

The watch mode polls the USB bus and prints present, attached, and removed descriptors. Stop it with Ctrl+C. Polling can miss very short-lived connections and is not an operating-system hardware event subscription. The interval must be 0.5–60 seconds. The native libusb library/driver must be available on the host.

Discovery does not run security tests. VID/PID, manufacturer/product strings, and serial numbers can be absent, duplicated, spoofed, or unrelated to the TV's network identity. Do not use them alone for authentication.

## Real-TV testing gate

The Python CLI currently refuses `--device usb` and `--device generic` for test execution until a vendor-documented protocol and verified device-authentication method are implemented and validated for the exact model. `sim` and `usb-loopback` exercise simulator behavior only.

The .NET UI's ADB/SDB/ares-device and Roku ECP features are discovery mechanisms only. Install vendor tools using their official instructions and enable device-side developer access only when authorized. No test should be enabled merely because a device appears in the list.

Before enabling an adapter, require model/firmware-specific evidence for:
- supported documented transport and command framing;
- authenticated device identity and user authorization;
- explicit capability negotiation;
- timeouts, malformed responses, disconnect and reconnect handling;
- read-only defaults and separate approval for state-changing tests;
- positive/negative hardware fixtures and redacted evidence;
- an explicit supported-model matrix and CI regression tests.

## Troubleshooting

- **No USB devices found:** check the cable/port, device power, OS permissions, optional PyUSB dependency, and native libusb driver.
- **Device appears but cannot be tested:** expected for unverified hardware; discovery is not test authorization.
- **Windows blocks the executable:** check package provenance and signatures. Do not disable Windows security controls to run an unknown package.
- **USB storage fills up:** archive reports securely, then remove only reports you no longer need; do not overwrite the package in place.
- **Application fails on a non-Windows host:** this package targets Windows x64 and is not a native TV app or a cross-platform build.
