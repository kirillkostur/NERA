# NERA Sprint Finalization Backlog

Updated: 2026-08-25

This file is the working source of truth for closing Sprint 01-10 in order.
Status values: `DONE`, `PARTIAL`, `TODO`, `BLOCKED`.

Definition of Done and the milestone order are documented in
`First_Playable_Status_and_Roadmap_2026-08-04.md`. Текущий проверенный
технический baseline и приоритеты находятся в
`Current_Project_Audit_2026-08-25.md`.

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
- DONE: data-driven Expedition 01 quest stages are persisted in save version 20.
- DONE: Dynamic Parkour Player and parkour surfaces are integrated into the
  production Player prefab and Expedition 01.
- PARTIAL: Expedition 01 currently contains a package-style parkour playground
  and `Map/TestRoom`, not a focused production route.
- DONE: persist consumed `WorldItem`, defeated `IOEnemyController` and enemy
  drops across Continue; full checkpoint snapshot restores them after death.
- PARTIAL: technical respawn/checkpoint flow and HUD message are implemented;
  health HUD, damage feedback and authored death screen remain.
- DONE: ProjectValidator checks missing/duplicate persistent IDs and the
  current enabled scenes contain no tracked scene instance with an empty ID.
- TODO: rerun the validator after the final Expedition 01 route is authored.
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
- DONE: save version 20 for quest progress/history, maintainable objects,
  checkpoint scene/authored spawn or player pose, and supported world-object
  state.
- DONE: compact HUD with highest-priority main and side quest objectives.
- PARTIAL: event-driven autosave has a 2-second debounce, 10-second dirty cap,
  transition/lifecycle flushes and rolling per-slot backups, but serialization
  and `File.WriteAllText` are synchronous on the main thread; the old
  `background writer` description was inaccurate.
- TODO: measure save spikes in WindowsPlayer and move file I/O behind a
  single-writer queue if the capture shows a player-visible stall.
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
- PARTIAL: checkpoint save/restore message is present; player health and damage
  feedback remain.
- DONE: configurable localized loading screen covers New Game, Continue,
  additive scene transitions and the complete death-to-revive interval; image
  and tip pools are independent and the default minimum display time is three
  seconds.
- TODO: objective and interaction result feedback pass.
- TODO: terminal NEW markers and state messages.
- TODO: Expedition route readability pass.
- TODO: planned VFX placeholders.
- TODO: planned SFX and ambience placeholders; no authored project audio assets
  were found during the repeated 2026-08-25 audit.
- TODO: internal playtest checklist.

## Sprint 08 - QA and Performance Baseline

Status: `PARTIAL`

- PARTIAL: Unity 6000.0.71f1 automated baseline is not deterministic. PlayMode
  passes `41/41`; the first full EditMode run under the Russian ambient locale
  passed 206 and failed 5, while a later English-state run passed `211/211`.
- TODO: isolate/restore locale in Quest, Laboratory and StationSystems EditMode
  fixtures, then pass two consecutive full runs under both RU and EN.
- PARTIAL: permanent editor validation command covers required scenes and
  station foundations, including production Company/Product identity, but
  additive validation from `Testing.unity` emits a Directional vs
  Non-Directional lightmap mode Console Error.
- TODO: make validator scene/lightmap checks isolated and state-restoring.
- TODO: inject synthetic StationSystemsConfig into controller tests instead of
  reading mutable production Resources assets.
- TODO: isolate every PlayMode fixture with an explicit clean scene and teardown.
- DONE: baked-lighting flow enters New Game through `MainMenuController` and
  verifies backup, normal and sandstorm/low-energy modes in Player_Station.
- PARTIAL: validator covers persistent IDs and core project structure; station
  upgrade graph and installed-visual purity remain candidates for expansion.
- DONE: authored maintainable initial condition is restored on New Game.
- DONE: installed upgrade visuals are collider-free at runtime.
- DONE: failed/same-scene transitions return explicit results and do not create
  false checkpoints.
- DONE: turret firing uses atomic `FiringEnergyPerShot` instead of a one-frame
  consumer state.
- DONE: invalid/incompatible restored parts are rejected and staged rollback
  cannot clear a part until inventory/storage accepts it; Return To Main Menu
  aborts instead of saving item loss.
- DONE: exact-energy session-end snapshot and regression coverage; pause,
  application quit and Return To Main Menu force a current-state save without
  turning continuous energy changes into periodic disk writes.
- DONE: Standalone Low, Medium and High URP quality preset baseline.
- DONE: IO target registry, projectile pooling and event-driven refresh for the
  main terminal/laboratory screens.
- DONE: reproducible Editor benchmark for Player_Station and Expedition_01;
  optimized result is the median of three runs. Station BehaviourUpdate is
  down 35.3% and GC 27.9%; Expedition BehaviourUpdate is down 15.1%.
- DONE: high-return runtime optimization pass over energy, lighting, fog,
  maintenance, enemies, interaction, terminal and cached cameras.
- ROLLED BACK: parkour-specific optimization from `3ac2afe`; original
  detection/query/update behaviour is restored. Dynamic Rigidbody
  interpolation from `7385c61` remains enabled and tested.
- DONE: last known Windows Development Build — 164.56 MB, 0 errors and
  0 warnings (2026-07-30).
- DONE: fresh First Playable Windows Development Build — 224.4 MB, 0 build
  errors, 34 warnings, four scenes, 343.37 seconds (2026-08-25).
- PARTIAL: fresh 20-second headless startup smoke initialized Mono, Input System
  and PhysX without a gameplay exception; it is not a Boot-to-gameplay pass.
- TODO: repair/reimport `LiberationSans SDF - Fallback.asset`; the successful
  build still emitted a NativeFormatImporter inconsistent-result Console Error.
- TODO: fix Shader Graph/fog compile diagnostics and verify the materials
  visually.
- TODO: rename the misleading lower-case `ClimbController.onAnimatorIK` helper
  and add an Animator IK integration regression (`UNT0033`).
- TODO: standalone full-flow QA.
- DONE: current bug list and priorities are recorded in the 2026-08-25 audit.
- PARTIAL: repeated Editor CPU/frame-time baseline and current build size are
  recorded; connected player GPU, reliable GC/RAM, loading and 1% low remain.
- TODO: hardware profiling and visual review of all three PC presets.
- TODO: First Playable technical report.

## Sprint 09 - First Playable Lock

Status: `TODO`

- TODO: close blocker and critical issues.
- TODO: remove/archive tracked `/profile.data` and add a precise ignore rule.
- TODO: remap production references away from AI Navigation/TMP sample assets,
  then remove confirmed unused DOTween/demo dependencies with a build after
  each package change.
- TODO: create and archive NERA_FP_LOCK_v0.1.0.
- TODO: release notes, known issues and lock checklist.

## Sprint 10 - Milestone 02 Foundation

Status: `PARTIAL`

Scope note: no further Sprint 10 feature/content expansion before Sprint 09
First Playable lock. Only compatibility fixes for already-added foundations are
in scope.

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

1. Restore a deterministic gate: locale-isolated EditMode tests, isolated
   validator, stable TMP fallback import, explicit parkour IK helper and clean
   Shader Graph/fog compilation.
2. Run two consecutive clean First Playable builds and preserve the reports.
3. Remove test content from Player Station and turn Expedition_01 from a
   parkour playground into one focused production blockout; add the demo-coda.
4. Add health HUD, combat/damage/death feedback and respawn/reload
   presentation; author the minimum audio/VFX/objective feedback kit.
5. Pass the complete Boot -> Station -> Expedition -> research/coda ->
   checkpoint/death -> Continue -> Return to Menu flow in WindowsPlayer.
6. Connect Unity Profiler to WindowsPlayer and capture
   CPU/GPU/GC/RAM/loading/1% low on High, with Medium/Low smoke passes.
7. Run an external playtest, close blocker/critical issues and create the First
   Playable lock.
8. After lock, migrate deprecated Cinemachine, split large controllers and
   remove confirmed dead/sample packages before expanding Expedition 02–08 or
   Unknown Signal content.
