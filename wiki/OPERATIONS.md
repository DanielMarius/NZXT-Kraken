# Operations

Use the [canonical operations guide](https://github.com/DanielMarius/NZXT-Kraken/blob/dev/docs/OPERATIONS.md).

Resolve the actual release from the quoted `KrakenSupervisor.PathName`, then read
its `runtime/health.json` and `logs/` files through the hidden/background launcher.
Check freshness, controller PID/start identity, RPM and duty; do not trust a stale
build folder or service state alone.

`--max-cooling` and `--reset-lcd` are exclusive hardware operations, not parallel
diagnostics. Preserve working cooling. Stop, uninstall and CAM-restoration helpers
require explicit authorization and a verified fallback/handoff plan.
