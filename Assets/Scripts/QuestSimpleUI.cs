using UnityEngine;
using TMPro;

public class QuestSimpleUI : MonoBehaviour
{
    [SerializeField] private QuestManager questManager;
    [SerializeField] private TextMeshProUGUI text;

    private void OnEnable()
    {
        QuestManager.OnQuestsUpdated += Refresh;
        Refresh();
    }

    private void OnDisable()
    {
        QuestManager.OnQuestsUpdated -= Refresh;
    }

    private void Refresh()
    {
        if (questManager == null) return;

        text.text = "";

        foreach (var quest in questManager.GetActiveQuests())
        {
            text.text += quest.Asset.Title + "\n";

            foreach (var obj in quest.Objectives)
            {
                text.text += $"- {obj.DisplayText}: {obj.CurrentValue}/{obj.TargetValue}\n";
            }
        }
    }
}
