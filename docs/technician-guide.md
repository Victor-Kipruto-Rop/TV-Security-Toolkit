# Technician guide

## Before connecting

Use only a device you own or are explicitly authorized to test. Confirm its model, hardware revision, firmware, and the
diagnostic protocol are supported by this toolkit build. The repository's protocol is a reference implementation; a
successful simulator run does not prove that a physical TV is compatible.

## Connect and start

1. Start `TV-Security-Toolkit.exe` from the signed release package, or run the WPF project during development.
2. Open **Device** and choose the approved connection type. For USB, enter the device VID/PID, interface, and bulk endpoint
   numbers supplied by the device/platform owner. Install only an approved driver.
3. Select **Connect**. Confirm the displayed model, device identity, and firmware information before testing. Stop if the
   identity is unexpected or the application reports an error.
4. Open **Diagnostics** and run the read-only health and diagnostic probes first.

## Select a profile and run

1. In **Settings**, select `production-readonly` for read-only checks. Its policy blocks state-changing tests in the
   engine, not just in the UI.
2. Use `staging` or `development` only on a dedicated, recoverable test device and only when the test plan permits
   state changes.
3. In **Test Selection**, choose the approved tests/category. Review any test marked destructive or state-changing.
4. Keep the TV powered and connected during execution. Do not run firmware, factory-reset, rollback, or interruption tests
   on a production/customer device.

## Interpret results

- **PASS** means the observed response matched the expected result for that executed check. A simulator PASS is evidence
  only about simulator behavior.
- **FAIL** means an assertion did not match the expected behavior. Review the test, response, and evidence; do not repeat a
  state-changing test until the device state and recovery plan are understood.
- **SKIPPED** means policy prevented the test from running or the test was otherwise not selected/executable. It is not a
  pass and provides no evidence that the control is effective.
- **ERROR** means execution could not produce a valid result, for example because of a communication or configuration
  failure. Resolve the cause and rerun only when safe.

## Reports and failures

Reports are written under the configured output directory, in `reports` (by default `output/reports` for the app).
Evidence and logs are kept in their configured locations; review them for sensitive values before sharing.

When a test fails, preserve its report, note the test ID and device/firmware version, and follow the approved recovery
procedure. Escalate unexpected state changes, identity mismatches, or suspected security defects to the device/platform
owner. Do not use customer entitlements or production signing credentials for reproduction.
