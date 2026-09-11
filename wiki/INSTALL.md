# Install Guide

Follow the [canonical install guide](https://github.com/DanielMarius/NZXT-Kraken/blob/dev/docs/INSTALL.md)
and [safety validation gates](https://github.com/DanielMarius/NZXT-Kraken/blob/dev/docs/SAFETY-VALIDATION.md).

v0.2 publishes `net10.0-windows`/`win-x64` as an untrimmed, non-single-file,
self-contained release. Stage into a new directory with isolated build outputs;
run the hardware-free `--self-test` hidden/background before live qualification.
Never overwrite or launch another hardware controller beside a running release.

The installer is for fresh installations only. An existing installation needs
explicitly authorized, verified fallback cooling, a single-owner handoff and
rollback. Do not remove a service or bypass an ownership guard to force installation.
