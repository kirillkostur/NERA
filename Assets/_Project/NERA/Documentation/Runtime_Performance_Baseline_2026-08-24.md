# NERA Runtime Performance Baseline

Дата замера: 2026-08-24

Unity: 6000.0.71f1

Среда: Windows Editor

Сценарии: Player_Station и Expedition_01

> Актуализация 2026-08-25: таблицы ниже остаются историческими Editor-замерами.
> Свежая First Playable build имеет 224.4 MB, 0 build errors и 34 warnings.
> Текущий gate и найденные Console/test проблемы описаны в
> `Current_Project_Audit_2026-08-25.md`.

## Методика

`RuntimePerformanceBenchmarkTests` запускает Boot-equivalent New Game flow,
загружает Player_Station, затем Expedition_01. Для каждого сценария выполняется:

1. 180 кадров прогрева;
2. 600 измеряемых кадров;
3. сбор frame time, Main Thread, CPU Total, Render Thread, GC Alloc,
   BehaviourUpdate, Physics.Simulate и render counters;
4. повторение optimized-прогона три раза;
5. итоговое значение — медиана трёх прогонов.

Во время benchmark `vSyncCount = 0`, `Application.targetFrameRate = -1`, а
save-файлы перенаправляются во временную директорию через `NERA_SAVE_ROOT`.

## Результаты

### Player_Station idle

| Метрика | Baseline | Optimized | Изменение |
|---|---:|---:|---:|
| Frame median | 6.734 ms | 6.506 ms | -3.4% |
| Main Thread median | 3.275 ms | 3.267 ms | -0.3% |
| CPU Total median | 6.713 ms | 6.434 ms | -4.2% |
| BehaviourUpdate median | 0.554 ms | 0.358 ms | -35.3% |
| GC median | 52,221 B/frame | 37,641 B/frame | -27.9% |
| Physics.Simulate mean | 0.085 ms | 0.088 ms | +0.003 ms |
| Draw calls | 52 | 52 | без изменений |
| Triangles | 64,799 | 64,799 | без изменений |

### Expedition_01 idle

| Метрика | Baseline | Optimized | Изменение |
|---|---:|---:|---:|
| Frame median | 6.877 ms | 6.879 ms | нейтрально |
| Main Thread median | 3.691 ms | 3.542 ms | -4.0% |
| CPU Total median | 6.902 ms | 6.835 ms | -1.0% |
| BehaviourUpdate median | 0.296 ms | 0.251 ms | -15.1% |
| GC median | 25,561 B/frame | 24,447 B/frame | -4.4% |
| Physics.Simulate mean | 0.076 ms | 0.104 ms | +0.028 ms |
| Draw calls | 357 | 357 | без изменений |
| Triangles | 130,175 | 130,175 | без изменений |

## Внесённые оптимизации

- energy simulation выполняется с интервалом 0.1 s и переиспользует рабочие
  коллекции;
- battery, solar, station power, terminal и baked lighting подписываются на
  lifecycle events вместо покадрового поиска controller;
- enemy target discovery кешируется и выполняется с интервалом;
- interaction кеширует `Collider -> IInteractable` и проверяет distance до
  obstruction raycast;
- fog shader globals отправляются только при изменении transform/collider;
- time-of-day visual refresh ограничен 20 Hz;
- maintenance update выполняется 10 Hz только для активного состояния;
- CinemachineBrain, renderers и часто используемые компоненты вне parkour
  кешируются;
- physics queries турелей и projectile используют явные layer masks;
- terminal UI объединяет event bursts в одно обновление за 0.1 s.

После основного benchmark locomotion Rigidbody дополнительно переведён на
`RigidbodyInterpolation.None` во время ходьбы/бега с возвратом `Interpolate`
после остановки. Это изменение покрыто parkour tests, но не включено в цифры
таблицы и должно войти в следующий player-build baseline.

Update 2026-08-25: parkour-часть optimization pass `3ac2afe` отменена по
запросу. Исходная частота parkour raycast/ground/slope проверок, allocating
поиск точек и authoring-helper поведение восстановлены; динамическая Rigidbody
interpolation сохранена. Таблицы ниже получены до этого отката и больше не
являются текущим baseline для сцен с активным parkour.

## Интерпретация

Основной подтверждённый результат — снижение стоимости `BehaviourUpdate` и GC,
особенно на станции. Геометрия и draw calls не менялись, поэтому scripting gains
не получены за счёт уменьшения контента.

Render Thread в Editor изменялся нестабильно: примерно `+4.9%` на станции и
`+15.2%` в Expedition_01. Один raw-recording прогон также был явным CPU
выбросом. Это влияние нельзя автоматически считать player regression:
Profiler recording, Scene/Game windows и EditorLoop сами добавляют нагрузку.

## Verification 2026-08-25

После функциональных исправлений benchmark повторён три раза с той же схемой
180/600 кадров. Медиана трёх прогонов:

| Сценарий | Frame | Main Thread | CPU Total | BehaviourUpdate | GC |
|---|---:|---:|---:|---:|---:|
| Player_Station | 5.529 ms | 2.594 ms | 5.563 ms | 0.307 ms | 51,989 B/frame |
| Expedition_01 | 6.530 ms | 3.072 ms | 6.581 ms | 0.211 ms | 38,867 B/frame |

Относительно предыдущего optimized Editor baseline CPU Main улучшился на
20.6% на станции и 13.3% в Expedition_01; BehaviourUpdate — на 14.3% и 15.9%
соответственно. Draw calls/triangles остались практически неизменными:
`51/64,799` на станции и `356/130,175` в Expedition_01.

GC recorder вырос на 38.1%/59.0%. Все три повторения дали близкие значения,
однако этот counter снимается внутри Editor/Test Runner и включает allocations
того же процесса. До WindowsPlayer capture это следует считать profiling
риском, а не доказанной gameplay-регрессией.

Windows First Playable Development Build успешно создан:

- 4 сцены: Boot, MainScene, Player_Station, Expedition_01;
- BuildReport: 223.14 MB, 0 errors, 22 warnings;
- output: `Builds/WindowsDevelopment/NERA_FirstPlayable.exe`;
- Development Player/debugger/profiler transport подтверждён smoke-запуском.

Эти значения заменены свежей build того же дня: `224.4 MB`, `0 build errors`,
`34 warnings`, `343.37 s`. Player data пересобран; 20-секундный headless smoke
подтвердил startup без gameplay exception. Это не full-flow и не новый
performance baseline.

Финальный 15-секундный snapshot показал около 293.5 MiB Working Set, но
assemblies только что закончили загрузку, а profiler transport был активен.
Это диагностическое значение, не финальный RAM baseline.

## Следующий profiling gate

1. Открыть Unity Profiler и подключить собранный `WindowsPlayer` до заполнения
   его profiler buffer.
2. Измерить Station idle, Station sandstorm/lighting transitions, Expedition
   idle и Expedition combat.
3. Для каждого сценария записать CPU Main, Render Thread, GPU frame time, GC,
   RAM, draw calls, triangles, loading time и 1% low.
4. Полный прогон выполнить на High; Medium и Low проверить минимум smoke-pass.
5. Сравнивать PlayerLoop и профиль build, а не общий EditorLoop.
6. Не менять quality settings до получения GPU-bound кадров из player build.
7. Если Profiler не подключается сразу, запускать с
   `-profiler-maxusedmemory 268435456`; стандартные 128 MB были заполнены за
   время unattended smoke.

## Локальные артефакты

Raw и JSON результаты текущего Editor benchmark находятся в
`Library/NERAProfiling/`:

- `baseline.json`, `baseline.raw`;
- `optimized.json`, `optimized.raw`;
- `optimized-repeat-1.json`, `optimized-repeat-2.json`;
- `comparison.md`.

`Library` не входит в source control. Этот документ является сохраняемым
сводным baseline; raw captures при необходимости нужно архивировать отдельно.
