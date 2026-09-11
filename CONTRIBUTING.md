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
- `--max-cooling` and `--reset-lcd` require exclusive hardware ownership; do not run them beside live cooling
- preserve working cooling, existing releases and diagnostics; lifecycle changes require explicit authorization and verified fallback/handoff/rollback
- run every shell and console child hidden/background with bounded timeouts

## Before You Commit

Follow [INSTALL.md](docs/INSTALL.md) and [SAFETY-VALIDATION.md](docs/SAFETY-VALIDATION.md).
For v0.2, use a verified .NET 10 SDK to publish Release `net10.0-windows` for
`win-x64`, self-contained, untrimmed and non-single-file. Put publish output,
`BaseOutputPath`, `BaseIntermediateOutputPath` and `MSBuildProjectExtensionsPath`
in separate, new staging locations outside the live release. Disable shared
compilation and node reuse. Never build over installed binaries.

Run the staged `KrakenHost.exe --self-test` through the existing hidden launcher;
it must pass before any real-device validation. Retain build/test output and
review direct/transitive dependency advisories. Hardware-free tests do not prove
cooling continuity, authorize a handoff, or qualify the operating system.

For runtime changes, separately authorized live qualification must establish:

- one hardware owner, verified fallback and a supervised handoff with rollback
- fresh health from the actual release, matching controller PID/start identity
- measured pump/fan RPM and duty, real temperatures and LCD outcome
- preserved original configuration and old release

The installer is fresh-install only; do not execute it against an existing service
to test it. Record untested recovery/failure paths explicitly.

## Documentation Expectations

Update docs when behavior changes:

- `README.md` for user-facing setup or usage changes
- `docs/INSTALL.md` for install flow changes
- `docs/ARCHITECTURE.md` for process or health model changes
- `docs/OPERATIONS.md` for runbook changes
- `docs/TROUBLESHOOTING.md` for new failure and recovery paths
- `CHANGELOG.md` for notable released changes
- wiki pages should link to canonical docs, not duplicate lifecycle commands
- qualification reports must distinguish local proof from vendor support and retain measured limits

## Commit Identity

For automated repo updates from Codex, the local Git identity can remain:

- name: `Codex`
- email: `codex@local.invalid`

If a human contributor wants different authorship, they should set their own local Git config before committing.
