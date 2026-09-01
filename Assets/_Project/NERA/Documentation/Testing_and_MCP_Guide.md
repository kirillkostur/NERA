# Тестирование и Unity MCP

Актуально на 2026-09-01. Среда аудита: Unity 6000.0.71f1, проект NERA.

## Проверенный baseline

До обновления набор содержал 362 теста:

- EditMode: 288, из них 10 падали;
- PlayMode: 74, из них 12 падали.

Контрольный результат после актуализации: 361 тест:

- EditMode: 287;
- PlayMode: 74;
- всего: 361.

Один устаревший дублирующий TestCase удалён: загрязнённый на 50% дрон теперь остаётся работоспособным, но расходует больше энергии. Поломка при нулевом condition и энергетический штраф загрязнения проверяются отдельными тестами.

Тестовые сборки находятся в:

- Assets/_Project/NERA/Tests/EditMode;
- Assets/_Project/NERA/Tests/PlayMode.

## Полный запуск

В Unity Test Runner сначала запустить все EditMode-тесты, затем все PlayMode-тесты. Итог считается успешным только при нуле failed и skipped в обоих режимах.

При запуске через Unity MCP:

1. Проверить mcpforunity://editor/state и дождаться ready_for_tools.
2. Запустить run_tests с mode EditMode, дождаться результата через get_test_job.
3. Повторить для mode PlayMode. Для PlayMode использовать увеличенный init timeout.
4. После изменения скриптов дождаться компиляции и domain reload. Если первый job потерян во время reload и редактор уже idle, очистить только orphaned job через clear_stuck и повторить тот же запуск.
5. Проверить Console на ошибки компиляции и необработанные исключения.

Initialization timeout или потеря job во время domain reload не являются падением теста. Падением считается завершённый job со статусом failed и результатами NUnit.

## Правила устойчивых тестов

### Quest System

- Production-каталог проверяется по текущим asset-данным. На момент аудита в нём три main-квеста: main.restore_station, main.launch_drone_expedition_01 и main.expedition_01.
- Изолированные проверки сигналов и repeatable-логики создают временные QuestDefinition и QuestCatalog, а не используют удалённые production ID.
- Цепочка main.expedition_01 включает активацию station_laboratory перед ResearchAnalyzed.

### Энергия и системы станции

- Фикстура должна явно подготовить заряд, maintenance condition и requested-active state.
- StationPowerController и EnergySystemController моделируются раздельно; после восстановления батареи тест синхронизирует bridge питания.
- Некоторые consumers обновляются по runtime-интервалу. Тест ждёт наблюдаемое состояние IsOperational с ограниченным deadline, а не предполагает готовность на следующем кадре.
- Грязный дрон может летать с повышенным расходом; только сломанный дрон блокирует запуск.

### PlayMode lifecycle

- Каждый lifecycle-тест загружает MainScene, ждёт Player_Station и отключает persistence для изоляции.
- Loading timing измеряется от момента запроса загрузки. После смерти учитывается трёхсекундная задержка до показа loading screen.
- Возврат источников энергии проверяется по регистрации источников, capacity и исходной generation, без предположения о ясной погоде.
- Анимационные mini-view могут находиться в неактивной UI-иерархии. Тест включает выбранную иерархию и проверяет синхронизацию после повторной активации.

### UI, локализация и погода

- RectTransform UI должен оставаться на UI layer. Вложенные 3D preview-объекты могут использовать StationUI layer.
- Runtime UI не должен включать TMP auto sizing; фиксированные размеры в Editor builders допустимы.
- Weather fade-тесты масштабируют длительность sandstorm относительно настроенного fade duration, чтобы не зависеть от конкретного баланса.

## Поддержка baseline

При изменении поведения:

- сначала обновить production assets или код;
- затем обновить проверки по наблюдаемому контракту;
- не отключать тест без эквивалентной поведенческой проверки;
- после изменения числа тестов обновить этот документ;
- перед передачей изменений повторить полный EditMode и PlayMode прогон.


## Известные Console-сообщения

Контрольные наборы проходят полностью, но аудит Console отдельно зафиксировал:

- ожидаемую ошибку отсутствующего test spawn missing_transition_test_spawn; негативный сценарий закрыт LogAssert.Expect;
- автодобавление отсутствующего Rigidbody для EXP01_NERA_MemoryCore, которое сейчас пишет Error при повторных загрузках сцены;
- два сообщения There can be only one active Event System во время полного PlayMode lifecycle-прогона;
- служебное Saving results to TestResults.xml, которое MCP Console классифицирует как Exception без stack trace.

Последние три пункта не меняют NUnit-результат, но остаются отдельными Console-cleanup задачами. Не следует считать зелёный Test Runner доказательством полностью чистого Console.


### Дополнение по EditMode

- UpgradeModeKeepsStagedPartWhenAllReturnSlotsAreFull намеренно пишет две Error-записи о невозможности вернуть staged part; обе закрыты LogAssert.Expect и проверяют lossless rollback.
- ProjectValidator при открытии Expedition_01 повторяет сообщение об автодобавлении Rigidbody для EXP01_NERA_MemoryCore.
- После удаления выбранных временных QuestDefinition во время domain reload может появиться SerializedObjectNotCreatableException в QuestDefinitionEditor.OnEnable. Это Editor lifecycle finding, а не падение завершённого NUnit job.
