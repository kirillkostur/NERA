using UnityEngine;

public class LimitMinimapCamera : MonoBehaviour
{
    public GameObject _player;

    private void LateUpdate()
    {
        transform.position = new Vector3(_player.transform.position.x, 40, _player.transform.position.z);
    }
}
