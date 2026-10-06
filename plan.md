# IP Family Switcher Implementation Plan

## Delivery rules

- [x] Create a .NET 10 WPF Windows x64 solution scaffold.
- [x] Keep implementation changes small and commit each coherent phase.
- [x] Update this checklist in the same commit as every implementation phase.
- [x] Validate each phase with the smallest relevant build or test command.
- [x] Use descriptive commit messages with the required Copilot co-author trailer.

## Phase 1 - Foundation

- [x] Create the solution and WPF application project.
- [x] Target `net10.0-windows` with WPF enabled and nullable reference types.
- [x] Add application identity, administrator manifest, and publish settings.
- [x] Add shared domain models and deterministic rule naming.

## Phase 2 - Configuration and safety

- [x] Add versioned configuration schema for managed applications.
- [x] Add canonical `.exe` path validation and case-insensitive duplicate detection.
- [x] Add atomic configuration writes, backup recovery, and logging.
- [x] Add unit tests for serialization, migration, paths, and rule names.

## Phase 3 - Firewall ownership and reconciliation

- [x] Implement a native Windows Firewall service scoped to the managed group and app GUID.
- [x] Create, validate, enable, disable, remove, and enumerate managed rules.
- [x] Implement safe mode transitions with conflict removal and verification.
- [x] Implement startup reconciliation, missing/incorrect/disabled/orphan states, and repair.
- [x] Preserve disabled restricted-mode rules so reconciliation reports `Disabled` instead of `Missing`.
- [x] Add firewall service tests with clearly separated administrator-required integration coverage.

## Phase 4 - Core application workflow

- [x] Add executable selection, add/remove, locate, and edit workflows.
- [x] Add per-application mode and rule enabled-state operations.
- [x] Add global enable/disable and remove-all behavior with confirmation.
- [x] Add privilege detection and understandable user-facing errors.

## Phase 5 - WPF interface

- [x] Build the main dashboard with summary counts, search, and application rows.
- [x] Display status text and accessible indicators for all reconciliation states.
- [x] Add repair, refresh, and confirmation dialogs.
- [ ] Add advanced details without cluttering the primary workflow.

## Phase 6 - Optional v1.1 features

- [ ] Add connection inspection using Windows TCP/UDP tables.
- [ ] Add running-process status and connection verification.
- [ ] Add settings, logging toggle, and safe startup preferences.
- [ ] Add system tray support only if elevation/startup behavior remains safe.
- [ ] Add import/export and executable hash information.

## Phase 7 - Release

- [x] Add release documentation and manual test checklist.
- [x] Add self-contained `win-x64` publish configuration.
- [x] Add portable packaging without touching unmanaged firewall rules.
- [x] Run the automated build, unit tests, and self-contained publish verification.
- [ ] Run the live elevated Windows Firewall manual safety review (blocked: current shell is not administrator).

## Verification notes

- Automated tests: 10 passed.
- Release build: passed with 0 warnings and 0 errors.
- Self-contained `win-x64` publish: passed.
- Portable archive: `IPFamilySwitcher-Portable-x64.zip`.
- Live firewall create/remove verification must be run from an elevated Administrator shell.
