# Staged Validation — 2026-09-11

## Source authority and containment

- GitHub `DanielMarius/NZXT-Kraken`, `dev`: `e7dd98db3f5297d8bbee8a4ae0480491713482d8`.
- Local `dev` matched that exact commit before edits; no upstream update was needed.
- Existing untracked `NzxtRgbTool/` preserved; excluded from the controller compile inputs.
- Source backups are byte-for-byte verified under
  `C:/Users/Daniel/Documents/Codex Drakula Apps/backups/kraken-dev-20260910T224018314Z/`.
- No commit, push, live deployment, controller/service restart or hardware test was performed.

## Build and test evidence

Installed SDK: .NET 7.0.306. Final staged output:

`C:/Users/Daniel/Documents/Codex Drakula Apps/backups/kraken-dev-20260910T224018314Z/validation/build5/out/`

Build used Release configuration, a separate `build5/obj/` intermediate directory,
`UseSharedCompilation=false`, and `-nodeReuse:false`. Child processes were hidden.
The tool runtime omitted standard NuGet profile environment paths; explicit
child-only Windows profile/ProgramFiles/ProgramData values allowed restore/build.
No global environment or Windows policy was changed.

- Build succeeded: **0 errors, 3 compatibility warnings**.
- Warnings: existing System.CodeDom, System.Management and System.IO.Ports 9.0.0
  packages do not support/test the project's .NET 7 target. They were not suppressed.
- Staged `KrakenHost.exe --self-test`: **exit 0; 53 hardware-free checks passed**.
- Test evidence:
  `C:/Users/Daniel/AppData/Local/Temp/KrakenSafetyChecks-d95d93206752463e918571dacd620752/result.json`.
- Atomic-file stress: 382/500 publication attempts accepted, 118 failed attempts;
  no partial JSON observed across 500 reads. Final uncontended publication and
  exact readback succeeded. Causes of individual failed attempts were not traced.
  This proves visibility/progress in this test, not guaranteed real-time latency.
- Q565 640x640 encoding, median of three: reference GetPixel **98.545 ms**;
  LockBits **1.393 ms**. Reference bytes matched, including padded/negative-stride
  cases. This is encoder-only timing, not total USB/LCD latency or whole-PC speed.
- PowerShell installer syntax parsed without errors. Its execution was blocked
  by the host execution policy; policy was not changed. The existing-service
  refusal path was reviewed statically, not claimed as an executed install test.
- `git diff --check` passed (Git reported only line-ending conversion notices).

## Live installation preserved

Original supervisor PID 6596 and controller PID 4604 remained running. The live
KrakenHost.exe, KrakenHost.dll and config.json hashes were unchanged.

Read-only sampling from 01:48:39 to 01:54:24 local time: 70 samples; no missing,
stale, non-ok or below-threshold RPM sample. CPU maximum 68 C; liquid maximum
32.5 C; minimum observed pump 2608 RPM and fan 1500 RPM. At 01:54:27, health was
ok, pump 2857 RPM, fan 1785 RPM, liquid 32.5 C and LCD updated.

These are observations of the original live version, not hardware validation of
the staged patch. No task-owned build/test process remained after checks.

## Remaining gates

1. Qualify a supported .NET runtime/SDK and dependencies separately.
2. Prove an independent cooling fallback and a supervised exclusive handoff.
3. Validate real hardware behavior, USB timeout recovery, startup RPM, service
   lifecycle and rollback before installing the staged controller.

Until then, keep the existing live cooling controller running. See
`SAFETY-VALIDATION.md` for behavior changes and explicit limitations.
