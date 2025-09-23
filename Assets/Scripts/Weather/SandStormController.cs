using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SandStormController : MonoBehaviour
{
    [Header("Настройки бури")]
    public bool autoLoop = true;
    public float stormDuration = 20f;
    public Vector2 cooldownRange = new Vector2(60f, 120f);

    [Header("Эффекты бури")]
    public List<GameObject> stormEffects = new List<GameObject>();
    [Header("Солнечные панели")]
    public List<SolarPanelSystem> panelsToDirty = new List<SolarPanelSystem>();

    [Header("Цвет и интенсивность при буре (только днём)")]
    public Gradient stormSunColor;
    public AnimationCurve stormSunIntensity;
    public float transitionTime = 2f; // Плавность переходов

    public static bool StormActive { get; private set; }
    public static SandStormController Instance;

    [HideInInspector] public float StormBlend { get; private set; } = 0f;

    private Coroutine loopRoutine;
    private Coroutine fadeOutRoutine;
    private AlertManager alerts;

    private readonly List<ParticleSystem> allStormPS = new List<ParticleSystem>();
    private readonly List<float> defaultRates = new List<float>();

    private void Awake()
    {
        Instance = this;

        foreach (var obj in stormEffects)
            if (obj != null) obj.SetActive(false);

        CacheStormParticleSystems();
    }
    private void Start()
    {
        alerts = FindFirstObjectByType<AlertManager>();
    }

    private void OnEnable()
    {
        if (autoLoop) loopRoutine = StartCoroutine(StormLoop());
    }

    private void OnDisable()
    {
        if (loopRoutine != null) StopCoroutine(loopRoutine);
        if (fadeOutRoutine != null) StopCoroutine(fadeOutRoutine);
    }

    private IEnumerator StormLoop()
    {
        while (true)
        {
            yield return new WaitForSeconds(Random.Range(cooldownRange.x, cooldownRange.y));
            StartStorm();
            yield return new WaitForSeconds(stormDuration);
            StopStorm();
        }
    }

    public void StartStorm()
    {
        if (StormActive) return;
        StormActive = true;

        GameEvents.RaiseQuestEvent("storm_started", 1);

        if (fadeOutRoutine != null)
        {
            StopCoroutine(fadeOutRoutine);
            fadeOutRoutine = null;
        }

        // Повторно кэшируем после fade-out, чтобы восстановить значения rateOverTime
        CacheStormParticleSystems();

        foreach (var obj in stormEffects)
            if (obj != null) obj.SetActive(true);

        for (int i = 0; i < allStormPS.Count; i++)
        {
            var ps = allStormPS[i];
            if (ps == null) continue;

            var emission = ps.emission;
            float baseRate = (i < defaultRates.Count && defaultRates[i] > 0f)
                ? defaultRates[i]
                : 20f; // Запасное значение, если вдруг не сохранилось

            SetRate(ref emission, baseRate);
            ps.Clear();
            ps.Play();
        }

        foreach (var panel in panelsToDirty)
            if (panel != null)
                StartCoroutine(panel.DirtyOverTime(stormDuration));

        alerts?.ShowAlert("Буря началась! Срочно укройтесь!", true);
        Logger.Log("🌪 Буря началась!");
    }

    public void StopStorm()
    {
        if (!StormActive) return;
        StormActive = false;

        GameEvents.RaiseQuestEvent("storm_ended", 1);

        if (fadeOutRoutine != null) StopCoroutine(fadeOutRoutine);
        fadeOutRoutine = StartCoroutine(FadeOutAllStormParticles());
    }

    private void Update()
    {
        StormBlend = Mathf.MoveTowards(StormBlend, StormActive ? 1f : 0f, Time.deltaTime / transitionTime);
    }

    public Color GetStormColor() => stormSunColor.Evaluate(StormBlend);
    public float GetStormIntensity() => stormSunIntensity.Evaluate(StormBlend);

    private void CacheStormParticleSystems()
    {
        allStormPS.Clear();
        defaultRates.Clear();

        foreach (var obj in stormEffects)
        {
            if (obj == null) continue;

            var pss = obj.GetComponentsInChildren<ParticleSystem>(true);
            foreach (var ps in pss)
            {
                allStormPS.Add(ps);
                var emission = ps.emission;
                defaultRates.Add(GetRate(emission));
            }
        }
    }

    private IEnumerator FadeOutAllStormParticles()
    {
        // Сохраняем текущие скорости испускания
        var startRates = new float[allStormPS.Count];
        for (int i = 0; i < allStormPS.Count; i++)
        {
            var ps = allStormPS[i];
            if (ps == null) continue;
            var emission = ps.emission;
            startRates[i] = GetRate(emission);
        }

        float elapsed = 0f;
        while (elapsed < transitionTime)
        {
            if (StormActive) yield break; // Если буря возобновилась — прерываем fade-out

            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / transitionTime);

            for (int i = 0; i < allStormPS.Count; i++)
            {
                var ps = allStormPS[i];
                if (ps == null) continue;

                var emission = ps.emission;
                float newRate = Mathf.Lerp(startRates[i], 0f, t);
                SetRate(ref emission, newRate);
            }

            yield return null;
        }

        // Останавливаем эмиссию, но не очищаем сразу — ждём затухания
        for (int i = 0; i < allStormPS.Count; i++)
        {
            var ps = allStormPS[i];
            if (ps == null) continue;
            ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }

        // Ждём, пока все частицы исчезнут
        bool particlesAlive = true;
        while (particlesAlive)
        {
            particlesAlive = false;
            foreach (var ps in allStormPS)
            {
                if (ps != null && ps.IsAlive(true))
                {
                    particlesAlive = true;
                    break;
                }
            }
            yield return null;
        }

        foreach (var obj in stormEffects)
            if (obj != null) obj.SetActive(false);

        // Пересохраняем базовые значения после fade-out
        CacheStormParticleSystems();

        fadeOutRoutine = null;
    }

    private static float GetRate(ParticleSystem.EmissionModule emission)
    {
        var curve = emission.rateOverTime;
        switch (curve.mode)
        {
            case ParticleSystemCurveMode.Constant: return curve.constant;
            case ParticleSystemCurveMode.TwoConstants: return curve.constantMax;
            case ParticleSystemCurveMode.Curve: return curve.Evaluate(0f);
            case ParticleSystemCurveMode.TwoCurves: return curve.Evaluate(0f);
            default: return 0f;
        }
    }

    private static void SetRate(ref ParticleSystem.EmissionModule emission, float value)
    {
        var curve = emission.rateOverTime;
        curve.mode = ParticleSystemCurveMode.Constant;
        curve.constant = Mathf.Max(0f, value);
        emission.rateOverTime = curve;
    }
}
