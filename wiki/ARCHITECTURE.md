# Architecture

## Process Model

There are two intended processes:

- `KrakenHost.exe`
- `KrakenHost.exe --windows-service`

The controller owns Kraken hardware access.

The supervisor checks controller health every 10 seconds and restarts it if needed.

## Health Model

Health requires:

- controller process exists
- fresh `health.json`
- `Status == ok`
- pump RPM >= `1500`
- fan RPM >= `1000`
- LCD status not reporting repeated upload failure

## Default Policy

- below `40C`: fan `80%`, pump `80%`
- at or above `40C`: fan `100%`, pump `100%`
- LCD minimum push interval: `2s`
