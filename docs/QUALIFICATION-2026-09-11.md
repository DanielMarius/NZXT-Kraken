# Drakula-PC live qualification — 2026-09-11

This is the time-stamped qualification record preceding the `v0.2` source
release. See `RELEASE-v0.2.md` for release metadata and publication scope. Later
version stamping does not retroactively change the measured binary hashes below.

## Outcome

The .NET 10 candidate was built, tested and installed side-by-side. A measured
firmware-hold test and supervised exclusive handoffs passed. Original binaries,
configuration and rollback evidence were retained. The original 80%/100% policy
was restored byte-for-byte after temporary full-duty commissioning.

This closes local runtime and normal-operation handoff qualification. It does
not claim vendor support for Windows 10 Pro, uninterrupted measurement during
process transitions, power-loss persistence, arbitrary USB-failure recovery or
execution of every rollback branch.

## Source and runtime

- GitHub `DanielMarius/NZXT-Kraken`, `dev`, baseline
  `e7dd98db3f5297d8bbee8a4ae0480491713482d8`; changes were uncommitted/unpushed
  when this hardware qualification was recorded, before the `v0.2` release.
- SDK 10.0.401, runtime 10.0.12, downloaded from Microsoft's release metadata;
  archive SHA-512 matched and the SDK host Authenticode signature was valid.
- SDK is task-local; no global SDK/PATH/OS/security-policy change.
- Target `net10.0-windows`, `win-x64`, self-contained, untrimmed, non-single-file.
- Release: `C:/drakula/tools/KrakenHost-releases/20260911-net10-qualified/`.
- Live `coreclr.dll` was loaded from that release. CPU telemetry and NVML GPU
  readings succeeded on the actual machine; LCD uploads were acknowledged.
- Windows 10 Pro 22H2/build 19045 is not listed in the current .NET 10
  [supported-OS matrix](https://github.com/dotnet/core/blob/main/release-notes/10.0/supported-os.md).
  .NET 10 itself is [LTS](https://dotnet.microsoft.com/en-us/platform/support/policy/dotnet-core).

## Build and hardware-free tests

- Corrected release publish: exit 0, no warning/error output; separate output
  and intermediate paths, shared compilation disabled, node reuse disabled.
- NuGet audit enabled for all direct/transitive packages; no advisories reported.
  The isolated project-assets path was explicitly verified. LHM 0.9.4 remains;
  its Management/Ports 9.0.0 dependencies are compatible assets, not a .NET 9
  runtime requirement. Their servicing refresh remains a separate maintenance
  consideration; no security defect was inferred merely from their version.
- `KrakenHost.exe --self-test`: **77 hardware-free checks passed**, exit 0.
- Test evidence:
  `C:/Users/Daniel/AppData/Local/Temp/KrakenSafetyChecks-594657cbf18a442ca87046f2c158fa41/`.
- Concurrent file test: 353/500 writes accepted, 147 best-effort failures; all500
  reads complete, final uncontended write/read succeeded. This is not a promise
  of guaranteed per-attempt publication under contention.
- 640x640 Q565 encoder median-of-three: GetPixel 92.011 ms, LockBits 1.652 ms;
  bytes identical. This measures encoding only, not total system performance.
- Installer syntax and hidden/fresh-install/static contracts passed. Installer
  was not executed against the existing service.

## Cooling correction and measured fallback

The device is `1e71:300c`. Its custom curve has40 points for20–59 C. The previous
39-point packet omitted the last point; the corrected44-byte packet explicitly
sets the final critical-temperature duty to100 for both pump and fan. Seventeen
pure packet assertions cover channels, headers, lengths, bounds and final point.
The protocol is corroborated by the
[Linux driver](https://github.com/torvalds/linux/blob/master/drivers/hwmon/nzxt-kraken3.c).

At04:44:58 local time, the verified old controller was replaced by commissioning
PID32496 after its supervisor stopped while its cooling telemetry remained fresh.
One global controller mutex stayed held throughout the new proof and startup.

- Full cooling established and actual duties/RPM read back.
- HID handle closed/reopened without USB reset or power interruption.
- **20.0739659 seconds** of query-only observation, ten fresh hold samples.
- Every hold sample: actual pump duty100, fan duty100, liquid31.9 C.
- Pump2857–3191 RPM; fan1785 RPM across the hold samples.
- Normal startup followed in the same process, with real CPU/GPU/liquid
  telemetry, RPM, duty readback and successful LCD uploads.
- Proof: release `runtime/cooling-hold-proof.json` (commissioning PID32496).

The first supervisor adopted that process successfully. A temporary-helper
stdout inheritance issue was then identified before closing the task. With both
duties again verified100%, a final exclusive SCM-owned handoff removed that
temporary dependency. It did not reset USB or change any unrelated service.

At04:49:26 local time:

- `KrakenSupervisor`: Running/Auto, PID30452.
- Controller PID6920, direct parent30452, start time
  `2026-09-11T01:49:22.7009314Z`, qualified executable path verified.
- CPU55 C, GPU35 C, liquid33.6 C; pump2857 RPM, fan1785 RPM.
- Actual pump/fan duties100%; LCD updated; fresh PID/start-bound health.
- Temporary low-duty100 configuration subsequently restored to the original
  low80/high100 bytes. No persistent commissioning helper is required.

Final service-owned observation,04:49:25–04:52:43 local:67 samples; zero non-ok,
stale-over8-second or below-threshold RPM samples. Minimum pump2857 RPM,
minimum fan1675 RPM, maximum liquid33.7 C, maximum CPU73 C, maximum health age
1.134 seconds. The final readback at04:52:44 showed CPU63 C, GPU36 C, liquid33.4 C,
pump3157 RPM, fan1785 RPM, actual duties100%, and a healthy LCD update cadence.

Over a separate66.622-second window the controller consumed0.4375 CPU seconds:
approximately0.021% of total32-logical-processor capacity, with74.6 MiB working
set at the final sample. This is a short controller-only measurement, not a
whole-PC benchmark or a guarantee about long-term load. Both commissioning
helpers exited, and the temporary read-only sampling timer was stopped. Only
the intended service/supervised controller remain running. All47 original
rollback runtime files were rehashed and remain unchanged.

## Preserved recovery evidence

`C:/Users/Daniel/Documents/Codex Drakula Apps/backups/kraken-gates-20260911T013000195Z/`
contains verified source snapshots,47 original runtime files (9,662,221 bytes),
configuration snapshots, guarded handoff scripts and `handoff-events.jsonl`.
The old release remains at `C:/drakula/tools/KrakenHost/bin/Release/net7.0-windows/`.
No files were deleted; no unrelated workflow was stopped.

Published host SHA-256:
`95a4a2b1f91f6cd0c07c19939be560edcd0784382ef408e40b83bdf5f7a25917`.
Published application DLL SHA-256:
`e8be5bb0e6a376b3f0a58922cdbcfbfa0308a511b4e33b54cd55090bd717c1fa`.

Recovery scripts were reviewed and their predicates checked; failure rollback
was not deliberately triggered on live cooling. No stress-heating, USB unplug,
forced firmware fault or physical LCD visual inspection was performed.
