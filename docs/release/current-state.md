# Release: current state

Phase 0 repository audit. Audited commit `93f14ca`. No functionality was modified.

## CI/CD: configured, never executed

Four workflows exist: `build.yml`, `test.yml`, `security.yml`, `release.yml`.

The repository is public and the workflows trigger correctly on push. **Every job fails before a
runner is assigned:**

| Workflow | Job | Duration | Steps executed | Runner |
| --- | --- | --- | --- | --- |
| Test | `python` | 2-5 s | 0 | none (`runner_id: 0`) |
| Test | `dotnet` | 1-3 s | 0 | none |
| Security | `dependency-audit` | 3-4 s | 0 | none |
| Security | `codeql` | 3-4 s | 0 | none |
| Build | `build` | 2-5 s | 0 | none |

An empty `steps` array with no assigned runner and a 1-5 second lifetime is the signature of a job
rejected during scheduling, not a build or test failure. **No CI build or test result exists in either
direction.** The precise banner was not retrievable because the workflow-logs API requires a token
that is not configured, so the cause is inferred from the job records rather than read directly.

The most likely causes are account or organisation settings: Actions enablement, a spending limit, or
pending billing. This is not a repository defect and cannot be fixed from the working tree.

**Therefore CodeQL, the NuGet vulnerability scan, and the release workflow have never executed.**

Workflow structure gaps against Phase 2: there is no single `ci.yml` gate, no `dependency-review.yml`,
no secret scanning, no SBOM generation, no catalog validation step, no package validation step, no
coverage or test-result publishing, and no artifact upload. GitHub Actions are referenced by major
version tag rather than pinned to a commit SHA.

## Code signing: proven, but not usable for distribution

What exists and has been demonstrated:

- SHA-256 Authenticode signing via an authentic Microsoft `signtool.exe`, with RFC 3161 timestamping.
- Verification integrated, with `-RequireSigned`, `-AllowUnsigned` and `-ForceResign`.
- Signing and timestamping were **proven end to end** with a temporary certificate.
- A self-signed chain was **correctly rejected** by verification, which proves the release gate does
  not ship an untrusted package.
- Skipping already-valid binaries reduced the signing set from 411 files to 15, a 96.4% reduction in
  timestamp calls.
- Signing secrets come from GitHub secrets and are cleared from the environment after use.

The blocker: **no publicly trusted Code Signing EKU certificate exists locally.** Without one, the
distributed package will not start on any machine with Smart App Control enabled, so clean-machine
testing and distribution are both impossible.

## Packaging

`scripts/package-usb.ps1` produces a self-contained portable USB package with a launcher,
package-relative output, catalog/config data, and platform discovery. Staging integrity was verified:
all 599 package files matched their staged SHA-256 hashes.

`scripts/verify-package-signatures.ps1` verifies an already-produced package independently of the
build, which is the correct shape for a release gate.

**Gaps (Phase 46):** no `package.zip.sha256`, no `package.zip.sig`, no `SHA256SUMS` file, no
`release-manifest.json`, and no SBOM. There is no installer package; only the portable form exists.

## Release process

`docs/release-process.md` documents signing, timestamping, and verification. The process is coherent
but has never been executed end to end, because it terminates at the certificate.

Missing: branch protection on `main`, `release/*` and `v*` tags (Phase 3); a `CHANGELOG.md`; a
tagging and provenance story; a documented rollback procedure; and any release has been published.

## SBOM and supply chain (Phase 42, Phase 43)

There is **no SBOM** of any format. There is no Dependabot configuration, no dependency review
workflow, no license scanning, and no artifact attestation or provenance. Package versions are
centrally managed in `Directory.Packages.props`, which is the correct foundation, but there is no
lock file and no transitive-dependency policy.

Python has **no dependency manifest at all**, so Python dependencies could not be enumerated in an
SBOM even if one were generated.

## Release blockers, in order of cost

1. **GitHub Actions is not executing** (B2). Cheapest to fix, and it unblocks build, test, and
   supply-chain evidence simultaneously.
2. **No code-signing certificate** (B1). Blocks clean-machine testing, distribution, and signing the
   evidence manifest.
3. **No authorized hardware or vendor protocol specification** (B3). Blocks every hardware claim and
   the device-authentication gap.

## What can honestly be claimed today

- The solution builds with 0 warnings and 0 errors, in the working tree and in a clean clone.
- The unit, integration, and Python suites were green at commit `0244d89`.
- Signing and timestamping work, and an untrusted certificate is correctly refused.
- Staged package contents are hash-verified.

**Cannot** be claimed: that CI passes, that a release exists, that the package installs on a clean
machine, or that any result reflects real television hardware.
