## Summary

Describe the change in 2-5 lines.

## What Changed

- 

## Verification

- [ ] `dotnet build .\KrakenHost.csproj -c Release`
- [ ] Manual controller run tested if runtime behavior changed
- [ ] Service install/reinstall tested if supervisor or scripts changed
- [ ] Checked `runtime\health.json`
- [ ] Checked `logs\controller.log` and `logs\supervisor.log`

## Hardware Safety Review

- [ ] Single controller ownership is preserved
- [ ] No Python runtime dependency added
- [ ] No LCD hammering / retry churn introduced
- [ ] No second hardware controller/watchdog introduced

## Notes

Include any migration notes, known limitations, or follow-up work.
