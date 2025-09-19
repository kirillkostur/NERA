using UnityEngine;

[RequireComponent(typeof(SandStormController))]
public class SandStormWeatherAdapter : MonoBehaviour, IWeatherEffect
{
    private SandStormController storm;

    private void Awake()
    {
        storm = GetComponent<SandStormController>();
    }

    public void StartEffect()
    {
        storm.StartStorm();
    }

    public void StopEffect()
    {
        storm.StopStorm();
    }

    public void UpdateEffect()
    {
        // Пока ничего не делаем каждый кадр
    }
}
