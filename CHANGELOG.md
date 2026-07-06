# Changelog

All notable changes to this project should be recorded in this file.

## 2026-07-06

- imported the Kraken C# controller into Git on branch `dev`
- added repo-ready `README.md` with install, service, and emergency usage
- added `docs/INSTALL.md`
- added `docs/ARCHITECTURE.md`
- added `docs/OPERATIONS.md`
- added `docs/TROUBLESHOOTING.md`
- added MIT license in `LICENSE.md`
- kept standard `LICENSE` file for host/repo license detection
- cleaned `.gitignore` so runtime artifacts and logs do not get committed

## 2026-06-24

- stabilized Kraken LCD upload path in C#
- removed Python runtime dependency from the controller path
- added service supervisor model with real health checks
- set fan and pump policy to `80%` below `40C`, `100%` at or above `40C`
- set LCD minimum push interval to `2s`
