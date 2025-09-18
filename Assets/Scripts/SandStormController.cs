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
    public Gradient stormSunColor;          // Градиент цвета для шторма
    public AnimationCurve stormSunIntensity;// Кривая интенсивности для шторма
    public float transitionTime = 2f;       // Плавность перехода (сек)

    public static bool StormActive { get; private set; } = false;
    public static SandStormController Instance;

    [HideInInspector] public float StormBlend { get; private set; } = 0f;

    private Coroutine loopRoutine;
    private float stormElapsed = 0f;
    private float stormProgress = 0f;

    private void Awake()
    {
        Instance = this;

        foreach (var obj in stormEffects)
            if (obj != null) obj.SetActive(false);
    }

    private void OnEnable()
    {
        if (autoLoop) loopRoutine = StartCoroutine(StormLoop());
    }

    private void OnDisable()
    {
        if (loopRoutine != null) StopCoroutine(loopRoutine);
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

        foreach (var obj in stormEffects)
        {
            if (obj != null)
            {
                obj.SetActive(true);
                var fx = obj.GetComponent<ParticleSystem>();
                if (fx != null)
                {
                    fx.Clear();
                    fx.Play();
                }
            }
        }

        foreach (var panel in panelsToDirty)
            if (panel != null)
                StartCoroutine(panel.DirtyOverTime(stormDuration));

        Logger.Log("🌪 Буря началась!");
    }

    public void StopStorm()
    {
        if (!StormActive) return;
        StormActive = false;

        foreach (var obj in stormEffects)
        {
            if (obj != null)
            {
                var fx = obj.GetComponent<ParticleSystem>();
                if (fx != null)
                {
                    fx.Stop();
                    fx.Clear();
                }
                obj.SetActive(false);
            }
        }

        Logger.Log("🌤 Буря закончилась.");
    }

    private void Update()
    {
        if (StormActive)
        {
            stormElapsed += Time.deltaTime;
            stormProgress = Mathf.Clamp01(stormElapsed / stormDuration);
            StormBlend = Mathf.MoveTowards(StormBlend, 1f, Time.deltaTime / transitionTime);
        }
        else
        {
            stormElapsed = 0f;
            stormProgress = 0f;
            StormBlend = Mathf.MoveTowards(StormBlend, 0f, Time.deltaTime / transitionTime);
        }
    }

    public Color GetStormColor()
    {
        return stormSunColor.Evaluate(stormProgress);
    }

    public float GetStormIntensity()
    {
        return stormSunIntensity.Evaluate(stormProgress);
    }
}
