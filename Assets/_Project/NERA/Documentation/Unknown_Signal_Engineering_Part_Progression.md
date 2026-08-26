# NERA — Unknown Signal: объекты сцен и прогрессия Engineering Parts

Дата проверки: 2026-08-26

Статус: фактическая расстановка Engineering Parts выполнена и сверена с
прогрессией. Окружение сцен пока остаётся placeholder-контентом.

## Scope

Документ связывает:

- `Assets/_Project/NERA/Configs/Locations/CFG_UnknownSignal_01`–`12`;
- `Assets/_Project/NERA/Scenes/UnknownSignal_01`–`12`;
- детали из `Assets/_Project/NERA/Configs/Items/Item_EngineeringPart`;
- общие характеристики антенны из
  `Assets/_Project/NERA/Resources/Station/StationSystems_Default.asset`.

`Assets/_Project/NERA/Configs/Expeditions` и обычные Expedition scenes в scope
не входят и этим планом не изменяются.

## Целевая progression антенны

Базовый `Antenna Scan Range` равен `1`. Поэтому `CFG_UnknownSignal_01` с
`Required Antenna Scan Range = 1` доступен с самого начала без единого апгрейда
антенны. `Antenna Array`, найденный уже внутри Signal 01, нужен не для открытия
первой локации, а для перехода к range 2.

Остальные сигналы не образуют цепочку последовательных одиночных gate. Как и в
исходном плане, они открываются пачками: игрок исследует несколько локаций
текущего уровня, находит среди них range-деталь и затем получает доступ к
следующей пачке.

| Уровень антенны | Доступные сигналы | Где лежит range-деталь | Что она открывает | Время после установки |
|---:|---|---|---|---:|
| 1 | `UnknownSignal_01` | Signal 01: `item_antenna_array_01` | range 2 | 120 s |
| 2 | `UnknownSignal_02`–`04` | Signal 04: `item_calibration_module_01` | range 3 | 100 s |
| 3 | `UnknownSignal_05`–`07` | Signal 07: `item_signal_amplifier_01` | range 4 | 90 s |
| 4 | `UnknownSignal_08`–`10` | Signal 09: `item_signal_processor_01` | range 5 | 80 s |
| 5 | `UnknownSignal_11`–`12` | финальная пачка, следующего gate пока нет | будущий контент | 80 s |

Критические условия этой схемы:

- игрок должен иметь возможность вернуться в уже открытую локацию, если ушёл
  без range-детали;
- после возвращения на станцию ключевую деталь нужно установить до запуска
  поиска сигналов следующего уровня;
- порядок `KnownLocations` в `MainScene` должен оставаться `01 -> 12`, потому
  что `AntennaController` выбирает первый доступный неиспользованный сигнал;
- благодаря этому порядку сигналы внутри пачки открываются последовательно, но
  установка range-детали не заставляет пропустить оставшиеся локации текущего
  уровня.

Сохранённые Location assets соответствуют этой схеме. Фактический ряд:
`1,2,2,2,3,3,3,4,4,4,5,5`.

## Какое время действительно управляет сканированием

Поле `droneScanDuration: 5` внутри каждого Location config не является временем
калибровки антенны. Это длительность полёта дрона (`Drone Flight Duration`).

`AntennaController.CalibrationDuration` читает stat `CalibrationDuration` из
`StationSystems_Default`. Его новое базовое значение — `120 s`. Модификаторы
деталей применяются аддитивно:

| Установленные antenna parts | Range | Energy за калибровку | Duration |
|---|---:|---:|---:|
| нет | 1 | 5 | 120 s |
| `Antenna Array` | 2 | 6 | 120 s |
| + `Calibration Module` | 3 | 8 | 100 s |
| + `Signal Amplifier` | 4 | 8 | 90 s |
| + `Signal Processor` | 5 | 12 | 80 s |

В `MainScene` у антенны сейчас `signalDiscoveryChance = 0.5`. Поэтому 120/100/
90/80 секунд — время одной попытки, а не гарантированного открытия. При
вероятности 50% среднее число попыток равно двум. Для демо это отдельная точка
баланса: либо оставить риск неудачи осознанно, либо на время демо сделать
открытие сигнала гарантированным.

## Фактические объекты в Unknown Signal scenes

Окружение всех 12 сцен пока остаётся одинаковым placeholder-набором. Набор
Engineering Parts уже отличается по сценам согласно progression-таблице ниже.
Проверка выполнена 26 августа 2026 года по hierarchy Unity Editor и
сериализованным сценам.

В каждой сцене присутствуют:

| Объект | Количество | Назначение / состояние |
|---|---:|---|
| `Directional Light` | 1 | общий свет |
| `Plane` | 1 | временная поверхность с renderer и collider |
| `ExpeditionSystems` | 1 | `ExpeditionSceneBootstrap` |
| `ExpeditionObjectiveController` | 1 | пустой дочерний объект `ExpeditionSystems` |
| `Spawn_UnknownSignalXX_Start` | 1 | уникальный spawn ID соответствующей сцены |
| `Expedition_To_Station_Exit` | 1 | возврат на станцию |
| `WorldItem_EngineeringPart` | 2–4 | pickup-объекты из progression-таблицы; ряд в двух метрах от выхода |

Итого на сцену: placeholder-инфраструктура и от двух до четырёх Engineering
Parts. Всего размещено 35 Engineering Part WorldItem instances. Они находятся
на `y = 0.5`, `z = 5.8`, то есть в двух метрах от
`Expedition_To_Station_Exit`; несколько предметов разнесены по `x` с шагом
1 метр. Ранее отмеченные `IO_Blue_Weak` и `WorldItem_NERAMemoryCore_01` в
текущих сценах отсутствуют. Реальных разбитых дронов, ретрансляторов, солнечных
ферм, турельных позиций или остатков старой станции пока нет — это будущие
environmental objects.

## Расстановка Engineering Parts по сценам

Колонка Engineering Parts описывает фактическую расстановку pickup-prefabs в
сценах. Колонка окружения остаётся целевым планом замены placeholder-контента.

Число в скобках — количество физических экземпляров одного ItemData.

| Порядок | Target range | Необходимый объект окружения | Engineering Parts | Роль |
|---:|---:|---|---|---|
| 1 | 1 | мачта или ретранслятор старой группы | `item_antenna_array_01`, `item_voltage_regulator_01`, `item_power_controller_01` | переход range 1 -> 2; первая стабилизация питания |
| 2 | 2 | разбитый разведывательный дрон | `item_advanced_stabilizer_01`, `item_capacitor_01` (1/2) | первый обязательный drone upgrade; открывает дальность 2 и Expeditions 3–4 |
| 3 | 2 | солнечный сервисный лагерь | `item_solar_cells_01`, `item_solar_dust_repeller_01`, `item_energy_cells_01` (1/2) | подготовка генерации и батареи |
| 4 | 2 | калибровочный пост антенны и распределительный шкаф | `item_calibration_module_01`, `item_power_bus_01` | переход range 2 -> 3; ускорение до 100 s |
| 5 | 3 | разбитый грузовой дрон и его силовой отсек | `item_capacitor_01` (2/2), `item_power_core_01` (1/2) | первый power-core upgrade; открывает дальность 3 и Expeditions 5–6 |
| 6 | 3 | законсервированный оборонительный периметр | `item_chassis_01` (1/2), `item_cooling_01` (1/2), `item_emitter_damage_01` (1/3) | первый turret-комплект |
| 7 | 3 | подземный энергоузел и усилитель дальней связи | `item_signal_amplifier_01`, `item_cooling_system_01`, `item_energy_cells_01` (2/2) | переход range 3 -> 4; завершает battery-cell slots |
| 8 | 4 | автоматическая солнечная ферма с разбитым обслуживающим дроном и приводами | `item_solar_mppt_controller_01`, `item_solar_tracker_01`, `item_propulsion_01`, `item_servo_drive_01` (1/2) | propulsion открывает дальность дрона 4 и Expedition 7; первая поздняя деталь привода турели |
| 9 | 4 | вычислительный узел анализа сигнала | `item_signal_processor_01`, `item_servo_01` (1/2), `item_sensor_array_01` | переход антенны range 4 -> 5; вспомогательный drone upgrade найден до финального power core |
| 10 | 4 | разрушенная огневая позиция | `item_chassis_01` (2/2), `item_cooling_01` (2/2), `item_emitter_damage_01` (2/3), `item_sensor_01` (1/2) | основа второго turret-комплекта |
| 11 | 5 | ремонтная мастерская с силовым оборудованием и приводами | `item_servo_01` (2/2), `item_power_core_01` (2/2), `item_power_converter_01` | финальный power-core upgrade открывает дальность дрона 5 и Expedition 8; закрывает servo slots и усиливает батарею |
| 12 | 5 | защищённый командный или оружейный модуль | `item_sensor_01` (2/2), `item_emitter_damage_01` (3/3), `item_servo_drive_01` (2/2) | финальные turret upgrades и второй servo-drive |

Всего: 25 уникальных ItemData assets и 35 физических WorldItem instances.

## Баланс открытия экспедиций и атак на станцию

Обязательная цепочка дальности дрона теперь разнесена по прогрессии и не
выдаёт два уровня подряд в одной ранней пачке:

| Этап | Найденная ключевая деталь | Результат |
|---|---|---|
| старт | базовый дрон, дальность 1 | доступны Expeditions 1–2 |
| Signal 02 | `item_advanced_stabilizer_01` | дальность 2, доступны Expeditions 3–4 |
| Signal 05 | `item_power_core_01` (1/2) | дальность 3, доступны Expeditions 5–6 |
| Signal 08 | `item_propulsion_01` | дальность 4, доступна Expedition 7 |
| Signal 09 | `item_sensor_array_01` | вспомогательная деталь найдена до финального апгрейда |
| Signal 11 | `item_power_core_01` (2/2) | дальность 5, доступна Expedition 8 |

Атака на станцию может с малым шансом запускаться после бури либо после
завершения Expedition 3 и далее. Поэтому первый turret-комплект в Signal 06
появляется после входа игрока в зону риска и служит ответом на новую угрозу.
Детали турелей в Signals 8–12 распределены по нескольким локациям: игрок
постепенно усиливает оборону, а не получает два одинаковых апгрейда сразу.

Числовые затраты энергии, длительности и прочие временные коэффициенты сейчас
являются тестовыми. Они не использовались как основание для этой раскладки;
баланс здесь проверяется по порядку доступа, обязательным деталям и новым
угрозам.

## Какие prefab-объекты ставить в сцены

В сценах инстанцированы WorldItem prefabs, а не `_Visual` prefabs и не
декоративные meshes. В конфиге используется префикс `item_`, в
prefab-компоненте `WorldItem` — префикс `id_`.

| Item Id в конфиге | World prefab | Persistent Id в prefab |
|---|---|---|
| `item_antenna_array_01` | `P_WorldItem_AntennaArray` | `id_antenna_array_01` |
| `item_calibration_module_01` | `P_WorldItem_CalibrationModule` | `id_calibration_module_01` |
| `item_signal_amplifier_01` | `P_WorldItem_SignalAmplifier` | `id_signal_amplifier_01` |
| `item_signal_processor_01` | `P_WorldItem_SignalProcessor` | `id_signal_processor_01` |
| `item_cooling_system_01` | `P_WorldItem_CoolingSystem` | `id_cooling_system_01` |
| `item_energy_cells_01` | `P_WorldItem_EnergyCells` | `id_energy_cells_01` |
| `item_power_bus_01` | `P_WorldItem_PowerBus` | `id_power_bus_01` |
| `item_power_controller_01` | `P_WorldItem_PowerController` | `id_power_controller_01` |
| `item_power_converter_01` | `P_WorldItem_PowerConverter` | `id_power_converter_01` |
| `item_voltage_regulator_01` | `P_WorldItem_VoltageRegulator` | `id_voltage_regulator_01` |
| `item_advanced_stabilizer_01` | `P_WorldItem_AdvancedStabilizer` | `id_advanced_stabilizer_01` |
| `item_capacitor_01` | `P_WorldItem_Capacitor` | `id_capacitor_01` |
| `item_power_core_01` | `P_WorldItem_PowerCore` | `id_power_core_01` |
| `item_propulsion_01` | `P_WorldItem_Propulsion` | `id_propulsion_01` |
| `item_sensor_array_01` | `P_WorldItem_SensorArray` | `id_sensor_array_01` |
| `item_solar_cells_01` | `P_WorldItem_PhotovoltaicCells` | `id_solar_cells_01` |
| `item_solar_dust_repeller_01` | `P_WorldItem_DustProtection` | `id_solar_dust_repeller_01` |
| `item_solar_mppt_controller_01` | `P_WorldItem_PowerOptimizer` | `id_solar_mppt_controller_01` |
| `item_solar_tracker_01` | `P_WorldItem_TrackingDrive` | `id_solar_tracker_01` |
| `item_chassis_01` | `P_WorldItem_Chassis` | `id_chassis_01` |
| `item_cooling_01` | `P_WorldItem_Cooling` | `id_cooling_01` |
| `item_emitter_damage_01` | `P_WorldItem_EmitterDamage` | `id_emitter_damage_01` |
| `item_sensor_01` | `P_WorldItem_Sensor` | `id_sensor_01` |
| `item_servo_01` | `P_WorldItem_Servo` | `id_servo_01` |
| `item_servo_drive_01` | `P_WorldItem_ServoDrive` | `id_servo_drive_01` |

В текущей progression одинаковые физические детали распределены по разным
сценам. Все scene instances используют базовый `persistentId`, уже записанный
в prefab. Суффиксы `left` / `right` и overrides для одинаковых предметов внутри
одной сцены не используются. Один и тот же `ItemData` и prefab можно повторно
использовать в разных сценах: уникальность persistence key обеспечивает имя
сцены (`sceneName/persistentId`).

Старые Engineering Part IDs без префикса `item_` поддерживаются при чтении
существующих сохранений через `ItemCatalogData`. После восстановления предметы
и установленные детали сохраняются уже с каноническими новыми ID.

Prefab `P_WorldItem_NERAMemoryCore` остаётся в проекте и ссылается на
`Item_NERAMemoryCore_01`, но в текущих Unknown Signal scenes больше не
размещён. Его нельзя переименовывать в Engineering Part; все детали добавлены
отдельными корректными prefab instances.

## Проверенные настройки и оставшиеся blockers

### 1. Range values сохранены корректно

Текущие значения: `01=1`, `02`–`04=2`, `05`–`07=3`, `08`–`10=4`,
`11`–`12=5`. Они соответствуют целевой progression. Signal 01 достигается
базовой антенной range 1 и не требует предварительной установки детали.

### 2. Ссылка prefab у Dust Protection исправлена

`Item_dust_repeller_01.asset` ссылается на правильный
`P_WorldItem_DustProtection`, который содержит ItemData Dust Protection.

### 3. Sensor Array размещён до финального апгрейда дрона, но требует настройки

`item_sensor_array_01` теперь находится в Signal 09, раньше финального
`item_power_core_01` в Signal 11. Это выполняет требование прогрессии, но сам
конфиг пока имеет только `Flight Energy Consumption +1`, хотя описание обещает
улучшение навигации и дальнего сканирования. Перед финальным балансом нужен
положительный stat либо исправление описания/роли.

### 4. Range gate допускает soft-lock по контенту

Если ключевая antenna part плохо заметна, недоступна из-за геометрии или теряет
состояние после save/load, следующая пачка локаций не откроется. Для Signal 01,
04, 07 и 09 нужны objective marker, гарантированная доступность pickup и
отдельный тест `pickup -> return -> install -> save/load -> next range`.

### 5. Вероятность обнаружения 50% удваивает ожидаемое время

После перехода на базовые 120 секунд неудачная попытка становится дорогой для
демо. Целевая продолжительность одной попытки настроена корректно, но отдельно
нужно подтвердить, что случайная неудача является желаемой частью demo loop.

## Authoring checklist для каждой Unknown Signal scene

1. Сохранить целевой target range и порядок `KnownLocations`.
2. Заменить `Plane` на читаемый environmental object из таблицы.
3. Если `WorldItem_NERAMemoryCore_01` будет возвращён как record, не подменять
   им Engineering Part.
4. Сохранять фактическую раскладку перечисленных `P_WorldItem_*` prefab
   instances при замене placeholder-окружения.
5. Для `WorldItem` использовать базовый `persistentId` prefab. Не размещать два
   одинаковых prefab в одной сцене: повторные детали распределять по разным
   локациям и оставлять `trackWorldState = true`.
6. Не использовать `_Visual` prefabs как pickup.
7. Ключевые antenna parts в Signal 01, 04, 07 и 09 выделить
   светом/VFX/objective marker.
8. Разнести обычные награды по логичным узлам объекта: силовые детали в
   энергоблок, sensor/processor в электронику, propulsion/servo в механику.
9. При возвращении IO-контента проверить столкновения и NavMesh для противников,
   spawn, pickup-объектов и exit.
10. Проверить pickup каждого предмета, возврат на станцию, установку, save/load
    и Continue.
11. До установки деталей проверить, что базовая антенна range 1 открывает
    `CFG_UnknownSignal_01`.
12. После Signal 01/04/07/09 отдельно проверить достижение range 2/3/4/5 и
    длительность следующей калибровки 120/100/90/80 секунд.
13. После изменения расстановки запускать persistent-ID validator и профильные
    EditMode/PlayMode tests.
14. Не добавлять эти предметы в `Configs/Expeditions`.
