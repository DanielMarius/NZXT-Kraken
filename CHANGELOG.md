# Changelog

All notable changes to this project should be recorded in this file.

## v0.2 — 2026-09-11

Qualified cooling safety release. Application/package version: `0.2.0`; Git tag:
`v0.2`. See [release notes](docs/RELEASE-v0.2.md) for verification and limitations.

- atomic, producer-identified health publication; bounded read uncertainty
- single-supervisor ownership for service and command-line modes
- no automatic killing of a live unhealthy controller without proven fallback
- conservative startup/missing-telemetry cooling and validated configuration
- LCD fault isolation, bounded HID/WinUSB waits and faster byte-equivalent frame encoding
- reduced repetitive logs; active-log rotation preserving all archives
- hardware-free regression entry point; isolated controller compile inputs
- installer refuses an existing service/controller instead of stopping cooling before a build
- .NET 10 self-contained Windows x64 release with hardware-free checks before fresh installation
- corrected 40-point cooling profile; final 59 C point always requests 100%
- exclusive, bounded firmware-hold commissioning test and actual pump/fan duty in health
- Drakula-PC qualification/deployment evidence is recorded separately in `docs/QUALIFICATION-2026-09-11.md`; Windows 10 Pro support limitations remain explicit

## 2026-07-06

- imported the Kraken C# controller into Git on branch `dev`
- added repo-ready `README.md` with install, service, and emergency usage
- added `docs/INSTALL.md`
- added `docs/ARCHITECTURE.md`
- added `docs/OPERATIONS.md`
- added `docs/TROUBLESHOOTING.md`
- added MIT license files
- kept standard `LICENSE` file for host/repo license detection
- cleaned `.gitignore` so runtime artifacts and logs do not get committed

## 2026-06-24

- stabilized Kraken LCD upload path in C#
- removed Python runtime dependency from the controller path
- added service supervisor model with real health checks
- set fan and pump policy to `80%` below `40C`, `100%` at or above `40C`
- set LCD minimum push interval to `2s`
