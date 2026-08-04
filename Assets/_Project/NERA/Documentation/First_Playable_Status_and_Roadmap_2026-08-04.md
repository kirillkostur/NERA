# NERA: состояние и план до первого полноценного среза

Дата аудита: 2026-08-04  
Unity: 6000.0.71f1  
Целевая платформа: Standalone Windows x64

## Краткий вывод

Проект имеет сильную системную основу, но первый полноценный срез ещё не собран как цельный пользовательский опыт.

- Техническая база и автоматическая регрессия: `GREEN`.
- Основной игровой цикл: `PARTIAL`.
- Production-контент Station + Expedition 01: `NOT READY`.
- Сохранение, checkpoint resume и rollback мира: `IMPLEMENTED / NEEDS FULL-FLOW QA`.
- Player-facing UX, смерть/восстановление и аудио: `NOT READY`.
- Актуальная standalone-сборка после интеграции parkour: `NOT VERIFIED`.
- First Playable lock: `NOT READY`.

Главный дефицит проекта сейчас — не отсутствие новых подсистем. Уже существуют питание станции, терминал, дрон, карта, экспедиции, бой, предметы, инвентарь, исследование, библиотека, квесты и сохранение. Не хватает одного короткого, устойчивого и отполированного пути, в котором эти системы последовательно работают для игрока.

## Проверенный baseline

- Git working tree был чист перед обновлением документации.
- Unity Console после проверки не содержит ошибок и предупреждений.
- Unity Test Framework после save/checkpoint rewrite: 119/119 EditMode passed.
  В графическом batch-run 19/20 PlayMode passed; один существующий UI drag-test
  требует обычного Game View, потому что batch стартует в 640x480 и размещает
  authored storage target за границей экрана. До rewrite обычный Editor run был
  20/20.
- В Build Settings включены 12 сцен: Boot, MainScene, Player Station, Expedition 01-08 и Unknown Signal 01.
- Boot открыт без dirty state; MainScene, Player Station и Expedition 01 были проверены через Unity hierarchy.
- Последняя документированная Windows Development Build собрана 2026-07-30: 164.56 MB, 0 errors, 0 warnings. Она предшествует крупной интеграции parkour от 2026-08-01 — 2026-08-04 и больше не является актуальным release-кандидатом.
- Production identity зафиксирован: Company Name `Measured Field`, Product Name
  `Nera`, version `0.1.0`.
- Production save path зафиксирован на стандартном Unity
  `Application.persistentDataPath`; добавлена безопасная одноразовая миграция
  сохранения из прежнего пути `DefaultCompany/My project`.

Автотесты подтверждают структуру и большое число системных контрактов, но не подтверждают читаемость маршрута, качество боя, UX, аудио, производительность и прохождение полного цикла в standalone build.

## Definition of Done первого среза

Первый срез — один 15–25-минутный путь:

`Boot -> New Game -> Player Station -> восстановление батареи -> терминал -> запуск дрона -> открытие Expedition 01 -> путешествие -> parkour/исследование -> встреча и бой с Blue IO -> получение Blue IO Shard -> возврат на станцию -> анализ в лаборатории -> явное завершение демо`

Срез считается готовым только если:

- новый игрок понимает следующую цель без подсказок разработчика;
- все обязательные действия имеют визуальную и звуковую обратную связь;
- смерть ведёт к понятному recovery flow, а не к зависшему ragdoll;
- подобранные одноразовые объекты и завершённые события не появляются снова после reload/save-load;
- Continue запускает документированное состояние: безопасный хаб или сохранённый checkpoint;
- финальный анализ выдаёт результат и явное завершение среза;
- полный путь дважды пройден в Windows Development Build: New Game и Continue;
- Console чистая, автоматические тесты зелёные, blocker/critical bugs закрыты;
- записаны frame time, FPS/1% low, RAM и loading time на High хотя бы на одном целевом PC.

Expedition 02-08, Unknown Signal, полный набор улучшений станции и масштабная переработка архитектуры не входят в этот срез.

## Что есть сейчас и чего не хватает

| Участок | Состояние | Что уже работает | Главный пробел |
|---|---|---|---|
| Boot/menu | PARTIAL | New Game/Continue с 3 слотами, overwrite confirmation, Options/Exit dialogs, additive launch и legacy migration в slot 1 | нет актуального standalone build smoke полного menu flow |
| Player/movement | PARTIAL | единый Player prefab, parkour, камера, взаимодействие, оружие, ragdoll | требуется ручной production-route regression после свежих изменений |
| Station | PARTIAL | питание, терминал, карта, дрон, лаборатория, storage, upgrades | сцена содержит тестовые предметы, отключённых врагов, `TestStation` и parkour-примеры |
| Quest flow | PARTIAL | data-driven quests, 4 main quests, side quests, HUD, save version 16 и opt-in checkpoint после выбранных этапов | нет журнала/уведомлений; после анализа Blue IO отсутствует authored coda/следующая цель |
| Expedition 01 | PARTIAL | spawn/return, 2 Blue IO, loot, parkour geometry | production-сцена смешана с `Map/TestRoom` и parkour playground; нет цельного маршрута и presentation pass |
| Combat | PROTOTYPE | hitscan weapon, projectiles, health, damage, enemy drop, death ragdoll и автоматический checkpoint recovery | нет health/damage HUD, hit/death feedback и death screen; AI движется напрямую к игроку без authored navigation/obstacle policy |
| Inventory/research | PARTIAL | instance inventory, energy, laboratory analysis, library unlock, простой background save, per-slot rolling backups и checkpoint snapshot | нет полного player-facing результата и проверенного standalone resume pass |
| World state | PARTIAL | `WorldItem`, `IOEnemyController`, булевы флаги дверей/головоломок и полный rollback после смерти сохраняются в version 16 | production-объектам нужны authored IDs; многосоставные состояния требуют отдельной модели |
| Audio/VFX | NOT READY | предусмотрены отдельные hooks, есть базовые runtime materials/particles | в project content нет authored audio assets; combat/ambience/VFX pass не выполнен |
| QA/performance | PARTIAL | 121 EditMode green, 19/20 graphical batch PlayMode, validator, PC quality presets | повторить 20 PlayMode в обычном Game View; нет актуальной post-parkour сборки, standalone full-flow и performance capture |

## Подтверждённые риски

### Закрыто в коде, требуется full-flow QA: одноразовый контент

`SaveGameData` version 16 хранит consumed IDs `WorldItem`, defeated IDs
`IOEnemyController`, булевы флаги сюжетных объектов и динамическую позицию
чекпоинта. Текущее состояние пишется фоном, а отдельный checkpoint snapshot
откатывает inventory и мир вместе. Важный квест или головоломка могут сразу
заменить точку отката после выдачи награды.

Для production refactor уровня нужно заменить fallback hierarchy keys на
стабильные authored `Persistent Id` и пройти сценарии Continue/Death в build.

### P0. Нет завершённого failure/recovery loop

`PlayerHealth` теперь автоматически восстанавливается из полного checkpoint
snapshot после короткой ragdoll-паузы, а HUD кратко подтверждает успешное
создание контрольной точки. Всё ещё отсутствуют health bar, damage feedback и отдельный death screen,
поэтому recovery работает технически, но ещё не готов как финальный UX.

### P0. Production-сцены смешаны с тестовым контентом

Player Station содержит debug/prototype content. Expedition 01 содержит `Map/TestRoom`, parkour examples и повторяющиеся тестовые конструкции. Это полезно для разработки механики, но не формирует читаемый уровень с завязкой, темпом, боем, добычей и возвратом.

### P1. Placeholder-локации выглядят готовее, чем являются

Expedition 02-08 включены в Build Settings. Текущая проверка показала, что Expedition 03-08 отличаются от Expedition 02 только именем и ID spawn point — четыре строки diff. Их нельзя учитывать как готовый контент или использовать для расширения прогрессии до First Playable lock.

### P1. Validator проверяет структуру, а не готовность среза

`NERA -> Validate Project` проверяет production identity, build scenes, Player
prefab, location configs, quest catalog, upgrade prefabs и PC quality assets. Он
не проверяет scene-object persistence, health/death UX, audio, authored route,
current build и full-flow completion.

## Зафиксированное решение по identity и сохранениям

- Company Name: `Measured Field`.
- Product Name: `Nera`.
- Текущие Windows paths:
  `%USERPROFILE%\AppData\LocalLow\Measured Field\Nera\nera_save_1.json`,
  `nera_save_2.json`, `nera_save_3.json`.
- Pre-slot legacy path:
  `%USERPROFILE%\AppData\LocalLow\Measured Field\Nera\nera_save.json`.
- Previous-identity legacy path:
  `%USERPROFILE%\AppData\LocalLow\DefaultCompany\My project\nera_save.json`.
- Старое одиночное сохранение один раз мигрирует в slot 1.
- Существующий slot 1 всегда имеет приоритет и никогда не перезаписывается
  legacy-файлом.
- Legacy-файл удаляется только после успешного копирования и проверки размера.
- Маркер миграции не позволяет старому файлу восстановиться после сброса slot 1.

## Порядок разработки

### Этап 0 — зафиксировать границы среза

Сделать первым:

1. `DONE 2026-08-04`: зафиксировать Company Name `Measured Field`, Product Name
   `Nera` и политику одноразовой миграции сохранений.
2. Зафиксировать единственный player-facing scope: Boot, MainScene, Player Station и Expedition 01.
3. Скрыть Expedition 02-08/Unknown Signal из пути игрока и пометить их template content; отдельный First Playable build profile не должен считать их готовыми уровнями.
4. Записать короткий expected flow и чек-лист прохождения из Definition of Done выше.

Критерий выхода: у команды один маршрут, одна финальная точка и нет задач по новому контенту вне среза.

### Этап 1 — закрыть целостность gameplay loop

1. `IMPLEMENTED 2026-08-04`: persisted consumption/defeat state для
   `WorldItem`, `IOEnemyController` и enemy drops; назначить production authored
   IDs объектам финального маршрута.
2. `IMPLEMENTED 2026-08-04`: простой background save, отдельный полный
   checkpoint snapshot, current scene/spawn, Continue resume и death rollback;
   выполнить end-to-end PlayMode/standalone QA.
3. `PARTIAL 2026-08-04`: автоматическое revive/reload и краткий checkpoint HUD готовы;
   добавить health HUD, damage feedback и player-facing death screen.
4. Добавить authored завершение после `research_io_blue_shard_01`: сообщение результата, завершение квеста и демо-coda.
5. Добавить интеграционные PlayMode tests для pickup -> save -> reload и death -> recovery.

Критерий выхода: срез нельзя сломать reload или смертью, а после анализа есть однозначный финал.

### Этап 2 — собрать production blockout

1. Вынести parkour test room в `_Development/Parkour/Testing.unity` и оставить в Expedition 01 только нужные приёмы.
2. Очистить Player Station от test items, disabled enemies и временных объектов.
3. Собрать критический маршрут Expedition 01: вход, обучение движению, читаемый landmark, encounter, reward и короткий путь назад.
4. Проверить enemy navigation/line-of-sight policy и исключить прохождение сквозь level geometry.
5. Провести первый внешний 15–25-минутный playtest на сером blockout.

Критерий выхода: игрок проходит маршрут без developer guidance и понимает пространственную цель.

### Этап 3 — player-facing feedback

1. Objective started/updated/completed notifications; журнал можно ограничить активными и завершёнными main quests.
2. Health, hit, enemy hit/death, loot, interaction result и research result feedback.
3. Минимальный audio kit: station ambience, UI, interaction, weapon, impact, enemy, pickup, objective, research.
4. VFX и lighting pass только для событий, важных для чтения геймплея.
5. Balance pass: урон, здоровье, дальность, скорость снарядов, энергия и длительность сканирования.

Критерий выхода: каждое обязательное действие читается без Console и debug-сообщений.

### Этап 4 — standalone QA и lock

1. Собрать новый Windows Development Build после всех parkour/gameplay изменений.
2. Пройти New Game, Continue, save/reload, death/recovery и возврат в меню.
3. Проверить Low/Medium/High, зафиксировать performance baseline и loading times.
4. Исправить blocker/critical bugs, составить known issues.
5. Выпустить `NERA_FP_LOCK_v0.1.0` с build, checklist и release notes.

## Что не делать до First Playable lock

- не собирать production-контент Expedition 02-08;
- не переносить parkour на новый Cinemachine pipeline;
- не раскладывать механически крупные controllers без тестовой страховки;
- не расширять station upgrade tree;
- не делать массовую оптимизацию без standalone profiler capture;
- не считать новый prefab/config завершённым gameplay content без полного прохода.

## Ближайший рабочий пакет

Background save, checkpoint snapshot и технический death recovery реализованы
2026-08-04. Следующий приоритет — не новая система сохранений и не production-
контент Expedition 02:

1. end-to-end проверка `pickup/kill -> Continue` и `pickup/kill -> death -> rollback`;
2. authored `Persistent Id` для объектов финального маршрута Expedition 01;
3. health/death UX и damage feedback;
4. очистка и пересборка Station + Expedition 01 в один production blockout;
5. явная demo-coda после анализа Blue IO shard.
