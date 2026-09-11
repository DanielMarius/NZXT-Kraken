# Architecture

See the [canonical architecture](https://github.com/DanielMarius/NZXT-Kraken/blob/dev/docs/ARCHITECTURE.md).

In v0.2, one controller owns HID/USB and one supervisor reads producer-matched
health. Missing/invalid temperatures select full cooling. LCD-only failures are
contained and do not restart otherwise healthy cooling. The supervisor can
restart an exited controller when ownership is free; it does not kill a live
unhealthy controller on uncertain evidence.
