# NERA Current Project Audit

Date: 2026-07-30  
Unity: 6000.0.71f1  
Target: StandaloneWindows64

## Readiness summary

- Technical foundation: `READY FOR CONTINUED DEVELOPMENT`.
- Automated regression baseline: `GREEN`.
- First Playable lock: `NOT READY`.
- Gameplay progression beyond Expedition 01: `BLOCKED` by legacy progression
  and missing per-scene objective/save data.
- Release packaging: `NOT READY`.

The project is stable enough to continue development, but it should not be
called a locked First Playable yet. The remaining work is not a compiler or
scene-loading problem; it is progression persistence, authored content,
player-facing feedback and standalone QA.

## Verified baseline

- Git working tree was clean before this audit.
- Unity Console contained no errors or warnings.
- After the current changes, Unity Test Framework passes 72 EditMode and 17
  PlayMode tests.
- Twelve scenes are enabled in Build Settings: Boot, MainScene, Player Station,
  Expeditions 01-08 and Unknown Signal 01.
- `NERA -> Validate Project` passes through the EditMode regression suite.
- A current Windows Development Build succeeds with 0 errors and 0 warnings:
  `Builds/Verification_2026-07-30/NERA.exe`, 164.56 MB.
- Boot contains only the authored menu, camera, light, EventSystem and Canvas.
- MainScene owns the persistent additive `RuntimeRoot`, Player, gameplay camera,
  HUD, input and runtime services.
- Save data is version 14. Inventory instances, station storage, laboratory
  slots, antenna state, energy, research, Library and station systems are
  mapped. Active quests, condition progress, completion history and
  maintainable-object condition are also persisted.
- Low, Medium and High Standalone URP assets match
  `PC_Quality_Presets.md`.

## Documentation reconciliation

The current pipeline is:

`Boot menu -> MainScene/RuntimeRoot (additive) -> one active content scene`

`RuntimeRoot` belongs to MainScene, not Boot, and is never moved to
`DontDestroyOnLoad`.

The permanent validator checks:

- the fixed build prefix: Boot, MainScene and Player Station;
- every `ExpeditionLocationData` scene reference, spawn point, unique Location
  Id, discovery type and Map Slot;
- registration of all nine location configs and authored 3D map slots;
- four station upgrade prefabs;
- Low, Medium and High PC quality assets.
- the default quest catalog and the persistent `QuestController` registration.

The old audit statement about “six required scenes” was no longer accurate.
All configured location scenes are validated through their data assets instead
of a fixed six-scene list.

## Safe code corrections made in this audit

### Data-driven quest runtime

The Expedition 01-only `ExpeditionProgressController` and its four transient
booleans were removed. `QuestController` now consumes typed gameplay signals,
loads ScriptableObject definitions, supports static and dynamic instances,
prevents duplicates and exposes one event/model surface. The authored
`HUD_Canvas/Quest_System` now shows the highest-priority active main and side
quest through `QuestHUDController`; full terminal presentation is still pending.

`Main_Expedition01`, `Side_CleanSolarPanel` and `Side_RestoreTurret` are authored
configs. Save version 14 persists active stages, condition progress, pending
activation progress, completion history and maintainable-object condition.

### Station upgrade lifecycle

`StationUpgradeStageController` previously compared the station-system
singleton every frame only to detect a replacement instance. It now subscribes
to an explicit `InstanceChanged` lifecycle event and still receives normal
`SystemsChanged` updates. Late binding and stage switching have regression
coverage.

Stage-owned runtime sources must use one stable device ID across their visual
levels. When an active stage changes, `StationBattery` now re-registers its
authored capacity immediately. Updating an existing battery ID replaces its
capacity and new-game initial charge without duplicating the source or adding
free energy to the current session.

## Remaining correctness findings

### 1. One-time world content can reappear

`WorldItem` destroys or disables only its current scene instance.
`PlayerInventory` allows multiple instances with the same item ID, and no
saved scene-object consumption registry is applied when a content scene is
loaded.

The backlog claim that all one-time Expedition 01 content is hidden after
reload was not supported by the current runtime code. Add stable scene object
IDs plus saved consumed/completed state before relying on collectible
persistence.

### 2. Expeditions 02-08 are template duplicates, not authored levels

The seven scene files have the same structure and prefab set; comparing
Expedition 02 and 03 produces only two changed lines: scene spawn name and
spawn ID. Each currently contains the Expedition 02 research items, weak Blue
IO and the Expedition 01 return prefab.

Keep them as templates if useful, but do not count them as completed content or
run progression through them until location-specific objectives and unique
content are authored.

### 3. The shared return prefab is Expedition 01-specific

`Prefabs/Managers/Expedition_To_Station_Exit.prefab` targets
`Station_ReturnFromExpedition01`. It is safe as the Expedition 01 exit, but it
is not a generic return prefab for other locations.

Create location-specific variants or make target spawn/progression data-driven
before production use in Expeditions 02-08 and Unknown Signal.

## High-priority production findings

### Player settings and save-path risk

`ProjectSettings.asset` still uses:

- Company Name: `DefaultCompany`;
- Product Name: `My project`;
- Version: `0.1.0`.

Changing Company or Product Name changes `Application.persistentDataPath` on
Windows, so existing saves can appear to disappear. Finalize these values now
and either accept a pre-release save reset or implement a one-time migration
from the old path.

### Standalone verification

The source state now has a successful Windows Development Build. This confirms
buildability, but it does not replace a manual full gameplay pass or a
performance capture on target hardware.

### Player-facing completion

The following remain necessary for a First Playable:

- Expedition 01 combat feedback, player health HUD, audio and balancing;
- post-research objective hand-off;
- save/load verification across collect -> return -> analyze -> reload;
- objective/result feedback and terminal state polish;
- route readability and an internal playtest checklist.

## Code and performance follow-up

### Large controllers

The highest-risk files by size are:

- `InventoryLabHUDController` — 1184 lines;
- `TerminalStationScreenController` — 1015 lines;
- `LaboratoryScreenController` — 964 lines;
- `SaveGameController` — 863 lines;
- `StationSystemsController` — about 784 lines.

Do not split them mechanically. First add focused tests around hierarchy
binding, navigation and save mapping, then extract rendering, input and domain
actions one responsibility at a time.

### Repeated hierarchy fallback

Terminal and laboratory screens expose serialized references but retain
recursive/name-based fallback lookup. Finish assigning references in authored
prefabs, add validator coverage and only then remove the fallback.

### Asset size

- Fourteen player animation FBX files total about 471.34 MB and average
  33.67 MB. They appear to carry repeated character payloads. Re-export
  animation-only clips or use a shared avatar/mesh, then compare curves and
  root motion before replacing sources.
- `Assets/Samples` adds 212 files / about 4.03 MB.
- `TextMesh Pro/Examples & Extras` adds 284 files / about 6.35 MB.

Remove samples only after a GUID reference scan. Package removal and animation
replacement are intentionally deferred because they can break serialized
references.

## Recommended next order

1. Finalize Company/Product Name and decide save-path migration/reset policy.
2. Generalize location/objective progression and add saved scene-object state.
3. Replace or clearly quarantine duplicated Expedition 02-08 template content.
4. Complete Expedition 01 presentation, quest notifications, terminal journal
   and post-research flow.
5. Run a fresh Development Build, full-flow QA and Low/Medium/High profiling.
6. Fix blocker/critical issues and create the First Playable lock.
7. Refactor the largest UI/save controllers only behind focused regression
   tests.
