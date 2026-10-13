# Objective 10/10 acceptance gates

This is an evidence checklist, not a marketing score. A gate is complete only when its evidence exists and can be reproduced. The repository is not considered a 10/10 production TV security product until every release-critical gate below is met. Simulator-only evidence must never be described as real-device validation.

## Current verified baseline

As of 2026-10-13:

- Historical Build run [38417030322](https://github.com/Victor-Kipruto-Rop/TV-Security-Toolkit/actions/runs/38417030322), Security run [38417030341](https://github.com/Victor-Kipruto-Rop/TV-Security-Toolkit/actions/runs/38417030341), and Test run [38417030342](https://github.com/Victor-Kipruto-Rop/TV-Security-Toolkit/actions/runs/38417030342) were reported as successful on an earlier commit. These results are historical and do not establish the state of the current release candidate.
- The latest investigated Build [38422242085](https://github.com/Victor-Kipruto-Rop/TV-Security-Toolkit/actions/runs/38422242085), Test [38422242005](https://github.com/Victor-Kipruto-Rop/TV-Security-Toolkit/actions/runs/38422242005), and Security [38422242052](https://github.com/Victor-Kipruto-Rop/TV-Security-Toolkit/actions/runs/38422242052) runs failed before any configured step executed. Their jobs returned no steps or logs; downloading logs returned `BlobNotFound`. This is not evidence that source tests failed, but it also cannot count as passing CI.
- The workflows already begin with non-secret runner diagnostics. Do not keep changing application code to compensate for jobs that never reach their first step. Resolve the Actions/runner-level failure first, then assess actual build, test, dependency-audit, and CodeQL results.
- No claim of physical-TV compatibility, trusted production signing, clean-machine installation, or independent security review is made by the simulator or historical CI runs.

## Release-critical gates

| Gate | Required evidence | Current status |
|---|---|---|
| Reproducible build and CI | Clean build, complete .NET/Python tests, dependency audit and CodeQL passing on the exact release commit | **Blocked: latest investigated runs failed before steps; CI evidence must be restored** |
| Test reliability | Remove timing-sensitive flakes; demonstrate repeatable full-suite passes and integration coverage | **Open; repeatability evidence required** |
| Production trust boundary | Production execution refuses lab-only keys and simulator trust material; no fallback to lab credentials | **Code guards added; CI and vendor authentication verification pending** |
| Transport-level safety | Enforce authorization and read-only policy at the command boundary, not only during catalog selection | **Fail-closed allowlist and Python policy parity added; CI and device-auth verification pending** |
| Device identity and authentication | Vendor-documented trust model, verified identity, authorization before commands, negative tests for unknown/forged devices | **Blocked on vendor specification and authorized device** |
| Protocol compatibility | Protocol framing, version/capability negotiation and error behavior validated against a named supported model/firmware | **Blocked on vendor specification and authorized device** |
| Hardware evidence | Reproducible positive/negative runs on each claimed model/OS/firmware; unsupported checks reported as unsupported, never passed | **Not yet established** |
| Evidence integrity and privacy | Verify archives/manifests, test redaction with realistic fixtures, document retention and deletion | **Partial; unsigned hash chain is tamper-evident, not proof of origin** |
| Accessibility | Keyboard-only navigation, meaningful screen-reader names, progress announcements, focus and contrast checks | **Automation names/status semantics added; manual assistive-technology validation open** |
| Trusted release | Publicly trusted signing certificate, timestamped signatures, signature verification, checksum validation | **Blocked on compatible certificate and protected signing configuration** |
| Clean-machine distribution | Install/launch on a clean Windows machine with normal security controls enabled; verify USB copy hashes | **Open** |
| Independent review | Security review of threat model, cryptography, permissions, data handling, and failure modes with findings resolved | **Open** |

## Definition of done

A release candidate can be called **production-ready for a specific TV model** only when:

1. All release-critical automated checks pass on the exact release commit.
2. No unresolved critical/high security findings remain; any exceptions are documented and approved.
3. Production execution cannot use the public lab key, simulator identity, or unverified device responses as trust.
4. Commands are authorized at the final transport boundary, default to read-only, and reject unknown commands.
5. A vendor-supported authentication and protocol implementation has passed positive and negative hardware tests.
6. The release package is signed by a trusted certificate, signature-verified, checksum-verified, and launched on a clean Windows machine.
7. Evidence and reports are redacted, integrity-checked, and covered by a retention/deletion policy.
8. Accessibility and usability checks are completed and findings resolved.
9. The compatibility matrix lists only exact hardware/OS/firmware combinations with recorded evidence.
10. An independent reviewer has examined the threat model and security-sensitive code, and the release notes state the tested scope and known limitations.

## Safe next actions

1. Restore Actions job execution by checking repository/org Actions policies, hosted-runner restrictions, and usage/billing; inspect job summaries for a runner-level error.
2. Once jobs execute, fix the first genuine build/test/audit finding and rerun every workflow on the same candidate commit.
3. Run the full .NET and Python suites repeatedly to identify timing-sensitive failures.
4. Arrange written authorization, one development TV, and official protocol/authentication documentation.
5. Complete signing and clean-machine validation when a compatible trusted certificate is available.
6. Obtain an independent review and resolve all critical/high findings.

Do not disable Windows security controls, claim broad compatibility from discovery alone, or use simulator PASS results as proof of production TV security.
