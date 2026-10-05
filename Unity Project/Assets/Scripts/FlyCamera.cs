using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

public class FlyCamera : MonoBehaviour
{
    [SerializeField] float moveSpeed = 8f;
    [SerializeField] float lookSensitivity = 0.15f;
    // Seconds of glide after the move keys are released. 0 stops at once
    [SerializeField] float moveMomentum = 0.12f;

    float yaw;
    float pitch;
    bool looking;
    Vector3 velocity;

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

        if (!looking)
        {
            velocity = Vector3.zero;
            return;
        }

        Vector2 delta = mouse.delta.ReadValue();
        yaw += delta.x * lookSensitivity;
        pitch -= delta.y * lookSensitivity;
        pitch = Mathf.Clamp(pitch, -89f, 89f);
        transform.rotation = Quaternion.Euler(pitch, yaw, 0f);

        Vector3 wish = Vector3.zero;
        if (keyboard.wKey.isPressed) wish += Vector3.forward;
        if (keyboard.sKey.isPressed) wish += Vector3.back;
        if (keyboard.aKey.isPressed) wish += Vector3.left;
        if (keyboard.dKey.isPressed) wish += Vector3.right;
        if (wish.sqrMagnitude > 1f) wish.Normalize();
        wish *= moveSpeed;

        float blend = moveMomentum <= 0f ? 1f : 1f - Mathf.Exp(-Time.deltaTime / moveMomentum);
        velocity = Vector3.Lerp(velocity, wish, blend);
        if (velocity.sqrMagnitude < 0.0001f) return;

        transform.Translate(velocity * Time.deltaTime, Space.Self);
    }

    static bool PointerOverUi()
    {
        EventSystem eventSystem = EventSystem.current;
        return eventSystem != null && eventSystem.IsPointerOverGameObject();
    }
}
