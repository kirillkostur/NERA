using UnityEngine;

[DisallowMultipleComponent]
public class PersistentUIRoot : MonoBehaviour
{
    public static PersistentUIRoot Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning($"PersistentUIRoot duplicate destroyed: {name}");
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }
}