# NERA: полный аудит проекта и готовность First Playable

Дата проверки: 2026-08-25

Unity: 6000.0.71f1

Целевая платформа: Standalone Windows x64

Ветка: `main`

Save schema: 20

## Итог

Проект имеет рабочую системную основу и собирается, но **демо-версия пока не
готова к First Playable lock**. Главный путь существует как набор связанных
систем, однако ещё не подтверждён как законченный 15–25-минутный опыт.

Технический gate также нельзя считать полностью зелёным:

- свежая First Playable Development Build собрана успешно: `224.4 MB`,
  `0 errors`, `34 warnings`, четыре утверждённые сцены;
- PlayMode: `41/41 passed`;
- EditMode: один полный запуск дал `206 passed / 5 failed` при русской locale,
  повторный полный запуск после смены locale дал `211/211 passed`;
- solution build: `0 errors`, `28 warnings`;
- Unity Console после validator/build не чистая;
- новый player прошёл только короткий startup smoke, а не полный gameplay flow.

Следовательно, формулировка `252/252 green` верна только для конкретного
состояния locale и не является воспроизводимым CI-quality baseline.

## Что именно проверено

- актуальные документы, roadmap, backlog Sprint 01–10 и `.codex/doc_extract`;
- Git-история, рабочее дерево, крупные tracked-файлы и `.gitignore`;
- все собственные runtime/editor/test C#-файлы;
- package manifest/lock, сборочные DLL и фактические ссылки на sample assets;
- сцены, prefabs, ScriptableObjects, missing scripts и persistent IDs;
- Unity Test Framework через подключённый MCP;
- `dotnet restore/build` для `NERA.slnx`;
- `NERA/Validate Project`;
- свежая `First Playable Development x64` build и короткий headless smoke.

Не выполнен ручной пользовательский проход New Game/Continue и player-build
profiling. Эти проверки остаются обязательными и не могут быть достоверно
заменены Editor tests.

## Проверенный baseline

| Проверка | Результат | Оценка |
|---|---|---|
| Unity scene validation активной `Testing` | 0 missing scripts, 0 broken prefabs | PASS |
| Solution build | 0 errors, 28 warnings | PASS WITH WARNINGS |
| EditMode, первый полный запуск | 206 passed, 5 failed из-за русской locale | FAIL |
| EditMode, повторный полный запуск | 211 passed | CONDITIONALLY PASS |
| PlayMode | 41 passed за ~110 s | PASS |
| Project Validator | контентных исключений нет, но возникает lightmap Console Error | FAIL CLEAN-CONSOLE GATE |
| First Playable Development Build | succeeded, 224.4 MB, 343.37 s, 0 errors, 34 warnings | PASS WITH WARNINGS |
| Player startup smoke | процесс жив 20 s; Mono/Input/PhysX загружены; gameplay exception нет | PASS, STARTUP ONLY |
| Standalone full-flow | не выполнен | BLOCKED / MANUAL |
| WindowsPlayer Profiler baseline | не выполнен | TODO |

Build report: `Builds/WindowsDevelopment/NERA_FirstPlayable.exe`. Содержимое
player data пересобрано 2026-08-25 около 19:06. Launcher EXE не получил новый
timestamp, потому что его бинарное содержимое не изменилось.

## Готовность относительно Definition of Done

Целевой путь:

`Boot -> New Game -> Player Station -> battery -> terminal -> drone ->
Expedition 01 -> parkour/exploration -> Blue IO combat -> shard -> station ->
laboratory analysis -> explicit demo end`

| Gate | Состояние | Чего не хватает |
|---|---|---|
| Boot/New Game/Continue | PARTIAL | два полных standalone-прохода и проверка всех save slots |
| Station onboarding | PARTIAL | очистка debug/test content, читаемая цель и feedback |
| Drone/terminal/travel | FUNCTIONAL | ручная проверка формулировок, состояний и input mode |
| Expedition 01 | NOT PRODUCTION READY | вместо цельного маршрута остаются playground и `Map/TestRoom` |
| Combat/reward | PROTOTYPE | health/hit/death feedback, баланс, authored enemy presentation |
| Death/checkpoint/recovery | TECHNICALLY IMPLEMENTED | player-facing death UX и standalone rollback pass |
| Return/research/coda | PARTIAL | post-research objective и явный финал демо |
| Audio/VFX | NOT READY | в собственном project content нет ни одного audio asset |
| Persistence | IMPLEMENTED / NEEDS E2E | Continue и death rollback полного маршрута в player build |
| Automated regression | UNSTABLE | убрать зависимость EditMode tests от ambient locale |
| Build/Console | NOT CLEAN | 34 warnings, importer error, shader и validator diagnostics |
| Performance | NOT VERIFIED | GPU, GC, RAM, loading, FPS/1% low из WindowsPlayer |
| External playtest | NOT DONE | хотя бы один проход без подсказок разработчика |

## Сверка с milestone и sprint-документами

| Этап | Фактическое состояние | Вывод |
|---|---|---|
| Sprint 01–03 | DONE | foundation, station power, terminal, drone и travel подтверждены тестами |
| Sprint 04 | PARTIAL | Expedition 01 и combat технически есть, production route/feedback нет |
| Sprint 05 | PARTIAL | research/library работают, post-research направление не закрыто |
| Sprint 06 | PARTIAL | save/checkpoint реализованы, full-flow player verification отсутствует |
| Sprint 07 | PARTIAL | основной дефицит — UX, audio, VFX и playtest |
| Sprint 08 | PARTIAL | build есть, но tests/validator/Console не детерминированы и нет player profiling |
| Sprint 09 | TODO | lock criteria не выполнены |
| Sprint 10 | PARTIAL EARLY | M02 templates начаты до First Playable lock; дальнейшее расширение нужно заморозить |

`Current Scope Decisions` отменяет старую Translation-систему. Упоминания
Translation в ранних Milestone 02/Sprint extracts являются историческими и не
должны возвращаться в текущий scope. Текущий M02 loop —
Research/Library/Antenna/Unknown Signal.

В Build Settings включены 23 сцены: Boot, MainScene, Player Station,
Expedition 01–08 и Unknown Signal 01–12. First Playable builder корректно
собирает только четыре сцены. Expedition 02–08 и Unknown Signal 01–12 —
template content, а не готовые уровни: Expedition 03–08 почти повторяют
Expedition 02, Unknown Signal scenes отличаются в основном ID/именами.

## Наиболее важные проблемы

### P0 — блокеры готовности демо

1. Нет подтверждённого полного standalone-маршрута New Game и Continue.
2. Expedition 01 остаётся development playground, а не production blockout.
3. Нет health/damage/death presentation, минимального audio kit и финальной
   demo-coda после анализа Blue IO shard.
4. Нет WindowsPlayer performance capture и внешнего playtest.

### P1 — технические gate-проблемы

#### Тесты зависят от locale

Пять EditMode tests ожидают английские строки, не устанавливая/восстанавливая
locale:

- три `QuestSystemTests`;
- `Sprint05LaboratoryTests.EachSampleMustBeScannedOnceWithoutDuplicateResearch`;
- `StationSystemsTests.UpgradeInteractionServicesThenStartsThenOpensUpgrade`.

При русской locale они падают, после переключения на английскую проходят.
Исправление: в каждом затронутом fixture явно задавать и восстанавливать locale
или проверять localization keys/state вместо отображаемого текста. Gate:
полный EditMode run дважды подряд при RU и EN без зависимости от порядка.

#### TMP fallback импортируется нестабильно

Свежая build успешна, но Console содержит:

`Importer(NativeFormatImporter) generated inconsistent result for asset ...
LiberationSans SDF - Fallback.asset`.

Последний commit `e3c0f44` одновременно меняет IK-код и сокращает этот asset
примерно на 1277 строк. Связь с importer error вероятна, но требует проверки:
перегенерировать/reimport TMP fallback, проверить русские glyphs, собрать два
раза подряд и вынести font fix в отдельный commit.

#### Validator зависит от открытой сцены

`NERA/Validate Project`, запущенный из development `Testing.unity`, открывает
enabled scenes additively и получает Console Error о несовместимом lightmaps
mode: current Directional, loaded Non-Directional. Контентная валидация не
падает, но clean-console gate нарушен. Validator должен открывать сцены в
изолированном setup и гарантированно восстанавливать scene/lightmap state.

#### Опасное имя parkour IK callback

`ClimbController.onAnimatorIK` имеет неверный регистр для Unity message, из-за
чего solution build выдаёт `UNT0033`. Сейчас метод вызывается вручную из
`VaultClimbLedge`, поэтому это не доказанный runtime-break, но имя маскируется
под callback и легко ломается при рефакторинге. Переименовать в явный helper,
например `ApplyAnimatorIK`, и добавить integration regression для Animator IK.

#### Build warnings не закрыты

Свежая build: 34 warnings. Основные группы:

- deprecated `CinemachineFreeLook` и `CinemachineOrbitalTransposer`;
- Shader Graph `pow(f, e)` с потенциально отрицательным основанием в
  `BaseShaderForModels` и `DissolveShader`;
- division by zero в `Tutorial/VolumetricFog`/URP compile path;
- importer inconsistency TMP fallback.

До lock нужно закрыть importer/shader warnings и подтвердить visual regression.
Миграцию всей Cinemachine architecture безопаснее оставить отдельным пакетом
после lock, если deprecation не ломает player.

### P2 — качество кода и сопровождение

#### Крупные controllers

Собственный runtime: 154 C#-файла, около 32.7k строк; editor: 17 файлов,
около 5.9k строк; tests: 15 файлов, около 11.1k строк. Самые крупные runtime
классы:

| Файл | Строк |
|---|---:|
| `SaveGameController.cs` | 1296 |
| `ClimbController.cs` | 1236 |
| `InventoryLabHUDController.cs` | 1093 |
| `EnergySystemController.cs` | 1018 |
| `QuestController.cs` | 947 |
| `LaboratoryScreenController.cs` | 902 |
| `StationSystemsController.cs` | 795 |
| `StationUpgradeModeController.cs` | 744 |
| `QuestHUDController.cs` | 742 |

Один runtime asmdef и крупные multi-responsibility controllers повышают цену
изменений. До demo lock не нужен массовый rewrite. После lock выделить save
storage/serialization, UI presentation, quest state machine и parkour sensor
logic за существующими тестами.

#### Autosave не является background writer

`AutoSaveService` debounce-ит события, но вызывает `SaveGameController.Save()`
из `Update`; JSON serialization, temporary file и `File.WriteAllText` идут
синхронно на главном потоке. Тексты `background writer` в comments/docs
неточны. Для демо сначала измерить spike в WindowsPlayer. Если он заметен —
готовить immutable snapshot на main thread и выполнять только file I/O в
worker task с single-writer queue, flush и корректным shutdown.

#### Runtime logging

В runtime-коде 97 вызовов `Debug.Log*`. Часть полезна для ошибок сохранения и
валидации, часть создаёт шум. Перед release ввести категории/уровень verbosity,
оставив warnings/errors и убрав routine development logs из player.

#### Unplaced extension components

В сценах/prefabs не найдены serialized references на:

- `AnomalyElectronicDevice`;
- `QuestSignalEmitter`;
- `PersistentWorldFlag`;
- `PlayerCheckpointTrigger`.

Это не автоматически dead code: классы документированы как extension points.
Перед lock принять решение для каждого: реально разместить на финальном
маршруте и покрыть сценовым тестом либо удалить код и соответствующую
документацию.

#### Security surface

В собственном runtime нет сетевого клиента и не обнаружены committed API keys,
passwords или tokens. Сохранения локальные JSON и не защищены от ручного
редактирования — для offline demo это приемлемо, но их нельзя считать
trustworthy input для будущей сетевой/competitive логики.

## Мусор, повторы и зависимости

### Подтверждённый мусор

| Объект | Размер | Действие |
|---|---:|---|
| tracked `/profile.data` | 33,030,056 B (~31.5 MiB) | удалить из Git, добавить точное правило `/profile.data` или каталог profiler captures в `.gitignore` |
| `Assets/Plugins/Demigiant/DOTween` | ~0.73 MB source assets; `DOTween.dll` ~174 KB в player | собственный код не использует `DG.Tweening`; удалить package/settings вместе после контрольной build |

`profile.data` добавлен в debug commit и является profiler capture, а не
исходным ресурсом проекта. В этом аудите он не удалялся: сначала нужно решить,
нужен ли его архив вне репозитория.

### Точные бинарные дубликаты

SHA-256 scan файлов от 64 KB нашёл около **37.96 MiB** повторного содержимого:

- четыре пары Erika textures между `Erika.fbm` и `Art/Parkour/Textures` —
  ~13.50 MiB;
- `Scenes/Player_Station/Lightmap-0_comp_light.exr` и
  `Scenes/Player_Station/Red/Lightmap-0_comp_light.exr` — ~24.46 MiB.

Удалять автоматически нельзя: сначала проверить FBX importer/material GUIDs и
визуально сравнить baked presets, затем remap/rebake и только после этого убрать
лишние assets.

### Sample assets загрязнили production dependencies

`Assets/Samples/AI Navigation/2.0.9` нельзя просто удалить: production content
ссылается на sample assets.

- `black.mat`: шесть turret engineering-part prefabs;
- `grey.mat`: `Testing`, `MainScene`, `Expedition_01`;
- `modify_crosshair.png`: 27 item configs;
- `BanyanBark.mat`: battery VoltageRegulator prefab;
- `BanyanBranches.mat`: battery PowerBus prefab.

Нужно скопировать эти пять ресурсов в собственный namespace, remap GUIDs,
проверить сцены/prefabs, затем удалить sample folder. Собственный gameplay-код
не использует `UnityEngine.AI`/`NavMeshSurface`; после route decision можно
проверить удаление прямого `com.unity.ai.navigation`.

`Assets/TextMesh Pro/Examples & Extras` занимает около 6.35 MB. Его texture
`Fruit Jelly (B&W).jpg` используется production-материалом
`Assets/Shaders/Sand.mat`; сначала скопировать/remap texture, затем удалить
Examples & Extras. `TextMesh Pro/Resources` оставить.

### Кандидаты на package cleanup

В собственном коде/контенте не найдено фактического использования Visual
Scripting, Multiplayer Center и Collab Proxy. `feature.cinematic` транзитивно
подключает Timeline, Recorder, Alembic и FBX. Свежий Development Player всё ещё
содержит DLL этих систем, а также ProBuilder, AI Navigation samples, MCP и
DOTween. Только Visual Scripting DLL занимают около 1.75 MB, ProBuilder —
около 0.85 MB.

Порядок cleanup:

1. отдельная ветка/commit;
2. remap sample/TMP dependencies;
3. удалять по одному package с reimport, tests и player build после каждого;
4. заменить `feature.cinematic` только нужной прямой Cinemachine dependency;
5. закрепить Unity MCP на commit/version вместо mutable `#main` и исключить его
   runtime DLL из release player, если package позволяет;
6. ProBuilder удалять только после конвертации/проверки production blockout.

## Что нельзя считать мусором

- Addressables нужны Localization package, даже если собственный код напрямую
  их не вызывает;
- `QuestTypes.cs` выглядит low-reference при поиске по имени файла, но его
  типы активно используются;
- extension components выше могут быть запланированными authoring hooks;
- baked lightmaps и FBX-generated textures нельзя удалять только по хэшу;
- Development Build содержит PDB/debug/profiler payload по назначению; размер
  release build нужно измерять отдельно.

## План до демо

### Gate A — восстановить воспроизводимый технический baseline

1. Исправить/перегенерировать TMP fallback, проверить EN/RU glyphs.
2. Изолировать locale во всех EditMode fixtures.
3. Исправить validator lightmap/scene isolation.
4. Переименовать ложный `onAnimatorIK` helper и добавить IK regression.
5. Исправить Shader Graph/fog warnings с visual pass.
6. Удалить или вынести из Git `profile.data`.
7. Дважды подряд выполнить: solution build, EditMode RU/EN, PlayMode,
   validator, First Playable build. Console после каждого этапа должна быть
   чистой от неожиданных Error/Exception.

Критерий выхода: повторяемая зелёная автоматизация и две чистые build без
importer inconsistency.

### Gate B — собрать единственный production flow

1. Заморозить Sprint 10/M02 feature work.
2. Очистить Player Station от test/debug content.
3. Вынести playground из Expedition 01 и собрать маршрут: вход, обучение,
   landmark, encounter, reward, короткий возврат.
4. Разместить/удалить unplaced extension components по реальному маршруту.
5. Добавить post-research objective и явную demo-coda.

Критерий выхода: разработчик проходит весь путь без Console/debug shortcuts.

### Gate C — player-facing feedback

1. Health HUD, hit/damage/enemy death, checkpoint/death/revive presentation.
2. Objective start/update/complete, terminal NEW/state и research result.
3. Минимальный audio kit: ambience, UI, interaction, weapon, impact, enemy,
   pickup, objective, research.
4. Минимальный VFX/lighting pass и balance pass.

Критерий выхода: новый игрок понимает каждое обязательное действие без
подсказок разработчика.

### Gate D — standalone QA и First Playable lock

1. Полный New Game pass.
2. Save/reload, Continue, death/rollback, Return To Menu pass.
3. WindowsPlayer Profiler: High full-flow; Medium/Low smoke; CPU, Render Thread,
   GPU, GC, RAM, loading, FPS и 1% low.
4. Минимум один внешний 15–25-минутный playtest.
5. Закрыть blocker/critical, записать known issues и повторить два clean builds.
6. Архивировать `NERA_FP_LOCK_v0.1.0` с checklist и release notes.

### После lock

- package/sample cleanup с последовательными builds;
- миграция deprecated Cinemachine;
- разбиение крупных controllers/asmdefs;
- асинхронная очередь save I/O, если player profiling подтвердит spike;
- только затем production expansion Expedition 02–08/Unknown Signal.

## Решение о готовности

**First Playable: NOT READY.**

Для следующего пересмотра статуса обязательны четыре доказательства:

1. детерминированные tests при RU и EN;
2. две последовательные чистые First Playable builds;
3. два полных player flows — New Game и Continue/death rollback;
4. WindowsPlayer performance report и внешний playtest без developer guidance.
