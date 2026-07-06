# Contributing

## Branching

- primary working branch: `dev`
- keep changes small and reviewable
- prefer one logical change per commit

## Development Rules

- controller hardware ownership must stay single-process
- the supervisor must not become a second hardware controller
- do not add Python runtime dependencies
- do not add restart loops or watchdog behavior that hammers the LCD
- keep emergency recovery paths simple: `--max-cooling`, `--reset-lcd`, process stop, optional CAM restore

## Before You Commit

Run:

```powershell
dotnet build .\KrakenHost.csproj -c Release
```

If you changed runtime behavior, also verify:

- service still installs cleanly
- controller starts manually
- `runtime\health.json` updates
- `logs\controller.log` does not show repeated LCD upload failures

## Documentation Expectations

Update docs when behavior changes:

- `README.md` for user-facing setup or usage changes
- `docs/INSTALL.md` for install flow changes
- `docs/ARCHITECTURE.md` for process or health model changes
- `docs/OPERATIONS.md` for runbook changes
- `docs/TROUBLESHOOTING.md` for new failure and recovery paths
- `CHANGELOG.md` for notable released changes

## Commit Identity

For automated repo updates from Codex, the local Git identity can remain:

- name: `Codex`
- email: `codex@local.invalid`

If a human contributor wants different authorship, they should set their own local Git config before committing.
