# NERA Content Assembly Guide

Рабочая инструкция по сборке контента из готовых моделей, текстур, звуков и VFX.

Документ описывает текущую структуру проекта. Основной порядок всегда один:

`исходные ассеты -> визуальные префабы -> игровые префабы -> конфиги -> регистрация -> сцена -> Build Settings -> проверка`

## 1. Главное правило

Сцена хранит размещение объектов, освещение, навигацию и локальные связи.

Конфиги хранят игровые данные: ID, названия, параметры предметов, research, Library, оружия, IO и локаций.

Префабы хранят повторно используемую иерархию компонентов и визуал.

Нельзя хранить важное состояние прохождения только в сценовом GameObject или UI. Состояние должно проходить через контроллеры на `MainScene/RuntimeRoot` и сохранение.

## 2. Куда класть готовые ассеты

### 2.1. Общий визуальный контент

Переиспользуемые модели, текстуры и материалы:

`Assets/_Project/NERA/Art/`

Общий контент мира распределяется по смыслу:

- `Content/AncientNERA/` — архитектура и устройства древней NERA;
- `Content/Human/` — человеческие устройства, контейнеры, инструменты и следы миссий;
- `Content/IO/` — общие модели, материалы и эффекты IO;
- `Content/Planet/` — грунт, скалы, растения, природные материалы;
- `Content/Shared/` — нейтральные ассеты для нескольких типов локаций;
- `Content/Station/` — ассеты, используемые только на станции.

Если ассет используется только в одной экспедиции:

`Content/Expeditions/Expedition_XX/`

Рекомендуемая локальная структура:

```text
Expedition_02/
  Art/
  Materials/
  Prefabs/
  VFX/
  Audio/
  Data/
```

Если объект потенциально нужен в нескольких экспедициях, его нельзя оставлять внутри `Expedition_02` — он должен перейти в `AncientNERA`, `Human`, `IO`, `Planet` или `Shared`.

### 2.2. Общие игровые префабы

Префабы, используемые в разных сценах:

- `Prefabs/Items/` — подбираемые и экипированные предметы;
- `Prefabs/IO/` — сущности IO;
- `Prefabs/Interaction/` — общие интерактивные объекты;
- `Prefabs/Managers/` — переходы и сценовые служебные объекты;
- `Prefabs/Player/` — игрок вместе с gameplay-камерой и Cinemachine rig;
- `Prefabs/Parkour/` — настраиваемые parkour-поверхности и точки;
- `Prefabs/UI/` — общий UI.

Локальный hero prop одной сцены хранится в папке конкретной экспедиции, а не в общей `Prefabs`.

### 2.3. Конфиги

- `Configs/Items/` — `ItemData` и `ItemEnergyDefinition`;
- `Configs/Combat/` — `WeaponDefinition`;
- `Configs/Research/` — `ResearchDefinition`;
- `Configs/Library/` — обычные Library entries;
- `Configs/Expeditions/` — обычные локации, открываемые дроном;
- `Configs/Locations/` — особые типы локаций, например Unknown Signal;
- `Configs/IO/` — параметры IO;
- `Resources/Library/` — Library entries, которые должны быть доступны глобальной загрузке;
- `Resources/ItemCatalog_Default.asset` — автоматически синхронизируемый каталог всех `ItemData`;
- `Resources/Energy/EnergyBalance_Default.asset` — общий баланс энергии,
  потребление и индивидуальные пороги отключения объектов;
- `Resources/Station/StationSystems_Default.asset` — объекты станции, их
  управление и улучшения;
- `Resources/Inventory/DefaultInventoryConfig.asset` — общий размер инвентаря.

Не редактировать список `ItemCatalog_Default` вручную. `ItemCatalogSynchronizer` автоматически собирает все `ItemData` и останавливает build при повторяющихся `itemId`.

Для каждого объекта станции порог задаётся в
`EnergyBalance_Default -> Station Object Cutoffs -> Minimum Charge Percent`.
У повторяющихся систем, например турелей, в соседнем поле указывается их
`Object Id`.
Если заряд ниже этого значения, объект автоматически теряет питание, а его
переключатель в терминале показывает `Low Power` и блокируется. Запрошенное
игроком состояние сохраняется, поэтому после восстановления заряда объект
включается автоматически. Порог освещения, которого нет в списке объектов
терминала, задаётся отдельно в
`EnergyBalance_Default -> Lighting Minimum Charge Percent`.

## 3. Именование

### Файлы

```text
Item_<Name>_01.asset
ItemEnergy_<Name>_01.asset
Weapon_<Name>_01.asset
Research_<Name>_01.asset
Library_<Topic>_01.asset
Location_Expedition02.asset
CFG_UnknownSignal_01.asset
CFG_IO_<Variant>.asset
P_WorldItem_<Name>.prefab
P_Equipped_<Name>.prefab
```

### ID

ID не меняется после попадания объекта в сохранение:

```text
itemId: nera_relay_core_02
researchId: research_nera_relay_core_02
entryId: expedition02_relay_core
locationId: Expedition02
```

Требования:

- ID уникальный;
- без пробелов;
- один стиль регистра;
- не использовать отображаемое название как ID;
- после релиза изменение ID требует миграции save data.

## 4. Сборка обычного предмета

### Шаг 1. World prefab

Создать `P_WorldItem_<Name>.prefab` в `Prefabs/Items/`.

Рекомендуемая иерархия:

```text
P_WorldItem_<Name>
  Visual
  InteractionPoint (опционально)
  VFX (опционально)
```

На корне:

- `WorldItem`;
- Collider на слое `Interactable` (6) или `Item` (7), попадающий в радиус
  взаимодействия игрока;
- Rigidbody только если предмет должен участвовать в физике;
- корректный interaction layer;
- масштаб корня `(1,1,1)`.

В `WorldItem`:

- `Item Data` — будущий `ItemData` этого предмета;
- `Destroy After Pickup` — обычно включён;
- действие — `Pick Up`;
- режим — `Press` для обычного предмета или `Hold` для важной находки.

Визуал размещать дочерним объектом. Не компенсировать плохой pivot масштабом и вращением корня префаба.

### Шаг 2. ItemData

Создать через:

`Create -> NERA -> Items -> Item Data`

Путь:

`Configs/Items/Item_<Name>_01.asset`

Заполнить:

- `Item Id` — постоянный уникальный ID;
- `Display Name`;
- `Description`;
- `Item Type`;
- `Icon`;
- `World Prefab`;
- `Equipped Visual Prefab` — только для экипируемых предметов;
- `Quick Access Action` — только если предмет используется кнопкой;
- `Use Key`.

Тип определяет секцию инвентаря:

- `Equipment` -> Quick Access;
- `Anomaly` -> Anomaly slots;
- остальные типы -> Backpack.

### Шаг 3. Обратная связь

После создания `ItemData` вернуться в world prefab и назначить этот asset в поле `WorldItem.Item Data`.

Получается замкнутая связь:

`ItemData.WorldPrefab -> P_WorldItem -> WorldItem.ItemData -> ItemData`

Это ожидаемая связь, а не ошибка.

## 5. Экипируемый энергетический предмет

Дополнительно к обычному предмету нужны два элемента.

### 5.1. Equipped visual prefab

Создать `P_Equipped_<Name>.prefab`.

Он содержит только визуал предмета и локальные VFX. На нём не должно быть `WorldItem`, Rigidbody или логики подбора.

В `ItemData` назначить:

- `Equipped Visual Prefab`;
- `Equipment Anchor Name`, сейчас стандарт — `mixamorig1:RightHand`;
- `Equipped Local Position`;
- `Equipped Local Euler Angles`.

### 5.2. Energy definition

Создать через:

`Create -> NERA -> Items -> Item Energy Definition`

Путь:

`Configs/Items/ItemEnergy_<Name>_01.asset`

Заполнить:

- `Capacity` — максимальный заряд;
- `Initial Charge`;
- `Energy Per Use`;
- `Recharge Per Second`.

Назначить asset в `ItemData.Energy Definition`.

Если `Energy Definition` не назначен, предмет считается незаряжаемым. Если заряда меньше `Energy Per Use`, действие блокируется. Заряд восстанавливается на столе зарядки станции.

## 6. Энергетическое оружие

Для оружия дополнительно создать:

`Create -> NERA -> Combat -> Weapon Definition`

Путь:

`Configs/Combat/Weapon_<Name>_01.asset`

Заполнить:

- `Weapon Id`;
- `Display Name`;
- урон;
- дальность;
- cooldown;
- `Hit Mask`;
- цвет и длительность beam feedback.

В `ItemData`:

- `Item Type = Equipment`;
- `Quick Access Action = Fire`;
- назначить `Weapon Definition`;
- назначить `Energy Definition`;
- назначить world и equipped prefabs.

На `Player/PlayerModel` должны оставаться `PlayerEquipmentController` и
`PlayerEnergyWeaponController`. Отдельного aim-режима нет: оружие стреляет по
направлению `MainCamera`, не меняя положение камеры и Animator.

## 7. Исследуемый предмет

Цепочку лучше собирать с конца: Library -> Research -> Item -> Prefab.

### Шаг 1. Library entry

Создать через:

`Create -> NERA -> Library -> Entry`

Заполнить:

- `Entry Id`;
- `Title`;
- `Category`: `Station`, `Anomaly` или `Records`;
- короткое содержательное `Description`;
- `Illustration`.

### Шаг 2. Research definition

Создать через:

`Create -> NERA -> Research -> Definition`

Путь:

`Configs/Research/Research_<Name>_01.asset`

Заполнить:

- `Research Id`;
- `Display Name`;
- `Analysis Duration`;
- `Item Fate`: `Return` или `Destroy`;
- `Unlocked Entry` — созданная Library entry.

### Шаг 3. ItemData

В `ItemData.Research Definition` назначить созданный research asset.

После подбора предмет попадёт в инвентарь как исследуемый. После анализа `ResearchController` откроет связанную запись Library.

Для важной находки Expedition 02 итоговая связь должна выглядеть так:

```text
P_WorldItem_NERARelayCore
  -> WorldItem.ItemData
Item_NERARelayCore_02
  -> WorldPrefab
  -> ResearchDefinition
Research_NERARelayCore_02
  -> LibraryEntryData
Library_Expedition02_RelayCore
```

## 8. IO prefab

Создать config через:

`Create -> NERA -> IO -> Enemy Config`

Путь:

`Configs/IO/CFG_IO_<Variant>.asset`

Config хранит здоровье, обнаружение, дистанцию атаки, движение, projectile, цвет энергии и death drop.

Префаб хранить в `Prefabs/IO/` или в папке конкретной экспедиции, если вариант уникален.

На корне игрового prefab:

- `IOEnemyController`;
- назначенный `IOEnemyConfig`;
- Collider;
- NavMeshAgent, если вариант использует навигацию;
- визуальный child;
- необходимые точки VFX/атаки.

Death drop должен ссылаться на готовый world item prefab. Сам предмет по-прежнему описывается через `ItemData`.

## 9. Создание локации и её конфига

### 9.1. Сцена

Игровые сцены хранятся в:

`Assets/_Project/NERA/Scenes/`

Сцена выбирается в поле `Scene` location config из списка включённых сцен
Build Settings. Ссылка хранит GUID и путь, поэтому перенос или переименование
scene asset не требует ручного ввода имени.

Минимальная иерархия:

```text
Expedition_02
  _Scene
    Lighting
    Environment
    Navigation
  _Gameplay
    Spawn_Expedition02_Start
    Objectives
    Interactables
    Items
    IO
    Exit_To_Station
  _Presentation
    VFX
    Audio
```

В сцене должны быть:

- `SceneSpawnPoint` со стабильным `Spawn Point Id`;
- без локального Player и gameplay Camera: постоянный Player с камерой уже
  загружен из `MainScene/RuntimeRoot`;
- проходимая геометрия и colliders;
- NavMesh для навигационных сущностей;
- возврат на станцию;
- сценовый bootstrap/progression компонент;
- lighting, volumes и audio;
- все обязательные предметы и события.

### 9.2. Spawn point

Для Expedition 02:

```text
GameObject: Spawn_Expedition02_Start
SceneSpawnPoint.spawnPointId: Expedition02_Start
```

Значение должно точно совпасть с `ExpeditionLocationData.Spawn Point Id`.

Для станции return transition должен указывать существующий station spawn point, например `Station_Start` или специально созданный `Station_FromExpedition`.

### 9.3. Return transition

`Prefabs/Managers/Expedition_To_Station_Exit.prefab` сейчас является выходом
именно из Expedition 01: он указывает на
`Station_ReturnFromExpedition01`. Не использовать его как универсальный выход
для других локаций.

Для новой локации создать вариант с собственным station spawn point либо
сначала сделать target spawn data-driven.

Проверить `SceneTransitionInteractable`:

- `Target Scene Name = Player_Station`;
- `Target Spawn Point Id` совпадает с ID точки на станции;
- сцена `Player_Station` включена в Build Settings;
- interaction collider находится на слое `Interactable`/`Item`, доступен по
  дистанции и не закрыт стеной из obstruction mask.

### 9.4. Location config

Создать через:

`Create -> NERA -> Expeditions -> Location`

Для обычной экспедиции:

```text
Location Id: Expedition02
Location Type: Expedition
Scene: Expedition_02 [Build]
Spawn Point Id: Expedition02_Start
Discovery Source: Drone
Initial State: Unknown
Map Symbol: Expedition
Map Slot: ссылка на уникальный MapSlotData asset
```

Для неизвестного человеческого сигнала:

```text
Location Type: UnknownSignal
Discovery Source: Antenna
Map Symbol: Unknown
```

Дрон не должен открывать Unknown Signal, а антенна — обычную Expedition.
Для Unknown Signal поле `Map Slot` можно оставить пустым: антенна временно
показывает сигнал на одном из уже открытых слотов.

### 9.5. 3D-слоты карты

Слоты находятся в `MainScene`:

`MapScreen -> MapUIPreview -> MapUIRoot -> SM_UI_3D`

На `SM_UI_3D` расположен `MapLocationSlotRegistry`. Он автоматически собирает
все дочерние компоненты `MapLocationSlot`, поэтому количество слотов не
зафиксировано в коде и имена объектов могут быть любыми.

Чтобы добавить слот:

1. Создать asset через `Create -> NERA -> Terminal Map -> Slot`.
2. Указать уникальный стабильный `Slot Id`.
3. Добавить или скопировать 3D-объект под `SM_UI_3D`.
4. Добавить ему `MapLocationSlot` и назначить созданный asset.
5. Назначить тот же asset в поле `Map Slot` location config.

`Signal Anchor` необязателен. Если он не назначен, маркер антенны создаётся
относительно transform самого 3D-слота.

Можно иметь свободные 3D-слоты без location config. Можно удалять лишние слоты
и location configs — диапазона `1..8` и требования иметь ровно восемь больше
нет. Один `MapSlotData` нельзя назначать двум обычным экспедициям или двум
3D-объектам.

### 9.6. Регистрация location config

Открыть:

`Scenes/MainScene.unity`

Выбрать:

`RuntimeRoot -> ExpeditionDiscoveryController -> Known Locations`

Добавить нужные location configs в список. Текущее наполнение содержит восемь
экспедиций и один неизвестный сигнал, но это не архитектурное ограничение:

- `Location_Expedition01`;
- `Location_Expedition02`;
- `Location_Expedition03` ... `Location_Expedition08`;
- `CFG_UnknownSignal_01`.

Без регистрации конфиг существует как asset, но Drone, Antenna, Terminal и Save его не увидят.

### 9.7. Build Settings

Добавить сцену и включить её. Первые три позиции всегда фиксированы:

- `Boot`;
- `MainScene`;
- `Player_Station`.

После них должны быть включены все сцены, выбранные в location configs.
Выпадающий список `Scene` показывает только включённые Build Settings сцены.
Команда `NERA -> Validate Project` проверяет ссылки, уникальные Location Id,
стабильные Map Slot Id, соответствие 3D-слотов конфигам и регистрацию configs
в `MainScene`.

## 10. Связи через MainScene/RuntimeRoot

`RuntimeRoot` — постоянный центр систем внутри аддитивно загруженной
`MainScene`. Он не переносится в `DontDestroyOnLoad`. На нём находятся:

- `BootInitializer`;
- `StationPowerController`;
- `ExpeditionDiscoveryController`;
- `SaveGameController`;
- `DroneScanController`;
- `EnergySystemController`;
- `LibraryController`;
- `ResearchController`;
- `QuestController`;
- `AntennaController`;
- `LaboratoryWorkstationController` — четыре зарядных слота и два
  подготовленных слота синтеза в общем окне лаборатории.

При добавлении контента обычно не нужно создавать второй контроллер в сцене
экспедиции. Нужно создать данные и зарегистрировать их в существующей системе.
Переходы между станцией и экспедициями должны вызывать
`BootInitializer.LoadGameplayScene`, чтобы не выгрузить `MainScene`.

### Квесты и состояние сцены

Квесты создаются через `NERA -> Quests -> Open Quest Editor`. Есть два варианта:
одноразовый и повторяемый. `QuestCatalog_Default` синхронизируется
автоматически, поэтому вручную регистрировать конфиг не нужно. Полное описание
окна списка, Inspector, каждого поля, игровых сигналов, объектных экземпляров,
компактного HUD, валидации и сохранения находится в
`Quest_System_Guide.md`.

`SaveGameData` версии 14 хранит квестовый прогресс и состояние обслуживаемых
объектов. Обычный `WorldItem` всё ещё уничтожает или отключает только текущий
scene instance: для одноразового контента нужен отдельный стабильный object ID
и восстановление consumed/completed state при загрузке сцены.

## 11. Рекомендуемый порядок сборки Expedition 02

1. Создать папку локального контента Expedition 02.
2. Импортировать и настроить модели, материалы, текстуры, VFX и audio.
3. Собрать модульные environment prefabs.
4. Собрать hero prop Signal Array.
5. Собрать world prefab Ancient Record 02.
6. Создать Library entry для Ancient Record 02, если он должен открывать текст.
7. Собрать world prefab Research Object 02.
8. Создать Library entry результата анализа.
9. Создать `ResearchDefinition` и связать его с Library entry.
10. Создать `ItemData` и связать его с research и world prefab.
11. Собрать IO prefab/event и его config.
12. Собрать сцену `Expedition_02` из готовых prefabs.
13. Добавить `SceneSpawnPoint` с ID `Expedition02_Start`.
14. Добавить return transition на `Player_Station`.
15. Проверить `Location_Expedition02.asset`.
16. Проверить регистрацию config на `MainScene/RuntimeRoot`.
17. Проверить Build Settings.
18. Создать одноразовый `QuestDefinition` через окно квестов и связать authored
    события сцены через `QuestSignalEmitter` или существующие runtime-сигналы.

## 12. Чек-лист одного предмета

- [ ] Модель и материалы лежат в правильной content-папке.
- [ ] World prefab имеет `WorldItem` и collider.
- [ ] Equipped prefab не содержит world/gameplay компонентов.
- [ ] Создан уникальный `ItemData.itemId`.
- [ ] В `ItemData` назначены icon и world prefab.
- [ ] Для оборудования назначены action, key и equipped prefab.
- [ ] Для энергетического предмета назначен `ItemEnergyDefinition`.
- [ ] Для оружия назначен `WeaponDefinition`.
- [ ] Для research item назначен `ResearchDefinition`.
- [ ] `ResearchDefinition` ссылается на Library entry.
- [ ] World prefab обратно ссылается на `ItemData`.
- [ ] Предмет появился в `ItemCatalog_Default` автоматически.
- [ ] Нет повторяющегося `itemId`.
- [ ] Подбор, save/load, расход энергии и зарядка проверены.

## 13. Чек-лист одной сцены

- [ ] Имя сцены совпадает с location config.
- [ ] Сцена включена в Build Settings.
- [ ] Spawn point ID совпадает с location config.
- [ ] Location config зарегистрирован в `Known Locations`.
- [ ] Правильно выбраны `Location Type` и `Discovery Source`.
- [ ] Player появляется в ожидаемой точке.
- [ ] Все interactables имеют colliders на слое `Interactable`/`Item`, доступны
  по дистанции и не закрыты геометрией.
- [ ] Все world items ссылаются на правильные `ItemData`.
- [ ] Return transition ведёт в существующую сцену и spawn point.
- [ ] NavMesh построен, если в сцене есть навигационные сущности.
- [ ] Одноразовые находки не дублируются после reload.
- [ ] Состояние восстанавливается после save/load.
- [ ] Сцена пройдена через Boot, а не запущена напрямую.
- [ ] Console не содержит ошибок.

## 14. Что проверять после каждой партии контента

1. Дождаться завершения импорта и компиляции.
2. Проверить Console.
3. Запустить EditMode tests.
4. Начать игру через Boot.
5. Проверить обнаружение локации правильной системой.
6. Проверить переход и spawn.
7. Подобрать предметы.
8. Вернуться на станцию.
9. Проверить inventory, вкладки лаборатории Power/Scan/Upgrade и Library.
10. Выполнить save/load и повторно открыть сцены.

Контент считается подключённым только после прохождения всей цепочки. Наличие prefab или config в Project window само по себе не означает, что объект участвует в игре.
