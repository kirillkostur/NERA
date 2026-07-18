using UnityEngine;

namespace NERA.Interaction
{
    public interface IInteractable
    {
        Transform InteractionTransform { get; }
        InteractionPrompt GetPrompt();
        void BeginInteraction(GameObject interactor);
        void CancelInteraction(GameObject interactor);
        void CompleteInteraction(GameObject interactor);
    }
}
