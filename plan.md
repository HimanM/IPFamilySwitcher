# IP Family Switcher Implementation Plan

## Delivery rules

- [x] Create a .NET 10 WPF Windows x64 solution scaffold.
- [x] Keep implementation changes small and commit each coherent phase.
- [x] Update this checklist in the same commit as every implementation phase.
- [ ] Validate each phase with the smallest relevant build or test command.
- [ ] Use descriptive commit messages with the required Copilot co-author trailer.

## Phase 1 - Foundation

- [x] Create the solution and WPF application project.
- [x] Target `net10.0-windows` with WPF enabled and nullable reference types.
- [x] Add application identity, administrator manifest, and publish settings.
- [x] Add shared domain models and deterministic rule naming.

## Phase 2 - Configuration and safety

- [x] Add versioned configuration schema for managed applications.
- [x] Add canonical `.exe` path validation and case-insensitive duplicate detection.
- [x] Add atomic configuration writes, backup recovery, and logging.
- [ ] Add unit tests for serialization, migration, paths, and rule names.

## Phase 3 - Firewall ownership and reconciliation

- [ ] Implement a native Windows Firewall service scoped to the managed group and app GUID.
- [ ] Create, validate, enable, disable, remove, and enumerate managed rules.
- [ ] Implement safe mode transitions with conflict removal and verification.
- [ ] Implement startup reconciliation, missing/incorrect/disabled/orphan states, and repair.
- [ ] Add firewall service tests with clearly separated administrator-required integration coverage.

## Phase 4 - Core application workflow

- [ ] Add executable selection, metadata/icon lookup, add/remove, locate, and edit workflows.
- [ ] Add per-application mode and rule enabled-state operations.
- [ ] Add global enable/disable and remove-all behavior with confirmation.
- [ ] Add privilege detection and understandable user-facing errors.

## Phase 5 - WPF interface

- [ ] Build the main dashboard with summary counts, search, and application rows.
- [ ] Display status text and accessible indicators for all reconciliation states.
- [ ] Add repair, refresh, and confirmation dialogs.
- [ ] Add advanced details without cluttering the primary workflow.

## Phase 6 - Optional v1.1 features

- [ ] Add connection inspection using Windows TCP/UDP tables.
- [ ] Add running-process status and connection verification.
- [ ] Add settings, logging toggle, and safe startup preferences.
- [ ] Add system tray support only if elevation/startup behavior remains safe.
- [ ] Add import/export and executable hash information.

## Phase 7 - Release

- [ ] Add release documentation and manual test checklist.
- [ ] Add self-contained `win-x64` publish configuration.
- [ ] Add installer/portable packaging without touching unmanaged firewall rules.
- [ ] Run build, unit tests, publish verification, and final manual safety review.
