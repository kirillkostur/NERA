# NERA Main Menu Assembly

Updated: 2026-08-25

## Scene ownership

### Boot

Build index 0. Contains only the main-menu presentation and its local input:

- `MainMenuFlow` with `MainMenuController`;
- `MainMenuCamera`;
- `Directional Light`;
- `EventSystem`;
- the authored menu Canvas, background and animation.
- `TestStation` and `Plane` for the 3D menu presentation;
- `VirtualCam/VirtualCam_01` and `VirtualCam_02`.

Do not put Player, GameplayHUD, save/progression services or station systems in
Boot.

### MainScene

Build index 1. Contains the single runtime `RuntimeRoot`:

- Player and gameplay input;
- Player camera and AudioListener;
- GameplayHUD;
- gameplay EventSystem;
- save, progression, energy, research, inventory and station services.

`MainMenuController` loads MainScene additively. `BootInitializer` initializes
the selected session, unloads Boot and then loads `Player_Station` additively.
MainScene itself stays loaded for the entire gameplay session; RuntimeRoot is
never moved to `DontDestroyOnLoad`.

## Wiring the authored Boot UI

`MainMenuController` resolves the authored controls under `Canvas/Panel`
automatically. Its serialized root-button references remain optional. Keep these
stable object names:

- `RootButton/background_button/NewGameButton`;
- `RootButton/background_button/ContinueButton`;
- `RootButton/background_button/OptionsButton`;
- `RootButton/background_button/ExitButton`;
- `ContinueScreen/background_Screen_station`;
- `OptionsScreen/background_Screen_station`;
- `ExitScreen/background_exit`.

Each `Panel_Save_1..3` must contain an `Image` and a `Button`, plus the authored
`#_Text`, `Data_Text` and `Complete_Text` children. The controller binds all
buttons automatically. Do not add duplicate persistent OnClick entries.

Additional settings buttons can call these public methods through OnClick:

- `SetLowQuality`;
- `SetMediumQuality`;
- `SetHighQuality`.

Menu behavior:

- root `ContinueButton` opens `ContinueScreen`; only occupied slots can be
  selected;
- root `NewGameButton` opens the same screen; all three slots can be selected;
- confirming New Game in an occupied slot opens `background_overwrite_slot`;
- overwrite `YES` starts a reset in that slot; `NO` closes the warning and
  clears the selection;
- `CloseButton` in Continue and Options restores `RootButton`;
- root `OptionsButton` opens the placeholder `OptionsScreen`; its
  `ContinueButton` is reserved for future settings confirmation;
- root `ExitButton` opens `ExitScreen`; `YES` exits and `NO` closes the warning.

## Boot camera switching

`MainMenuController` switches only Cinemachine priority:

- `VirtualCam_01`: priority `10` on RootButton, Options and Exit;
- `VirtualCam_02`: priority `10` on the New Game/Continue slot screen;
- inactive camera: priority `0`;
- Close/Back returns priority to `VirtualCam_01`.

The cameras may be assigned to `Root Menu Camera` and `Save Slot Camera` on
`MainMenuFlow`. If references are empty, the controller resolves them by the
stable paths under `VirtualCam`.

Camera interpolation does not belong to `MainMenuController`. It is configured
on `MainMenuCamera/CinemachineBrain` through
`Resources/MainMenuCamera Custom Blends.asset`. The current custom blend uses
2 seconds. Change the transition duration in that asset; do not add another
lerp or coroutine to the menu code.

Automated tests should open the menu through `MainMenuController` or use a
recursive button helper. The pre-3D-menu path `RootButton/NewGameButton` is no
longer valid.

## Save location and migration policy

Production identity is fixed to:

- Company Name: `Measured Field`;
- Product Name: `Nera`.

The save stays under Unity's standard `Application.persistentDataPath`. On
Windows uses three numbered files:

- slot 1:
  `%USERPROFILE%\AppData\LocalLow\Measured Field\Nera\nera_save_1.json`;
- slot 2:
  `%USERPROFILE%\AppData\LocalLow\Measured Field\Nera\nera_save_2.json`;
- slot 3:
  `%USERPROFILE%\AppData\LocalLow\Measured Field\Nera\nera_save_3.json`;
- pre-slot legacy:
  `%USERPROFILE%\AppData\LocalLow\Measured Field\Nera\nera_save.json`;
- previous-identity legacy:
  `%USERPROFILE%\AppData\LocalLow\DefaultCompany\My project\nera_save.json`.

Slots have a fixed one-to-one mapping to these files and are never sorted or
compacted in the menu. A session started or continued from slot 2 keeps slot 2
active for every manual save, autosave and save-on-exit; slots 1 and 3 are not
touched. The same rule applies independently to every slot. New Game deletes
an occupied slot only after the player selects that exact slot and confirms
the overwrite warning.

Every slot also owns rolling current-state backups
`nera_save_N.backup_1.json` through `backup_3.json` and a separate full
checkpoint snapshot `nera_save_N.checkpoint.json` with one checkpoint backup.
These files are never shown as menu slots and never move or renumber the
selected slot. On load, a corrupt or missing primary falls back to the newest
valid backup. Clearing a slot removes only that slot's current state,
checkpoint, temporary files and backups.

The pre-slot single save is migrated once into slot 1. The copy is written to a
temporary file and size-checked before it becomes the slot file; only then is
the source removed. An existing slot 1 always wins and is never overwritten.
The migration marker prevents an ignored legacy file from resurrecting after a
later slot reset.

`Data_Text` displays the file modification date as `dd.MM.yyyy - H:mm`.
`Complete_Text` currently reads the stored `completionPercent`, which defaults
to `0%`; quest-derived completion calculation is intentionally deferred.

For development builds, the Unity Editor menu exposes selective cleanup under
`Project > Save > Clear`: `Slot 1`, `Slot 2`, `Slot 3`, or `All Slots`. Every
destructive action requires confirmation. Clearing an inactive slot during
Play Mode leaves the current session untouched; clearing the active slot or all
slots also resets current runtime progress.

`AutoSaveService` on `MainScene/RuntimeRoot` writes changed gameplay state in
the background. `CheckpointService` owns safe spawn snapshots and death
rollback. There is no separate settings window; authored points use
`AutoSaveCheckpoint`. See `Autosave_System_Guide.md`.

## Runtime flow

```text
Application start
  -> Boot
     -> New Game
        -> ContinueScreen -> select slot -> Continue
           -> occupied slot -> overwrite YES/NO
        -> MainScene (additive, becomes runtime owner)
           -> activate selected slot
           -> clear selected slot and reset runtime state
           -> unload Boot
           -> Player_Station (additive, becomes active scene)
     -> Continue
        -> ContinueScreen -> select occupied slot -> Continue
        -> MainScene (additive, becomes runtime owner)
           -> activate selected slot and load its current state
           -> unload Boot
           -> load the scene and spawn stored by the last checkpoint

Gameplay
  -> MainScene remains loaded
  -> unload current content scene
  -> load target Station/Expedition scene additively
  -> target content scene becomes active
  -> successful scene transition creates a full checkpoint snapshot

Death
  -> load the selected slot checkpoint snapshot
  -> roll inventory and world state back to that snapshot
  -> force-reload the checkpoint scene and revive at its spawn
```

Station/Expedition transitions are routed through
`BootInitializer.LoadGameplayScene`. Do not call a Single-mode
`SceneManager.LoadScene` from gameplay code: that would unload MainScene.

For a future Pause/Exit-to-menu button, connect its OnClick to:

`RuntimeRoot -> BootInitializer -> ReturnToMainMenu`

This saves progress, unloads the current content scene, loads Boot additively,
makes Boot active and unloads MainScene.

## Duplication rules

- Boot and MainScene may overlap only while an asynchronous menu transition is
  in progress.
- Keep exactly one active AudioListener.
- Keep one EventSystem in Boot and one under RuntimeRoot; the Boot instance is
  unloaded before the runtime one becomes active in gameplay.
- Do not place a second Player, PlayerCamera or GameplayHUD in Station or
  Expedition scenes.
- Do not call `DontDestroyOnLoad` for MainScene objects.
