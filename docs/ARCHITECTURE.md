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
- tracks exact controller PID, start identity and executable
- retries transient health-read failures without refreshing their age
- restarts an exited controller only when the controller mutex is free
- reports live unhealthy cooling for operator attention; it does not kill it without proven fallback

## Control Loop

The controller loop:

- establishes 100% fixed cooling before optional telemetry/LCD initialization
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
- payload producer PID/start time matches the controller
- LCD-only failure does not make cooling unhealthy

Missing or invalid temperatures select 100% duty. Invalid/missing configuration
also selects full cooling. LCD errors use bounded retries/backoff and do not
take down otherwise working cooling. Only the controller accesses HID/WinUSB.

This avoids fake liveness from stale PID files.

## Runtime Files

- `runtime\state.json`
- `runtime\health.json`
- `runtime\kraken_lcd.png`
- `logs\controller.log`
- `logs\supervisor.log`
