# NERA Sprint Finalization Backlog

Updated: 2026-08-14

This file is the working source of truth for closing Sprint 01-10 in order.
Status values: `DONE`, `PARTIAL`, `TODO`, `BLOCKED`.

Definition of Done and the milestone order are documented in
`First_Playable_Status_and_Roadmap_2026-08-04.md`. Текущий проверенный
технический baseline и приоритеты находятся в
`Current_Project_Audit_2026-08-14.md`.

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
- DONE: physical device service/manual restart shares requested state with the
  terminal and resolves maintainable objects by stable ObjectId.
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
- DONE: data-driven Expedition 01 quest stages are persisted in save version 19.
- DONE: Dynamic Parkour Player and parkour surfaces are integrated into the
  production Player prefab and Expedition 01.
- PARTIAL: Expedition 01 currently contains a package-style parkour playground
  and `Map/TestRoom`, not a focused production route.
- DONE: persist consumed `WorldItem`, defeated `IOEnemyController` and enemy
  drops across Continue; full checkpoint snapshot restores them after death.
- PARTIAL: technical respawn/checkpoint flow and HUD message are implemented;
  health HUD, damage feedback and authored death screen remain.
- TODO: assign authored `Persistent Id` values to every production item and
  enemy on the final Expedition 01 route.
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
- DONE: production identity — Company Name `Measured Field`, Product Name `Nera`.
- DONE: standard `Application.persistentDataPath` policy and safe migration
  chain from the previous Company/Product path into the production identity.
- DONE: Boot menu three-slot selection, slot dates/completion placeholders,
  overwrite confirmation, Options placeholder and Exit confirmation.
- DONE: pre-slot `nera_save.json` migration into `nera_save_1.json`; the selected
  slot is carried through Boot -> MainScene and owns all later autosaves.
- DONE: config-driven static/dynamic QuestController and Expedition 01 quest.
- DONE: save version 19 for quest progress/history, maintainable objects,
  checkpoint scene/authored spawn or player pose, and supported world-object
  state.
- DONE: compact HUD with highest-priority main and side quest objectives.
- DONE: simple event-driven background writer with 2-second debounce,
  10-second dirty cap, transition/lifecycle flushes and rolling per-slot
  backups.
- DONE: separate full checkpoint snapshot per slot, checkpoint scene with an
  authored spawn or dynamic player pose, Continue resume and death rollback.
- DONE: Expedition 01 and Expedition 02 start-spawn checkpoint triggers and
  brief authored checkpoint HUD indicator.
- DONE: individual quest stages can opt into a full checkpoint after completion;
  `PlayerCheckpointTrigger` exposes the same operation to authored UnityEvents.
- PARTIAL: supported world state covers scene `WorldItem`, Blue IO enemies,
  their drops and boolean `PersistentWorldFlag` objects; multi-state objects
  still require explicit integration.
- TODO: full terminal quest journal and objective notifications.
- DONE: scene bootstrap applies persisted item/enemy state.
- TODO: full First Playable Continue/death rollback verification in a Windows
  build.

## Sprint 07 - UX, Audio and VFX Polish

Status: `PARTIAL`

- DONE: standardized Press/Hold prompt.
- DONE: station power lighting feedback.
- DONE: Player death ragdoll foundation and automatic checkpoint restore/revive.
- PARTIAL: parkour integration has automated coverage but still needs a manual
  production-route regression pass.
- PARTIAL: checkpoint save/restore message is present; player health, damage
  feedback and authored death screen remain.
- TODO: objective and interaction result feedback pass.
- TODO: terminal NEW markers and state messages.
- TODO: Expedition route readability pass.
- TODO: planned VFX placeholders.
- TODO: planned SFX and ambience placeholders; no authored project audio assets
  were found during the 2026-08-04 audit.
- TODO: internal playtest checklist.

## Sprint 08 - QA and Performance Baseline

Status: `PARTIAL`

- PARTIAL: current Unity 6000.0.71f1 baseline — 133/139 EditMode and 21/24
  PlayMode. One PlayMode failure is order-dependent and passes alone; the
  remaining failures are stale production-config assumptions, slot drift and
  one missing localization entry. Full details are in the 2026-08-14 audit.
- DONE: permanent editor validation command for required scenes and station
  foundations, including production Company/Product identity.
- TODO: inject synthetic StationSystemsConfig into controller tests instead of
  reading mutable production Resources assets.
- TODO: isolate every PlayMode fixture with an explicit clean scene and teardown.
- TODO: add station upgrade graph, installed-visual purity, localization and
  persistent-ID checks to ProjectValidator.
- TODO: add regression tests for authored turret initial condition, failed
  scene transition, staged-part teardown recovery, invalid installed-part
  restore and turret per-shot energy at different FPS.
- DONE: exact-energy session-end snapshot and regression coverage; pause,
  application quit and Return To Main Menu force a current-state save without
  turning continuous energy changes into periodic disk writes.
- DONE: Standalone Low, Medium and High URP quality preset baseline.
- DONE: IO target registry, projectile pooling and event-driven refresh for the
  main terminal/laboratory screens.
- DONE: last known Windows Development Build — 164.56 MB, 0 errors and
  0 warnings (2026-07-30).
- TODO: new Windows Development Build after the 2026-08-01 — 2026-08-04
  parkour and Player changes.
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
  content. Expedition 03-08 currently differ from Expedition 02 only by spawn
  name and ID.
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

1. Freeze First Playable scope to Boot, MainScene, Player Station and
   Expedition 01; create a dedicated build profile and deterministic
   Addressables build.
2. Restore a fully green, isolated EditMode/PlayMode baseline and expand
   ProjectValidator over the station/content graph.
3. Fix authored maintainable initial conditions, stable persistent IDs and
   scene-transition failure handling.
4. Make installed upgrade visuals collider-free and guarantee staged-part
   recovery on ESC, scene unload and application quit.
5. Make turret firing energy independent of FPS.
6. Add health HUD, damage/death feedback and respawn/reload flow.
7. Remove test content from Player Station and turn Expedition 01 from a
   parkour playground into a focused production blockout.
8. Add the post-research coda, objective notifications and minimum quest
   history/journal presentation.
9. Complete combat, audio, VFX and route-readability feedback.
10. Build and pass the current standalone full-flow and performance baseline.
11. Close blocker/critical issues and create the First Playable lock.
12. Only then remove confirmed dead/demo dependencies and split large
   controllers behind focused regression tests.
13. Only then author Expedition 02-08 or expand Unknown Signal content.
