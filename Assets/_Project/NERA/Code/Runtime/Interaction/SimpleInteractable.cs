using UnityEngine;
using UnityEngine.Events;

public class SimpleInteractable : BaseInteractable
{
    [Header("Test")]
    [SerializeField] private string message = "Test interaction completed.";

    protected override void OnInteractCompleted()
    {
        Debug.Log($"{name}: {message}");
    }
}