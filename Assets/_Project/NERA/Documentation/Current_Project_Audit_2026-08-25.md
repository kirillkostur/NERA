# NERA: актуальный технический baseline

Дата: 2026-08-25

Unity: 6000.0.71f1

Целевая платформа: Standalone Windows x64

Save schema: 20

## Краткий вывод

Автоматический технический baseline снова зелёный:

- EditMode: `211/211`;
- PlayMode: `41/41`;
- First Playable Development Build: успешно, `0 errors`, `22 warnings`,
  `223.14 MB` по BuildReport;
- скрытый standalone smoke: Boot загружается, графика/physics/input
  инициализируются, исключений gameplay-кода нет.

First Playable ещё не готов к lock. Оставшиеся блокеры находятся главным
образом в ручном full-flow QA, player-build profiling и production-контенте,
а не в отсутствии очередной подсистемы.

## Проверенный baseline

| Параметр | Значение |
|---|---|
| Unity | 6000.0.71f1 |
| Platform | StandaloneWindows64 |
| Save schema | 20 |
| Enabled build scenes | 23 |
| First Playable build scenes | Boot, MainScene, Player_Station, Expedition_01 |
| EditMode | 211 passed / 0 failed |
| PlayMode | 41 passed / 0 failed |
| Общий test result | 252 passed / 0 failed |
| Development Build | 223.14 MB, 0 errors, 22 warnings |
| Build output | `Builds/WindowsDevelopment/NERA_FirstPlayable.exe` |

`WindowsPlayerBuild` теперь содержит отдельную команду
`NERA/Build/First Playable Development x64`. Она валидирует проект, собирает
только четыре утверждённые сцены, включает Development, Allow Debugging и
Connect With Profiler. Обычная команда полного Windows build сохранена.

## Что исправлено в этом проходе

### Parkour rollback

Parkour-specific optimization из `3ac2afe` отменена до состояния `c5736ef`:
восстановлены исходные update/query/detection paths, debug defaults и поведение
authoring helpers. Остальные подсистемы optimization pass не затронуты.
Динамическое переключение `Rigidbody.interpolation` из `7385c61` сохранено и
остаётся покрыто отдельным EditMode test. Предыдущие performance tables были
сняты до rollback и требуют повторного player capture для parkour-сцен.

### Автотесты и реальные runtime-контракты

- save-version test использует `SaveGameData.CurrentVersion`;
- Quest HUD editor migration больше не задаёт production font size из кода;
- maintenance quest проверяет generic title и target-specific objective;
- Boot lighting test использует `MainMenuController`, а не устаревший путь
  дочерней кнопки;
- HUD tests ожидают актуальный bullet `•`;
- terminal status test учитывает coalesced refresh `0.1 s`;
- decorative 3D station/map нормализуются на Default layer без изменения
  отдельного `InteractionPoint`;
- drone range test явно отделён от сюжетной sandstorm, которая закономерно
  блокирует повторный запуск.

### Baked lighting

Полный PlayMode flow теперь действительно проходит через Boot -> New Game ->
Player_Station и подтверждает:

1. выключенная основная батарея -> `Backup Power Emergency`;
2. включённая батарея -> `Normal Operation`;
3. sandstorm -> `Low Energy Warning`.

Проверка больше не падает до загрузки станции. Пустые массивы `Light` или
lightmap textures по-прежнему не блокируют вторую часть preset.

### Upgrade safety

`RestoreInstalledParts()` теперь принимает сохранённую деталь только если:

- slot существует в station definition;
- item существует в `ItemCatalog_Default`;
- item имеет тип `EngineeringPart`;
- compatibility совпадает с system type, object ID и slot ID.

Отмена staged-детали стала lossless: запись удаляется из staged state только
после успешного возврата. Если inventory и storage одновременно заполнены,
upgrade mode остаётся открытым, autosave guard не снимается, а Return To Main
Menu отменяется. Это защищает от сохранения состояния с потерянным предметом.

### Persistent IDs

`ProjectValidator` уже содержит проверку отсутствующих и дублирующихся ID для
tracked `WorldItem`, `IOEnemyController` и `PersistentWorldFlag`, а также
команду `NERA/Assign Missing Persistent IDs`. Повторный статический проход по
всем включённым scene YAML не нашёл tracked scene instances с пустым ID.

Новые объекты финального маршрута всё равно должны проходить validator перед
каждой сборкой.

### Shader safety

В `VolumetricFog.shader` исправлена формула Henyey-Greenstein: числитель теперь
использует scattering², а основание степени имеет нижнюю границу. Это убирает
деление на ноль/`pow` от отрицательного значения в собственном fog shader.

## Повторное Editor profiling

После изменений выполнены три одинаковых прогона: 180 warm-up + 600 measured
frames для станции и Expedition_01. Ниже медиана трёх прогонов.

| Сценарий | Метрика | Предыдущий optimized | Сейчас | Изменение |
|---|---:|---:|---:|---:|
| Player_Station | Frame median | 6.506 ms | 5.529 ms | -15.0% |
| Player_Station | Main Thread median | 3.267 ms | 2.594 ms | -20.6% |
| Player_Station | CPU Total median | 6.434 ms | 5.563 ms | -13.5% |
| Player_Station | BehaviourUpdate median | 0.358 ms | 0.307 ms | -14.3% |
| Player_Station | GC median | 37,641 B/frame | 51,989 B/frame | +38.1% |
| Expedition_01 | Frame median | 6.879 ms | 6.530 ms | -5.1% |
| Expedition_01 | Main Thread median | 3.542 ms | 3.072 ms | -13.3% |
| Expedition_01 | CPU Total median | 6.835 ms | 6.581 ms | -3.7% |
| Expedition_01 | BehaviourUpdate median | 0.251 ms | 0.211 ms | -15.9% |
| Expedition_01 | GC median | 24,447 B/frame | 38,867 B/frame | +59.0% |

CPU/Behaviour результат устойчиво лучше предыдущего Editor baseline. GC вырос
во всех трёх прогонах, но `GC Allocated In Frame` записан внутри Editor/Test
Runner процесса и включает editor/test overhead. Это сигнал для player capture,
а не доказанная runtime-регрессия. GPU, 1% low и RAM также нельзя честно
получить из EditorLoop.

## Development Build и smoke

Финальная clean build из четырёх сцен завершилась за `105.34 s`. BuildReport
показывает `223.14 MB`; фактический размер файлов на диске — около
`223.35 MiB`. Финальный 15-секундный и отдельный 45-секундный скрытый smoke
подтвердили:

- Direct3D 11 и RTX 4060 Ti инициализировались;
- assemblies, Input System и PhysX загрузились;
- Development Player объявил debugger/profiler connection;
- Boot не записал gameplay exception.

Финальный 15-секундный snapshot показал около `293.5 MiB` Working Set, но это
не RAM baseline: Development profiler transport и только что завершившаяся
загрузка assemblies входят в значение. При запуске без подключённого Profiler
player через некоторое время
заполняет стандартный 128 MB profiler buffer и останавливает запись. Для
следующего замера нужно заранее открыть Profiler и выбрать WindowsPlayer либо
запустить player с увеличенным `-profiler-maxusedmemory`.

## Известные build warnings

Сборка имеет 22 warnings:

- legacy `CinemachineFreeLook` и `CinemachineOrbitalTransposer`;
- `pow` warnings в `BaseShaderForModels.shadergraph` и
  `DissolveShader.shadergraph`;
- один URP shadow compile warning.

Собственный fog `pow` исправлен; финальная clean build уже содержит эту правку.
Оставшиеся graph/camera warnings требуют визуального regression pass; их нельзя
безопасно закрывать только текстовой заменой.

## Оставшиеся задачи

### Требуют ручного прохода в Unity/standalone

1. Пройти Boot -> New Game -> Battery -> Terminal -> Drone -> Expedition_01 ->
   checkpoint/death -> Continue -> Return To Menu.
2. В подключённом WindowsPlayer Profiler записать Station idle, sandstorm,
   Expedition idle и combat: CPU Main, Render Thread, GPU, GC, RAM, loading и
   1% low. High — полный проход, Medium/Low — smoke.
3. Визуально сравнить камеры до/после будущей миграции Cinemachine и материалы
   после исправления Shader Graph `Power` nodes.
4. Проверить все три lightmap preset глазами: renderer indices, отсутствие
   вспышки/чёрного кадра и соответствие реальных ламп baked-картам.

### Production/content

1. Очистить Player_Station от test/debug content.
2. Пересобрать Expedition_01 из parkour playground в читаемый 15–25-минутный
   production blockout.
3. Добавить health/damage/death UI, combat feedback и demo-coda после анализа
   Blue IO shard.
4. Добавить минимальный audio/VFX kit и пройти внешний playtest.

### Отдельный технический пакет после First Playable lock

- миграция legacy Cinemachine pipeline;
- визуальное исправление Shader Graph `pow` warnings;
- synthetic configs для оставшихся controller tests;
- multi-state persistence для новых составных объектов.

## Следующий критерий готовности

- текущие 252 automated tests остаются зелёными;
- текущая clean build после fog shader fix проходит ручной full-flow;
- получен WindowsPlayer, а не EditorLoop performance baseline;
- blocker/critical bugs закрыты;
- production route и player-facing feedback готовы к двум полным прохождениям
  New Game и Continue.
