# TV Security Toolkit Quick Start

## What you need

- A Windows PC or laptop. The toolkit is a Windows application; it does not run on a TV.
- The complete toolkit folder on the USB drive.
- For TV platform discovery, the official platform tools you intend to use, installed and authorized through their vendor-supported process.

Plugging the USB into a TV will not launch the program. Plug the USB into the Windows PC instead.

## Start the program from USB

1. Insert the USB drive into the Windows PC.
2. Open the USB drive in File Explorer.
3. Open the `TV-Security-Toolkit` folder.
4. Double-click `Start-TV-Security-Toolkit.cmd`.
5. If Windows displays a security confirmation, proceed only if you trust the USB package and its source.

Keep the toolkit folder together. The launcher starts the application with that folder as its working directory. Windows does not reliably autorun programs when removable media is inserted.

## Run a simulator assessment

1. Open **Device** in the toolkit.
2. Select **Simulator** as the connection type.
3. Click **Connect**.
4. Open **Test selection** and choose the tests you want to run.
5. Open **Test execution** and select **Run selected tests**.
6. Review the live progress, final summary, findings, and reports.

Simulator results verify toolkit behavior against the simulator. They are not evidence that a production TV has the same protections or behavior.

## Check for supported platform tools

1. On the **Device** page, click **Check vendor platform tools**.
2. Review the read-only discovery results for Android/Google TV (`adb`), Samsung Tizen (`sdb`), and LG webOS (`ares-device`).
3. Install missing tools only from their official vendor sources and follow vendor instructions to authorize a device.

The toolkit only runs fixed device-list commands for these tools. A listed device is not authenticated and its compatibility is not verified.

### Optional Roku device-information check

1. On the **Device** page, enter the Roku TV's IP address or hostname in the Roku section.
2. Click **Check Roku endpoint**.
3. Review the returned status. The check makes one read-only device-information request to the entered host; it does not scan the network.

The reported model is unverified and does not authenticate the device.

## Hardware testing limitation

The current toolkit does **not** run security tests against non-simulator connections. It intentionally blocks hardware test execution until vendor authentication and a compatible target diagnostic protocol have been implemented and verified. Do not treat transport connectivity or platform-tool discovery as authorization to run tests.

To add supported hardware testing, the device owner or vendor must provide an authorized target, its exact model and OS/firmware version, the supported diagnostic interface and protocol, and the applicable authentication and trust requirements.

## Find reports, evidence, and logs

For the USB package, generated files are stored under the toolkit folder:

- Reports: `reports\<session-id>\`
- Evidence archives: `evidence\<session-id>\`
- Operational logs: `logs\`

The application shows report and evidence paths after a run. Keep the USB connected while the application is running so it can save its output.

## Troubleshooting

- **The launcher is missing or the application does not start:** Confirm that the entire package was copied to the USB and start `Start-TV-Security-Toolkit.cmd` from inside the `TV-Security-Toolkit` folder.
- **A vendor tool is reported missing:** Install the official tool, ensure its executable is available on `PATH`, and restart the toolkit.
- **No device is listed:** Check the vendor-supported connection and authorization steps. A USB driver, device setting, or network configuration may also be required.
- **Discovery succeeds but tests are blocked:** This is expected for hardware connections until the vendor-authenticated diagnostic integration is available. Use the simulator for software verification.
- **Reports cannot be saved:** Check that the USB is connected, has free space, and is not write-protected.

For current implementation limits and validation status, see [the implementation audit](implementation-audit.md) and [the architecture overview](architecture.md).
