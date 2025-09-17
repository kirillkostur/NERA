using System.Collections;
using UnityEngine;

public class SandStormController : MonoBehaviour
{
    [Header("Настройки бури")]
    public bool autoLoop = true;
    public float stormDuration = 20f;
    public Vector2 cooldownRange = new Vector2(60f, 120f);

    [Header("Эффекты")]
    public ParticleSystem stormFx;

    [Header("Цели (панели)")]
    public SolarPanelSystem[] panelsToBreak;

    private Coroutine loopRoutine;

    private void OnEnable()
    {
        if (autoLoop) loopRoutine = StartCoroutine(Loop());
    }

    private void OnDisable()
    {
        if (loopRoutine != null) StopCoroutine(loopRoutine);
    }

    public void StartStorm()
    {
        if (stormFx != null) stormFx.Play();

        // Ломаем все панели. Игрок должен их "почистить"/починить.
        foreach (var p in panelsToBreak)
        {
            var r = p != null ? p.GetComponent<RepairableObject>() : null;
            if (r != null) r.BreakObject();
        }

        Debug.Log("🌪 Буря началась! Панели повреждены.");
        Invoke(nameof(StopStorm), stormDuration);
    }

    public void StopStorm()
    {
        if (stormFx != null) stormFx.Stop();
        Debug.Log("🌤 Буря закончилась.");
    }

    private IEnumerator Loop()
    {
        while (true)
        {
            yield return new WaitForSeconds(Random.Range(cooldownRange.x, cooldownRange.y));
            StartStorm();
            yield return new WaitForSeconds(stormDuration);
            StopStorm();
        }
    }
}
