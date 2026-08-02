# Parkour Player: устройство и настройка

## Что используется сейчас

Игровой персонаж NERA построен на Dynamic Parkour System. Старые
`PlayerController`, `PlayerFollowCamera`, отдельный `Player_Camera`, camera
presets и aim-crosshair удалены.

Единственный production-префаб игрока:

`Assets/_Project/NERA/Prefabs/Player/Player.prefab`

Он хранится в `MainScene/RuntimeRoot` и живёт между загрузками станции и
экспедиций. В контентные сцены нельзя добавлять второго Player или вторую
gameplay-камеру. Камера меню Boot остаётся отдельной и выгружается при старте
игры.

## Иерархия Player

```text
Player                         камера и Cinemachine rig
  Main Camera                  единственная камера с тегом MainCamera
  Free Look Camera             основная parkour-камера
  Slide Camera                 камера перехода во время slide
  PlayerModel                  физический и игровой объект, tag Player
    Animator                   оригинальный parkour Animator Controller
    Rigidbody                  единственный motor Rigidbody
    CapsuleCollider x2         normal и sliding; активен только один
    parkour controllers
    ParkourPlayerBridge
    PlayerInteractionController
    PlayerInventory
    PlayerEquipmentController
    PlayerEnergyWeaponController
    PlayerHealth
    Erika skeleton             отдельный ragdoll
```

NERA-компоненты должны находиться на `PlayerModel`, а не на внешнем `Player`.
Именно `PlayerModel` имеет тег `Player` и слой `Player` (3). Это нужно для
квестовых trigger-событий, врагов, инвентаря, оружия и интерактивных объектов.

`ParkourPlayerBridge` — стабильная точка связи NERA с пакетом. Через него UI
блокирует ввод, `SceneSpawnPoint` телепортирует Rigidbody, а смерть отключает
parkour. Gameplay-код не должен напрямую зависеть от отдельных контроллеров
пакета или конкретного Cinemachine-компонента.

## Взаимодействие

Наведение камерой и aim-focus больше не используются.

`PlayerInteractionController` ищет доступный `IInteractable` в радиусе вокруг
игрока:

- объект можно обнаружить спереди, сзади и сбоку;
- выбирается ближайший collider доступного объекта;
- несколько collider одного объекта поддерживаются;
- `Interaction Distance` задаёт дистанцию появления prompt;
- `Release Distance` создаёт небольшой hysteresis для hold-взаимодействия;
- стена или пол из `Obstruction Mask` блокируют взаимодействие;
- направление взгляда и положение камеры не проверяются;
- стандартная клавиша взаимодействия — `E`.

Для нового интерактивного объекта нужен collider на слое `Interactable` (6) или
`Item` (7) и компонент, реализующий `IInteractable`. Collider может находиться
на дочернем объекте: контроллер ищет интерфейс вверх по иерархии.

## Оружие без aim-состояния

`PlayerEquipmentController` крепит экипированный visual prefab к humanoid-кости
`RightHand`. Старые имена вида `mixamorig1:RightHand` остаются совместимыми:
сначала используется humanoid bone, затем точное имя и имя без префикса.

`PlayerEnergyWeaponController` обрабатывает `Quick Access Action = Fire` и
стреляет лучом вдоль `MainCamera`. Это не включает отдельную aim-анимацию, не
меняет положение камеры и не показывает crosshair. Если камера отсутствует,
используется настраиваемый `Fire Origin`, затем transform игрока.

`WeaponDefinition/Hit Mask` должен содержать слой `Enemy` и слои блокирующей
геометрии. Игрок в mask не входит: луч от камеры не заденет владельца, а стена
остановит выстрел до врага.

## Смерть и ragdoll

Parkour motor и ragdoll разделены намеренно:

- `PlayerModel/Rigidbody` двигает живого игрока и никогда не входит в ragdoll;
- normal/sliding motor colliders отключаются при смерти;
- скелет Erika содержит 12 kinematic Rigidbody и 11 CharacterJoint;
- ragdoll colliders выключены, пока игрок жив;
- при смерти выключаются ввод, parkour controllers и Animator;
- затем включаются gravity/collisions ragdoll и импульс передаётся в Hips.

`RestoreFullHealth` не оживляет уже активированный ragdoll. Для respawn нужно
пересоздать/перезагрузить Player либо добавить отдельный контролируемый pipeline
возрождения.

## Слои и parkour-поверхности

Пакет больше не зависит от произвольных строковых tags.

| Слой | Индекс | Назначение |
|---|---:|---|
| `ParkourLedge` | 14 | уступы для hang/climb |
| `ParkourSurface` | 15 | vault, slide, pole, reach и другие поверхности |
| `ParkourPoint` | 16 | `HandlePoints` и сгенерированные `GPoint` |

На parkour-геометрию добавляется `ParkourSurface`. Поле `Surface Type` — flags,
поэтому один объект может поддерживать несколько действий:

- `Vault`;
- `VaultOver`;
- `Slide`;
- `Reach`;
- `Ledge`;
- `Pole`;
- `Climb`.

Для точек используется `Resources/Parkour/Climbing/GPoint.prefab`.
`HandlePoints` при генерации принудительно назначает слой `ParkourPoint`.

## Где находятся файлы пакета

- `Code/Runtime/Parkour/` — runtime-код;
- `Code/Editor/Parkour/` — editor helper пакета;
- `Art/Parkour/` — Erika, Animator и анимации;
- `Prefabs/Parkour/` — примеры поверхностей;
- `Resources/Parkour/` — action configs, camera blends и GPoint;
- `_Development/Parkour/Testing.unity` — рабочая smoke-сцена, не Build Scene;
- `Documentation/ThirdParty/DynamicParkourSystem/` — лицензия и оригинальный PDF.

## Инструменты и проверка

`NERA -> Parkour -> Rebuild Player Integration` повторно:

1. нормализует parkour prefabs и слои;
2. пересобирает NERA-компоненты Player;
3. собирает ragdoll Erika;
4. мигрирует development-сцену;
5. заменяет Player в MainScene;
6. удаляет старые camera-zone объекты из Player_Station.

После ручных изменений Player или parkour prefabs запустить:

1. `NERA -> Parkour -> Rebuild Player Integration`;
2. `NERA -> Validate Project`;
3. EditMode и PlayMode tests;
4. ручной проход `_Development/Parkour/Testing.unity`;
5. ручной проход станции: terminal, inventory, interaction, weapon, смерть;
6. `NERA -> Build -> Windows x64`.

Production-сборка по-прежнему запускается на High и с лимитом 100 FPS.

## Что нельзя менять без регресса

- не добавлять второй locomotion Rigidbody на `PlayerModel`;
- не включать normal и sliding CapsuleCollider одновременно;
- не включать ragdoll bodies до смерти;
- не возвращать camera aim-focus в interaction;
- не добавлять Player/Camera в контентные сцены;
- не менять GUID перемещённых parkour assets;
- не удалять оригинальную лицензию пакета.

Текущий Cinemachine rig оставлен в compatibility-режиме, чтобы сохранить
ощущение камеры пакета. Его обновление на современный Cinemachine pipeline
следует делать отдельной задачей с визуальным и игровым регрессом.
