"""Read-only TV platform discovery; never equates discovery with authentication.

This module deliberately does not issue diagnostic/security commands to TVs. Real-device
security tests remain blocked until a vendor-specific protocol and authentication strategy
have been verified and explicitly implemented.
"""
from __future__ import annotations

import shutil
import subprocess
from dataclasses import asdict, dataclass
from typing import Optional


@dataclass(frozen=True)
class PlatformResult:
    platform: str
    tool: str
    status: str
    summary: str
    discovery_only: bool = True
    authenticated: bool = False
    hardware_security_tests_authorized: bool = False

    def to_dict(self) -> dict:
        return asdict(self)


# Fixed arguments only: never invoke a shell or pass user-provided arguments to vendor tools.
_TOOL_PROBES = (
    ("Android TV / Google TV", "adb", ("devices", "-l")),
    ("Samsung Tizen", "sdb", ("devices",)),
    ("LG webOS", "ares-device", ("--list",)),
)


def _summarize(platform: str, output: str) -> str:
    if platform == "LG webOS":
        if not output.strip():
            return "Tool returned no devices; device identity is not authenticated."
        return "Vendor tool returned output; format and device identity are not authenticated."

    connected = unauthorized = offline = 0
    for raw in output.splitlines():
        columns = raw.split()
        if len(columns) < 2:
            continue
        state = columns[1].lower()
        if state == "device":
            connected += 1
        elif state == "unauthorized":
            unauthorized += 1
        elif state == "offline":
            offline += 1

    if connected:
        return f"{connected} connected candidate(s); identity is not authenticated."
    if unauthorized or offline:
        return f"{unauthorized + offline} candidate(s) unauthorized or offline; identity is not authenticated."
    return "No devices reported; identity is not authenticated."


def _probe(platform: str, tool: str, args: tuple[str, ...], timeout: float) -> PlatformResult:
    executable = shutil.which(tool)
    if not executable:
        return PlatformResult(
            platform, tool, "tool-missing",
            f"{tool} is not installed or is not on PATH. Install the official vendor tool."
        )
    try:
        completed = subprocess.run(
            [executable, *args],
            shell=False,
            check=False,
            capture_output=True,
            text=True,
            timeout=timeout,
            errors="replace",
        )
    except subprocess.TimeoutExpired:
        return PlatformResult(platform, tool, "timeout", "Vendor discovery timed out; no compatibility was established.")
    except OSError:
        return PlatformResult(platform, tool, "tool-error", "Vendor discovery could not be started.")

    if completed.returncode != 0:
        return PlatformResult(
            platform, tool, "tool-error",
            f"Vendor tool exited with code {completed.returncode}; no compatibility was established."
        )
    # Avoid placing raw serials, IPs, or other tool output into reports/logs.
    return PlatformResult(platform, tool, "discovered", _summarize(platform, completed.stdout[:32768]))


def discover_platforms(timeout: float = 5.0) -> list[PlatformResult]:
    """Probe only installed vendor CLIs with fixed, read-only device-list commands.

    This function does not scan the network, authenticate devices, or authorize security tests.
    """
    if timeout <= 0:
        raise ValueError("timeout must be greater than zero")
    return [_probe(platform, tool, args, timeout) for platform, tool, args in _TOOL_PROBES]


def compatibility_matrix() -> list[dict]:
    """Return the supported discovery surface without probing any device."""
    return [
        {
            "platform": "Android TV / Google TV",
            "discovery": "adb devices -l",
            "security_testing": "blocked-until-authenticated-vendor-protocol",
        },
        {
            "platform": "Samsung Tizen",
            "discovery": "sdb devices",
            "security_testing": "blocked-until-authenticated-vendor-protocol",
        },
        {
            "platform": "LG webOS",
            "discovery": "ares-device --list",
            "security_testing": "blocked-until-authenticated-vendor-protocol",
        },
        {
            "platform": "Roku TV",
            "discovery": "explicit-host read-only ECP device-info probe in the .NET app",
            "security_testing": "blocked-until-authenticated-vendor-protocol",
        },
    ]
