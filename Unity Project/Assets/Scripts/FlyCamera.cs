using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

public class FlyCamera : MonoBehaviour
{
    [SerializeField] float moveSpeed = 8f;
    [SerializeField] float lookSensitivity = 0.15f;

    float yaw;
    float pitch;
    bool looking;

    void Start()
    {
        Vector3 euler = transform.eulerAngles;
        yaw = euler.y;
        pitch = euler.x;
        if (pitch > 180f) pitch -= 360f;
    }

    void Update()
    {
        Mouse mouse = Mouse.current;
        Keyboard keyboard = Keyboard.current;
        if (mouse == null || keyboard == null) return;

        if (mouse.leftButton.wasPressedThisFrame)
        {
            looking = !PointerOverUi();
        }
        if (mouse.leftButton.wasReleasedThisFrame)
        {
            looking = false;
        }

        if (!looking) return;

        Vector2 delta = mouse.delta.ReadValue();
        yaw += delta.x * lookSensitivity;
        pitch -= delta.y * lookSensitivity;
        pitch = Mathf.Clamp(pitch, -89f, 89f);
        transform.rotation = Quaternion.Euler(pitch, yaw, 0f);

        Vector3 move = Vector3.zero;
        if (keyboard.wKey.isPressed) move += Vector3.forward;
        if (keyboard.sKey.isPressed) move += Vector3.back;
        if (keyboard.aKey.isPressed) move += Vector3.left;
        if (keyboard.dKey.isPressed) move += Vector3.right;
        if (move.sqrMagnitude == 0f) return;

        transform.Translate(move.normalized * moveSpeed * Time.deltaTime, Space.Self);
    }

    static bool PointerOverUi()
    {
        EventSystem eventSystem = EventSystem.current;
        return eventSystem != null && eventSystem.IsPointerOverGameObject();
    }
}
