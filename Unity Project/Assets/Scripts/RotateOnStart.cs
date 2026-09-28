using UnityEngine;

public class RotateOnStart : MonoBehaviour
{
    [SerializeField] Quaternion rotation;

    private void Start()
    {
        transform.rotation = rotation;
    }
}
