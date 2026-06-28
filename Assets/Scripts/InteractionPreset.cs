using UnityEngine;

[CreateAssetMenu(
    fileName = "IP_NewInteraction",
    menuName = "NERA/Interaction/Interaction Preset"
)]
public class InteractionPreset : ScriptableObject
{
    [Header("UI")]
    [SerializeField] private string prompt = "Interact";

    [Header("Animation")]
    [SerializeField] private InteractionAnimationType animationType = InteractionAnimationType.Use;

    [Header("Timing")]
    [SerializeField] private float duration = 0f;
    [SerializeField] private bool canBeInterrupted = true;

    [Header("Behaviour")]
    [SerializeField] private bool faceTarget = true;

    public string Prompt => prompt;
    public InteractionAnimationType AnimationType => animationType;
    public float Duration => Mathf.Max(0f, duration);
    public bool CanBeInterrupted => canBeInterrupted;
    public bool FaceTarget => faceTarget;
}
