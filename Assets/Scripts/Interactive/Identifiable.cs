using UnityEngine;

/// <summary>
/// Простой компонент для присвоения стабильного ID любому сценному объекту.
/// Не зависит от других систем.
/// </summary>
public class Identifiable : MonoBehaviour
{
    [Tooltip("Уникальный ID, который будут видеть квесты/события (например: station_battery, turret_1, solar_panel_A)")]
    public string uniqueID = "object_id";

    public string Id => uniqueID;
}
