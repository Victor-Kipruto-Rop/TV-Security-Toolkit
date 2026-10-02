# Changelog
## 1.0.0
- .NET 10 solution: Core, Engine, Security, Device (USB/serial/TCP/HTTP, simulator), Reporting (HTML/JSON/PDF), WPF app.
- 48 catalog-driven tests across PayG, firmware, update, network and local categories.
- Added TCP EOF handling, TLS minimum-version checks, and Python CLI filter/policy regression fixes.
- Added Windows build/test/security workflows and a self-contained USB package script; signed releases require protected CI certificate secrets.
