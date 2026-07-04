using UnityEngine;

[DisallowMultipleComponent]
public class PersistentCamera : MonoBehaviour
{
    public static PersistentCamera Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning($"PersistentCamera duplicate destroyed: {name}");
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        Debug.Log($"PersistentCamera initialized: {name}");
    }
}
