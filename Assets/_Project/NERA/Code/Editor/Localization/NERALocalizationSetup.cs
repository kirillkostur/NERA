using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NERA.Combat;
using NERA.Enemies;
using NERA.Expeditions;
using NERA.Items;
using NERA.Library;
using NERA.Localization;
using NERA.Quests;
using NERA.Research;
using NERA.Station;
using NERA.Terminal;
using NERA.UI;
using TMPro;
using UnityEditor;
using UnityEditor.Localization;
using UnityEditor.Localization.Plugins.CSV;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.Localization.Tables;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace NERA.Editor.Localization
{
    public static class NERALocalizationSetup
    {
        private const string Root = "Assets/_Project/NERA/Localization";
        private const string LocaleRoot = Root + "/Locales";
        private const string TableRoot = Root + "/StringTables";
        private const string ExportRoot = Root + "/Exports";
        private const string SettingsPath = Root + "/NERA Localization Settings.asset";

        private static readonly string[] TableNames =
        {
            NERALocalization.CommonTable,
            NERALocalization.MainMenuTable,
            NERALocalization.HudTable,
            NERALocalization.TerminalTable,
            NERALocalization.InventoryLaboratoryTable,
            NERALocalization.ContentTable,
            NERALocalization.QuestsTable
        };

        private static readonly Dictionary<string, string> StaticRussian =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["ACTIVE"] = "АКТИВНО",
                ["Active"] = "Активно",
                ["Antenna"] = "Антенна",
                ["Battery"] = "Батарея",
                ["CALIBRATION"] = "КАЛИБРОВКА",
                ["CLOSE"] = "ЗАКРЫТЬ",
                ["CONTINUE"] = "ПРОДОЛЖИТЬ",
                ["Description"] = "Описание",
                ["DRON"] = "ДРОН",
                ["DROP"] = "ВЫБРОСИТЬ",
                ["EXIT"] = "ВЫХОД",
                ["Inventory TAB"] = "ИНВЕНТАРЬ [TAB]",
                ["[E] Interact"] = "[E] Взаимодействовать",
                ["Progress 100%"] = "Прогресс 100%",
                ["LAUNCHE"] = "ЗАПУСК",
                ["LIBRARY"] = "БИБЛИОТЕКА",
                ["MAP"] = "КАРТА",
                ["Move to the location?"] = "Переместиться в выбранную локацию?",
                ["NAME"] = "НАЗВАНИЕ",
                ["NEW GAME"] = "НОВАЯ ИГРА",
                ["NO"] = "НЕТ",
                ["OFF"] = "ВЫКЛ",
                ["ON"] = "ВКЛ",
                ["OPTIONS"] = "НАСТРОЙКИ",
                ["OVERWRITE SLOT"] = "ПЕРЕЗАПИСАТЬ СОХРАНЕНИЕ",
                ["POWER"] = "ПИТАНИЕ",
                ["Progress"] = "Прогресс",
                ["SCAN"] = "СКАНИРОВАТЬ",
                ["SAVING..."] = "СОХРАНЕНИЕ...",
                ["SELECT A GAME SAVE"] = "ВЫБЕРИТЕ СОХРАНЕНИЕ",
                ["STATION"] = "СТАНЦИЯ",
                ["STATUS"] = "СОСТОЯНИЕ",
                ["STORAGE"] = "ХРАНИЛИЩЕ",
                ["UPGRADE"] = "УЛУЧШИТЬ",
                ["UPGRADES"] = "УЛУЧШЕНИЯ",
                ["YES"] = "ДА",
                ["Engineering part used to restore and upgrade station mechanisms."] = "Инженерная деталь для восстановления и улучшения механизмов станции.",
                ["Battery, powers the station"] = "Батарея питает станцию",
                ["Improved high-power battery."] = "Улучшенная батарея высокой мощности.",
                ["Generates station power. Clean it outside to restore efficiency."] = "Вырабатывает энергию станции. Очистите панель снаружи, чтобы восстановить эффективность.",
                ["Stores generated energy and supplies all active consumers."] = "Накапливает выработанную энергию и питает всех активных потребителей.",
                ["Standard station cell bank."] = "Стандартный блок аккумуляторов станции.",
                ["Expanded high-capacity cell bank."] = "Расширенный блок аккумуляторов высокой ёмкости.",
                ["Central terminal. It cannot be stopped or upgraded from itself."] = "Центральный терминал. Его нельзя отключить или улучшить через него самого.",
                ["Surveys nearby sectors. Upgrade its drive to reach distant expeditions."] = "Исследует ближайшие секторы. Улучшайте привод, чтобы отправляться в дальние экспедиции.",
                ["Short-range expedition drive."] = "Экспедиционный привод малого радиуса действия.",
                ["Medium-range expedition drive."] = "Экспедиционный привод среднего радиуса действия.",
                ["Long-range expedition drive."] = "Экспедиционный привод большого радиуса действия.",
                ["Analyzes recovered objects and unlocks Library records."] = "Анализирует найденные объекты и открывает записи в библиотеке.",
                ["Restores charge to energy-powered equipment."] = "Восстанавливает заряд энергетического оборудования.",
                ["Finds unknown signals. Install a replacement drive before first use."] = "Обнаруживает неизвестные сигналы. Перед первым использованием установите новый привод.",
                ["Restores short-range signal reception."] = "Восстанавливает приём сигналов малого радиуса.",
                ["Improves signal resolution and range."] = "Повышает точность и дальность обнаружения сигналов.",
                ["Enables deep-range signal acquisition."] = "Позволяет обнаруживать сигналы на большой дальности.",
                ["Automatic defense platform for the first station sector."] = "Автоматическая защитная платформа первого сектора станции.",
                ["Automatic defense platform for the second station sector."] = "Автоматическая защитная платформа второго сектора станции.",
                ["Automatic defense platform for the third station sector."] = "Автоматическая защитная платформа третьего сектора станции.",
                ["Automatic defense platform for the fourth station sector."] = "Автоматическая защитная платформа четвёртого сектора станции.",
                ["Factory-installed defensive platform."] = "Защитная платформа заводской комплектации.",
                ["Improves tracking and sustained fire."] = "Улучшает сопровождение целей и ведение непрерывного огня.",
                ["Maximum performance for the first defense sector."] = "Максимальная эффективность первого оборонительного сектора.",
                ["Installs the second defensive platform."] = "Устанавливает вторую защитную платформу.",
                ["Reinforces its tracking assembly."] = "Усиливает систему сопровождения целей.",
                ["Adds an autonomous long-range control core."] = "Добавляет автономное управляющее ядро большого радиуса действия.",
                ["Builds the third defensive platform."] = "Создаёт третью защитную платформу.",
                ["Adds a stabilized drive and processing core."] = "Добавляет стабилизированный привод и вычислительное ядро.",
                ["Installs redundant signal and targeting channels."] = "Устанавливает резервные каналы связи и наведения.",
                ["Restores the fourth defense sector."] = "Восстанавливает четвёртый оборонительный сектор.",
                ["Upgrades its signal and rotation systems."] = "Улучшает системы связи и поворота.",
                ["Installs a high-load command assembly."] = "Устанавливает усиленный командный модуль."
            };

        private static readonly Dictionary<string, string> ContentRussian =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["item.ancient_record_02.name"] = "Древняя запись ретранслятора",
                ["item.ancient_record_02.description"] = "Повреждённая служебная табличка из сигнальной сети. Сохранившиеся фрагменты могут объяснить, почему ретранслятор прекратил передачу.",
                ["item.energy_pistol_01.name"] = "Энергетический пистолет",
                ["item.energy_pistol_01.description"] = "Компактное экспедиционное оружие, стреляющее сфокусированными энергетическими импульсами по нестабильным сущностям IO.",
                ["item.io_blue_shard_01.name"] = "Осколок Blue IO",
                ["item.io_blue_shard_01.description"] = "Сконденсированная аномалия, оставшаяся после сущности Blue IO. Занимает отдельную ячейку аномалий.",
                ["item.io_integrator_01.name"] = "Интегратор IO",
                ["item.io_integrator_01.description"] = "Специализированный лабораторный инструмент для интеграции исследованных камней аномалий IO.",
                ["item.nera_memory_core_01.name"] = "Ядро памяти NERA",
                ["item.nera_memory_core_01.description"] = "Компактное ядро хранения данных NERA, найденное на Древнем аванпосте. Герметичная конструкция сохранила фрагменты записей станции.",
                ["item.nera_signal_relay_02.name"] = "Ядро сигнального ретранслятора NERA",
                ["item.nera_signal_relay_02.description"] = "Плотное ядро ретранслятора, найденное в Экспедиции 02. Сохранившуюся структуру маршрутизации можно исследовать в лаборатории станции.",
                ["item.capacitor_01.name"] = "Конденсатор",
                ["item.cooling_01.name"] = "Система охлаждения",
                ["item.emitter_damage_01.name"] = "Эмиттер урона",
                ["item.sensor_01.name"] = "Датчик",
                ["item.servo_01.name"] = "Сервомеханизм",
                ["item.servo_drive_01.name"] = "Сервопривод",
                ["item.servo_drive_01.description"] = "Инженерная деталь для восстановления и улучшения механизмов станции.",
                ["library.io_blue_shard_01.title"] = "ОСКОЛОК BLUE IO // АНАЛИЗ АНОМАЛИИ",
                ["library.io_blue_shard_01.description"] = "Сконденсированный энергетический кристалл, извлечённый из сущности Blue IO. Лабораторный анализ выявил разрушительный резонанс средней силы. После интеграции в совместимое оборудование осколок создаёт один радиальный импульс, повреждающий ближайшие аномалии IO.",
                ["library.station_primer.title"] = "СТАНЦИЯ NERA // РУКОВОДСТВО ПО ЭКСПЛУАТАЦИИ",
                ["library.station_primer.description"] = "Станция служит базой для экспедиций. Восстанавливайте и обслуживайте критические системы, запускайте разведывательный дрон, изучайте обнаруженные локации и готовьте оборудование перед выходом. Найденные артефакты будут пополнять библиотеку по мере исследований.",
                ["library.expedition01_memory_core.title"] = "ЯДРО ПАМЯТИ NERA // ПЕРВИЧНЫЙ АНАЛИЗ",
                ["library.expedition01_memory_core.description"] = "В найденном ядре сохранились фрагменты записей станции. Его энергетический рисунок стабилен и имеет искусственное происхождение, в отличие от нестабильных сигнатур Blue IO. Архив подтверждает, что перед эвакуацией аванпост намеренно сохранил эти записи.",
                ["library.expedition02_ancient_record.title"] = "ДРЕВНЯЯ ЗАПИСЬ 02 // ПРОТОКОЛ РЕТРАНСЛЯТОРА",
                ["library.expedition02_ancient_record.description"] = "Фрагмент служебной записи NERA описывает сигнальную сеть, предназначенную для предупреждения отдалённых поселений. Несколько последних пакетов так и не были переданы. В сохранившихся символах повторяются понятия: сигнал, предупреждение, врата и память.",
                ["library.expedition02_signal_relay.title"] = "СИГНАЛЬНЫЙ РЕТРАНСЛЯТОР NERA // ЭКСПЕДИЦИЯ 02",
                ["library.expedition02_signal_relay.description"] = "Найденное ядро принадлежало планетарной сигнальной сети. Таблица маршрутизации постоянно перенаправляет трафик в область, отмеченную искажением IO. В массиве также содержится неактивная последовательность калибровки, совместимая с антенной станции.",
                ["research.research_io_blue_shard_01.name"] = "Осколок Blue IO",
                ["research.research_ancient_record_02.name"] = "Древняя запись ретранслятора",
                ["research.research_nera_memory_core_01.name"] = "Ядро памяти NERA",
                ["research.research_nera_signal_relay_02.name"] = "Ядро сигнального ретранслятора NERA",
                ["weapon.energy_pistol_01.name"] = "Энергетический пистолет",
                ["integration.io_blue_discharge.name"] = "Разряд Blue IO",
                ["enemy.io_blue_weak.name"] = "Слабый Blue IO",
                ["enemy.io_blue_relay_guard.name"] = "Страж ретранслятора Blue IO",
                ["location.unknownsignal01.name"] = "?",
                ["location.unknownsignal01.description"] = "Неизвестный сигнал. Требуется анализ антенной.",
                ["quest.main.expedition_01.title"] = "Древний аванпост",
                ["quest.main.expedition_01.stage.01.title"] = "Отправляйтесь на Древний аванпост",
                ["quest.main.expedition_01.stage.02.title"] = "Исследуйте Древний аванпост",
                ["quest.main.expedition_01.stage.05.description"] = "Проведите анализ Осколка Blue IO в лаборатории.",
                ["quest.main.launch_drone_expedition_01.stage.01.description"] = "Запустите разведывательный дрон и дождитесь завершения сканирования Экспедиции 01."
            };

        private static readonly Dictionary<string, string> QuestEnglish =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["quest.main.expedition_01.title"] = "Ancient Outpost",
                ["quest.main.expedition_01.description"] = "Explore the ancient outpost discovered by the drone and investigate the signal source.",
                ["quest.main.expedition_01.stage.01.title"] = "Travel to the Ancient Outpost",
                ["quest.main.expedition_01.stage.01.description"] = "Select the discovered location on the map and begin the expedition.",
                ["quest.main.expedition_01.stage.02.title"] = "Explore the Ancient Outpost",
                ["quest.main.expedition_01.stage.02.description"] = "Find the source of the Blue IO activity.",
                ["quest.main.expedition_01.stage.03.title"] = "Recover a Blue IO Sample",
                ["quest.main.expedition_01.stage.03.description"] = "Collect the shard left after the encounter.",
                ["quest.main.expedition_01.stage.04.title"] = "Return to the Station",
                ["quest.main.expedition_01.stage.04.description"] = "Deliver the sample to the laboratory.",
                ["quest.main.expedition_01.stage.05.title"] = "Analyze the Sample",
                ["quest.main.expedition_01.stage.05.description"] = "Analyze the Blue IO Shard in the laboratory.",
                ["quest.main.launch_drone_expedition_01.title"] = "Launch the Drone Expedition",
                ["quest.main.launch_drone_expedition_01.description"] = "Send the survey drone on an expedition and wait for it to return with data about a new location.",
                ["quest.main.launch_drone_expedition_01.stage.01.title"] = "Launch the Drone Expedition",
                ["quest.main.launch_drone_expedition_01.stage.01.description"] = "Launch the survey drone and wait for the Expedition 01 scan to finish.",
                ["quest.main.restore_battery.title"] = "Restart the Battery",
                ["quest.main.restore_battery.description"] = "Restore power to the station.",
                ["quest.main.restore_battery.stage.01.title"] = "Restore Station Power",
                ["quest.main.restore_battery.stage.01.description"] = "Restore power to the station.",
                ["quest.main.first_terminal.title"] = "Open the Terminal",
                ["quest.main.first_terminal.description"] = "Open the station terminal.",
                ["quest.main.first_terminal.stage.01.title"] = "Open the Terminal",
                ["quest.main.first_terminal.stage.01.description"] = "Open the station terminal.",
                ["quest.side.clean_solar_panel.title"] = "Clean {targetName}",
                ["quest.side.clean_solar_panel.description"] = "Contamination has reduced the efficiency of {targetName}.",
                ["quest.side.clean_solar_panel.stage.01.title"] = "Clean {targetName}",
                ["quest.side.clean_solar_panel.stage.01.description"] = "Restore the object's condition to at least 95%.",
                ["quest.side.restore_turret.title"] = "Restart {targetName}",
                ["quest.side.restore_turret.description"] = "The system was disabled by an external malfunction.",
                ["quest.side.restore_turret.stage.01.title"] = "Restart {targetName}",
                ["quest.side.restore_turret.stage.01.description"] = "Enable the object from the station terminal."
            };

        [MenuItem("NERA/Localization/Setup or Update All")]
        public static void SetupAll()
        {
            string activeScenePath = SceneManager.GetActiveScene().path;
            EnsureFolders();
            (Locale english, Locale russian) = EnsureSettingsAndLocales();
            EnsureCollections(english, russian);
            AddRuntimeEntries();
            AddContentEntries();
            AddQuestEntries();
            MigratePrefabTexts();
            MigrateScene("Assets/_Project/NERA/Scenes/Boot.unity", true);
            MigrateScene("Assets/_Project/NERA/Scenes/MainScene.unity", false);
            if (!string.IsNullOrEmpty(activeScenePath))
                EditorSceneManager.OpenScene(activeScenePath, OpenSceneMode.Single);
            ExportCsv();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("NERA localization setup completed: English and Russian tables, UI bindings and language selector are ready.");
        }

        [MenuItem("NERA/Localization/Export CSV")]
        public static void ExportCsv()
        {
            EnsureFolder(ExportRoot);
            foreach (StringTableCollection collection in
                     LocalizationEditorSettings.GetStringTableCollections())
            {
                string path = $"{ExportRoot}/{collection.TableCollectionName}.csv";
                using (StreamWriter writer = new StreamWriter(path, false, new System.Text.UTF8Encoding(true)))
                    Csv.Export(writer, collection);
            }
            AssetDatabase.Refresh();
        }

        [MenuItem("NERA/Localization/Sync Item Tables")]
        public static void SyncItemTables()
        {
            EnsureFolders();
            (Locale english, Locale russian) = EnsureSettingsAndLocales();
            EnsureCollections(english, russian);
            AddSimpleContent<ItemData>(
                "item",
                "itemId",
                "displayName",
                "description",
                preserveRussian: true);
            ExportCsv();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("NERA item localization synchronized. Existing Russian translations were preserved.");
        }

        public static void SelectContentTable()
        {
            Object table = AssetDatabase.LoadMainAssetAtPath(
                TableRoot + "/Content.asset");
            if (table == null)
                return;

            Selection.activeObject = table;
            EditorGUIUtility.PingObject(table);
        }

        private static (Locale english, Locale russian) EnsureSettingsAndLocales()
        {
            LocalizationSettings settings =
                AssetDatabase.LoadAssetAtPath<LocalizationSettings>(SettingsPath);
            if (settings == null)
            {
                settings = ScriptableObject.CreateInstance<LocalizationSettings>();
                settings.name = "NERA Localization Settings";
                AssetDatabase.CreateAsset(settings, SettingsPath);
            }
            LocalizationEditorSettings.ActiveLocalizationSettings = settings;
            // The game has its own language button. Unity Localization's editor
            // Game View toolbar is also unstable during PlayMode test reloads in
            // Unity 6, so keep that editor-only duplicate selector disabled.
            LocalizationEditorSettings.ShowLocaleMenuInGameView = false;

            Locale english = EnsureLocale("en", "English");
            Locale russian = EnsureLocale("ru", "Русский");
            LocalizationSettings.InitializeSynchronously = true;
            LocalizationSettings.ProjectLocale = english;
            LocalizationSettings.StartupLocaleSelectors.Clear();
            LocalizationSettings.StartupLocaleSelectors.Add(
                new PlayerPrefLocaleSelector
                {
                    PlayerPreferenceKey = NERALocalization.LocalePreferenceKey
                });
            LocalizationSettings.StartupLocaleSelectors.Add(new SystemLocaleSelector());
            LocalizationSettings.StartupLocaleSelectors.Add(
                new SpecificLocaleSelector { LocaleId = english.Identifier });
            EditorUtility.SetDirty(settings);
            return (english, russian);
        }

        private static Locale EnsureLocale(string code, string localeName)
        {
            Locale locale = LocalizationEditorSettings.GetLocale(code);
            if (locale != null)
                return locale;
            locale = Locale.CreateLocale(code);
            locale.name = localeName;
            AssetDatabase.CreateAsset(locale, $"{LocaleRoot}/{localeName}.asset");
            LocalizationEditorSettings.AddLocale(locale);
            return locale;
        }

        private static void EnsureCollections(Locale english, Locale russian)
        {
            foreach (string tableName in TableNames)
            {
                StringTableCollection collection =
                    LocalizationEditorSettings.GetStringTableCollection(tableName) ??
                    LocalizationEditorSettings.CreateStringTableCollection(
                        tableName,
                        TableRoot,
                        new List<Locale> { english, russian });
                foreach (StringTable table in collection.StringTables)
                    LocalizationEditorSettings.SetPreloadTableFlag(table, true);
            }
        }

        private static void AddRuntimeEntries()
        {
            Add(NERALocalization.CommonTable, "common.yes", "YES", "ДА");
            Add(NERALocalization.CommonTable, "common.no", "NO", "НЕТ");
            Add(NERALocalization.CommonTable, "common.close", "CLOSE", "ЗАКРЫТЬ");
            Add(NERALocalization.MainMenuTable, "save.select_new_slot", "SELECT A SAVE SLOT", "ВЫБЕРИТЕ ЯЧЕЙКУ СОХРАНЕНИЯ");
            Add(NERALocalization.MainMenuTable, "save.select_existing_slot", "SELECT A GAME SAVE", "ВЫБЕРИТЕ СОХРАНЕНИЕ");
            Add(NERALocalization.MainMenuTable, "save.completion", "{0}% COMPLETE", "ПРОЙДЕНО: {0}%", true);
            Add(NERALocalization.MainMenuTable, "save.date_format", "MM.dd.yyyy - HH:mm", "dd.MM.yyyy - HH:mm");
            Add(NERALocalization.MainMenuTable, "save.empty", "EMPTY", "ПУСТО");
            Add(NERALocalization.MainMenuTable, "options.language", "LANGUAGE: {0}", "ЯЗЫК: {0}", true);

            Add(NERALocalization.HudTable, "quest.main_header", "MAIN QUEST", "ОСНОВНОЕ ЗАДАНИЕ");
            Add(NERALocalization.HudTable, "quest.side_header", "SIDE QUEST", "ПОБОЧНОЕ ЗАДАНИЕ");
            Add(NERALocalization.HudTable, "interaction.press", "[{0}] Press — {1}", "[{0}] Нажать — {1}", true);
            Add(NERALocalization.HudTable, "interaction.hold", "[{0}] Hold — {1}", "[{0}] Удерживать — {1}", true);
            Add(NERALocalization.HudTable, "interaction.hold_progress", "[{0}] Hold — {1}%", "[{0}] Удерживать — {1}%", true);
            AddPrompt("action", "Interact", "Взаимодействовать");
            AddPrompt("action", "Pick Up", "Подобрать");
            AddPrompt("action", "Open Terminal", "Открыть терминал");
            AddPrompt("action", "Return to Station", "Вернуться на станцию");
            AddPrompt("action", "Use Laboratory", "Использовать лабораторию");
            AddPrompt("action", "Start Laboratory", "Запустить лабораторию");
            AddPrompt("action", "Restore Power", "Восстановить питание");
            AddPrompt("action", "Use Terminal", "Использовать терминал");
            AddPrompt("action", "Start Computer", "Запустить компьютер");
            AddPrompt("action", "Clean Solar Panel", "Очистить солнечную панель");
            AddPrompt("action", "Service Antenna", "Обслужить антенну");
            AddPrompt("action", "Service Turret", "Обслужить турель");
            AddPrompt("action", "Service Device", "Обслужить устройство");
            AddPrompt("unavailable", "Unavailable", "Недоступно");
            AddPrompt("unavailable", "Item data missing", "Данные предмета отсутствуют");
            AddPrompt("unavailable", "Laboratory has no power", "Лаборатория обесточена");
            AddPrompt("unavailable", "Station Power Online", "Питание станции включено");
            AddPrompt("unavailable", "Terminal Offline — Restore Power First", "Терминал отключён — сначала восстановите питание");

            AddTerminalEntries();
            AddLaboratoryEntries();
        }

        private static void AddTerminalEntries()
        {
            AddT("station.select_object", "SELECT STATION OBJECT", "ВЫБЕРИТЕ ОБЪЕКТ СТАНЦИИ");
            AddT("station.tab.status", "STATUS", "СОСТОЯНИЕ");
            AddT("station.select_object_hint", "Select an object in the 3D station preview.", "Выберите объект на трёхмерной модели станции.");
            AddT("station.power.low", "Low Power", "Мало энергии");
            AddT("station.power.active", "Active", "Активно");
            AddT("station.power.inactive", "Inactive", "Неактивно");
            AddT("station.no_object_selected", "NO OBJECT SELECTED", "ОБЪЕКТ НЕ ВЫБРАН");
            AddT("map.travel_confirmation", "Travel to {0}?", "Переместиться в локацию «{0}»?", true);
            AddT("map.select_sector", "Select a sector on the 3D map.", "Выберите сектор на трёхмерной карте.");
            AddT("map.drone_target", "DRONE TARGET\n{0}", "ЦЕЛЬ ДРОНА\n{0}", true);
            AddT("map.drone_unavailable", "DRONE UNAVAILABLE", "ДРОН НЕДОСТУПЕН");
            AddT("map.scanning", "SCANNING {0}%", "СКАНИРОВАНИЕ {0}%", true);
            AddT("map.recharging", "RECHARGING {0}s", "ПЕРЕЗАРЯДКА {0} с", true);
            AddT("map.signal_found", "SIGNAL FOUND\n{0}", "СИГНАЛ ОБНАРУЖЕН\n{0}", true);
            AddT("map.antenna_hint", "ANTENNA\nCalibrate to reveal a hidden signal on an opened sector.", "АНТЕННА\nВыполните калибровку, чтобы обнаружить скрытый сигнал в открытом секторе.");
            AddT("map.antenna_unavailable", "ANTENNA UNAVAILABLE", "АНТЕННА НЕДОСТУПНА");
            AddT("map.calibrating", "CALIBRATING {0}%", "КАЛИБРОВКА {0}%", true);
            foreach (string state in new[] { "Idle", "Scanning", "Returning", "Charging", "Ready", "Calibrating", "SignalFound" })
                AddT("map.state." + KeyPart(state), state.ToUpperInvariant(), TranslateState(state));
        }

        private static void AddLaboratoryEntries()
        {
            AddL("scan.progress", "SCANNING {0}%", "СКАНИРОВАНИЕ {0}%", true);
            AddL("scan.progress_mixed_case", "Progress - {0}%", "Прогресс — {0}%", true);
            AddL("scan.start", "START SCAN", "НАЧАТЬ СКАНИРОВАНИЕ");
            AddL("scan.known_item", "KNOWN ITEM", "ИЗВЕСТНЫЙ ПРЕДМЕТ");
            AddL("scan.scanned", "SCANNED", "ОТСКАНИРОВАНО");
            AddL("charger.ready", "Laboratory charger ready.", "Лабораторное зарядное устройство готово.");
            AddL("charger.charge", "CHARGE {0}%", "ЗАРЯД {0}%", true);
            AddL("inventory.select_item", "SELECT AN ITEM", "ВЫБЕРИТЕ ПРЕДМЕТ");
            AddL("laboratory.status.ready", "Laboratory ready.", "Лаборатория готова.");
            AddL("laboratory.status.paused_stopped", "Scanning paused — laboratory is stopped.", "Сканирование приостановлено — лаборатория остановлена.");
            AddL("laboratory.status.paused_energy", "Scanning paused — insufficient station energy.", "Сканирование приостановлено — недостаточно энергии станции.");
            AddL("laboratory.status.analysis_not_required", "This item does not require analysis.", "Этот предмет не требует анализа.");
            AddL("laboratory.status.scanning_item", "Scanning {0}...", "Сканирование: {0}...", true);
            AddL("laboratory.status.stopped_from_terminal", "Laboratory is stopped from the station computer.", "Лаборатория остановлена с терминала станции.");
            AddL("laboratory.status.already_identified", "This item is already identified and does not require analysis.", "Этот предмет уже опознан и не требует анализа.");
            AddL("laboratory.status.already_scanned", "{0} is already scanned.", "Предмет «{0}» уже отсканирован.", true);
            AddL("laboratory.status.insufficient_energy", "Insufficient station energy.", "Недостаточно энергии станции.");
            AddL("laboratory.status.insufficient_power", "Insufficient station power.", "Питание станции недоступно.");
            AddL("laboratory.status.sample_already_scanned", "This sample is already scanned.", "Этот образец уже отсканирован.");
            AddL("laboratory.status.analysis_complete", "Analysis complete: {0}", "Анализ завершён: {0}", true);
            AddL("laboratory.status.sample_complete", "Sample scan complete: {0}", "Сканирование образца завершено: {0}", true);
            AddL("laboratory.status.no_inventory_slot", "No free inventory slot for this sample.", "Для этого образца нет свободной ячейки инвентаря.");
            AddL("laboratory.status.known_type_requires_scan", "{0} type is known. This sample still requires scanning.", "Тип «{0}» известен. Этот образец всё ещё нужно отсканировать.", true);
            AddL("laboratory.status.loaded_ready", "{0} loaded. Ready to scan.", "Загружено: {0}. Можно начинать сканирование.", true);
        }

        private static void AddContentEntries()
        {
            AddSimpleContent<ItemData>("item", "itemId", "displayName", "description");
            AddSimpleContent<ExpeditionLocationData>("location", "locationId", "displayName", "description", true);
            AddSimpleContent<LibraryEntryData>("library", "entryId", "title", "description");
            AddSimpleContent<ResearchDefinition>("research", "researchId", "displayName", null);
            AddSimpleContent<WeaponDefinition>("weapon", "weaponId", "displayName", null);
            AddSimpleContent<AnomalyIntegrationDefinition>("integration", "integrationId", "displayName", null);
            AddSimpleContent<IOEnemyConfig>("enemy", "enemyId", "displayName", null);
            AddSimpleContent<MapSlotData>("map_slot", "slotId", "displayName", null);
            AddStationContent();
        }

        private static void AddSimpleContent<T>(
            string category,
            string idProperty,
            string nameProperty,
            string descriptionProperty,
            bool addTargetAlias = false,
            bool preserveRussian = false) where T : Object
        {
            foreach (T asset in LoadAssets<T>())
            {
                SerializedObject serialized = new SerializedObject(asset);
                string id = ReadString(serialized.FindProperty(idProperty));
                if (string.IsNullOrWhiteSpace(id))
                    continue;
                string name = ReadString(serialized.FindProperty(nameProperty));
                string nameKey = $"{category}.{KeyPart(id)}." +
                    (category == "library" ? "title" : "name");
                AddContent(nameKey, name, preserveRussian);
                if (!string.IsNullOrEmpty(descriptionProperty))
                {
                    string description = ReadString(serialized.FindProperty(descriptionProperty));
                    AddContent(
                        $"{category}.{KeyPart(id)}.description",
                        description,
                        preserveRussian);
                }
                if (addTargetAlias)
                    AddTarget(id, name, TranslateContent(nameKey, name));
            }
        }

        private static void AddStationContent()
        {
            foreach (StationSystemsConfig config in LoadAssets<StationSystemsConfig>())
            {
                SerializedObject serialized = new SerializedObject(config);
                SerializedProperty systems = serialized.FindProperty("stationObjects");
                for (int index = 0; index < systems.arraySize; index++)
                {
                    SerializedProperty system = systems.GetArrayElementAtIndex(index);
                    SerializedProperty typeProperty = system.FindPropertyRelative("systemType");
                    string type = ((StationSystemType)typeProperty.enumValueIndex).ToString();
                    string objectId = ReadString(system.FindPropertyRelative("objectId"));
                    string id = string.IsNullOrWhiteSpace(objectId) ? "shared" : KeyPart(objectId);
                    string baseKey = $"station.{KeyPart(type)}.{id}";
                    string name = ReadString(system.FindPropertyRelative("displayName"));
                    AddContent(baseKey + ".name", name);
                    AddContent(baseKey + ".description", ReadString(system.FindPropertyRelative("description")));
                    if (!string.IsNullOrWhiteSpace(objectId))
                        AddTarget(objectId, name, TranslateContent(baseKey + ".name", name));

                    SerializedProperty stats =
                        system.FindPropertyRelative("baseStats");
                    for (int statIndex = 0;
                         stats != null && statIndex < stats.arraySize;
                         statIndex++)
                    {
                        SerializedProperty stat =
                            stats.GetArrayElementAtIndex(statIndex);
                        string statName = ReadString(
                            stat.FindPropertyRelative("displayName"));
                        if (!string.IsNullOrWhiteSpace(statName))
                            AddContent($"{baseKey}.stat.{statIndex}", statName);
                    }
                }
            }
        }

        private static void AddQuestEntries()
        {
            foreach (QuestDefinition quest in LoadAssets<QuestDefinition>())
            {
                SerializedObject serialized = new SerializedObject(quest);
                string questId = ReadString(serialized.FindProperty("questId"));
                string baseKey = "quest." + KeyPart(questId);
                AddQuest(baseKey + ".title", ReadString(serialized.FindProperty("title")));
                AddQuest(baseKey + ".description", ReadString(serialized.FindProperty("description")));
                SerializedProperty stages = serialized.FindProperty("stages");
                for (int index = 0; index < stages.arraySize; index++)
                {
                    SerializedProperty stage = stages.GetArrayElementAtIndex(index);
                    string stageKey = $"{baseKey}.stage.{index + 1:00}";
                    AddQuest(stageKey + ".title", ReadString(stage.FindPropertyRelative("title")));
                    AddQuest(stageKey + ".description", ReadString(stage.FindPropertyRelative("description")));
                }
            }
        }

        private static void AddQuest(string key, string source)
        {
            string english = QuestEnglish.TryGetValue(key, out string translated)
                ? translated
                : source;
            string russian = ContentRussian.TryGetValue(key, out string ru)
                ? ru
                : source;
            Add(NERALocalization.QuestsTable, key, english, russian);
        }

        private static void MigrateScene(string scenePath, bool addLanguageButton)
        {
            Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            if (addLanguageButton)
                EnsureLanguageButton(scene);
            foreach (GameObject root in scene.GetRootGameObjects())
                EnsureResponsiveCanvases(root);
            foreach (TMP_Text label in Object.FindObjectsByType<TMP_Text>(
                         FindObjectsInactive.Include,
                         FindObjectsSortMode.None))
            {
                if (label.gameObject.scene != scene)
                    continue;
                MigrateLabel(label, scene.name, GetPath(label.transform));
            }
            EditorSceneManager.SaveScene(scene);
        }

        private static void MigratePrefabTexts()
        {
            string[] guids = AssetDatabase.FindAssets(
                "t:Prefab",
                new[] { "Assets/_Project/NERA/Prefabs" });
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                GameObject root = PrefabUtility.LoadPrefabContents(path);
                bool changed = EnsureResponsiveCanvases(root);
                foreach (TMP_Text label in root.GetComponentsInChildren<TMP_Text>(true))
                    changed |= MigrateLabel(label, "Prefab", GetPath(label.transform));
                if (changed)
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static bool EnsureResponsiveCanvases(GameObject root)
        {
            bool changed = false;
            foreach (Canvas canvas in root.GetComponentsInChildren<Canvas>(true))
            {
                if (canvas == null || canvas.renderMode == RenderMode.WorldSpace ||
                    !canvas.isRootCanvas)
                {
                    continue;
                }

                CanvasScaler scaler = canvas.GetComponent<CanvasScaler>() ??
                    canvas.gameObject.AddComponent<CanvasScaler>();
                if (scaler.uiScaleMode !=
                        CanvasScaler.ScaleMode.ScaleWithScreenSize ||
                    scaler.referenceResolution !=
                        ResponsiveCanvasLayout.DefaultReferenceResolution ||
                    scaler.screenMatchMode !=
                        CanvasScaler.ScreenMatchMode.MatchWidthOrHeight)
                {
                    scaler.uiScaleMode =
                        CanvasScaler.ScaleMode.ScaleWithScreenSize;
                    scaler.referenceResolution =
                        ResponsiveCanvasLayout.DefaultReferenceResolution;
                    scaler.screenMatchMode =
                        CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
                    EditorUtility.SetDirty(scaler);
                    changed = true;
                }

                if (canvas.GetComponent<ResponsiveCanvasLayout>() == null)
                {
                    canvas.gameObject.AddComponent<ResponsiveCanvasLayout>();
                    changed = true;
                }
            }

            return changed;
        }

        private static bool MigrateLabel(TMP_Text label, string scope, string path)
        {
            string source = label.text?.Trim();
            if (!ShouldLocalize(label, source, path))
                return false;
            string table = PickTable(scope, path);
            string key = $"ui.{KeyPart(scope)}.{KeyPart(path)}";
            string russian = TranslateStatic(source);
            Add(table, key, source, russian);
            LocalizedTMPText localizer = label.GetComponent<LocalizedTMPText>() ??
                Undo.AddComponent<LocalizedTMPText>(label.gameObject);
            localizer.Configure(table, key, source);
            EditorUtility.SetDirty(localizer);
            return true;
        }

        private static bool ShouldLocalize(TMP_Text label, string source, string path)
        {
            if (string.IsNullOrWhiteSpace(source) ||
                !source.Any(char.IsLetter) ||
                string.Equals(source, "New Text", StringComparison.OrdinalIgnoreCase) ||
                path.IndexOf("LanguageButton", StringComparison.OrdinalIgnoreCase) >= 0)
                return false;

            string name = label.gameObject.name;
            string[] dynamicNames =
            {
                "description_update", "info_update", "info_required",
                "Text_info_LVL", "Text_nameObj", "Text_info_obj", "Text_Status",
                "Text_Move", "Text_progress", "Text_Name", "Text_Description",
                "Complete_Text", "Data_Text", "Status", "Name", "Description"
            };
            return !dynamicNames.Any(value =>
                string.Equals(name, value, StringComparison.OrdinalIgnoreCase));
        }

        private static void EnsureLanguageButton(Scene scene)
        {
            GameObject background = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<Transform>(true))
                .FirstOrDefault(item => item.name == "OptionsScreen")?
                .Find("background_Screen_station")?.gameObject;
            if (background == null || background.transform.Find("LanguageButton") != null)
                return;
            Transform source = background.transform.Find("ContinueButton");
            if (source == null)
                return;
            GameObject languageButton = Object.Instantiate(source.gameObject, background.transform);
            languageButton.name = "LanguageButton";
            RectTransform rect = languageButton.GetComponent<RectTransform>();
            if (rect != null)
                rect.anchoredPosition = Vector2.zero;
            Button button = languageButton.GetComponent<Button>();
            if (button != null)
                button.onClick = new Button.ButtonClickedEvent();
            TMP_Text label = languageButton.GetComponentInChildren<TMP_Text>(true);
            if (label != null)
                label.text = "LANGUAGE: ENGLISH";
            languageButton.AddComponent<LanguageToggleButton>();
        }

        private static void AddContent(
            string key,
            string english,
            bool preserveRussian = false)
        {
            Add(
                NERALocalization.ContentTable,
                key,
                english,
                TranslateContent(key, english),
                preserveRussian: preserveRussian);
        }

        private static string TranslateContent(string key, string english)
        {
            if (ContentRussian.TryGetValue(key, out string translated))
                return translated;
            Match expedition = Regex.Match(key, @"^location\.expedition_?(\d+)\.(name|description)$");
            if (expedition.Success)
            {
                string number = expedition.Groups[1].Value.PadLeft(2, '0');
                return expedition.Groups[2].Value == "name"
                    ? number == "01"
                        ? $"Экспедиция {number} — Древний аванпост"
                        : $"Экспедиция {number} — Сигнальная сеть"
                    : number == "01"
                        ? "Недавно обнаруженный древний объект. Найдите артефакты и вернитесь на станцию."
                        : "Неактивная сигнальная сеть NERA, окружённая скоплением Blue IO. Найдите ядро ретранслятора и уцелевшие записи.";
            }
            Match mapSlot = Regex.Match(key, @"^map_slot\.map_slot_(\d+)\.name$");
            if (mapSlot.Success)
                return "Ячейка карты " + mapSlot.Groups[1].Value;
            return TranslateStatic(english);
        }

        private static string TranslateStatic(string english)
        {
            if (string.IsNullOrEmpty(english) || ContainsCyrillic(english))
                return english;
            if (StaticRussian.TryGetValue(english.Trim(), out string translated))
                return translated;

            Match slot = Regex.Match(english.Trim(), @"^Slot (\d+) - %$");
            if (slot.Success)
                return "Ячейка " + slot.Groups[1].Value + " — %";

            string value = english;
            value = Regex.Replace(value, @"\bTURRET\b", "ТУРЕЛЬ", RegexOptions.IgnoreCase);
            value = Regex.Replace(value, @"\bLEVEL\b", "УРОВЕНЬ", RegexOptions.IgnoreCase);
            value = Regex.Replace(value, @"\bBATTERY\b", "БАТАРЕЯ", RegexOptions.IgnoreCase);
            value = Regex.Replace(value, @"\bDRONE\b", "ДРОН", RegexOptions.IgnoreCase);
            value = Regex.Replace(value, @"\bANTENNA\b", "АНТЕННА", RegexOptions.IgnoreCase);
            value = Regex.Replace(value, @"\bSOLAR PANEL\b", "СОЛНЕЧНАЯ ПАНЕЛЬ", RegexOptions.IgnoreCase);
            value = Regex.Replace(value, @"\bCOMPUTER\b", "ТЕРМИНАЛ", RegexOptions.IgnoreCase);
            value = Regex.Replace(value, @"\bLABORATORY\b", "ЛАБОРАТОРИЯ", RegexOptions.IgnoreCase);
            return value;
        }

        private static void AddTarget(string id, string english, string russian)
        {
            Add(
                NERALocalization.ContentTable,
                $"target.{KeyPart(id)}.name",
                english,
                russian);
        }

        private static void AddPrompt(string group, string english, string russian)
        {
            Add(
                NERALocalization.HudTable,
                $"interaction.{group}.{KeyPart(english)}",
                english,
                russian);
        }

        private static void AddT(string key, string english, string russian, bool smart = false)
        {
            Add(NERALocalization.TerminalTable, key, english, russian, smart);
        }

        private static void AddL(string key, string english, string russian, bool smart = false)
        {
            Add(NERALocalization.InventoryLaboratoryTable, key, english, russian, smart);
        }

        private static void Add(
            string tableName,
            string key,
            string english,
            string russian,
            bool smart = false,
            bool preserveRussian = false)
        {
            StringTableCollection collection =
                LocalizationEditorSettings.GetStringTableCollection(tableName);
            if (collection == null)
                throw new InvalidOperationException($"String table '{tableName}' is missing.");
            SetEntry(collection.GetTable("en") as StringTable, key, english, smart);
            SetEntry(
                collection.GetTable("ru") as StringTable,
                key,
                russian,
                smart,
                !preserveRussian);
        }

        private static void SetEntry(
            StringTable table,
            string key,
            string value,
            bool smart,
            bool overwrite = true)
        {
            if (table == null)
                throw new InvalidOperationException($"Locale table for '{key}' is missing.");
            StringTableEntry entry = table.GetEntry(key);
            if (entry == null)
            {
                entry = table.AddEntry(key, value ?? string.Empty);
                entry.IsSmart = smart;
            }
            else if (overwrite || string.IsNullOrWhiteSpace(entry.Value))
            {
                entry.Value = value ?? string.Empty;
                entry.IsSmart = smart;
            }
            EditorUtility.SetDirty(table);
            EditorUtility.SetDirty(table.SharedData);
        }

        private static IEnumerable<T> LoadAssets<T>() where T : Object
        {
            return AssetDatabase.FindAssets($"t:{typeof(T).Name}", new[] { "Assets/_Project/NERA" })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<T>)
                .Where(asset => asset != null);
        }

        private static string ReadString(SerializedProperty property)
        {
            return property?.stringValue?.Trim() ?? string.Empty;
        }

        private static string PickTable(string scope, string path)
        {
            if (scope == "Boot")
                return NERALocalization.MainMenuTable;
            string lower = path.ToLowerInvariant();
            if (lower.Contains("terminal") || lower.Contains("stationscreen") ||
                lower.Contains("mapscreen") || lower.Contains("storagescreen") ||
                lower.Contains("libraryscreen"))
                return NERALocalization.TerminalTable;
            if (lower.Contains("inventory") || lower.Contains("laboratory") ||
                lower.Contains("scanscreen") || lower.Contains("powerscreen"))
                return NERALocalization.InventoryLaboratoryTable;
            return NERALocalization.HudTable;
        }

        private static string GetPath(Transform transform)
        {
            List<string> parts = new List<string>();
            while (transform != null)
            {
                parts.Add(transform.name);
                transform = transform.parent;
            }
            parts.Reverse();
            return string.Join("/", parts);
        }

        private static string KeyPart(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return string.Empty;
            string normalized = Regex.Replace(value.Trim().ToLowerInvariant(), @"[^a-z0-9._]+", "_");
            return Regex.Replace(normalized, @"_+", "_").Trim('_');
        }

        private static bool ContainsCyrillic(string value)
        {
            return !string.IsNullOrEmpty(value) && Regex.IsMatch(value, "[А-Яа-яЁё]");
        }

        private static string TranslateState(string state)
        {
            switch (state)
            {
                case "Idle": return "ОЖИДАНИЕ";
                case "Scanning": return "СКАНИРОВАНИЕ";
                case "Returning": return "ВОЗВРАЩЕНИЕ";
                case "Charging": return "ЗАРЯДКА";
                case "Ready": return "ГОТОВО";
                case "Calibrating": return "КАЛИБРОВКА";
                case "SignalFound": return "СИГНАЛ НАЙДЕН";
                default: return state.ToUpperInvariant();
            }
        }

        private static void EnsureFolders()
        {
            EnsureFolder(Root);
            EnsureFolder(LocaleRoot);
            EnsureFolder(TableRoot);
            EnsureFolder(ExportRoot);
        }

        private static void EnsureFolder(string path)
        {
            string[] parts = path.Split('/');
            string current = parts[0];
            for (int index = 1; index < parts.Length; index++)
            {
                string next = current + "/" + parts[index];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[index]);
                current = next;
            }
        }
    }
}
