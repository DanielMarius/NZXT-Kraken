# Kraken v0.2 Wiki

Kraken is a C# cooling controller with a Windows service supervisor. The maintained
runbooks live with the source on `dev`; these wiki pages point to those documents
instead of duplicating instructions that can drift.

- [Install guide](https://github.com/DanielMarius/NZXT-Kraken/blob/dev/docs/INSTALL.md)
- [Architecture](https://github.com/DanielMarius/NZXT-Kraken/blob/dev/docs/ARCHITECTURE.md)
- [Operations](https://github.com/DanielMarius/NZXT-Kraken/blob/dev/docs/OPERATIONS.md)
- [Troubleshooting](https://github.com/DanielMarius/NZXT-Kraken/blob/dev/docs/TROUBLESHOOTING.md)
- [Local qualification and limits](https://github.com/DanielMarius/NZXT-Kraken/blob/dev/docs/QUALIFICATION-2026-09-11.md)

v0.2 uses a self-contained .NET 10 release. Keep one hardware controller, preserve
working cooling, and run tooling hidden/background. Tests and a running service
alone do not prove a safe hardware handoff. Windows 10 Pro local qualification
does not establish vendor support for that OS/runtime combination.
