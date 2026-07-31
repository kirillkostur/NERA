# NERA Sprint Finalization Backlog

Updated: 2026-07-30

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
- DONE: config-driven per-object battery cutoffs and terminal low-power lockout.
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
- DONE: research-capable Ancient Record and NERA Memory Core interaction
  prototypes.
- DONE: weak Blue IO enemy prototype with detection, pursuit and energy attack.
- DONE: Expedition objective progression controller.
- PARTIAL: visited, IO encounter, research sample and returned flags exist, but
  are Expedition 01-only and session-only.
- TODO: persist objective flags and hide consumed one-time content after scene
  reload/save load.
- TODO: replace the Blue IO placeholder mesh with the authored mesh + VFX prefab.
- TODO: final combat feedback, audio, balancing and player-facing health/objective HUD.

## Sprint 05 - Research and Library

Status: `PARTIAL`

- DONE: research IDs, states and controller.
- DONE: item analysis flow with per-instance scanned state.
- DONE: library entry IDs, unlock state and controller.
- DONE: Library UI entry selection and known-item catalogue.
- DONE: EditMode and PlayMode regression coverage.
- TODO: post-research objective hint.

## Sprint 06 - Persistent State and Save

Status: `PARTIAL`

- DONE: Boot main-menu scene and additive persistent MainScene runtime root
  without `DontDestroyOnLoad`.
- DONE: basic JSON save/load/reset.
- DONE: editor Save/Clear/Load menu.
- DONE: config-driven static/dynamic QuestController and Expedition 01 quest.
- DONE: save version 14 for quest progress/history and maintainable objects.
- DONE: compact HUD with highest-priority main and side quest objectives.
- TODO: unified GameSessionState.
- PARTIAL: SaveData mapping; one-time scene-object consumption remains.
- TODO: full terminal quest journal and objective notifications.
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

Status: `PARTIAL`

- DONE: automated baseline — 72 EditMode and 17 PlayMode tests pass on
  Unity 6000.0.71f1 (2026-08-01).
- DONE: permanent editor validation command for required scenes and station
  upgrade prefabs.
- DONE: Standalone Low, Medium and High URP quality preset baseline.
- DONE: IO target registry, projectile pooling and event-driven refresh for the
  main terminal/laboratory screens.
- DONE: current Windows Development Build — 164.56 MB, 0 errors and
  0 warnings (2026-07-30).
- TODO: standalone full-flow QA.
- TODO: bug list and priorities.
- TODO: FPS, frame-time, RAM, loading and build-size baseline.
- TODO: hardware profiling and visual review of all three PC presets.
- TODO: First Playable technical report.

## Sprint 09 - First Playable Lock

Status: `TODO`

- TODO: close blocker and critical issues.
- TODO: clean obsolete debug dependencies.
- TODO: create and archive NERA_FP_LOCK_v0.1.0.
- TODO: release notes, known issues and lock checklist.

## Sprint 10 - Milestone 02 Foundation

Status: `PARTIAL`

- DONE: stable string Location Id, serialized SceneReference, LocationType,
  LocationState and DiscoverySource.
- DONE: Expedition 02-08 and Unknown Signal placeholder configs/scenes.
- PARTIAL: Expeditions 02-08 are template duplicates and are not production
  content.
- DONE: Map/Locations presentation foundation.
- DONE: Research and Library collection foundations.
- DONE: AntennaState, calibration and maintenance flow.
- DONE: data-driven quest controller, signal model and persisted quest state.
- TODO: authored AreaExplored points and per-scene object state collection.
- PARTIAL: SaveData collection refactor (inventory instances, storage,
  laboratory, research, library, antenna and station systems are mapped;
  quest state and maintainable-object condition are mapped; one-time scene
  object consumption remains).
- TODO: First Playable compatibility validation.

## Current Order

1. Finalize Company/Product Name and choose a save-path migration/reset policy.
2. Add the terminal quest journal, objective notifications and authored
   Expedition 01 exploration points.
3. Persist one-time scene-object completion.
4. Quarantine or replace duplicated Expedition 02-08 template content.
5. Complete Expedition 01 presentation and combat feedback from Sprint 04.
6. Record a current standalone full-flow and performance baseline.
7. Complete polish, QA and First Playable Lock.
8. Split the largest UI/save controllers behind focused regression tests.
