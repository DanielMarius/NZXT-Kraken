# Validation And Live Cooling Gate

The initial staged-only results remain in `VALIDATION-2026-09-11.md` as a
historical record. Subsequent .NET 10 and supervised live qualification on
Drakula-PC is recorded in `QUALIFICATION-2026-09-11.md`. A test on that machine
does not replace commissioning on a different cooler or installation.

## Hardware-free checks

Build using a new, unused output directory and a separate intermediate directory.
Pass `-p:UseSharedCompilation=false -nodeReuse:false` to avoid leaving a compiler
server behind. Never use the running release directory. Publish for
`net10.0-windows`, `win-x64`, self-contained, untrimmed and non-single-file.
Run the resulting `KrakenHost.exe --self-test` hidden/background with a bounded
timeout. This route returns before controller/supervisor construction, mutexes,
telemetry readers or USB access. The checks retain their scratch evidence in a
unique `KrakenSafetyChecks-*` temporary directory; no user files are deleted.

Checks cover health producer identity and expiry, LCD-only degradation,
configuration/temperature failure, concurrent atomic health publication, locked
destinations, log I/O failure/rotation, byte-equivalent Q565 encoding, exact
40-point cooling packets and commissioning readback validation.
They do not prove real USB timeout behavior, startup RPM or a live cooling handoff.

## Limits that must remain explicit

- The exact tested device retained 100% pump/fan duty across HID close/reopen
  during a 20-second query-only observation. This does not prove behavior during
  power loss, USB removal or arbitrary firmware failure.
- A live unhealthy controller is no longer automatically killed. The supervisor
  logs the condition for prompt operator investigation. Crash restart remains
  supported after the old owner is gone and the controller mutex is free.
- Missing/invalid temperature readings select 100% cooling, including missing
  optional GPU readings. This trades noise for conservative behavior.
- Valid configuration retains the existing curve. Supported safety bounds:
  interval 1–5 seconds; duty 20–100 with high >= low; threshold 25–90 C;
  hysteresis 0–20 C; high hold 0–300 seconds. Invalid configuration uses 100%.
- Active logs rotate at 8 MiB; old logs/archives are preserved, so total archive
  storage still needs an operator retention decision.
- The runtime is .NET 10 LTS. Windows 10 Pro 22H2 is not listed in Microsoft's
  current .NET 10 supported-OS matrix; successful local qualification must not
  be described as full vendor support of that OS/runtime combination.
- `--verify-cooling-hold` requires exclusive controller ownership, writes full
  cooling, verifies actual duties/RPM, closes/reopens HID, samples for 20 seconds
  using only status queries, then continues into normal control. Never run it
  beside a live controller. Accept its proof only when PID and attempt timestamp
  match, followed by fresh normal health from that same process.

## Before any live replacement

1. Preserve executable/config/service backups and an executable rollback route.
2. Verify a cooling fallback independently, with real pump/fan readback. Do not
   run two USB writers or assume an installer/restart keeps cooling safe.
3. Arrange a supervised, exclusive handoff without unrelated process stops.
4. Validate real telemetry, safe duty/RPM, LCD output, controller ownership and
   service lifecycle. Prepare and review rollback. Do not describe unexecuted
   rollback branches or un-injected USB/crash faults as hardware-tested.

Until the applicable gates are met, keep the current controller and supervisor
running. Final installed controllers should be launched by their service, not
inherit a temporary commissioning helper's redirected output pipes.
