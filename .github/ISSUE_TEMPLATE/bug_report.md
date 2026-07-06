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

Paste relevant lines from:

- `bin\Release\net7.0-windows\runtime\health.json`
- `bin\Release\net7.0-windows\logs\controller.log`
- `bin\Release\net7.0-windows\logs\supervisor.log`

## Sanity Checks

- [ ] I confirmed CAM is not actively controlling the same Kraken device.
- [ ] I confirmed there is only one KrakenHost controller process.
- [ ] I checked the runtime health and logs before opening this issue.
