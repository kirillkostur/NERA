using UnityEngine;

public class TurretUpgradeInput : MonoBehaviour
{
    public ObjectLevelSwitch turretSwitcher;

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.R) && turretSwitcher != null)
        {
            turretSwitcher.UpgradeToNext();
            Debug.Log("Апгрейд турели: уровень " + (turretSwitcher.currentLevel + 1));
        }
    }
}
