# NERA Main Menu Assembly

Updated: 2026-07-28

## Scene ownership

### Boot

Build index 0. Contains only the main-menu presentation and its local input:

- `MainMenuFlow` with `MainMenuController`;
- `MainMenuCamera`;
- `Directional Light`;
- `EventSystem`;
- the authored menu Canvas, background and animation.

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

Select `MainMenuFlow/MainMenuController` and assign:

- `New Game Button`;
- `Continue Button`;
- `Exit Button`.

The controller binds these buttons automatically. Do not also add duplicate
OnClick entries for them.

Additional settings buttons can call these public methods through OnClick:

- `SetLowQuality`;
- `SetMediumQuality`;
- `SetHighQuality`.

`Continue` is disabled automatically when
`SaveGameController.DefaultSavePath` does not exist.

## Runtime flow

```text
Application start
  -> Boot
     -> New Game
        -> MainScene (additive, becomes runtime owner)
           -> clear save and reset runtime state
           -> unload Boot
           -> Player_Station (additive, becomes active scene)
     -> Continue
        -> MainScene (additive, becomes runtime owner)
           -> load save
           -> unload Boot
           -> Player_Station (additive, becomes active scene)

Gameplay
  -> MainScene remains loaded
  -> unload current content scene
  -> load target Station/Expedition scene additively
  -> target content scene becomes active
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
