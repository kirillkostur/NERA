using UnityEngine;

[DisallowMultipleComponent]
public class NeraContentProvider : MonoBehaviour
{
    public static NeraContentProvider Instance { get; private set; }

    [Header("Database")]
    [SerializeField] private NeraContentDatabase contentDatabase;

    public NeraContentDatabase ContentDatabase => contentDatabase;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning($"NeraContentProvider duplicate destroyed: {name}");
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        if (contentDatabase == null)
            Debug.LogWarning("NeraContentProvider: Content database is not assigned.");
    }
}