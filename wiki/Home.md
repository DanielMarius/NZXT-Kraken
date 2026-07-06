# Kraken Windows 10 Wiki

This wiki documents the C#-only Kraken controller and its Windows service supervisor.

## Pages

- [Install Guide](INSTALL)
- [Architecture](ARCHITECTURE)
- [Operations](OPERATIONS)
- [Troubleshooting](TROUBLESHOOTING)

## Core Rules

- one controller process only
- no Python runtime dependency
- the supervisor watches health but should not become a second hardware controller
- keep emergency paths simple: max cooling, LCD reset, process stop, optional CAM restore
