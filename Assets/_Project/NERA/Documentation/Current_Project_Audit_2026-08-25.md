# NERA: технический baseline после отладки

Дата: 2026-08-25

Unity: 6000.0.71f1

Целевая платформа: Standalone Windows x64

Save schema: 20

## Краткий вывод

Проект после августовской отладки стал заметно стабильнее по lifecycle и
runtime-нагрузке. Исправлены проблемы, которые в аудите от 2026-08-14 были
отмечены как P1: сохранение authored-состояния обслуживаемых объектов при New
Game, коллайдеры установленных upgrade visuals, FPS-зависимая стоимость выстрела
турели и обработка ошибок/same-scene переходов.

Добавлены и связаны с gameplay:

- четыре физических апгрейда солнечной панели;
- характеристика `Dust Tolerance`;
- три режима запечённого освещения станции;
- 3D Boot menu с двумя Cinemachine virtual cameras;
- event-driven связи энергетики, освещения и station lifecycle;
- кеширование и интервалы для наиболее частых runtime-проверок;
- автоматическое отключение Rigidbody interpolation во время наземного
  перемещения игрока.

Проект пока не является зелёным release candidate:

- EditMode: `206/209`;
- PlayMode: `36/41`;
- standalone Development Build после последних изменений не измерен;
- PlayMode-проверка запечённого света ломается на устаревшем пути Boot UI до
  того, как доходит до самой проверки света;
- остаются риски stable persistent ID, восстановления несовместимых деталей и
  потери staged-part при невозможности вернуть предмет;
- старый Cinemachine compatibility-код создаёт deprecation warnings.

## Проверенный baseline

| Параметр | Текущее значение |
|---|---|
| Unity | 6000.0.71f1 |
| Platform | StandaloneWindows64 |
| Save schema | 20 |
| Runtime C# files | 154 |
| Enabled build scenes | 23 |
| EditMode | 206 passed / 3 failed / 209 total |
| PlayMode | 36 passed / 5 failed / 41 total |
| Общий test result | 242 passed / 8 failed / 250 total |

Build Settings по-прежнему содержат Boot, MainScene, Player_Station,
Expedition_01-08 и UnknownSignal_01-12. Это технически рабочий набор, но он
шире First Playable scope: большинство поздних сцен остаются placeholder.

Unity scripts компилируются без ошибок. В проекте остаются предупреждения об
устаревших `CinemachineFreeLook` и `CinemachineOrbitalTransposer`; их миграцию
нельзя смешивать с обычным cleanup, потому что она меняет поведение камеры.

## Что закрыто после аудита 2026-08-14

| Прежний риск | Статус | Реализация |
|---|---|---|
| New Game задаёт всем объектам condition = 1 | Закрыт | `MaintainableObject` хранит `initialCondition`, `SaveGameController` вызывает `ResetToInitialCondition()` |
| Installed visual сохраняет Collider | Закрыт | `StationUpgradeSlot.MakeVisualOnly()` рекурсивно отключает `Collider`, behaviours и физику |
| Турель тратит энергию в зависимости от FPS | Закрыт | используется `FiringEnergyPerShot` и атомарный `TrySpendEnergy()` |
| Ошибка scene load считается успешным переходом | Закрыт | введён `SceneTransitionResult`, checkpoint создаётся только после `Success`, pending spawn очищается |
| Same-scene переход игнорирует spawn | Закрыт | отдельная ветка применяет pending spawn без перезагрузки |
| Энергия при штатном выходе может не попасть в save | Закрыт | lifecycle flush создаёт свежий snapshot независимо от dirty-состояния |
| Покадровые поиски energy/station controllers | В основном закрыт | lifecycle events и кеширование заменили постоянный polling |

## Новые системы и актуальные контракты

### Солнечная панель

`station_solar_01` является `StationUpgradeableObject`, а не отдельным
`StationDeviceInteractable`. Один компонент отвечает за обязательное
обслуживание/запуск удерживанием `E` и за вход в upgrade mode коротким нажатием.
Ставить оба interactable на один объект нельзя.

Текущие слоты:

| Slot | Деталь | Модификатор |
|---|---|---|
| `Slot_1` | Photovoltaic Cells | Generation `+10` |
| `Slot_2` | Dust Protection | Dust Tolerance `+35%` |
| `Slot_3` | Power Optimizer | Generation `+5` |
| `Slot_4` | Tracking Drive | Generation `x1.15` |

Полностью улучшенная панель имеет `63.25 kW` при ясной погоде вместо базовых
`40 kW`. `Dust Tolerance = 35%` означает, что при condition `0` панель сохраняет
35% потенциальной генерации. При condition `0.5` коэффициент равен `0.675`, а
при condition `1` — `1.0`.

### Освещение станции

`SwitchBakedLights` содержит три независимых preset:

- `Normal Operation`;
- `Low Energy Warning`;
- `Backup Power Emergency`.

Warning включается во время sandstorm либо при заряде не выше
`EnergyBalance_Default / Default Consumer Minimum Charge` (`25%`). Emergency
используется при выключенной основной батарее или нулевой main charge с
доступным backup reserve. Каждый preset отдельно переключает baked lightmaps и
свой массив `Light`. Пустой список источников света не блокирует карты, а
пустой список карт не блокирует источники.

### Boot menu

Boot содержит 3D station presentation и две камеры:

- `VirtualCam/VirtualCam_01` — root/options/exit;
- `VirtualCam/VirtualCam_02` — выбор save slot для New Game/Continue.

Код меняет только Cinemachine Priority `10/0`. Смешивание принадлежит
`MainMenuCamera/CinemachineBrain` и asset
`Resources/MainMenuCamera Custom Blends.asset`; текущая длительность — 2 s.

### Runtime performance

Создан воспроизводимый PlayMode benchmark: 180 warm-up frames и 600 measured
frames для Player_Station и Expedition_01. Повторный результат считается как
медиана трёх одинаковых запусков.

Ключевые изменения относительно baseline:

| Сценарий | Метрика | Было | Стало | Изменение |
|---|---:|---:|---:|---:|
| Player_Station | CPU Total median | 6.713 ms | 6.434 ms | -4.2% |
| Player_Station | BehaviourUpdate median | 0.554 ms | 0.358 ms | -35.3% |
| Player_Station | GC median | 52,221 B/frame | 37,641 B/frame | -27.9% |
| Expedition_01 | Main Thread median | 3.691 ms | 3.542 ms | -4.0% |
| Expedition_01 | BehaviourUpdate median | 0.296 ms | 0.251 ms | -15.1% |
| Expedition_01 | GC median | 25,561 B/frame | 24,447 B/frame | -4.4% |

Draw calls и геометрия не изменились. Render Thread в Editor показал высокую
вариативность, поэтому эти данные нельзя использовать как финальный GPU или
player-build baseline. Полная методика находится в
`Runtime_Performance_Baseline_2026-08-24.md`.

## Текущее состояние тестов

### EditMode: 206/209

| Failure | Классификация | Следующее действие |
|---|---|---|
| `CheckpointMetadataAndWorldStateRoundTripThroughJson` | stale test | заменить ожидание save version `19` на `SaveGameData.CurrentVersion` |
| `ProductionUiOwnsAllTextSizing` | authoring policy drift | убрать production sizing из `QuestHUDPrefabSetup` после проверки authored prefab |
| `DynamicMaintenanceQuestUsesTargetContextWithoutDuplicates` | product copy/test drift | утвердить generic текст `Запустите очистку` либо вернуть target-specific текст, затем обновить тест |

### PlayMode: 36/41

| Failure | Классификация | Следующее действие |
|---|---|---|
| `DroneCanSurveySecondLocationAfterRecharge` | fixture/state drift | явно привести drone maintenance condition к рабочему состоянию; `IsFlightReady` теперь требует condition = 1 и отсутствие sandstorm |
| `MainExpeditionQuestRunsFromRuntimeSignals` | stale test | HUD использует bullet `•`, тест всё ещё ожидает `-` |
| `StationBakedLightingFollowsRealPowerAndWeather` | broken test bootstrap | путь кнопки изменился на `RootButton/background_button/NewGameButton`; текущий NRE возникает до загрузки Player_Station и ничего не говорит о lightmaps |
| `StationTerminalIsStatusOnlyAndReflectsPhysicalParts` | async refresh expectation | terminal refresh coalesced до одного обновления за 0.1 s, тест читает текст через один frame; ждать условие/таймаут |
| `TerminalWorldDecorationFollowsPowerAndLastTab` | scene contract drift | определить правильный layer декоративной станции и нормализовать prefab либо тест |

PlayMode tests всё ещё слишком сильно зависят от production scene hierarchy,
текущего баланса и состояния, которое оставляют другие тесты. Общие действия
Boot menu должны выполняться через helper/API, а не через hardcoded дочерние
пути.

## Оставшиеся технические риски

### P1 — до следующего release candidate

1. **Stable persistent ID.** Scene objects с пустым authored ID продолжают
   строить ключ из hierarchy path и sibling index. Переименование может вернуть
   подобранный item или побеждённого врага.
2. **Restore installed parts.** `RestoreInstalledParts()` проверяет существование
   Slot ID, но не проверяет item catalog и полную compatibility детали.
3. **Staged-part recovery.** `RollbackAll()` очищает staged state даже если
   предмет не удалось вернуть ни в inventory, ни в storage.
4. **Baked lighting end-to-end.** Unit tests состояния проходят, но сломанный
   Boot bootstrap не подтверждает реальные renderer lightmap indices после
   полного New Game flow.
5. **Standalone performance.** Editor baseline подтверждает scripting gains,
   но не определяет GPU time, 1% low, RAM, loading и поведение Render Thread в
   Development Build.

### P2 — стабилизация и поддерживаемость

- перейти с устаревших Cinemachine FreeLook/OrbitalTransposer отдельной
  миграцией с визуальным regression pass;
- вынести mutable production configs из controller tests в synthetic fixtures;
- расширить ProjectValidator проверками persistent ID, upgrade compatibility,
  installed visual purity и Boot UI contracts;
- создать отдельный First Playable build profile только для Boot, MainScene,
  Player_Station и Expedition_01;
- продолжить разделение крупных UI/save controllers только после зелёного
  baseline.

## Рекомендуемый порядок следующих задач

1. Закрыть восемь текущих test failures без изменения утверждённого gameplay.
2. Исправить Boot test helper и выполнить реальный lighting end-to-end:
   battery off, battery on, charge threshold, sandstorm, main charge 0/backup.
3. Добавить validator для stable persistent ID и назначить ID production
   объектам First Playable сцен.
4. Валидировать restored installed parts через catalog + compatibility; сделать
   staged-part rollback без потери предмета.
5. Создать Development Build First Playable и снять CPU/GPU/GC/RAM/loading/1%
   low для High, затем smoke для Medium и Low.
6. Зафиксировать First Playable build scope и пройти Boot -> New Game -> Station
   -> Expedition_01 -> checkpoint/death -> Continue -> Return to Menu.
7. После технического baseline перейти к health/damage/death UI, combat/audio/
   VFX feedback и production blockout Expedition_01.
8. Миграцию Cinemachine и cleanup demo/dead assets выполнять отдельными
   задачами после lock.

## Критерий следующего baseline

- EditMode и PlayMode полностью зелёные;
- baked lighting подтверждено через Boot flow, а не только unit tests;
- First Playable Development Build проходит полный save/checkpoint flow;
- собраны player-build CPU/GPU/GC/RAM/loading/1% low;
- все tracked production instances имеют стабильный persistent ID;
- восстановление и rollback upgrade parts не могут создать несовместимое
  состояние или потерять предмет.
