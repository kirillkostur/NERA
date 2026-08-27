# NERA — инструкция по сборке игрового среза

Версия документа: 28.07.2026  
Основание: текущие сцены, prefab, ScriptableObject-конфиги и runtime-код проекта.

## 1. Для чего нужен этот документ

Это практическая инструкция по добавлению контента в уже работающий проект NERA. Она описывает:

- как собрать полный игровой срез из существующих систем;
- как создавать предметы, оружие, энергетическое оборудование и находки;
- как создавать IO-врагов;
- как создавать экспедиции и неизвестные сигналы;
- как связывать находку с лабораторией и Library;
- как добавлять объекты станции и их уровни улучшения;
- как собирать сцены и переходы;
- какие глобальные конфиги обязательно обновлять;
- что уже работает через конфиги, а что пока остаётся ограничением текущего кода.

Если старый документ противоречит этой инструкции, приоритет имеют:

1. текущий код и текущие сцены;
2. `NERA - Current Scope Decisions.txt`;
3. эта инструкция;
4. более старые Bible, Sprint и Milestone-документы.

Система переводов удалена из проекта. Полей Translation, Translation Level, Translation State и связанных объектов создавать не нужно. Все названия и описания сейчас задаются напрямую в конфигурациях.

---

## 2. Что уже составляет рабочий срез

В проекте уже существует базовая цепочка:

`Boot → станция → восстановление питания → терминал → карта → запуск дрона → экспедиция → находка/враг → возвращение → лаборатория → Library`

Расширенная цепочка Milestone 02:

`Станция → сканирование дроном → Expedition 02 → находки → лаборатория → Library → доступная антенна → калибровка → Unknown Signal → возвращение → новые записи`

Для демонстрационного среза достаточно, чтобы игрок мог:

1. начать игру только через сцену `Boot`;
2. включить питание станции;
3. открыть терминал;
4. выбрать сектор на 3D-карте;
5. запустить дрон и дождаться окончания сканирования;
6. перейти в открытую экспедицию;
7. встретить хотя бы одного врага;
8. подобрать хотя бы одну полезную находку;
9. вернуться на станцию;
10. исследовать находку в лаборатории;
11. увидеть результат в Library;
12. потратить находку на одно улучшение станции;
13. открыть неизвестный сигнал через антенну;
14. посетить сигнал и вернуться.

## 3. Главный принцип архитектуры

В проекте контент разделён на четыре уровня.

| Уровень | Что хранит | Пример |
|---|---|---|
| Config / ScriptableObject | Данные и баланс | `ItemData`, `IOEnemyConfig`, `ExpeditionLocationData` |
| Prefab | Внешний вид и компоненты объекта | `P_WorldItem_*`, `IO_Blue_*`, `P_StationTurret` |
| Scene | Расстановка и маршрут | `Expedition_02.unity` |
| Boot / RuntimeRoot | Постоянные сервисы и сохранение | энергия, инвентарь, дрон, антенна, Library |

Правильная зависимость:

`Config → назначается в Prefab → Prefab ставится в Scene → Boot управляет состоянием`

Не следует хранить баланс врага в четырёх разных prefab. Создаётся один prefab-шаблон и разные `IOEnemyConfig`. Аналогично, параметры предмета находятся в `ItemData`, а его внешний вид — в prefab.

## 4. Правила идентификаторов

Идентификаторы используются сохранением. После выпуска тестовой сборки их нельзя без миграции переименовывать.

| Сущность | Поле | Пример |
|---|---|---|
| Предмет | `itemId` | `nera_signal_relay_02` |
| Исследование | `researchId` | `research_nera_signal_relay_02` |
| Library-запись | `entryId` | `expedition02_signal_relay` |
| Локация | `locationId` | `Expedition02` |
| Враг | `enemyId` | `io_blue_relay_guard` |
| Оружие | `weaponId` | `energy_pistol_01` |
| Объект станции | `objectId` | `station_turret_02` |
| Батарея | `batteryId` | `station_battery_02` |
| Солнечная панель | `panelId` | `station_solar_01` |
| Точка появления | `spawnPointId` | `Expedition02_Start` |

Рекомендации:

- использовать латиницу без пробелов;
- для внутренних ID использовать `snake_case`;
- ID разных экземпляров повторяющегося объекта должны отличаться;
- `sceneName` должен точно совпадать с именем сцены в Build Settings;
- `spawnPointId` в конфиге локации должен точно совпадать с `SceneSpawnPoint` в сцене;
- `previewObjectName` должен точно совпадать с именем корня объекта в 3D-превью станции.

---

## 5. Структура папок

Основные места для контента:

```text
Assets/_Project/NERA/
├─ Art/                         модели, материалы, анимации, изображения
├─ Configs/
│  ├─ Camera/                  профили дистанции камеры
│  ├─ Combat/                  оружие
│  ├─ Expeditions/             обычные экспедиции
│  ├─ IO/                      конфиги IO-врагов
│  ├─ Items/                   предметы и энергия предметов
│  ├─ Library/                 локальные Library-конфиги
│  ├─ Locations/               неизвестные сигналы
│  └─ Research/                лабораторные исследования
├─ Prefabs/
│  ├─ Camera/
│  ├─ Interaction/
│  ├─ IO/
│  ├─ Items/
│  ├─ Managers/
│  ├─ Player/
│  ├─ Station/
│  ├─ StationUpgrade/
│  └─ UI/
├─ Resources/
│  ├─ Energy/EnergyBalance_Default.asset
│  ├─ Inventory/DefaultInventoryConfig.asset
│  ├─ Library/
│  ├─ Station/StationSystems_Default.asset
│  └─ ItemCatalog_Default.asset
└─ Scenes/
   ├─ Boot/Boot.unity
   ├─ Station/Player_Station.unity
   └─ Expeditions/
```

`Resources` используется только там, где код загружает конфиг по фиксированному пути. Не переносить следующие файлы без изменения кода:

- `Resources/ItemCatalog_Default.asset`;
- `Resources/Inventory/DefaultInventoryConfig.asset`;
- `Resources/Energy/EnergyBalance_Default.asset`;
- `Resources/Station/StationSystems_Default.asset`.

---

## 6. Boot и Build Settings

### 6.1. Игру всегда запускать через Boot

`Boot.unity` содержит постоянный `RuntimeRoot`, который переживает загрузку других сцен.

На `RuntimeRoot` находятся:

- `BootInitializer`;
- `StationPowerController`;
- `ExpeditionDiscoveryController`;
- `SaveGameController`;
- `DroneScanController`;
- `StationEnvironmentController`;
- `EnergySystemController`;
- `LibraryController`;
- `ResearchController`;
- `ExpeditionProgressController`;
- `AntennaController`;
- `LaboratoryWorkstationController`;
- `StationStorageController`;
- `StationSystemsController`.

Дочерние объекты:

- `Player`;
- `Player_Camera`;
- `HUD_Canvas`;
- `EventSystem`.

Не добавлять второй Player, Main Camera, HUD или EventSystem в игровые сцены. Они уже загружаются из Boot.

### 6.2. Текущий Build Settings

| Index | Scene |
|---:|---|
| 0 | `Boot` |
| 1 | `Player_Station` |
| 2 | `Expedition_01` |
| 3 | `Expedition_02` |
| 4 | `UnknownSignal_01_FirstPlayable` |

Каждую новую игровую сцену необходимо добавить в:

`File → Build Profiles / Build Settings → Scene List`

Сцена должна быть включена. Переходы проверяют `Application.CanStreamedLevelBeLoaded`, поэтому одной сцены в папке недостаточно.

### 6.3. BootInitializer

Текущие значения:

- `Initial Scene Name`: `Player_Station`;
- `Initial Spawn Point Id`: `Station_Start`.

Менять их нужно только если меняется стартовая сцена всей игры.

---

## 7. Слои, теги и взаимодействие

### 7.1. Слои проекта

| Layer | Назначение |
|---|---|
| `Player` | игрок |
| `Interactable` | интерактивные объекты |
| `Item` | предметы мира |
| `Enemy` | враги и попадания оружия |
| `Wall` | стены |
| `Ground` | земля |
| `Environment` | окружение |
| `Trigger` | служебные триггеры |
| `StationUI` | 3D-объекты UI-превью |

### 7.2. Как создать обычный интерактивный объект

1. Создать GameObject или prefab.
2. Добавить видимый объект.
3. Добавить Collider.
4. Поставить слой `Interactable`.
5. Добавить нужный наследник `BaseInteractable`:
   - `WorldItem` — предмет;
   - `SceneTransitionInteractable` — переход;
   - `MaintainableObject` — обслуживание;
   - `TerminalAccessInteractable` — терминал;
   - `LaboratoryTableInteractable` — лаборатория;
   - `PowerRestoreInteractable` — ручное включение станции.
6. Заполнить:
   - `Action Text`;
   - `Mode`: `Press` или `Hold`;
   - `Hold Duration`;
   - `Is Available`;
   - `Unavailable Reason`.

Игрок ищет объект лучом из центра камеры. Текущая дистанция взаимодействия — около `2.5`. Collider должен находиться перед камерой и не быть закрыт другим Collider.

При обнаружении интерактивного объекта камера включает существующий interaction-AIM. Не добавлять локальные камеры на каждый объект.

---

## 8. Предметы

### 8.1. Существующие ItemType

| ItemType | Куда попадает в инвентаре | Library |
|---|---|---|
| `EngineeringPart` | 8 обычных слотов | Details |
| `Artifact` | 8 обычных слотов | Details |
| `Record` | 8 обычных слотов | Record |
| `Equipment` | 4 quick-access слота | Equipment |
| `Anomaly` | 4 anomaly-слота | Anomaly |
| `Consumable` | 8 обычных слотов | Details |
| `KeyItem` | 8 обычных слотов | Details |

Тип определяет не только подпись, но и допустимую группу слотов.

Новый предмет существующего типа создаётся без кода. Новый `ItemType` потребует изменения:

- `ItemType.cs`;
- маршрутизации `PlayerInventory.GetSlotGroup`;
- Library-категорий;
- при необходимости UI склада и лаборатории;
- тестов сохранения и drag-and-drop.

### 8.2. Создание простого предмета

Пример: новая деталь станции.

1. В `Configs/Items`:
   `Create → NERA → Items → Item Data`.
2. Назвать asset, например `Item_AntennaCoil_01`.
3. Заполнить:

| Поле | Что писать |
|---|---|
| `Item Id` | стабильный уникальный ID, например `antenna_coil_01` |
| `Display Name` | имя в UI |
| `Description` | описание |
| `Item Type` | например `EngineeringPart` |
| `Icon` | Sprite для инвентаря |
| `World Prefab` | prefab подбираемого объекта |
| `Equipped Visual Prefab` | пусто для обычной детали |
| `Research Definition` | пусто, если предмет не исследуется |
| `Weapon Definition` | пусто |
| `Energy Definition` | пусто |

4. Создать world-prefab по разделу 8.3.
5. Вернуться в `ItemData` и назначить `World Prefab`.
6. Добавить `ItemData` в `Resources/ItemCatalog_Default.asset`.
7. Проверить `SaveGameController` на `RuntimeRoot`: основной `Item Database` должен ссылаться на этот каталог. Сериализованный fallback-список `Item Catalog` желательно также держать в актуальном состоянии.

Если предмет не добавлен в каталог, он может подбираться в текущей сессии, но сохранение не сможет надёжно восстановить его по `itemId`.

### 8.3. Создание world-prefab предмета

Самый безопасный путь:

1. Дублировать похожий prefab из `Prefabs/Items`.
2. Назвать `P_WorldItem_<Name>`.
3. На корне оставить:
   - Transform;
   - Collider;
   - Renderer или дочерний визуал;
   - `WorldItem`.
4. Слой корня — `Interactable`.
5. Collider не должен быть Trigger.
6. В `WorldItem` назначить новый `ItemData`.
7. `Destroy After Pickup` обычно включён.
8. В `BaseInteractable` задать понятный `Action Text`, например `Pick Up`.
9. Назначить prefab в `ItemData.World Prefab`.

При подборе `WorldItem`:

- создаёт уникальный `ItemInstance`;
- помещает его в соответствующую группу инвентаря;
- регистрирует обычный предмет в Library;
- для исследуемого предмета отмечает получение образца;
- уничтожает или выключает объект мира.

### 8.4. Размещение предмета в сцене

Использовать prefab, а не копию меша без `WorldItem`.

Проверить:

- объект достижим;
- Collider виден лучу камеры;
- перед предметом нет невидимой стены;
- нужная группа инвентаря не переполнена;
- предмет стоит выше поверхности и не провален в Collider земли.

### 8.5. Энергетический предмет

Создать:

`Create → NERA → Items → Item Energy Definition`

Поля:

| Поле | Значение |
|---|---|
| `Capacity` | максимальный заряд |
| `Initial Charge` | заряд нового экземпляра |
| `Energy Per Use` | расход за применение |
| `Recharge Per Second` | скорость зарядки в лаборатории |

Назначить asset в `ItemData.Energy Definition`.

Заряд принадлежит `ItemInstance`, а не общему `ItemData`. Два одинаковых пистолета могут иметь разный заряд.

### 8.6. Оборудование и quick access

Для предмета `Equipment` заполнить:

| Поле | Назначение |
|---|---|
| `Equipped Visual Prefab` | модель в руке |
| `Equipment Anchor Name` | имя кости, обычно `mixamorig1:RightHand` |
| `Equipped Local Position` | локальное смещение |
| `Equipped Local Euler Angles` | локальный поворот |
| `Quick Access Action` | действие |
| `Use Key` | клавиша применения |

Существующие действия:

- `None`;
- `ToggleLight`;
- `Scan`;
- `Fire`.

Новый тип действия требует кода.

### 8.7. Создание инструмента/оружия

1. Создать `ItemData` типа `Equipment`.
2. Создать:
   `Create → NERA → Combat → Weapon Definition`.
3. Заполнить:

| Поле | Назначение |
|---|---|
| `Weapon Id` | уникальный ID |
| `Display Name` | имя |
| `Damage` | урон |
| `Range` | дальность |
| `Cooldown` | пауза между выстрелами |
| `Hit Mask` | обычно только слой `Enemy` |
| `Beam Color` | цвет debug-луча |
| `Debug Beam Duration` | время отображения debug-луча |

4. Создать `ItemEnergyDefinition`.
5. В `ItemData` назначить:
   - `Quick Access Action = Fire`;
   - `Weapon Definition`;
   - `Energy Definition`;
   - `Equipped Visual Prefab`;
   - иконку и world-prefab.
6. Добавить `ItemData` в `ItemCatalog_Default`.

Обычное оружие и прочее `Equipment` не принимают IO-камни. Для интеграции используется отдельный предмет `Item_IOIntegrator_01` с `itemId = io_integrator_01`.

В его `ItemData` обязательно:

- `Item Type = Equipment`;
- `Accepts Anomaly Integration = true`;
- назначен `ItemEnergyDefinition`;
- начальный заряд равен полной ёмкости;
- `Quick Access Action = None`, потому что применение обрабатывается отдельной IO-логикой по `R`.

У остальных Equipment `Accepts Anomaly Integration` должен оставаться выключенным.

### 8.8. Создание IO-камня для интеграции

Для IO-камня нужны:

1. `ItemData` типа `Anomaly`;
2. `ResearchDefinition`;
3. `AnomalyIntegrationDefinition`:
   `Create → NERA → Combat → Anomaly Integration Definition`.

Поля `AnomalyIntegrationDefinition`:

| Поле | Назначение |
|---|---|
| `Integration Id` | стабильный уникальный ID способности |
| `Display Name` | название интегрированного эффекта |
| `Display Color` | цвет способности для будущего визуального различия |
| `Compatible Equipment` | разрешённые IO-интеграторы; пустой список разрешает любой Equipment с включённым `Accepts Anomaly Integration` |
| `Effect` | `Enable Electronics`, `Damage Anomalies` или `Disable Electronics` |
| `Radius` | радиус импульса |
| `Anomaly Damage` | урон IO для эффекта `Damage Anomalies` |
| `Electronic Duration` | длительность включения/отключения электроники |
| `Affected Layers` | слои объектов, попадающих в импульс |

Назначить этот asset в `ItemData.Anomaly Integration Definition`. Камень без этого поля остаётся обычной аномалией и не принимается правым слотом синтеза.

Текущий пример:

- `Item_IOBlueShard_01`;
- `Integration_IOBlue_Discharge`;
- одна интеграция даёт одно применение;
- радиальный урон IO в радиусе 8.

Чтобы объект реагировал на эффекты включения и отключения электроники:

1. добавить `AnomalyElectronicDevice`;
2. в `Powered Objects` указать визуальные/функциональные GameObject;
3. в `Powered Behaviours` указать компоненты, которые нужно включать или отключать;
4. Collider объекта должен попадать в `Affected Layers`.

### 8.9. Исследуемая находка

Для полной цепочки нужны три asset:

1. `ItemData`;
2. `ResearchDefinition`;
3. `LibraryEntryData`.

Порядок:

1. Создать Library-запись:
   `Create → NERA → Library → Entry`.
2. Заполнить:

| Поле | Назначение |
|---|---|
| `Entry Id` | уникальный стабильный ID |
| `Title` | заголовок |
| `Category` | `Station`, `Anomaly` или `Records` |
| `Description` | итоговое знание |
| `Illustration` | изображение |

3. Создать исследование:
   `Create → NERA → Research → Definition`.
4. Заполнить:

| Поле | Назначение |
|---|---|
| `Research Id` | уникальный ID |
| `Display Name` | имя процесса |
| `Analysis Duration` | длительность |
| `Item Fate` | `Return` или `Destroy` |
| `Unlocked Entry` | созданная Library-запись |

5. Назначить `ResearchDefinition` в `ItemData.Research Definition`.
6. Добавить `ItemData` в `ItemCatalog_Default`.

`Item Fate = Return` возвращает предмет после анализа.  
`Item Fate = Destroy` расходует образец.

Для записей, которые должны появляться в текстовом разделе Library, использовать `LibraryCategory.Records`. Anomaly и Equipment в основном строятся из исследованных `ItemData`.

---

## 9. Инвентарь, склад и лаборатория

### 9.1. Глобальный InventoryConfig

Файл:

`Resources/Inventory/DefaultInventoryConfig.asset`

Текущие значения:

- `Backpack Capacity = 8`;
- `Slot Prefab = P_InventorySlot`.

Anomaly и quick-access имеют по 4 слота в коде.

Prefab `P_InventorySlot` должен назначаться только в `InventoryConfig`. UI создаёт экземпляры этого prefab внутри authored spawn points.

### 9.2. Правило Slot_N

Объекты `Slot_N` в authored UI — это точки появления prefab `P_InventorySlot`, а не сами игровые слоты.

Не помещать внутрь них вручную иконки предметов. Runtime сам:

- создаёт `P_InventorySlot`;
- назначает изображение;
- добавляет drag-and-drop;
- обновляет количество/заряд;
- связывает слот с инвентарём или складом.

### 9.3. InventoryScreen

Текущая структура:

- `background_Screen_Storage_Slot_Invent` — 8 обычных `Slot_N`;
- `background_Screen_Storage_Slot_Invent_Anomaly` — 4 anomaly `Slot_N`;
- отдельный `Slot_Invent_Equipment` — 4 quick-access `Slot_N`;
- `DropButton` — выбрасывает выбранный предмет в мир.

Инвентарь открывается на `I`. Во время терминала или лаборатории обычное открытие инвентаря блокируется, quick-access HUD скрывается.

### 9.4. StorageScreen терминала

Склад разделён на:

- обычные предметы;
- аномалии;
- оборудование.

Корни склада:

- `background_Screen_Storage_Slot`;
- `background_Screen_Storage_Slot_Anomaly`;
- `background_Screen_Storage_Slot_Equipment`.

Копия инвентаря игрока:

- `background_Screen_Storage_Slot_Invent`;
- `background_Screen_Storage_Slot_Invent_Anomaly`;
- `background_Screen_Storage_Slot_Invent_Equipment`.

Информационный блок:

- `background_Screen_Storage_Info`;
- `Text_Name`;
- `Image_info`;
- `Text_Description`.

Склад никогда сам не переносит обычные предметы из инвентаря. Перенос выполняет игрок drag-and-drop.

Исключение: после явного нажатия `Upgrade` система станции расходует необходимые детали сначала со склада, затем из инвентаря. Это не автоматическое складирование, а оплата подтверждённого улучшения.

### 9.5. LaboratoryScreen

Главные вкладки:

- `PowerMapButton` → `PowerScreen`;
- `ScanMapButton` → `ScanScreen`;
- `UpgradeMapButton` → `UpgradeScreen`;
- `Q` и `E` циклично переключают вкладки.

Общий блок `Inventory_and_info_Screen` должен оставаться активным во всех вкладках.

Внутри:

- `background_Screen_Storage_Slot_Invent`;
- `background_Screen_Storage_Slot_Invent_Anomaly`;
- `background_Screen_Storage_Slot_Invent_Equipment`;
- `background_Screen_Storage_Info`;
- `Text_Name`;
- `Image_info`;
- `Text_Description`.

`LaboratoryScreenController.BuildInventoryGroup(...)` создаёт интерактивные копии слотов через `InventorySlotSpawnUtility.GetOrCreate`. Лабораторные слоты отражают те же `ItemInstance`, что и основной инвентарь, и поддерживают click, drag-and-drop и обновление информации.

#### PowerScreen

- `background_Screen_Storage_Slot/Slot_N` — места зарядки;
- `Text_progress_01`, `Text_progress_02` и далее — заряд соответствующего слота;
- `DropButton` — вернуть заряжаемые предметы в инвентарь.

В слот зарядки класть только предметы с `ItemEnergyDefinition`.

#### ScanScreen

- `background_Screen_Storage_Slot/Slot` — один исследуемый предмет;
- `ScanButton` — начать анализ;
- `Text_progress` — проценты;
- `DropButton` — вернуть образец, если анализ не идёт.

Предмет должен иметь `ResearchDefinition`. Сканирование принадлежит конкретному `ItemInstance`, а не общему типу предмета:

- каждый найденный IO-камень необходимо отсканировать отдельно;
- уже отсканированный экземпляр нельзя сканировать повторно;
- первый отсканированный экземпляр типа открывает данные в Library;
- последующие экземпляры с тем же `Research Id` проходят собственное сканирование, но не создают повторную запись Library.

#### UpgradeScreen

- `background_Screen_Storage_Slot/Slot_01`;
- `background_Screen_Storage_Slot/Slot_02`;
- `UpgradeButton`;
- `DropButton`.

Правила:

- `Slot_01` принимает только полностью заряженный `IO Integrator` с включённым `Accepts Anomaly Integration`;
- `Slot_02` принимает только `Anomaly` с назначенным `AnomalyIntegrationDefinition`;
- конкретный экземпляр камня в `Slot_02` должен иметь сохранённый флаг `IsScanned`;
- до выполнения условий `UpgradeButton` неактивна;
- после синтеза камень исчезает;
- тот же экземпляр IO Integrator остаётся в `Slot_01` и получает одно применение эффекта камня;
- `DropButton` возвращает инструмент в quick-access;
- `R` активирует эффект, полностью обнуляет энергетический заряд и удаляет текущую интеграцию;
- после применения IO Integrator нужно зарядить в `PowerScreen` до 100%;
- только после полной зарядки в него можно интегрировать следующий камень;
- пустой, частично заряженный или неинтегрированный инструмент по `R` ничего не делает.

Нельзя заменить неиспользованный камень другим: сначала требуется применить установленный эффект.

---

## 10. Создание IO-врага

### 10.1. Что уже поддерживает IOEnemyController

Враг:

- ищет объект с тегом `Player`;
- обнаруживает его по радиусу;
- напрямую движется к цели;
- зависает на заданной высоте;
- поворачивается к игроку;
- стреляет энергетическим снарядом;
- получает урон через `IDamageable`;
- после смерти создаёт `Death Drop Prefab`.

NavMesh не используется. Враг движется через `Vector3.MoveTowards`, поэтому арену нужно делать достаточно открытой. Сложные стены и узкие проходы могут заблокировать или визуально испортить преследование.

### 10.2. Создание конфига

`Create → NERA → IO → Enemy Config`

Поля:

| Группа | Поле | Назначение |
|---|---|---|
| Identity | `Enemy Id` | уникальный ID |
| Identity | `Display Name` | имя |
| Movement | `Max Health` | здоровье |
| Movement | `Detection Radius` | радиус обнаружения |
| Movement | `Attack Range` | дальность атаки, не больше detection |
| Movement | `Move Speed` | скорость |
| Movement | `Hover Height` | базовая высота |
| Movement | `Hover Amplitude` | амплитуда |
| Movement | `Hover Frequency` | частота |
| Attack | `Attack Cooldown` | пауза |
| Attack | `Projectile Speed` | скорость снаряда |
| Attack | `Projectile Lifetime` | время жизни |
| Attack | `Projectile Damage` | урон игроку |
| Attack | `Projectile Scale` | размер |
| Attack | `Projectile Prefab` | необязательный визуальный prefab |
| Visual | `Energy Color` | цвет |
| Visual | `Emission Intensity` | emission врага |
| Visual | `Projectile Emission Intensity` | emission снаряда |
| Drop | `Death Drop Prefab` | world-prefab находки |
| Drop | `Death Drop Offset` | смещение точки выпадения |

Если `Projectile Prefab` пуст, код создаёт светящуюся Sphere. Это безопасный текущий вариант.

### 10.3. Создание prefab врага

Самый надёжный путь:

1. Дублировать `IO_Blue_Weak.prefab`.
2. Назвать новый prefab.
3. На корне оставить:
   - Collider;
   - Renderer/MeshFilter или свой дочерний визуал;
   - `IOEnemyController`.
4. Поставить слой `Enemy`.
5. В `IOEnemyController.Config` назначить новый `IOEnemyConfig`.
6. Настроить размер Collider.
7. Проверить материал: контроллер создаёт runtime-копию материала и задаёт цвет/emission.
8. Поставить prefab в сцену.

Если используется собственный `Projectile Prefab`, не добавлять в него заранее `IOEnergyProjectile`: контроллер добавляет компонент в момент выстрела.

### 10.4. Настройка дропа

`Death Drop Prefab` должен быть полноценным `P_WorldItem_*`, а не только моделью.

Цепочка:

`IOEnemyConfig.Death Drop Prefab → WorldItem.ItemData → ResearchDefinition → LibraryEntry`

Так убийство врага становится частью основного игрового цикла.

---

## 11. Локации: общий конфиг

Обычная экспедиция и неизвестный сигнал используют один класс:

`ExpeditionLocationData`

Создание:

`Create → NERA → Expeditions → Location`

### 11.1. Поля ExpeditionLocationData

| Поле | Что задавать | Используется сейчас |
|---|---|---|
| `Location Id` | уникальный строковый ID | да, discovery/save |
| `Id` | значение `LocationId` enum | почти нет, метаданные |
| `Location Type` | `Expedition` или `UnknownSignal` | да |
| `Display Name` | имя в UI | да |
| `Description` | описание | да |
| `Scene Name` | точное имя сцены | да |
| `Spawn Point Id` | точный ID точки входа | да |
| `Discovery Source` | `Drone`, `Antenna` или `Debug` | да |
| `Drone Scan Duration` | время сканирования | да для Drone |
| `Required Drone Upgrade Level` | требуемый уровень дрона | да для Drone |
| `Initial State` | Unknown/Discovered/Visited/Completed | пока не обрабатывается |
| `Map Symbol` | Expedition/QuestionMark | пока не обрабатывается |
| `Map Sector Index` | сектор 0–8 | да |
| `Map Preview` | Sprite | пока не выводится новым 3D HUD |

Поля `Initial State`, `Map Symbol` и `Map Preview` следует заполнять семантически правильно, но текущий runtime на них не опирается.

### 11.2. Ограничение LocationId

`LocationId.cs` сейчас содержит только:

- `Expedition01`;
- `Expedition02`;
- `UnknownSignal01`.

Для новой локации рекомендуется добавить новое enum-значение. Строковый `locationId` является главным runtime/save ID, но оставлять enum с неверным значением создаёт путаницу в Inspector.

---

## 12. Создание новой экспедиции

### 12.1. Создать сцену

Безопаснее всего дублировать `Expedition_02.unity`.

1. Сохранить копию в `Scenes/Expeditions`.
2. Переименовать сцену, например `Expedition_03`.
3. Удалить старый blockout, врагов и находки, но сохранить системные объекты:
   - `Directional Light`;
   - `ExpeditionSystems` с `ExpeditionSceneBootstrap`;
   - `SceneSpawnPoint`;
   - выход на станцию.
4. Собрать:
   - землю с Collider;
   - основной маршрут;
   - ориентиры;
   - боевую арену;
   - место находки;
   - путь назад.
5. Добавить сцену в Build Settings.

Камеру и игрока в сцену не добавлять — они постоянные из Boot. Основной Directional Light в сцене оставить.

### 12.2. Настроить вход

1. Создать пустой объект, например `Spawn_Expedition03_Start`.
2. Добавить `SceneSpawnPoint`.
3. Заполнить:
   `Spawn Point Id = Expedition03_Start`.
4. Развернуть Transform в направлении начала маршрута.

### 12.3. Настроить выход

1. Добавить `Prefabs/Managers/Expedition_To_Station_Exit.prefab`.
2. В `SceneTransitionInteractable` проверить:
   - `Target Scene Name = Player_Station`;
   - `Target Spawn Point Id = Station_ReturnFromExpedition01` либо новая существующая точка возврата;
   - `Action Text = Return to Station`.
3. Выход должен иметь Collider и находиться на слое `Interactable`.

Текущий prefab использует общий `Station_ReturnFromExpedition01`. Название историческое, но технически его можно использовать для всех экспедиций. Для ясности в будущем лучше создать общий `Station_ReturnFromExpedition`.

### 12.4. Создать конфиг экспедиции

Создать asset в `Configs/Expeditions`, например `Location_Expedition03`.

Пример:

```text
Location Id: Expedition03
Id: Expedition03
Location Type: Expedition
Display Name: Expedition 03 — Ruined Depot
Description: ...
Scene Name: Expedition_03
Spawn Point Id: Expedition03_Start
Discovery Source: Drone
Drone Scan Duration: 6
Required Drone Upgrade Level: 2
Initial State: Unknown
Map Symbol: Expedition
Map Sector Index: 3
Map Preview: optional
```

### 12.5. Зарегистрировать экспедицию в Boot

Открыть:

`Boot → RuntimeRoot → ExpeditionDiscoveryController → Known Locations`

Добавить новый `ExpeditionLocationData`.

Порядок списка важен: дрон и антенна при поиске следующего кандидата обходят список сверху вниз.

### 12.6. Добавить сектор в 3D-карту

В `TerminalScreen/MapScreen` находится 3D-превью карты:

- `Map_RawImage`;
- `MapUICamera`;
- корень `SM_UI_3D`.

Для новой экспедиции:

1. Выбрать неиспользуемый `Map Sector Index` от 0 до 8.
2. В 3D-модели карты создать или продублировать сектор.
3. Назвать объект:
   `SM_Expedition_XX`, где `XX = Map Sector Index + 1`.
4. Пример:
   - index 0 → `SM_Expedition_01`;
   - index 3 → `SM_Expedition_04`.
5. Добавить Collider на кликаемый объект.
6. Поставить слой, который видит `MapUICamera`.
7. Проверить, что Collider не перекрыт другим объектом.

Код поддерживает девять authored-секторов. Для десятого потребуется расширение `Range(0, 8)` и 3D-карты.

### 12.7. Разместить контент

Минимальный набор:

- 1–3 IO-врага;
- 1 исследуемая находка;
- 1 инженерная деталь;
- понятный путь к выходу.

Находки ставятся prefab-экземплярами из `Prefabs/Items`.

### 12.8. Проверить экспедицию

1. Запустить Boot.
2. Включить станцию.
3. Открыть терминал.
4. Выбрать authored-сектор.
5. Проверить активность Launch.
6. Дождаться 100%.
7. Нажать сектор повторно.
8. Подтвердить YES.
9. Проверить появление в `Spawn Point Id`.
10. Убить врага и подобрать предмет.
11. Вернуться.
12. Проверить сохранение discovery и предмета.

---

## 13. Создание неизвестного сигнала

Неизвестный сигнал — это та же `ExpeditionLocationData`, но с другими полями.

### 13.1. Создать сцену

Дублировать `UnknownSignal_01_FirstPlayable.unity`.

Обязательно оставить или создать:

- `Directional Light`;
- `ExpeditionSystems` с `ExpeditionSceneBootstrap`;
- `SceneSpawnPoint`;
- выход на станцию;
- уникальную находку или след человека.

### 13.2. Создать конфиг

Хранить в `Configs/Locations`.

Пример:

```text
Location Id: UnknownSignal02
Id: UnknownSignal02
Location Type: UnknownSignal
Display Name: ?
Description: Unknown signal. Antenna analysis required.
Scene Name: UnknownSignal_02
Spawn Point Id: UnknownSignal02_Start
Discovery Source: Antenna
Drone Scan Duration: 5
Required Drone Upgrade Level: 0
Initial State: Unknown
Map Symbol: QuestionMark
Map Sector Index: 0
Map Preview: optional
```

Для сигнала критично:

- `Location Type = UnknownSignal`;
- `Discovery Source = Antenna`.

### 13.3. Добавить в Known Locations

Добавить asset в:

`Boot → RuntimeRoot → ExpeditionDiscoveryController → Known Locations`

Антенна выбирает первый подходящий неиспользованный сигнал в этом списке.

### 13.4. Как сигнал появляется на карте

Отдельный authored `SM_Expedition_XX` для сигнала не требуется.

После успешной калибровки:

1. антенна выбирает один уже открытый сектор экспедиции;
2. внутри соответствующего `SM_Expedition_XX` создаётся маленький runtime-куб;
3. клик по кубу выбирает `ActiveSignal`;
4. YES загружает сцену сигнала;
5. после перехода сигнал помечается использованным и не предлагается повторно.

### 13.5. Условия запуска калибровки

Калибровка доступна, если:

- станция запитана;
- есть хотя бы одна открытая экспедиция с сектором 0–8;
- в Known Locations есть неиспользованный `Discovery Source = Antenna`;
- антенна улучшена минимум с level 0 до level 1;
- система Antenna активна;
- состояние обслуживания выше 0;
- хватает энергии;
- сейчас нет другого Active Signal.

В `AntennaController` есть `Signal Discovery Chance`. В текущем Boot значение `0.5`. Для гарантированного демонстрационного среза временно поставить `1.0`; для игрового баланса вернуть желаемую вероятность.

### 13.6. Контент неизвестного сигнала

Для осмысленного результата создать отдельную цепочку:

`новый ItemData → новый world-prefab → новый ResearchDefinition → новая LibraryEntry`

Не использовать предмет из предыдущей экспедиции как финальную находку, если сигнал должен давать новое сюжетное знание.

---

## 14. Цели экспедиций: текущее ограничение

Discovery и переходы между сценами уже data-driven через `ExpeditionLocationData`.

Но отдельная система целей пока не data-driven:

- `ExpeditionProgressController` содержит конкретные флаги Expedition 01;
- текст цели также зашит в этот контроллер;
- `ExpeditionSceneBootstrap` только отмечает посещение;
- `Initial State`, `Visited`, `Completed` пока не образуют универсальный lifecycle;
- уникальное состояние каждого world-item в сцене не сохраняется.

Следствие:

- новую сцену, дрон-скан, переход, врагов, предметы и возврат можно создать без нового контроллера;
- уникальную цепочку целей «убить X → активировать Y → забрать Z» пока нельзя полностью собрать только Inspector-конфигом;
- повторный вход в сцену может снова создать ранее подобранный authored world-item.

Для текущего среза использовать простую цель: войти, найти предмет, вернуться. Для масштабирования потребуется отдельный `LocationProgressData`/`ObjectiveData` и сохранение уникальных pickup ID.

---

## 15. Станция

### 15.1. Главный конфиг

Файл:

`Resources/Station/StationSystems_Default.asset`

Это единственный основной список объектов станции. В нём не должно быть второго параллельного списка систем.

Каждый элемент `Station Objects` описывает один выбираемый объект терминала.

### 15.2. Поля StationSystemDefinition

| Поле | Назначение |
|---|---|
| `System Type` | тип системы |
| `Object Id` | уникальный ID конкретного объекта |
| `Preview Object Name` | имя объекта в 3D-превью |
| `Display Name` | название в терминале |
| `Description` | описание |
| `Controllable` | можно включать/выключать |
| `Initially Active` | состояние новой игры |
| `Initial Level` | стартовый уровень, допускается 0 |
| `Upgrade Levels` | уровни и стоимость |

Существующие типы:

- `SolarPanel`;
- `Battery`;
- `Terminal`;
- `Drone`;
- `Laboratory`;
- `Charger`;
- `Antenna`;
- `Turret`.

Новый объект существующего типа добавляется конфигом. Новый `StationSystemType` потребует изменения enum, терминала, энергопотребления, состояния, сохранения и тестов.

### 15.3. Когда Object Id пустой

Пустой `Object Id` означает общую единственную систему данного типа.

Подходит для:

- Solar Panel как общей записи;
- Battery как общей записи;
- Terminal;
- Laboratory;
- Charger.

Для повторяющихся объектов `Object Id` обязателен:

- `station_turret_01`;
- `station_turret_02`;
- `station_turret_03`;
- `station_turret_04`.

Дрон и антенна также уже используют:

- `station_drone`;
- `station_antenna`.

### 15.4. Уровни улучшения

Каждый `Upgrade Level` содержит:

| Поле | Назначение |
|---|---|
| `Target Level` | уровень, который будет установлен |
| `Display Name` | подпись слота |
| `Upgrade Icon` | иконка уровня в UI |
| `Description` | описание |
| `Required Items` | список ItemData и количество |
| `Energy Cost` | разовая стоимость энергии |

Уровни должны идти последовательно:

`0 → 1 → 2 → 3`

Перескочить через уровень нельзя.

Если объект уже стартует на level 1, уровень 1 можно оставить в конфиге как информационный:

- `Required Items = empty`;
- `Energy Cost = 0`.

UI показывает только реально сконфигурированные уровни в доступных `Slot_LVL_1..3`.

Иконки уровней назначаются именно в:

`StationSystems_Default → Station Objects → объект → Upgrade Levels → Upgrade Icon`

### 15.5. Как оплачивается улучшение

После нажатия `Upgrade` система:

1. проверяет текущий уровень;
2. проверяет все требуемые `ItemData`;
3. суммирует количество в складе и инвентаре;
4. проверяет энергию;
5. расходует детали сначала со склада;
6. затем из инвентаря;
7. списывает энергию;
8. меняет уровень;
9. обновляет physical stage и 3D-preview stage;
10. сохраняет состояние.

### 15.6. Что уровни реально меняют сейчас

| Система | Реальный gameplay-эффект |
|---|---|
| Drone | уровень сравнивается с `Required Drone Upgrade Level` локации |
| Antenna | level 0 блокирует работу, level 1 устанавливает систему |
| Turret | level 0 не установлена, level 1+ разрешает работу |
| Battery | уровень и визуальный stage меняются, но capacity автоматически не увеличивается |
| Остальные | в основном state/UI/visual, если нет специального кода |

Текущий конфиг не задаёт разные damage/range турели по уровням. Эти параметры находятся в `StationTurretController`.

Текущий конфиг улучшения батареи не меняет `StationBattery.capacity`. Если level 2 должен реально увеличивать ёмкость, нужна отдельная безопасная связь уровня с энергетической системой. Пока считать battery upgrade визуальным/прогрессионным.

---

## 16. Physical stage объекта станции

Prefab-шаблоны находятся в:

`Prefabs/StationUpgrade`

Текущие:

- `P_StationTurret_Stages`;
- `P_StationDrone_Stages`;
- `P_StationBattery_Stages`;
- `P_StationAntenna_Stages`.

### 16.1. Правильная структура

```text
P_Object_Stages
├─ Stage_0
│  └─ любой редактируемый Visual
├─ Stage_1
│  └─ любой редактируемый Visual
├─ Stage_2
│  └─ любой редактируемый Visual
└─ Stage_3
   └─ любой редактируемый Visual
```

Имена `Stage_N` обязательны. Код не меняет вложенные модели и не перестраивает prefab — он только включает нужный `Stage_N`.

Для каждого улучшаемого объекта шаблон всегда содержит полный диапазон от `Stage_0` до `Stage_Max`, даже если игра начинается с первого или второго уровня. Стартовый уровень задаётся данными, а не отсутствием предыдущих стадий:

- `Initial Level` в `StationSystems_Default` — источник игрового стартового состояния;
- `Initial Stage` на `StationUpgradeStageController` — fallback для prefab до инициализации систем;
- эти два значения должны совпадать;
- в сохранённом prefab активен только соответствующий стартовый `Stage_N`;
- после загрузки runtime включает этап из текущего состояния станции или сохранения.

### 16.2. StationUpgradeStageController

На корне заполнить:

| Поле | Значение |
|---|---|
| `System Type` | тип станции |
| `Max Stage` | максимальный уровень |
| `Object Id` | ID конкретного объекта либо пусто для turret под родителем |
| `Initial Stage` | fallback до загрузки системы |
| `Stage Container` | корень с `Stage_N`, либо пусто, если это сам объект |

Для физической турели безопасная схема:

```text
P_StationTurret_02
├─ StationTurretController (turretId = station_turret_02)
└─ P_StationTurret_Stages
   ├─ Stage_0
   ├─ Stage_1
   ├─ Stage_2
   └─ Stage_3
```

У stage-контроллера `Object Id` можно оставить пустым: он возьмёт `Turret Id` из родительского `StationTurretController`.

С учётом принятого решения лучше иметь четыре физических turret-prefab с разными `turretId`, чем вручную поддерживать сложные override одного экземпляра.

### 16.3. Важное правило визуала

Можно свободно менять содержимое каждого `Stage_N`:

- модель;
- материал;
- цвет;
- эффекты;
- дополнительные дочерние объекты.

Не переименовывать сам `Stage_N`.

---

## 17. 3D-превью станции в терминале

Главные объекты:

- `StationScreen`;
- `Station_RawImage`;
- `StationUICamera`;
- 3D-модель станции.

### 17.1. Выбор объекта

При клике код поднимается от Collider к родителям и ищет имя, совпадающее с `Preview Object Name` в `StationSystems_Default`.

Например:

| Config Object Id | Preview Object Name |
|---|---|
| `station_turret_01` | `SM_Turret_1` |
| `station_turret_02` | `SM_Turret_2` |
| `station_turret_03` | `SM_Turret_3` |
| `station_turret_04` | `SM_Turret_4` |
| `station_drone` | `SM_Drone` |
| `station_antenna` | `SM_Antenna` |

На кликаемой модели должен быть Collider. Дочерний визуал может называться как угодно, если один из его родителей имеет точное `Preview Object Name`.

### 17.2. Stage в превью

Текущая структура turret-preview:

```text
SM_Turret_1
├─ Stage_0
├─ Stage_1
├─ Stage_2
└─ Stage_3
```

На `SM_Turret_1`:

```text
System Type: Turret
Max Stage: 3
Object Id: station_turret_01
Initial Stage: 1
```

Для остальных:

- `SM_Turret_2` → `station_turret_02`, initial 0;
- `SM_Turret_3` → `station_turret_03`, initial 0;
- `SM_Turret_4` → `station_turret_04`, initial 0.

У preview-объекта `Object Id` заполняется явно, потому что там обычно нет родительского `StationTurretController`.

### 17.3. Status и Upgrade

Кнопки:

- `StatusMapButton` показывает только `background_Status`;
- `UpgradesMapButton` показывает только `background_Upgrade`.

Они не должны быть активны одновременно.

`Toggle` состоит из:

- `OnButton`;
- `OffButton`;
- `Handle`;
- `Text_Status`.

Для анимационного Handle контроллер ожидает состояния:

- `ToggleOn_clip`;
- `ToggleOff_clip`.

### 17.4. Особые системы

- выключение Terminal закрывает терминал;
- выключение Battery отключает grid, питание станции и терминал;
- после выключения критической системы её нужно восстановить физическим интерактивным объектом;
- Drone нельзя выключить во время активного сканирования;
- Solar Panel, Battery и Terminal имеют специальное управление и не должны превращаться в обычные controllable-объекты без проверки логики.

---

## 18. Батареи, солнечные панели и энергия

### 18.1. EnergyBalance_Default

Файл:

`Resources/Energy/EnergyBalance_Default.asset`

Содержит:

- ёмкость и стартовый заряд fallback-батареи;
- генерацию панели по погоде;
- износ наружных устройств;
- расход терминала;
- расход лаборатории;
- расход зарядки дрона;
- расход зарядки предметов;
- расход калибровки антенны;
- освещение;
- idle/fire расход турели;
- длительность зарядки дрона;
- длительность калибровки;
- длину суток, рассвет и закат.

Это глобальный баланс. Локальные параметры конкретной физической батареи находятся на `StationBattery`.

### 18.2. Новая батарея

На физический объект добавить `StationBattery`:

| Поле | Назначение |
|---|---|
| `Battery Id` | уникальный ID |
| `Capacity` | ёмкость |
| `Initial Charge` | стартовый вклад |

Две батареи по 1000 дадут 2000 только если их `batteryId` различаются. Пустой ID создаётся из пути иерархии, но явные ID надёжнее.

Пример:

- `station_battery_01`;
- `station_battery_02`.

### 18.3. Новая солнечная панель

На физический объект:

1. Collider;
2. `MaintainableObject`;
3. `SolarPanelInteractable`.

`MaintainableObject`:

- `Role = SolarPanel`;
- `Exposed To Weather = true`;
- `Initial Condition`;
- `Service Duration`;
- Renderer/VFX.

`SolarPanelInteractable`:

- уникальный `Panel Id`;
- `Output Multiplier`;
- ссылка на `MaintainableObject`.

Генерация:

`погода × день/ночь × Output Multiplier × Condition`

### 18.4. MaintainableObject

Роли:

- `Generic`;
- `SolarPanel`;
- `Antenna`;
- `Turret`.

Объект с condition 0 не работает. Игрок восстанавливает его удержанием interaction-клавиши.

---

## 19. Турель станции

Физический prefab содержит:

- `MaintainableObject`, role `Turret`;
- `StationTurretController`;
- `YawPivot`;
- `Muzzle`;
- визуал и Collider.

Поля:

| Поле | Назначение |
|---|---|
| `Turret Id` | совпадает с config `Object Id` |
| `Yaw Pivot` | поворотная часть |
| `Muzzle` | точка выстрела |
| `Detection Range` | радиус поиска IO |
| `Rotation Speed` | скорость наведения |
| `Fire Interval` | пауза |
| `Damage` | урон |
| `Line Of Sight Mask` | слои проверки видимости |

Турель ищет ближайший `IOEnemyController`. Она работает, если:

- её уровень выше 0;
- объект включён;
- condition выше 0;
- есть питание;
- потребитель энергии зарегистрирован;
- есть цель в радиусе и line of sight.

Четыре турели должны иметь четыре разных `turretId` и четыре отдельных элемента в `StationSystems_Default`.

---

## 20. Дрон

Дрон открывает только локации:

`Discovery Source = Drone`

Условия Launch:

- система Drone включена;
- станция запитана;
- дрон не сканирует;
- дрон не заряжается;
- локация ещё не открыта;
- текущий уровень дрона не ниже `Required Drone Upgrade Level`.

Время сканирования берётся из локации, а не из общего Drone-контроллера.

После сканирования:

- локация добавляется в discovered;
- запускается recharge;
- физический `Station_Drone` проигрывает `Dron_Start` и `Dron_End`, если Animator Controller настроен;
- состояние сохраняется.

Для дальних экспедиций достаточно повысить `Required Drone Upgrade Level`. Не создавать отдельный Drone Controller на каждую локацию.

---

## 21. Library

Главные вкладки терминала:

- `AnomalyButton`;
- `RecordButton`;
- `EquipmentButton`;
- `DetailsButton`.

Внутри каждой вкладки authored-кнопки:

`background_Slot_01`, `background_Slot_02` и далее.

В этих Library-слотах не нужны runtime-иконки предметов. Они показывают текстовую запись; при нажатии заполняется:

- `Text_Name`;
- `Image_info`;
- `Text_Description`.

Источники:

- Anomaly — исследованные `ItemData` типа `Anomaly`;
- Record — известные/исследованные `Record` и unlocked `LibraryEntryData` категории Records;
- Equipment — известные `Equipment`;
- Details — остальные известные типы.

Для надёжного восстановления после сохранения исследуемый `ItemData` должен быть в `ItemCatalog_Default`, а `ResearchDefinition.Unlocked Entry` должен ссылаться на Library asset.

---

## 22. Камера

Глобальная камера находится в Boot:

`RuntimeRoot/Player_Camera`

Основные параметры находятся в `PlayerFollowCamera`.

Профили дистанции:

- `CP_Outdoor.asset`;
- `CP_Indoor.asset`;
- `CP_Corridor.asset`.

Создание нового профиля:

`Create → NERA → Camera → Camera Preset`

Поля:

- `Min Distance`;
- `Max Distance`;
- `Default Distance`.

Для зоны:

1. Collider с `Is Trigger`;
2. `CameraDistanceZone`;
3. назначить `CameraPreset`;
4. убедиться, что Player имеет tag `Player`.

Не создавать отдельные gameplay-камеры внутри локаций без отдельной необходимости.

---

## 23. Сохранение

Файл сохранения:

`Application.persistentDataPath/nera_save.json`

Сохраняются:

- питание и энергия станции;
- состояние grid;
- discovered locations;
- активный и использованные antenna signals;
- condition антенны;
- предметы и их уникальные instance ID;
- заряд каждого ItemInstance;
- интегрированный IO-камень и готовность его единственного применения;
- слоты инвентаря;
- склад;
- лабораторные слоты;
- уровни и toggle-состояния станции;
- отдельные состояния объектов по `objectId`;
- выполненные исследования;
- Library entries и известные предметы.

Не сохраняются как универсальные data-driven данные:

- состояние каждого authored world-item в сцене;
- состояние каждого врага;
- универсальная цепочка objective каждой новой экспедиции;
- полноценный LocationState lifecycle.

После добавления нового предмета обязательно проверить:

1. он есть в `ItemCatalog_Default`;
2. сохранить игру;
3. выйти из Play Mode;
4. снова запустить Boot;
5. предмет восстановился в правильном слоте и с правильным зарядом.

При изменении ID старое сохранение перестанет находить сущность. Во время разработки после намеренного изменения ID удалить тестовый save или сделать миграцию.

---

## 24. Рекомендуемый порядок сборки одного вертикального среза

### Этап 1. Зафиксировать маршрут

На одной странице описать:

```text
Как игрок открывает локацию?
Что он там встречает?
Что подбирает?
Зачем возвращается?
Что открывает исследование?
На что тратится награда?
Что становится доступно дальше?
```

Пример:

```text
Drone level 1
→ Expedition 01
→ Weak Blue IO
→ Blue IO Shard + Servo Drive
→ Laboratory research
→ Library entry
→ Drone level 2
→ Expedition 02
→ Relay Guard
→ Signal Relay
→ Antenna level 1
→ Unknown Signal
```

### Этап 2. Создать данные

В таком порядке:

1. Library entry;
2. Research definition;
3. Item data;
4. Item energy/weapon, если нужны;
5. world-prefab;
6. Item Catalog;
7. Enemy Config;
8. Enemy prefab;
9. Location config;
10. Station upgrade cost.

### Этап 3. Создать сцену

1. дублировать template-экспедицию;
2. создать spawn;
3. создать exit;
4. сделать основной маршрут;
5. поставить врагов;
6. поставить находки;
7. добавить Directional Light;
8. добавить сцену в Build Settings.

### Этап 4. Зарегистрировать

Проверить все глобальные списки:

- новый ItemData → `ItemCatalog_Default`;
- новый LocationData → `Boot/ExpeditionDiscoveryController/Known Locations`;
- новая сцена → Build Settings;
- новая станционная система → `StationSystems_Default`;
- новый preview-объект → точное `Preview Object Name`;
- новый physical stage → правильные `Stage_N` и `Object Id`.

### Этап 5. Пройти с чистого сохранения

1. очистить тестовый save;
2. запустить Boot;
3. пройти путь без Inspector и debug-кнопок;
4. сохранить/перезапустить в середине;
5. повторно проверить progression.

---

## 25. Быстрые шаблоны

### 25.1. Checklist нового Item

- [ ] уникальный `itemId`;
- [ ] имя и описание;
- [ ] правильный `ItemType`;
- [ ] иконка;
- [ ] world-prefab;
- [ ] Collider и `WorldItem`;
- [ ] layer `Interactable`;
- [ ] optional energy/weapon/research;
- [ ] добавлен в `ItemCatalog_Default`;
- [ ] pickup проверен;
- [ ] save/load проверен;
- [ ] DropButton создаёт world-prefab.

### 25.2. Checklist нового Enemy

- [ ] уникальный `enemyId`;
- [ ] `IOEnemyConfig`;
- [ ] health/range/speed/attack;
- [ ] death drop;
- [ ] prefab с Collider;
- [ ] layer `Enemy`;
- [ ] назначен config;
- [ ] игрок получает урон;
- [ ] оружие попадает;
- [ ] дроп подбирается;
- [ ] арена не блокирует прямое движение.

### 25.3. Checklist новой Expedition

- [ ] новое значение `LocationId`;
- [ ] уникальный `locationId`;
- [ ] scene name;
- [ ] Build Settings;
- [ ] spawn point;
- [ ] exit;
- [ ] `ExpeditionSceneBootstrap`;
- [ ] `Discovery Source = Drone`;
- [ ] required drone level;
- [ ] свободный map sector;
- [ ] `SM_Expedition_XX` с Collider;
- [ ] Known Locations;
- [ ] враги;
- [ ] находки;
- [ ] возврат;
- [ ] research/Library.

### 25.4. Checklist нового Unknown Signal

- [ ] новое значение `LocationId`;
- [ ] `Location Type = UnknownSignal`;
- [ ] `Discovery Source = Antenna`;
- [ ] уникальная сцена;
- [ ] Build Settings;
- [ ] spawn и exit;
- [ ] Known Locations;
- [ ] антенна level 1+;
- [ ] есть открытый expedition sector;
- [ ] signal marker появляется;
- [ ] YES загружает сцену;
- [ ] сигнал после перехода consumed;
- [ ] уникальная находка и Library.

### 25.5. Checklist объекта станции

- [ ] элемент в `StationSystems_Default`;
- [ ] правильный `System Type`;
- [ ] уникальный `Object Id`, если объект повторяется;
- [ ] точный `Preview Object Name`;
- [ ] `Initial Level`;
- [ ] `Initially Active`;
- [ ] уровни без пропусков;
- [ ] иконка каждого уровня;
- [ ] предметы и количество;
- [ ] energy cost;
- [ ] physical `Stage_N`;
- [ ] preview `Stage_N`;
- [ ] stage controller ID совпадает;
- [ ] toggle влияет только на выбранный объект;
- [ ] save/load сохраняет уровень и состояние.

---

## 26. Текущие примеры, на которые стоит ориентироваться

### Предметы

- `Item_ServoDrive_01` — инженерная деталь;
- `Item_IOBlueShard_01` — аномалия и исследование;
- `Item_NERAMemoryCore_01` — запись/находка;
- `Item_NERASignalRelay_02` — находка Expedition 02;
- `Item_IOIntegrator_01` — отдельный инструмент для IO-интеграции;
- `Item_EnergyPistol_01` — энергетическое оружие.

### Враги

- `CFG_IO_Blue_Weak` + `IO_Blue_Weak.prefab`;
- `CFG_IO_Blue_RelayGuard` + `IO_Blue_RelayGuard.prefab`.

### Локации

- `Location_Expedition01.asset`;
- `Location_Expedition02.asset`;
- `CFG_UnknownSignal_01.asset`.

### Станция

- `StationSystems_Default.asset`;
- `P_StationTurret.prefab`;
- `P_StationTurret_Stages.prefab`;
- `P_StationDrone_Stages.prefab`;
- `P_StationBattery_Stages.prefab`;
- `P_StationAntenna_Stages.prefab`.

---

## 27. Известные границы текущего среза

Следующие вещи не следует считать готовыми data-driven механиками:

1. универсальные цели новых экспедиций;
2. уникальное сохранение каждого pickup в сцене;
3. сохранение убитых врагов;
4. полноценное использование `LocationState`;
5. автоматическое изменение capacity батареи от upgrade level;
6. изменение характеристик турели по уровням;
7. более девяти authored map sectors;
8. новые ItemType, LocationType, StationSystemType без изменения кода;
9. переводы — система намеренно отсутствует.

Это не мешает собрать вертикальный срез по схеме «открыть → посетить → победить → подобрать → вернуться → исследовать → улучшить». Но при расширении игры эти пункты следует превратить в отдельные data-driven системы.

---

## 28. Финальная проверка готового среза

Срез считается собранным, когда он проходится из чистого запуска без Inspector:

- [ ] Boot загружает Player_Station;
- [ ] Player, Camera, HUD и EventSystem не дублируются;
- [ ] питание можно восстановить вручную;
- [ ] терминал открывается только при питании;
- [ ] 3D-карта рендерится только при открытом MapScreen;
- [ ] сектор выбирается кликом;
- [ ] Drone Launch зависит от уровня;
- [ ] прогресс доходит до 100%;
- [ ] анимации взлёта/посадки работают;
- [ ] переход использует правильный spawn point;
- [ ] враги обнаруживают игрока и получают урон;
- [ ] дроп попадает в правильную группу инвентаря;
- [ ] DropButton создаёт предмет мира;
- [ ] возвращение ведёт на станцию;
- [ ] лаборатория принимает предмет;
- [ ] исследование показывает проценты;
- [ ] неизученный IO-камень не разрешает синтез;
- [ ] каждый экземпляр IO-камня требует собственного сканирования;
- [ ] уже отсканированный экземпляр нельзя сканировать повторно;
- [ ] одинаковые типы камней не дублируют запись Library;
- [ ] изученный IO-камень расходуется и заряжает IO Integrator одним эффектом;
- [ ] R активирует эффект, обнуляет энергию и удаляет интеграцию;
- [ ] следующий камень нельзя установить до полной зарядки IO Integrator;
- [ ] Library получает запись;
- [ ] склад переносит предметы только drag-and-drop;
- [ ] Station Upgrade расходует правильные детали и энергию;
- [ ] physical stage и preview stage меняются одновременно;
- [ ] toggle управляет только выбранным объектом;
- [ ] батареи суммируют capacity по уникальным ID;
- [ ] антенна открывает неизвестный сигнал;
- [ ] Q/E переключают главные страницы;
- [ ] I не открывает Inventory поверх терминала/лаборатории;
- [ ] сохранение и повторный запуск не сбрасывают progression;
- [ ] в Console нет Error и повторяющихся Warning.

---

## 29. Состояние автоматической проверки на дату документа

После унификации upgrade-stage и добавления IO-синтеза система компилируется без записей типа Error в Unity Console. Полный набор тестов зелёный:

- EditMode: 56/56;
- PlayMode: 14/14.

Исправлены четыре ранее выявленные несогласованности:

1. type-level вызовы станции теперь находят `Object Id` соответствующего объекта из `StationSystems_Default`;
2. уровень, стоимость и расход деталей улучшения дрона берутся из одного object-specific состояния;
3. все upgrade-prefab обязаны содержать `Stage_0...Stage_Max`, а активный этап соответствует стартовому уровню;
4. PlayMode-сценарий повторного запуска дрона начинается с чистого состояния систем и не зависит от пользовательского save.

Единое правило шаблона: наличие `Stage_0` не означает, что объект обязан начинать с нулевого уровня. Фактический старт задаёт `Initial Level`; `Stage_0` остаётся обязательной визуальной стадией для одинаковой структуры всех prefab.

PlayMode отдельно проверяет лабораторные копии инвентаря, блокировку синтеза до анализа, расход IO-камня и возврат интегрированного инструмента.
