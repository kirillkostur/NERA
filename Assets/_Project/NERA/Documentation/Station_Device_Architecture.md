# Архитектура устройств станции

Документ описывает единую настройку физических устройств в `Player_Station`
и их 3D-превью на экране улучшений в `MainScene`.

## Основное правило

У логического объекта станции есть один `StationObjectIdentity`. Компонент
хранит:

- `System Type`;
- стабильный `Object Id` из `StationSystems_Default`.

Остальные компоненты получают тип и ID через ближайший родительский
`StationObjectIdentity`. Копировать ID в `MaintainableObject`,
`StationDeviceInteractable`, `StationTurretController` или
`StationUpgradeStageController` не нужно.

## Физический объект в `Player_Station`

Рекомендуемая структура:

```text
StationObjectRoot
├── StationObjectIdentity
├── StationUpgradeStageController    // если есть уровни
├── MaintainableObject               // если есть износ/поломка
├── StationDeviceInteractable        // если возможен ручной запуск
├── профильный функциональный компонент
└── Stage_N                          // модели, коллайдеры, VFX, pivots
```

`StationObjectIdentity` и общее поведение находятся выше `Stage_N`. Визуальная
стадия может меняться, не меняя ID, condition и запрошенное состояние системы.

`StationDeviceInteractable` выполняет две последовательные функции:

1. Если condition ниже максимального — обслуживает устройство.
2. Если устройство исправно, доступно и выключено — запрашивает ручной запуск
   через `StationSystemsController`.

Параметры `Press`/`Hold`, текст и длительность задаются только в
`StationDeviceInteractable`.

## Превью в `MainScene`

Корень кликабельной 3D-модели должен содержать только:

```text
PreviewRoot
├── StationObjectIdentity
└── StationUpgradeStageController    // если модель имеет стадии
```

HUD поднимается от коллайдера к `StationObjectIdentity` и получает определение
из `StationSystems_Default`. Имя GameObject больше не участвует в привязке и
может меняться без поломки выбора.

На превью нельзя добавлять `MaintainableObject`, `StationBattery`,
`SolarPowerSource`, `StationTurretController` или другие runtime-
компоненты. Иначе UI-модель зарегистрируется как настоящее устройство.

## Стадии улучшений

`StationUpgradeStageController` читает тип и ID только из
`StationObjectIdentity`, слушает `StationSystemsController.SystemsChanged` и
включает один дочерний объект `Stage_N`.

В `Stage_N` следует хранить:

- модель и материалы;
- коллайдеры;
- VFX и точки визуальных эффектов;
- stage-specific pivots и параметры, если они действительно отличаются.

Не следует дублировать в стадиях одинаковые компоненты обслуживания,
взаимодействия и идентичности. Антенна уже собрана по этой схеме: один
`MaintainableObject` и один `StationDeviceInteractable` находятся на корне.

Батарея пока сохраняет stage-specific `StationBattery`, потому что стадии
регистрируют разную ёмкость. Турели сохраняют stage-specific боевой rig,
поскольку уровни отличаются дальностью, уроном, скорострельностью, материалами,
yaw pivot и muzzle. Их ID при этом всё равно приходит только с корня.

## Правила ID

- ID должен быть уникальным и стабильным после выхода сохранений.
- Один ID используется в `StationSystems_Default`, `StationObjectIdentity` и
  соответствующем cutoff в `EnergyBalance_Default`.
- Роль обслуживания не заменяет ID.
- Для четырёх турелей используются `station_turret_01`–
  `station_turret_04`.
- Логический ID батареи `station_battery` не равен физическому ID источника
  энергии `station_battery_01`. Это разные ключи разных подсистем.

Изменение существующего ID требует миграции сохранений.

## Добавление нового объекта

1. Добавить определение в `StationSystems_Default`.
2. Назначить уникальный `Object Id`.
3. Добавить `StationObjectIdentity` на физический корень.
4. При необходимости добавить `StationUpgradeStageController`,
   `MaintainableObject`, `StationDeviceInteractable` и профильный компонент.
5. Добавить отдельный `StationObjectIdentity` с тем же ID на корень превью в
   `MainScene`.
6. Добавить энергетический cutoff с тем же ID, если объект потребляет энергию.
7. Проверить выбор в терминале, независимость уровня, переключение `Stage_N`,
   ручной запуск, обслуживание и восстановление сохранения.

## Проверка перед сохранением prefab

- На логическом корне ровно один `StationObjectIdentity`.
- В `StationUpgradeStageController` нет отдельного поля ID.
- В `MaintainableObject` и `StationDeviceInteractable` нет копий ID.
- Превью не содержит runtime-функциональности.
- Все коллайдеры физического объекта находятся под его interaction root.
- У турелей четыре разных ID, даже если они используют один prefab стадий.
