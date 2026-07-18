# NERA Sprint Finalization Backlog

Updated: 2026-07-17

This file is the working source of truth for closing Sprint 01-10 in order.
Status values: `DONE`, `PARTIAL`, `TODO`, `BLOCKED`.

## Sprint 01 - Project Foundation

Status: `DONE`

- DONE: project folder structure.
- DONE: Boot, Station, Expedition and interaction test scenes.
- DONE: player movement and camera foundation.
- DONE: Press and Hold interaction flow with HUD prompt.
- DONE: scene transition prototype and spawn points.
- DONE: project tags and layers.
- DONE: runtime assembly definition.
- DONE: EditMode smoke tests.
- DONE: standalone Windows development build.
- DONE: Sprint 01 result notes.
- TODO: optional manual player-facing smoke pass of the archived build.

## Sprint 02 - Station Power and Terminal

Status: `DONE`

- DONE: station power state and restore interaction.
- DONE: offline and online visual switching.
- DONE: terminal access rules and terminal input mode.
- DONE: Status, Drone, Map and Library terminal panels.
- DONE: Station power EditMode regression tests.
- DONE: standalone build verification as part of the Sprint 01-02 baseline build.
- DONE: Sprint 02 result notes recorded in this backlog.
- TODO: optional manual terminal input-mode smoke pass in the standalone build.

## Sprint 03 - Drone and Expedition Unlock

Status: `DONE`

- DONE: Expedition 01 config and discovery.
- DONE: Map sector reveal and travel.
- DONE: Station to Expedition and return scene flow.
- DONE: DroneState state machine.
- DONE: timed scan and structured scan result.
- DONE: scanning percentage and completion feedback.
- DONE: Launch button state rules.
- DONE: discovery integration with the existing Map.
- DONE: EditMode regression tests.
- DONE: Windows development build verification.
- TODO: optional manual UI feel pass for scan duration and wording.

## Sprint 04 - Expedition 01 Gameplay Content

Status: `PARTIAL`

- DONE: Expedition 01 scene, spawn and return transition.
- DONE: basic collectible prototype.
- DONE: Ancient Record interaction and persistent state.
- DONE: NERA Memory Core hold interaction and persistent state.
- DONE: weak Blue IO enemy prototype with detection, pursuit and energy attack.
- DONE: Expedition objective progression controller.
- DONE: visited and returned state flags.
- DONE: hide completed one-time content after scene reload.
- TODO: replace the Blue IO placeholder mesh with the authored mesh + VFX prefab.
- TODO: final combat feedback, audio, balancing and player-facing health/objective HUD.

## Sprint 05 - Research, Library and Translation

Status: `TODO`

- TODO: research IDs, states and controller.
- TODO: Memory Core analysis flow.
- TODO: library entry IDs, unlock state and controller.
- TODO: Library UI entry selection.
- TODO: translation level and controller.
- TODO: post-research objective hint.

## Sprint 06 - Persistent State and Save

Status: `PARTIAL`

- DONE: persistent Boot runtime root.
- DONE: basic JSON save/load/reset.
- DONE: editor Save/Clear/Load menu.
- TODO: unified GameSessionState.
- TODO: complete SaveData mapping.
- TODO: ObjectiveController.
- TODO: scene bootstrap state application.
- TODO: full First Playable save/load verification.

## Sprint 07 - UX, Audio and VFX Polish

Status: `PARTIAL`

- DONE: standardized Press/Hold prompt.
- DONE: station power lighting feedback.
- TODO: objective and interaction result feedback pass.
- TODO: terminal NEW markers and state messages.
- TODO: Expedition route readability pass.
- TODO: planned VFX placeholders.
- TODO: planned SFX and ambience placeholders.
- TODO: internal playtest checklist.

## Sprint 08 - QA and Performance Baseline

Status: `TODO`

- TODO: Editor and standalone full-flow QA.
- TODO: bug list and priorities.
- TODO: FPS, frame-time, RAM, loading and build-size baseline.
- TODO: LOW preset review.
- TODO: First Playable technical report.

## Sprint 09 - First Playable Lock

Status: `TODO`

- TODO: close blocker and critical issues.
- TODO: clean obsolete debug dependencies.
- TODO: create and archive NERA_FP_LOCK_v0.1.0.
- TODO: release notes, known issues and lock checklist.

## Sprint 10 - Milestone 02 Foundation

Status: `PARTIAL`

- DONE: LocationId, LocationType, LocationState and DiscoverySource.
- DONE: Expedition 02 and Unknown Signal placeholder configs.
- DONE: Map/Locations presentation foundation.
- TODO: LocationController and state collection.
- TODO: Research and Library collection foundations.
- TODO: AntennaState and locked terminal placeholder.
- TODO: SaveData collection refactor.
- TODO: First Playable compatibility validation.

## Current Order

1. Finish Sprint 01 verification.
2. Finish Sprint 02 build verification.
3. Implement the missing Sprint 03 drone state flow.
4. Complete Expedition 01 gameplay content from Sprint 04.
5. Implement Sprint 05 Research, Library and Translation.
6. Consolidate state and save in Sprint 06.
7. Complete polish, QA and First Playable Lock.
8. Return to the remaining Sprint 10 foundation.
