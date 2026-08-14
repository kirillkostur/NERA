# NERA: полный аудит проекта

Дата: 2026-08-14  
Unity: 6000.0.71f1  
Целевая платформа: Standalone Windows x64

## Краткий вывод

В проекте не найдено дефектов уровня `P0`, повреждённых script references,
дублирующихся content ID или отсутствующих локальных GUID. Основные игровые
подсистемы связаны, новая физическая система апгрейдов является текущей, а
старая `PowerRestoreInteractable` отсутствует.

При этом проект пока нельзя считать технически зелёным:

- полный прогон тестов: `133/139 EditMode` и `21/24 PlayMode`;
- один из трёх PlayMode failures доказанно зависит от порядка тестов и проходит
  отдельно;
- риск потери точного уровня энергии при завершении сеанса исправлен после
  аудита; остальные риски сохранённого состояния перечислены ниже;
- установленные детали могут сохранять лишние коллайдеры;
- часть энергетической логики зависит от частоты кадров;
- широкие глобальные события вызывают лишние пересборки UI и визуалов;
- production build scope смешан с placeholder-сценами;
- в репозитории есть подтверждённо неиспользуемый код, demo-content и пакеты.

Отсутствующие батарейные Engineering Part считаются запланированным
наполнением контента и в список дефектов не входят. После их добавления нужно
заменить устаревший тест, который сейчас пытается установить дроновый
`capacitor_01` в батарею.

## Объём и способ проверки

Проверены:

- 1 892 файла репозитория без `Library`, `Temp`, `Logs` и `obj`;
- 170 C#-файлов NERA, из них 146 runtime-файлов;
- 36 422 строки C# в `Assets/_Project/NERA/Code`;
- 189 сцен, prefab и ScriptableObject assets NERA;
- все enabled build scenes, location configs, item catalog, station configs,
  physical prefabs и `StationUIPreview`;
- GUID, missing scripts, content IDs, serialized callbacks, AnimationEvent,
  `Resources.Load`, динамические `AddComponent` и reflection-like вызовы;
- полный каталог Unity Test Framework: 163 теста;
- Unity Console и базовая physics validation;
- package manifest, samples, demo-content и крупные assets;
- текущая документация и её соответствие конфигам.

Аудит выполнялся read-only для gameplay-кода и контента. В этом изменении
обновлена только документация; код, сцены, prefab, конфиги и пакеты не удалялись.

## Проверенный baseline

- Company Name: `Measured Field`.
- Product Name: `Nera`.
- Bundle version: `0.1.0`.
- Save schema version: `19`.
- Input handling: `Both`; это пока необходимо, потому что проект использует и
  legacy `Input`, и новый Input System.
- В Build Settings включены 23 сцены: Boot, MainScene, Player Station,
  Expedition 01–08 и Unknown Signal 01–12.
- Unity Console после прогонов не содержит compile errors или warnings.
- Статически подтверждено 20 location configs. Каждая ссылается на
  существующую enabled-сцену и ровно один соответствующий spawn point.
- `ItemCatalog_Default` содержит все 21 `ItemData`; duplicate item/location/
  quest/library/map-slot IDs не найдены.
- Основные physical station objects и `StationUIPreview` совпадают по слотам.
- Все 18 значений `StationObjectStat`, включая `Damage Taken`, реально
  используются конфигами и runtime. Удалять их нельзя.
- Последняя документированная Windows Development Build от 2026-07-30
  предшествует последующим изменениям Player, save и station upgrade. Она не
  является актуальным release candidate.

## P1 — исправить до следующего release-кандидата

### 1. ИСПРАВЛЕНО — точная энергия при штатном выходе

`EnergySystemController` меняет `currentEnergy` каждый simulation frame, но
`AutoSaveService` помечает save dirty только при смене диапазона
`EnergyState`. Изменение, например, с 90 до 60 внутри состояния `Normal` может
не попасть в файл: `Flush()` при `dirty == false` завершается без нового
snapshot.

Затронутые места:

- `Code/Runtime/Save/AutoSaveService.cs:94-101,168-170,260-273`;
- `Code/Runtime/Energy/EnergySystemController.cs:123-149,580-598`;
- `Code/Runtime/Core/BootInitializer.cs:373-381`.

Исправлено 2026-08-14: добавлен `AutoSaveService.FlushCurrentState()`, который
создаёт свежий snapshot независимо от dirty/suspended-флагов. Он вызывается при
pause, application quit и возврате в главное меню. Обычный `Flush()` остался
dirty-only, поэтому непрерывная энергия не создаёт постоянные записи на диск.
Regression test сохраняет старое значение, меняет энергию внутри того же
`EnergyState` и подтверждает точное значение в финальном JSON.

### 2. New Game стирает настроенное начальное состояние турелей

`SaveGameController.ResetRuntimeState()` вызывает `SetCondition(1f)` для всех
активных `MaintainableObject`. Поэтому даже если одна турель настроена сломанной
в prefab или сцене, новая игра немедленно делает её полностью исправной. Это
нарушает задуманный сценарий: сначала удерживание `E` для ремонта/запуска,
затем короткое `E` для апгрейда.

Затронутые места:

- `Code/Runtime/Save/SaveGameController.cs:1079-1084`;
- `Code/Runtime/Maintenance/MaintainableObject.cs:12-13,53-61`;
- `Prefabs/Station/Station_Turret_01.prefab`;
- `Prefabs/Station/Station_Turret_02.prefab`.

Что сделать: добавить в `MaintainableObject` явный `ResetToInitialCondition()`
или хранить initial condition в центральном station object config. New Game
должен возвращать authored-значение, а не универсальную единицу. Нужен
PlayMode-тест, подтверждающий разные начальные состояния двух турелей.

### 3. Установленный visual детали сохраняет Collider

`StationUpgradeSlot.MakeVisualOnly()` отключает `MonoBehaviour` и физику
`Rigidbody`, но не выключает дочерние `Collider`. При этом клики уже принимает
сохранённый Fake hitbox. Большинство деталей пока используют полный
`P_WorldItem_*` как `Installed Visual Prefab`, поэтому в слоте возникают два
набора коллизий.

Последствия:

- блокировка raycast и повторение конфликтов клика;
- влияние decorative visual на игрока, снаряды и физику;
- неодинаковое поведение трёх visual-only prefab и остальных world-prefab.

Затронутые места:

- `Code/Runtime/Station/StationUpgradeSlot.cs:65-90,140-175`;
- `Configs/Items/Item_EngineeringPart/`;
- `Prefabs/Items/Item_EngineeringPart/`.

Предпочтительное решение: отдельный mesh-only installed prefab для каждой
детали. Дополнительная защита runtime — рекурсивно отключать все `Collider` у
созданного visual. Единственным click target слота должен оставаться Fake
collider.

### 4. Энергия выстрела турели зависит от FPS

В кадр выстрела turret consumer переключается на
`FiringEnergyConsumption`, а на следующем `Update` возвращается к idle.
`EnergySystemController` интегрирует мощность через `deltaTime`, поэтому цена
одного выстрела фактически равна `firing kW × duration кадра`. На 30 и 120 FPS
она различается приблизительно в четыре раза.

Затронутые места:

- `Code/Runtime/Station/StationTurretController.cs:110-155,235-262`;
- `Code/Runtime/Energy/EnergySystemController.cs:123-139,512-520`.

Что сделать: ввести атомарную стоимость `Firing Energy Per Shot` и вызывать
`TrySpendEnergy()` перед нанесением урона. Альтернатива — фиксированное время
firing-state, но per-shot cost проще тестировать и балансировать. Нужен тест с
одинаковым числом выстрелов при разных frame rates.

### 5. Ошибка загрузки сцены обрабатывается как успешный переход

`SwitchGameplayScene()` при ошибке делает `yield break`, но не возвращает
caller-у статус. После этого caller может активировать checkpoint целевой
сцены и снова включить управление. `pendingSpawnPointId` назначается до
загрузки и при failure не очищается. Same-scene переход также не применяет
новый spawn point.

Затронутые места:

- `Code/Runtime/Core/BootInitializer.cs:187-203,244-324`;
- `Code/Runtime/Core/SceneTransitionState.cs:5-21`.

Что сделать: явный результат `Success/Failure`, очистка pending state в
`finally`, checkpoint только после подтверждённой загрузки и отдельная ветка
same-scene teleport.

### 6. Persistent ID части production-объектов зависит от иерархии

У сохраняемых scene-object с пустым `persistentId` ключ строится из имён и
`GetSiblingIndex()`. Переименование или перестановка объекта меняет ключ, после
чего уже подобранный предмет или побеждённый враг может появиться снова.

Найдены 27 пустых ID, включая:

- Memory Core в `Scenes/Expedition_01.unity`;
- Memory Core в `Scenes/UnknownSignal_02.unity` …
  `UnknownSignal_12.unity`;
- world-prefab Engineering Part, чьи scene instances не всегда задают
  authored override.

Затронутые места:

- `Code/Runtime/Save/PersistentSceneIdentity.cs:15-40`;
- `Code/Runtime/Items/WorldItem.cs:15-31`;
- `Code/Runtime/Enemies/IOEnemyController.cs:21-47`.

Что сделать: обязательный стабильный GUID для каждого tracked scene instance,
проверка пустых и duplicate ID в `ProjectValidator`, а для уже выпущенных
сохранений — таблица legacy aliases при переименовании.

## Текущее состояние тестов

Полный прогон дал `154 passed / 9 failed` из 163 тестов.

### EditMode: 133/139

| Failure | Причина |
|---|---|
| `AntennaCannotCalibrateSignalBeyondConfiguredScanRange` | Тест использует production config; все Unknown Signal сейчас требуют Scan Range 1. |
| `AntennaUsesCentralObjectCalibrationDuration` | Ожидается 8, текущий config содержит 120. |
| `EveryItemHasEnglishAndRussianContentEntries` | Реальный content defect: отсутствует EN key `item.antenna_array_01.name`. |
| `SaveVersion14SerializesQuestAndMaintenanceState` | Устаревшее имя и ожидание version 18; runtime version 19. |
| `DroneLaunchConsumesConfiguredBatteryCharge` | Ожидается Battery Charge 100, текущий config содержит 200. |
| `DroneRechargeTimeUsesOnlyMissingBatteryCharge` | Ожидается Energy Consumption 4, текущий config содержит 3. |

Пять из шести failures показывают архитектурную проблему тестов: они создают
контроллеры без изолированного `StationSystemsConfig`, после чего runtime
неявно загружает production `Resources/Station/StationSystems_Default`. Любая
правка баланса ломает тесты, не меняя программный контракт.

Исправление: инъекция небольшого synthetic config в каждый test fixture и
вычисление ожидаемых значений из него. Production asset должен проверяться
отдельными content validation tests без hardcoded старых чисел.

### PlayMode: 21/24

| Failure | Причина |
|---|---|
| `BatteryPartAppliesConfiguredCapacityImmediately` | Тест использует несовместимый дроновый capacitor. После создания батарейных деталей тест нужно перевести на реальный Battery item. |
| `DroneCanSurveySecondLocationAfterRecharge` | Тест устанавливает Propulsion в старый `Slot_4`; текущая конфигурация использует `Slot_6`. |
| `InteractionTargetUsesProximityFromAnySide` | Зависимость от порядка: тест наследует Player Station и выбирает активный `P_WorldItem_Propulsion`; отдельно из Boot проходит 1/1. |

Последний failure подтверждает отсутствие изоляции сцен между PlayMode tests.
Каждый fixture должен явно загружать чистую test scene или удалять все
созданные/унаследованные interactables в `TearDown`.

## P2 — важные логические и lifecycle-риски

### Upgrade и restore

- `StationSystemsController.RestoreInstalledParts()` проверяет существование
  Slot ID, но не применяет полную compatibility validation. Неизвестная или
  несовместимая деталь из старого/повреждённого save способна занять слот,
  приблизить `IsFullyUpgraded`, но не дать visual и modifiers.
- `StationUpgradeModeController.RollbackAll()` очищает staged items даже если
  возврат в исходный inventory/storage не удался. При teardown это может
  уничтожить предмет. Staged entry нужно удалять только после подтверждённого
  возврата; нужен recovery/overflow queue или безопасный world-drop.
- В 12 из 15 Engineering Part полным `P_WorldItem_*` одновременно служит
  installed visual. После перехода на mesh-only prefabs runtime и authoring
  станут проще и одинаковее.

### Energy и environment

- `StationEnvironmentController` не сохраняет current hour и weather, хотя они
  влияют на solar generation и weather wear. После reload энергия сохраняется,
  а её внешние условия сбрасываются к prefab defaults.
- Для consumer реализован unregister, а для battery/solar lifecycle
  несимметричен. Disabled/destroyed source способен остаться в dictionaries и
  продолжать давать capacity/generation.
- `DroneScanController.OnDisable()` не гарантирует выключение charger consumer;
  `AntennaController` аналогично требует явного завершения calibration
  consumer и симметричных подписок.
- Drone подписывается на `StationPowerController` только в `OnEnable`. Если
  singleton появился позже, зависимость найдётся, но событие power change уже
  не будет привязано. Нужен единый idempotent `BindPower()` или `InstanceChanged`.

### Контент и сборка

- Все 12 Unknown Signal требуют `Required Antenna Scan Range = 1`. Поэтому
  базовая антенна уже видит весь набор, а четыре `Scan Range +1` не создают
  прогрессию. Если дальность должна быть gate, распределить требования по
  уровням; если нет — убрать лишние модификаторы.
- В Player Station находится активный `P_WorldItem_Propulsion`. Он уже влияет
  на PlayMode test selection и должен быть явно классифицирован как production
  pickup либо удалён из release-сцены.
- 23 enabled scenes включают Expeditions 02–08 и Unknown Signal 01–12, большая
  часть которых является template/placeholder content. Нужен отдельный First
  Playable build profile: Boot, MainScene, Player Station, Expedition 01.
- `AddressableAssetSettings.m_BuildAddressablesWithPlayerBuild` использует
  значение Preferences. Результат build локализации зависит от EditorPrefs
  конкретной машины. Зафиксировать BuildWithPlayer или явно запускать
  `BuildPlayerContent` в `WindowsPlayerBuild`.
- В `Station_Battery.prefab` существует `Slot_6`, которого нет в central config
  и presenter references. После подтверждения, что он не предназначен для
  будущего контента, удалить его из prefab.
- В Turret 2 display name `Shassis` следует исправить на `Chassis`.

### Physics

Physics validation показала, что все project layers сталкиваются со всеми.
Это корректно функционально, но создаёт лишние пары broadphase и повышает риск
пересечения gameplay, interaction и upgrade hitboxes. Матрицу следует менять
только после инвентаризации слоёв; вероятные отдельные группы: Player,
Interactable, WorldItem, Enemy, Projectile и UpgradeSlot.

## Оптимизация без изменения дизайна

### Высокая отдача

1. Разделить `EnergyChanged` на structural state и telemetry. Сейчас событие
   публикуется каждый simulation frame, открытый terminal полностью
   форматирует station screen, а QuestController повторно обходит catalog.
   UI достаточно обновлять при изменении отображаемого значения или 5–10 раз/с.
2. Сделать события `StationSystemsController` адресными:
   `(systemType, objectId, slotId)`. Сейчас любое изменение заставляет все
   `StationObjectVisual`, включая `StationUIPreview`, делать Destroy +
   Instantiate всех деталей.
3. В `StationUpgradeSlot.ShowPart()` хранить текущий Item ID/prefab и выполнять
   no-op, если визуал не изменился.
4. Разделить `PlayerInventory.InventoryChanged` на structural change и charge
   change. Один выстрел сейчас пересоздаёт equipped visuals и обновляет все
   группы inventory UI.
5. Убрать `FindFirstObjectByType<PlayerInventory>()` из каждого кадра
   `InventoryLabHUDController` при позднем binding. Boot/runtime composition
   должен передавать inventory событием или через явную регистрацию.

### Низкорисковые технические улучшения

- `MovementCharacterController`: заменить `SweepTestAll` в `FixedUpdate` на
  non-alloc buffer.
- `StationUpgradeModeController`: заменить `RaycastAll + Array.Sort` на
  `RaycastNonAlloc` и выбор ближайшего допустимого hit.
- `StationUpgradeSlot`: закешировать Fake renderers/colliders вместо повторных
  `GetComponentsInChildren`.
- `IOEnemyController`: не выполнять `FindGameObjectWithTag` каждым врагом каждый
  кадр при отсутствии цели; использовать player registry и редкий fallback.
- `StationSystemsConfig`, `ItemCatalogData` и effective stats: построить
  dictionaries/snapshot при изменении деталей вместо каскада линейных поисков
  на каждый `GetStat`.
- `MovementCharacterController.showDebug` установить `false` по умолчанию для
  production prefab.
- Storage capacity (`16,16,16`) перенести из
  `TerminalStorageScreenController` в domain config. UI должен читать ёмкость,
  а не задавать её.

## Рекомендуемые переносы ответственности

### Save

`SaveGameController` — 1 447 строк; он одновременно выполняет capture,
restore, migration, file IO, fallback catalog lookup и quest synchronization.
Разделить на:

- `SaveRepository` — atomic file IO и backup;
- `SaveMigrator` — version-to-version transformations;
- `ISaveParticipant` — небольшие capture/restore adapters подсистем;
- `SaveCoordinator` — порядок и lifecycle flush.

Так environment, station upgrade и будущие системы будут добавляться без
роста одного центрального класса.

### Station upgrade

`StationUpgradeModeController` — 813 строк и три разные ответственности:

- transaction/session и гарантированный возврат предметов;
- hit-testing слотов;
- camera/input/UI presentation.

Transaction не должна зависеть от времени жизни HUD-компонента. Рекомендуемые
части: `StationUpgradeSession`, `StationUpgradeHitTester`,
`StationUpgradePresentation`. Это упростит тестирование quit/teardown и
исключит потерю staged items.

### Station stats

`StationSystemsController` оставить владельцем установленного состояния, но
расчёт effective values вынести в кешированный `StationStatsService`. Он
пересчитывает snapshot конкретного объекта только после изменения его слота и
публикует адресное событие.

### UI

`InventoryLabHUDController` (1 215 строк) и `LaboratoryScreenController`
(1 001 строк) одновременно ищут hierarchy, создают UI, выполняют domain
operations и управляют навигацией. Разделить authored View, presenter и
commands. То же направление применимо к terminal screens после стабилизации
First Playable.

### Parkour authoring

`HandlePoints` и `HandlePointConnection` используют `ExecuteInEditMode` и
проверяют authoring-флаги в `Update`. Generation/delete/refresh следует
перенести в CustomEditor buttons или editor utility, а в runtime оставить
только заранее сохранённый graph.

## Подтверждённые cleanup-кандидаты

Ниже перечислено то, для чего не найдено C#-, GUID-, UnityEvent-,
AnimationEvent- или reflection-ссылок. Удалять лучше отдельным cleanup-коммитом
с полным compile/test/build после каждой группы.

### Код и assets

- `Assets/Plugins/Demigiant/DOTween` — около 762 KB, 51 файл; ни одного
  использования `DG.Tweening`/DOTween. Вместе удалить
  `Assets/Resources/DOTweenSettings.asset`.
- `Assets/TextMesh Pro/Examples & Extras` — 6.35 MB, 284 файла и 34 demo script;
  внешних ссылок из проекта нет. `TextMesh Pro/Resources` оставить.
- `DrawLineIndividual.cs` и его CustomEditor `DrawLineVis` в
  `Code/Editor/Parkour/EditorHandler.cs`.
- Неиспользуемые поля `ExpeditionLocationData.initialState`, `mapSymbol`,
  `mapPreview`, их getters, `LocationState` и `MapSymbol`. После миграции YAML
  становятся кандидатами три preview-art asset: `Art/fire.png`, `Art/moon.jpg`,
  `Art/Sun.jpg`.
- Методы без найденных вызовов: `AnimationCharacterController.RootMotion`,
  `AnimationCharacterController.EnableIKSolver`,
  `ThirdPersonController.ResetMovement`,
  `ThirdPersonController.GetCurrentVelocity`, `Point.ReturnNeighbour`,
  `HandlePointConnection.IsDirectionAngleValid`.

`AnimationCharacterController.EnableController` удалять нельзя: он указан как
AnimationEvent в пяти FBX clips.

### Packages

Сильные кандидаты после отдельного compile/build:

- `com.unity.multiplayer.center`;
- `com.unity.visualscripting`;
- `com.unity.feature.2d` и явный `com.unity.2d.sprite`.

Условные кандидаты:

- `com.unity.ai.navigation`: gameplay NavMesh сейчас не использует, но проект
  ссылается на три assets из импортированного sample (`black.mat`, `grey.mat`,
  `modify_crosshair.png`). Сначала перенести их в project-owned папку и
  перепривязать ссылки. Не удалять папку `Assets/Samples` целиком до этого.
- `com.unity.feature.cinematic`: заменить прямой зависимостью
  `com.unity.cinemachine 3.1.7`; сам Cinemachine активно используется. Bundle
  дополнительно тянет Timeline, Sequences, FBX Exporter, Alembic и Recorder.
- `com.unity.probuilder`: удалить только если он больше не нужен для authoring.
- `com.unity.collab-proxy`: удалить, если команда не использует Unity Version
  Control.
- Rider/Visual Studio integration: оставить только реально используемую IDE.

Встроенные Unity modules лучше чистить последним этапом: часть вернётся через
dependencies, а runtime-выгода часто мала из-за managed stripping.

### Дубликаты art

Найдены четыре точные SHA-256 duplicate texture pairs между `Erika.fbm` и
`Textures`. Текстовых GUID-ссылок на копии нет, но FBX importer способен
подбирать texture по имени. Удаление допустимо только после проверки
`AssetDatabase.GetDependencies`, reimport модели и визуального сравнения.

## Не удалять как «старую систему»

- `StationDeviceInteractable` — текущий объединяющий компонент. Он отвечает за
  приоритет удерживаемого ремонта/запуска и короткое `E` для нового физического
  апгрейда. Компонент размещён в Player Station.
- `StationUpgradeModeController`, `StationUpgradeableObject`,
  `StationUpgradeSlot`, `StationObjectVisual` и Engineering Part definitions —
  активная новая система.
- Все значения `StationObjectStat` — используются.
- `LaboratoryMode.Upgrade` — не старая station upgrade system, а живая логика
  anomaly integration/synthesis. Чтобы не путать системы, её можно позднее
  переименовать в `Integration` или `Synthesis` с миграцией serialized names.
- `AnomalyElectronicDevice`, `QuestSignalEmitter`, `PersistentWorldFlag` и
  `PlayerCheckpointTrigger` сейчас не размещены, но являются документированными
  точками расширения. Сначала принять product/design-решение: разместить их или
  удалить вместе с соответствующими разделами документации.

## Что добавить в ProjectValidator

Текущий validator проверяет identity, обязательные build scenes, Player prefab,
locations, quest catalog и PC quality, но не валидирует station upgrade graph.
Нужен единый content graph pass:

1. Уникальный `(System Type, Object Id)`.
2. Уникальные stat и Slot ID внутри station definition.
3. Каждый physical/config slot ровно один раз представлен на physical prefab и
   `StationUIPreview`.
4. Каждая Engineering Part compatibility указывает на существующий object/slot.
5. Каждый modifier изменяет stat, объявленный target-объектом.
6. `World Prefab` ссылается обратно на тот же `ItemData`.
7. Installed visual не содержит `WorldItem`, interactable, Rigidbody или
   активные Collider.
8. Каждый tracked scene-object имеет непустой уникальный persistent ID.
9. Release build profile не содержит placeholder-сцены.
10. Localization tables содержат обе локали для каждого player-facing item.

## Рекомендуемый порядок работ

1. Исправить реальный localization key и восстановить полностью зелёный
   изолированный test baseline.
2. Гарантировать authored initial conditions и stable persistent IDs.
3. Исправить failure path scene transition.
4. Сделать installed visuals collider-free и проверить upgrade click/rollback/
   quit tests.
5. Сделать turret firing energy независимой от FPS.
6. Добавить station/save/content graph checks в ProjectValidator.
7. Создать First Playable build profile и детерминированный Addressables build.
8. Уменьшить частоту/объём Energy, Station и Inventory events.
9. Удалить подтверждённый demo/dead content отдельными небольшими коммитами.
10. После стабильного First Playable разделять крупные controllers по границам,
    описанным выше.

## Критерий следующего технического baseline

- 139/139 EditMode и 24/24 PlayMode проходят вместе и отдельно;
- Unity Console чистая;
- New Game сохраняет разные authored-состояния двух турелей;
- quit/menu flush сохраняет точную энергию — покрыто автоматическим тестом;
- staged upgrade всегда возвращается после ESC, scene unload и application quit;
- installed visual не участвует в physics/raycast;
- одинаковое число выстрелов турели тратит одинаковую энергию при разных FPS;
- release build содержит только утверждённые сцены и свежие Addressables;
- New Game и Continue полностью пройдены в актуальной Windows Development Build.
