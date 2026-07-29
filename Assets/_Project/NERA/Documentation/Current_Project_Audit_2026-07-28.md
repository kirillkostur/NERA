# NERA Current Project Audit

Date: 2026-07-28  
Unity: 6000.0.71f1  
Target: StandaloneWindows64

## Verified baseline

- Git working tree was clean before the audit.
- Unity Console contained no errors or warnings.
- The current Unity Test Framework run passes 61 EditMode and 15 PlayMode
  tests. This includes the permanent project validator and data-driven terminal
  map-slot validation; all four generated C# projects compile.
- Twelve scenes exist and are enabled in Build Settings: Boot, MainScene,
  Player Station, Expeditions 01-08 and Unknown Signal 01.
- Research, Library, inventory instances, station storage, laboratory slots,
  antenna state, station energy and per-object station upgrades have automated
  regression coverage.

## Documentation reconciliation

`Sprint_Finalization_Backlog.md` was behind the implementation:

- Sprint 05 Research and Library controllers, IDs, UI selection and tests
  already exist.
- Sprint 08 already has a useful automated QA baseline, although standalone
  full-flow and performance measurements are still missing.
- Sprint 10 already contains Research/Library foundations and the Antenna state
  machine.
- Save data is version 12 and maps substantially more state than the old
  backlog described, but Expedition 01 objective flags are still not part of
  `SaveGameData`.

The warning in `Content_Assembly_Guide.md` remains current:
`ExpeditionProgressController` is still hard-coded to Expedition 01. Do not
connect Expedition 02 objectives to its Expedition 01 methods.

## Permanent validation promoted from tests

The structural checks that previously existed only in EditMode tests now also
have an editor command:

`NERA -> Validate Project`

It validates:

- all six required scenes exist, are enabled and keep the required order;
- the four authored station upgrade prefabs exist;
- every expected `Stage_N` child exists;
- system type, maximum stage and initial active stage match authored data.

The official `NERA -> Build -> Windows x64` command runs this validation before
building. The EditMode build-scene test also validates all six scenes and the
Boot/MainScene ordering.

## Runtime scene flow

- `Boot` is build index 0 and contains the authored main menu.
- `MainScene` owns RuntimeRoot, Player, PlayerCamera, GameplayHUD, EventSystem
  and progression/save services.
- New Game and Continue load MainScene additively with a one-shot launch
  request.
- RuntimeRoot remains owned by MainScene, initializes or resets the save,
  unloads Boot and loads `Player_Station` additively.
- Station and expedition transitions go through the central runtime loader:
  MainScene remains loaded while only the additive content scene is replaced.
- Returning to the menu saves progress, unloads content, loads Boot additively
  and then unloads MainScene.
- MainScene objects do not use `DontDestroyOnLoad`.
- Location configs use GUID-backed scene references selected from enabled Build
  Settings scenes. The terminal map uses data-driven `MapSlotData` references
  shared by configs and authored 3D `MapLocationSlot` objects. Slot count and
  object naming are not hardcoded; location IDs, slot IDs and references are
  validated for duplicates.

## Optimization opportunities

### Implemented safe architecture slice

- Station turrets now query the lifecycle-managed
  `IOEnemyController.ActiveEnemies` registry instead of performing a
  scene-wide enemy scan per turret.
- IO energy projectiles use a bounded `UnityEngine.Pool.ObjectPool` per
  projectile prefab. The pool belongs to the active content scene and is
  released with that scene. The same release lifecycle is ready for projectile
  VFX; standalone VFX pooling should be connected when authored repeated-effect
  prefabs exist.
- Terminal Map, Terminal Station and Laboratory screens subscribe to domain
  events only while visible. Their periodic data refresh polling was removed.
- The same screens expose their main hierarchy references as serialized fields.
  Existing scenes remain compatible through one-time fallback lookup, so the
  migration can be completed prefab by prefab.
- `PlayerFollowCamera` limits missing-player lookup retries to a serialized
  0.5-second interval and suppresses repeated warnings.
- Low, Medium and High Standalone URP presets are configured and documented in
  `PC_Quality_Presets.md`. Presets alter presentation only.

### High value, low gameplay risk

1. Continue splitting the largest UI controllers by responsibility. Current
   sizes include
   `InventoryLabHUDController` (1183 lines),
   `TerminalStationScreenController` (1015 lines) and
   `LaboratoryScreenController` (964 lines). Their hierarchy binding, input,
   rendering and domain actions should become separate components.
2. Finish assigning the new serialized UI references in authored prefabs, then
   remove the compatibility fallback lookups once the project validator checks
   those references.

### Performance and project-size follow-up

1. Fourteen player animation FBX files total about 471 MB and average about
   33.7 MB each. They appear to contain a repeated character payload. Re-export
   animation-only FBX files or use one shared avatar/mesh; verify clip curves
   before replacing sources.
2. Imported examples add editor/import overhead:
   `Assets/Samples` is about 4.0 MB / 212 files and
   `TextMesh Pro/Examples & Extras` is about 6.35 MB / 284 files.
   Remove them only after a GUID reference scan confirms authored content does
   not depend on them.
3. Extend pooling to authored muzzle flashes, impact VFX and repeated ambience
   once their production prefabs and peak concurrent counts are known.
4. `StationUpgradeStageController` polls the station-system singleton every
   frame only to detect rebinding. Replace this with an instance/lifecycle event
   when the persistent service layer is generalized.

## Test and temporary content findings

- Test Runner creates ignored `Assets/InitTestScene*.unity` files. They are
  generated artifacts, not content candidates, and must not be moved into
  production scenes.
- `_Development` contains no authored scripts to promote.
- The reusable part of the test suite is structural validation, now available
  as a permanent editor tool.
- The PlayMode runner changed `m_EnterPlayModeOptions` during this audit; the
  tracked project setting was restored to its original value after the run.

## Recommended next safe slice

Profile Low, Medium and High in a standalone Development Build, then assign the
new serialized references in the authored terminal/laboratory prefabs. Remove
fallback string lookup only after a manual UI pass and validator coverage.
