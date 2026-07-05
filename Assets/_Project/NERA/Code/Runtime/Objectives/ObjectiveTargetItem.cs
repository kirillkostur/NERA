using UnityEngine;

[DisallowMultipleComponent]
public class ObjectiveTargetItem : MonoBehaviour
{
    [Header("Objective")]
    [SerializeField] private string objectiveId = "expedition_01_find_object";

    private bool completed;

    public void CompleteObjective()
    {
        if (completed)
            return;

        ExpeditionObjectiveController controller = ExpeditionObjectiveController.Instance;

        if (controller == null)
        {
            Debug.LogWarning($"{name}: ExpeditionObjectiveController not found.");
            return;
        }

        if (controller.ObjectiveId != objectiveId)
        {
            Debug.LogWarning(
                $"{name}: Objective id mismatch. " +
                $"Target='{objectiveId}', Controller='{controller.ObjectiveId}'"
            );
            return;
        }

        completed = true;
        controller.CompleteObjective();
    }
}