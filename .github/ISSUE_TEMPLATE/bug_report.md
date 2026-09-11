---
name: Bug report
about: Report a real problem with the Kraken controller, LCD, pump, fan, or service
title: "[Bug] "
labels: bug
assignees: ""
---

## Summary

Short description of the problem.

## Area

- [ ] Controller loop
- [ ] LCD upload/rendering
- [ ] Pump/fan control
- [ ] Supervisor service
- [ ] Installer/scripts
- [ ] Documentation

## Environment

- Windows version:
- Kraken release/version and commit:
- Actual installed release directory (from `KrakenSupervisor.PathName`):
- Runtime version / self-contained release:
- Kraken model:
- GPU:
- Is NZXT CAM installed:
- Is NZXT CAM running:

## Steps To Reproduce

1. 
2. 
3. 

## Expected Behavior

What should have happened?

## Actual Behavior

What actually happened?

## Logs / Health Data

Use the read-only path-resolution recipe in `docs/OPERATIONS.md` through the
hidden/background launcher. Do not substitute a build output folder for the
actual installed release. Redact personal paths or secrets before posting.

Paste relevant lines from the actual release directory:

- `runtime\health.json`, including timestamp and producer PID/start identity
- `logs\controller.log`
- `logs\supervisor.log`

For cooling faults, include observed pump/fan RPM, duty and temperatures. Do not
stop cooling or launch a competing controller to reproduce a fault.

## Sanity Checks

- [ ] I confirmed CAM is not actively controlling the same Kraken device.
- [ ] I confirmed there is only one KrakenHost controller process.
- [ ] I checked the runtime health and logs before opening this issue.
