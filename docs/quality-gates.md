# Local quality gates

Run these checks from the repository root before opening a pull request. Use PowerShell on Windows for the WPF app and USB packaging scripts.

## Python CLI and repository consistency

```powershell
python --version
python -m unittest discover -s tests/python -v
python launcher.py list
python launcher.py run --device sim --report json
```

The Python suite includes repository integrity checks that compare test IDs between the Python and .NET catalogs and verify that the Python, .NET, and `VERSION` version declarations agree. Update both catalog representations when adding a test. Simulator output is synthetic and is not evidence about a physical TV.

## .NET desktop application

Install the .NET SDK version specified in `global.json`, then run:

```powershell
dotnet restore TV-Security-Toolkit.sln
dotnet build TV-Security-Toolkit.sln --configuration Release --no-restore
dotnet test TV-Security-Toolkit.sln --configuration Release --no-build
```

## USB package

Only package on a Windows host with the matching .NET SDK. Inspect the package output before copying it to removable media:

```powershell
./scripts/package-usb.ps1 -OutputDirectory "$PWD/artifacts/TV-Security-Toolkit-USB"
Get-ChildItem "$PWD/artifacts/TV-Security-Toolkit-USB" -File -Recurse
```

Use `scripts/install-usb.ps1` only after checking the target drive letter and package directory. Installation is manual and never autoruns on insertion. Preserve the installer confirmation and hash verification safeguards.

## Hardware boundary

Do not use simulator or loopback results as real-device evidence. Before any physical-device test, record the exact model, region, operating system, firmware, authorization, vendor protocol, and supported authentication method. Keep the existing hardware execution gate closed until those details are implemented and verified. Discovery alone does not authenticate a device.

## CI failures before the first step

If a workflow job fails without executing any step, treat it as a runner/platform-level failure until logs establish otherwise. Check repository **Settings → Actions → General**, organization policies, hosted runner availability, and account usage/billing. Do not label the source code as failing or passing based on a zero-step run.
