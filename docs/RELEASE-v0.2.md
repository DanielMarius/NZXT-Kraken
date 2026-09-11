# v0.2 — 2026-09-11

- Git release tag: `v0.2`, on the `dev` branch.
- Application/package version: `0.2.0` (assembly/file version `0.2.0.0`).
- Target: .NET 10 LTS, Windows x64, self-contained, untrimmed, non-single-file.
- Source authority: [DanielMarius/NZXT-Kraken](https://github.com/DanielMarius/NZXT-Kraken).

## Included

- Single-controller and single-supervisor ownership; PID/start-bound health.
- Atomic diagnostic publication, bounded read uncertainty and preserved log archives.
- Full cooling on startup or invalid/missing temperature/configuration data.
- LCD-fault isolation, bounded HID/WinUSB waits and byte-equivalent faster Q565 encoding.
- Correct 40-point cooling curves; final 59 C endpoint always requests 100%.
- Explicit commissioning-only cooling-hold verification and actual duty readback.
- Fresh-install-only installer, isolated publish folders and pre-install self-tests.
- Updated operator docs, issue template and wiki pointers to canonical runbooks.

## Verification

The version-stamped release was published in a new isolated output directory:
build exit 0, no warnings/errors. `KrakenHost.exe --self-test` passed all 77
hardware-free checks before any hardware, telemetry or ownership initialization.
Executable and DLL file versions were verified as `0.2.0.0`, with product version
`0.2.0` plus source-revision metadata. Installer parsing produced zero errors;
it was not executed against the existing service. Release code review found no
blocking defect, and whitespace validation passed.

The preceding functional build passed real Drakula-PC commissioning: actual
100% pump/fan duty held for 20.0739659 seconds across HID close/reopen, followed by
CPU/GPU/liquid telemetry, LCD uploads and a directly service-owned controller.
Sixty-seven post-handoff samples were healthy. Exact timestamps, hashes, test
artifacts and limits are in [the qualification record](QUALIFICATION-2026-09-11.md).

This release-finalization step changes version metadata and documentation, not
the qualified control algorithm. It does not restart/redeploy the running cooler
merely to change its file-version stamp. Existing cooling and rollback files stay
in place. Unrelated local `NzxtRgbTool/` content is outside this release.

## Known limits and upgrade rule

- Windows 10 Pro 22H2 is locally tested but absent from the current .NET 10 supported
  OS matrix; this is not a claim of full vendor support for that platform.
- Live unhealthy controllers are preserved and reported; only exited controllers
  are restarted automatically when ownership is free.
- The firmware-hold test is not proof for USB removal, power loss or every rollback
  failure branch. No physical cooling-failure injection is claimed.
- Log archives are preserved without a total-size cap.
- LHM 0.9.4's Management/Ports 9.0.0 assets remain a separate servicing consideration;
  no vulnerability is inferred solely from their version numbers.

Never overwrite a running release. Follow [installation](INSTALL.md),
[operations](OPERATIONS.md) and [cooling gates](SAFETY-VALIDATION.md), using hidden
bounded execution, verified backups, a supervised handoff and a prepared rollback.
