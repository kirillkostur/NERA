using UnityEngine;

[CreateAssetMenu(fileName = "WaveConfig", menuName = "Game/WaveConfig")]
public class WaveConfig : ScriptableObject
{
    [System.Serializable]
    public class WaveData
    {
        [Header("Основные настройки волны")]
        public int day;
        public bool useSpawnPoints = false;

        [Header("Контроль количества")]
        public int maxSpidersOnScene = 10;
        public int spawnEvents = 3;
        public float spawnInterval = 5f;
        public int minSpidersPerSpawn = 2;
        public int maxSpidersPerSpawn = 5;

        [Header("Размер и скорость пауков")]
        public float baseSpeed = 3.5f;
        public float minScale = 0.6f;
        public float maxScale = 1.3f;
        public float sizeSpeedMultiplier = 0.3f;

        [Header("Здоровье пауков")]
        public int smallSpiderHP = 20;
        public int mediumSpiderHP = 50;
        public int largeSpiderHP = 100;

        [Header("Префабы пауков")]
        public GameObject[] spiderTypes;
    }

    public WaveData[] waves;

    /// <summary>
    /// Возвращает конфигурацию волны для текущего дня.
    /// Если точного совпадения нет, берёт последнюю доступную волну, 
    /// у которой day <= currentDay.
    /// </summary>
    public WaveData GetWaveForDay(int currentDay, bool gameStarted)
    {
        if (!gameStarted || waves == null || waves.Length == 0)
            return null;

        WaveData exactMatch = null;
        WaveData fallback = null;

        foreach (var wave in waves)
        {
            // Точное совпадение
            if (wave.day == currentDay)
                exactMatch = wave;

            // Запоминаем ближайший день, который меньше или равен текущему
            if (wave.day <= currentDay)
            {
                if (fallback == null || wave.day > fallback.day)
                    fallback = wave;
            }
        }

        // Если есть точное совпадение — возвращаем его
        if (exactMatch != null)
            return exactMatch;

        // Иначе возвращаем последнюю подходящую волну
        return fallback;
    }

    public int GetHealthByScale(float scale, WaveData wave)
    {
        if (wave == null) return 20;
        if (scale < 0.8f) return wave.smallSpiderHP;
        if (scale < 1.1f) return wave.mediumSpiderHP;
        return wave.largeSpiderHP;
    }
}
