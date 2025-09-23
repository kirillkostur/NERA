using UnityEngine;
using UnityEngine.UI;

public class DragItem : MonoBehaviour
{
    public static DragItem Instance { get; private set; }

    [Header("UI")]
    public Canvas canvas;      // Canvas HUD
    public Image dragIcon;     // Летающая иконка предмета

    public bool IsDragging { get; private set; }
    public SlotUI Origin { get; private set; }

    private void Awake()
    {
        Instance = this;
        if (dragIcon != null) dragIcon.enabled = false;
    }

    public void StartDrag(SlotUI origin, Sprite sprite)
    {
        Origin = origin;
        dragIcon.sprite = sprite;
        dragIcon.enabled = true;
        IsDragging = true;
        FollowCursor();
    }

    public void FollowCursor()
    {
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            (RectTransform)canvas.transform,
            Input.mousePosition,
            canvas.worldCamera,
            out var p);
        dragIcon.rectTransform.anchoredPosition = p;
    }

    public void EndDrag()
    {
        if (dragIcon != null) dragIcon.enabled = false;
        IsDragging = false;
        Origin = null;
    }
}
