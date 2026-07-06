# Architecture

## Process Model

The design intentionally keeps hardware ownership simple.

- controller: `KrakenHost.exe`
- supervisor: `KrakenHost.exe --windows-service`

The controller is the only process that should read or write Kraken hardware during normal operation.

The supervisor does not run the main control loop. It only:

- checks whether a controller exists
- reads the health snapshot
- evaluates health rules
- invokes emergency max cooling on failure
- restarts the controller if needed

## Control Loop

The controller loop:

- reads CPU temperature from LibreHardwareMonitor
- reads GPU temperature through NVML
- reads liquid temperature plus current RPM from the Kraken
- computes a duty target
- applies fan and pump duty
- renders the LCD layout
- pushes the LCD frame under the configured throttling rules
- writes runtime state and health snapshots

## Health Model

Health is determined from live controller output:

- fresh `health.json`
- `Status == ok`
- pump RPM >= `1500`
- fan RPM >= `1000`
- LCD status not reporting repeated upload failure

This avoids fake liveness from stale PID files.

## Runtime Files

- `runtime\state.json`
- `runtime\health.json`
- `runtime\kraken_lcd.png`
- `logs\controller.log`
- `logs\supervisor.log`
