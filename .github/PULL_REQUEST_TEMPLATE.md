## Summary

Describe the change in a few lines.

## What Changed

- 

## Verification

- [ ] `dotnet build .\KrakenHost.csproj -c Release`
- [ ] Manual controller run tested if runtime behavior changed
- [ ] Service install/reinstall tested if supervisor or scripts changed
- [ ] Checked `runtime\health.json`
- [ ] Checked `controller.log` and `supervisor.log`

## Hardware Safety Review

- [ ] Single controller ownership is preserved
- [ ] No Python runtime dependency added
- [ ] No LCD hammering or retry churn introduced
- [ ] No second hardware controller/watchdog introduced

## Notes

Anything reviewers should know.
