# NERA Documentation

## Актуальные документы

- [`Testing_and_MCP_Guide.md`](Testing_and_MCP_Guide.md) — проверенный тестовый baseline, запуск через Unity/MCP и правила поддержки устойчивых фикстур.
- [`Current_Project_Audit_2026-08-25.md`](Current_Project_Audit_2026-08-25.md) — главный текущий источник: полный code/asset/package/build audit, реальные test caveats, решение о готовности и план до демо.
- [`Runtime_Performance_Baseline_2026-08-24.md`](Runtime_Performance_Baseline_2026-08-24.md) — воспроизводимые Editor-замеры до/после оптимизации и следующий player-build profiling gate.
- [`First_Playable_Status_and_Roadmap_2026-08-04.md`](First_Playable_Status_and_Roadmap_2026-08-04.md) — границы первого полноценного среза и продуктовый порядок работ; фактический baseline обновлён аудитом от 2026-08-25.
- [`Sprint_Finalization_Backlog.md`](Sprint_Finalization_Backlog.md) — рабочий backlog с актуальными статусами.

## Руководства

- [`Content_Assembly_Guide.md`](Content_Assembly_Guide.md) — подключение предметов, локаций и контента.
- [`Quest_System_Guide.md`](Quest_System_Guide.md) — авторинг и устройство квестов.
- [`Parkour_Player_Integration.md`](Parkour_Player_Integration.md) — текущий Player prefab, parkour, камера, оружие и ragdoll.
- [`Main_Menu_Assembly_Guide.md`](Main_Menu_Assembly_Guide.md) — Boot/MainScene и переходы.
- [`Autosave_System_Guide.md`](Autosave_System_Guide.md) — debounce автосохранения, полный checkpoint rollback, authored-точки и резервные файлы; запись файла сейчас синхронная.
- [`Station_Device_Architecture.md`](Station_Device_Architecture.md) — пошаговая настройка физических слотов, Engineering Part, бонусов характеристик и StationUIPreview.
- [`Unknown_Signal_Engineering_Part_Progression.md`](Unknown_Signal_Engineering_Part_Progression.md) — распределение всех Engineering Part по 12 антенным локациям, range-gates, повторные экземпляры и authoring checklist; Expeditions не затрагиваются.
- [`Station_Lighting_Guide.md`](Station_Lighting_Guide.md) — три режима baked lighting, связь с батареей/sandstorm и порядок проверки через Boot.
- [`Camera_Orbit_Zones.md`](Camera_Orbit_Zones.md) — профили орбит `FreeLookCam` и триггеры комнат, коридоров и узких мест.
- [`PC_Quality_Presets.md`](PC_Quality_Presets.md) — PC quality baseline и profiling gate.
- [`Loading_Screen_Guide.md`](Loading_Screen_Guide.md) — окно загрузки для старта, сохранений, переходов и смерти; настройка времени, изображений и локализованных подсказок.

## Исторические срезы

- `Current_Project_Audit_2026-07-30.md`;
- `Current_Project_Audit_2026-08-14.md`;
- `Sprint_01_Result_Notes.md`;
- `Sprint_03_Result_Notes.md`.

Исторические документы фиксируют состояние на дату создания и не должны использоваться как текущий план работ.

Документы из `.codex/doc_extract` также являются входными milestone/sprint
срезами. При конфликте действует порядок: `Current Scope Decisions` -> текущий
аудит -> `Sprint_Finalization_Backlog` -> исторические extracts. Translation
system исключена из актуального scope; её упоминания в ранних M02-документах
не являются задачей текущего First Playable.
