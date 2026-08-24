# Освещение станции: baked presets и энергетические режимы

Updated: 2026-08-25

## Назначение

`SwitchBakedLights` переключает три набора запечённых lightmaps и три
независимых списка `Light` в зависимости от реального состояния станции.
Компонент работает в `Player_Station` и получает состояние из
`EnergySystemController` и `StationEnvironmentController`, принадлежащих
`MainScene`.

Игру для автоматической проверки нужно запускать через Boot/MainScene. При
прямом Play сцены Player_Station station controllers отсутствуют, поэтому
доступны только manual/debug режимы.

## Presets

В Inspector доступны три блока:

| Inspector block | Runtime mode | Назначение |
|---|---|---|
| `Normal Operation` | `Normal` | штатный свет станции |
| `Low Energy Warning` | `LowEnergyWarning` | низкий заряд или sandstorm |
| `Backup Power Emergency` | `BackupPowerEmergency` | батарея выключена либо main charge исчерпан |

Каждый блок содержит:

- `Lightmap Directions`;
- `Lightmap Colors`;
- `Light Sources`.

Поля независимы:

- пустой `Light Sources` не мешает смене baked lightmaps;
- пустой `Lightmap Colors` не мешает смене real lights;
- неполный `Lightmap Directions` включает режим non-directional и использует
  доступные color maps;
- `null` внутри массива color maps отменяет только смену карт этого preset, но
  его real lights всё равно переключаются.

## Приоритет выбора режима

Проверки выполняются в следующем порядке:

1. Если `Grid Enabled = false`, выбирается Emergency. Его lights включаются
   только при доступном backup reserve.
2. Если total capacity отсутствует, сохраняется Normal mode, но lights
   выключаются.
3. Если main charge равен нулю, выбирается Emergency; питание возможно от
   backup reserve.
4. Если активен sandstorm, выбирается Warning.
5. Если `Charge01` не выше
   `EnergyBalance_Default / Default Consumer Minimum Charge`, выбирается
   Warning.
6. Во всех остальных случаях выбирается Normal.

Текущее значение warning threshold — `25%`.

## Как назначать lightmaps

1. Геометрия и renderer hierarchy должны быть одинаковыми во всех трёх bake.
2. Для каждого состояния выполните отдельный bake и сохраните его textures.
3. Перенесите color maps в соответствующий `Lightmap Colors` в одном и том же
   порядке.
4. Если bake directional, перенесите соответствующие direction maps с тем же
   количеством и порядком элементов.
5. Назначьте real lights только в preset, которому они принадлежат.

Компонент не назначает lightmap отдельному Renderer. Unity Renderer уже хранит
`lightmapIndex` и scale/offset после bake. Скрипт находит первый используемый
индекс сцены Player_Station и заменяет соответствующий диапазон глобального
`LightmapSettings.lightmaps`, не затрагивая карты additive MainScene.

После каждого `sceneLoaded` индекс инвалидируется, а preset повторно
применяется через один frame. Пустые arrays не должны блокировать этот процесс.

## События и производительность

Освещение подписывается на:

- `EnergySystemController.InstanceChanged`;
- `EnergyChanged` активного energy controller;
- `StationEnvironmentController.InstanceChanged`;
- `EnvironmentChanged` активного environment controller;
- `SceneManager.sceneLoaded`.

Покадровой проверки энергии нет. `Update` используется для debug shortcuts и
редкой проверки отсутствующего controller с интервалом 0.5 s.

## Debug shortcuts

При включённом `Enable Keyboard Shortcuts`:

- `Ctrl+1` — Normal;
- `Ctrl+2` — Warning;
- `Ctrl+3` — Emergency;
- `Ctrl+0` — вернуть automatic station control.

Ручной режим остаётся активным до `Ctrl+0` или вызова
`ResumeAutomaticStationControl()`.

## Проверка полного flow

1. Запустить Boot и начать New Game.
2. До включения батареи проверить Emergency preset.
3. Включить батарею и проверить Normal preset.
4. Опустить main charge до `25%` и проверить Warning.
5. Начать sandstorm при нормальном заряде и проверить Warning.
6. Завершить sandstorm и проверить возврат Normal.
7. Обнулить main charge при наличии backup reserve и проверить Emergency.
8. Исчерпать backup reserve: Emergency maps остаются выбранными, real lights
   выключаются.
9. Проверить, что renderer geometry сохраняет корректные baked shadows во всех
   трёх состояниях.

PlayMode test должен открывать New Game через `MainMenuController` или искать
кнопку рекурсивно. Старый путь `RootButton/NewGameButton` больше не существует;
актуальный authored путь —
`RootButton/background_button/NewGameButton`.

