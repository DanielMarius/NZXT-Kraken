# Troubleshooting

Use the read-only checks in `OPERATIONS.md` through the existing hidden/background
launcher only. Do not open foreground consoles. Resolve the active release from
the quoted `KrakenSupervisor.PathName`, not a guessed build directory.

Do not run stop/reset commands against live cooling without explicit operator
authorization, independently verified fallback and a supervised single-owner handoff.
An LCD-only fault no longer restarts cooling. A live unhealthy controller is
preserved and reported for operator attention, not automatically killed. See
`SAFETY-VALIDATION.md` before any recovery action.

## LCD Is Corrupted Or Flickering

Check:

- no NZXT CAM process is running
- only one `KrakenHost.exe` controller exists
- the actual release's `logs\controller.log` records the LCD fault and whether
  cooling remains healthy

Do not restart working cooling for an LCD-only fault. `--reset-lcd` is a hardware
operation requiring exclusive ownership; it must never be run beside the live
controller. Preserve logs and investigate the display path before requesting a
separately authorized, hidden/background recovery with verified cooling fallback.

## Fan Or Pump Looks Wrong

Read `<releaseRoot>\runtime\health.json` using the path-resolution recipe in
`OPERATIONS.md`. Check freshness and producer PID/start identity before trusting
its reported RPM, temperatures or duty.

Expected thresholds:

- pump RPM >= `1500`
- fan RPM >= `1000`

Treat a genuine cooling fault as urgent operator attention. The `--max-cooling`
command requires exclusive ownership and verifies duty/RPM; invocation alone is
not proof of safe cooling. Do not launch it beside the owner or kill the owner to
acquire the mutex. Establish and verify an independent fallback before any
explicitly authorized handoff, using the existing hidden/background launcher.

## Service Will Not Install

Common reasons:

- the .NET 10 SDK is missing or a different SDK is being selected; use the
  installer's optional `-DotnetPath` with the verified SDK executable
- hidden-launch permissions or PowerShell execution policy prevent execution
- `KrakenSupervisor` already exists: this is an intentional fresh-install guard,
  not an instruction to remove/restart the service
- a controller is already running: preserve it and follow staged validation
- `-CoolingFallbackVerified` was not supplied after a real independent safety
  check
- folder publishing or the hardware-free `--self-test` failed; preserve output
  and logs, and do not proceed to service creation

Do not bypass these guards or overwrite installed files. For an authorized fresh
installation, `INSTALL.md` documents self-contained, untrimmed, non-single-file
`net10.0-windows`/`win-x64` publication into a unique release folder with isolated
intermediates. The installer must run through the existing hidden launcher; it
does not update a running installation. Do not change machine execution policy
as a troubleshooting shortcut.

## CAM Took Back Control

First establish which process actually owns the device and whether cooling is
healthy. Do not start another Kraken controller, blindly stop CAM, or restart the
service. Preserve existing cooling and report the ownership conflict. Any change
to CAM startup, controller ownership or the service requires explicit operator
authorization, an independently verified fallback and a supervised single-owner
handoff; use hidden/background helpers only.
