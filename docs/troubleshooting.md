# Troubleshooting
- "USB device not found": check VID/PID and that a WinUSB/libusb driver (Zadig) is bound to the interface.
- "read timeout": wrong endpoints or the device does not speak TVSP; verify `ep_in`/`ep_out` and the protocol.
- "crc mismatch"/"bad magic": the device replies in a different framing; adapt `Device/Protocol`.
- Payload tests fail on a correct simulator: run `dotnet test` and check `Shipped_payloads_verify_against_the_lab_key`.
