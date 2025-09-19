public interface IInteractable
{
    /// <summary>Начало взаимодействия (зажата клавиша)</summary>
    void StartInteract(UnityEngine.GameObject interactor);

    /// <summary>Отмена взаимодействия (отпустили клавишу или отошли)</summary>
    void CancelInteract();

    /// <summary>Продолжение взаимодействия (держим клавишу)</summary>
    void HoldInteract();
}
