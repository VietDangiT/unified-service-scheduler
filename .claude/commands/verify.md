---
description: Build, test and style-check the solution, then report results honestly
allowed-tools: Bash(dotnet build:*), Bash(dotnet test:*), Bash(dotnet format:*), Bash(docker info:*)
---

Run the quality gate for this repository and report the outcome.

1. `dotnet build ServiceScheduler.slnx -nologo` (warnings are errors).
2. `dotnet test tests/ServiceScheduler.UnitTests --no-build -nologo`.
3. Check whether Docker is running (`docker info`). If it is, run
   `dotnet test tests/ServiceScheduler.IntegrationTests --no-build -nologo`; if not, say integration
   tests were **skipped** and why. Never present skipped tests as passed.
4. `dotnet format ServiceScheduler.slnx --verify-no-changes`.
5. If steps 1–4 pass and there are uncommitted changes, run the `code-reviewer` agent on the diff.

Report as a short table: step, result (pass / fail / skipped), and the key numbers (tests passed/failed,
review findings by severity).
For any failure, quote the first relevant error lines and state the most likely cause. Do not fix
anything unless the user asks. $ARGUMENTS
