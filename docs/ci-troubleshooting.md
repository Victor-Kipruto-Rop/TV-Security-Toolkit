# GitHub Actions troubleshooting

## What the repository workflows do

- **Build** restores and builds the .NET solution in Release and runs the Python CLI regression suite.
- **Test** runs .NET unit/integration tests and Python tests as separate jobs.
- **Security** audits restored NuGet dependencies and runs CodeQL analysis.

The jobs now print a small, non-secret runner diagnostic before checkout. A log containing `RUNNER_DIAGNOSTIC_START` means a job started and reached its first step. If a job is marked failed but has no job log and no steps at all, source-code changes cannot fix that failure: GitHub has not provided an executable job environment, or the workflow was prevented from starting.

## Latest observed incident (2026-10-13)

The following runs were inspected and their failed jobs were retried:

- Build: [38422242085](https://github.com/Victor-Kipruto-Rop/TV-Security-Toolkit/actions/runs/38422242085)
- Test: [38422242005](https://github.com/Victor-Kipruto-Rop/TV-Security-Toolkit/actions/runs/38422242005)
- Security: [38422242052](https://github.com/Victor-Kipruto-Rop/TV-Security-Toolkit/actions/runs/38422242052)

After retry, the Build, both Test jobs, and both Security jobs still reported failure with `steps: null` and no log URL. Requests to download each job's logs returned `BlobNotFound`. Since the checked-in workflows have a runner-diagnostic command as their first step, this failure occurred before any configured shell command could report an application/build error. The exact cause is not exposed by the available job data, so do not assume it is a source-code defect or declare it fixed without a new run that reaches a step. The job API also reported `runner_id: 0`, an empty `runner_name`, and `steps: []` for Windows and Ubuntu jobs, confirming that no hosted runner was assigned to execute the steps.

Use the GitHub Actions run page to inspect the job summary and annotations. If the UI identifies a repository policy, account restriction, or hosted-runner provisioning issue, resolve that setting in GitHub first. Afterward dispatch/re-run the workflows and require normal step logs before treating CI as evidence.

## Triage jobs that fail before the first step

1. Open **Repository → Actions** and confirm Actions are enabled for the repository.
2. Open **Settings → Actions → General** and inspect the repository/org Actions policy. Ensure the repository is permitted to run the checked-in workflows and the referenced actions.
3. Check GitHub Actions usage/billing and any organization spending limit or hosted-runner restrictions. A public repository normally has hosted runner access, but organization policies and account restrictions can still block jobs.
4. Open the failed run and inspect the job summary/annotations. If the runner never starts, there will be no shell output from the diagnostic step.
5. Re-run a failed job only after resolving the repository/account restriction; repeated retries do not repair disabled Actions or exhausted usage.
6. Once the runner starts, use the diagnostic output to distinguish runner provisioning from checkout, SDK setup, restore, build, tests, dependency audit, or CodeQL failures. Fix the first failing step and rerun the full workflow.

Do not add a personal access token, signing certificate, or password to workflow YAML or diagnostic output. The build/test workflows need read-only repository permissions. The security workflow requests `security-events: write` for CodeQL SARIF upload.

## Local Windows verification

Run from the repository root in PowerShell:

```powershell
dotnet --info
python --version
git status --short
dotnet restore TV-Security-Toolkit.sln
dotnet build TV-Security-Toolkit.sln --configuration Release --no-restore
dotnet test TV-Security-Toolkit.sln --configuration Release --no-restore
python -m unittest discover -s tests/python -v
```

Keep the exact console output and first failing command if a check fails. Do not claim the CI is green based on local tests alone.

## Release blockers that require external evidence

- A real Windows clean-machine install test requires a separate Windows host/VM and an actual USB drive.
- Publicly trusted code signing requires an authorized code-signing certificate and protected secret storage; do not generate or commit fake release credentials.
- Real TV compatibility requires an explicitly authorized physical TV with exact model, region, OS, and firmware recorded. The USB watcher and simulator are not hardware-compatibility evidence.
