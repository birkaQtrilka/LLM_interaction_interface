using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerKeyboardController : MonoBehaviour
{
    private Animator animator;

    [SerializeField] private float dampTime = 0.15f;

    private void Start()
    {
        animator = GetComponent<Animator>();
    }

    private void Update()
    {
        float h = 0f;
        float v = 0f;

        if (Keyboard.current != null)
        {
            if (Keyboard.current.aKey.isPressed ||
                Keyboard.current.leftArrowKey.isPressed)
                h = -1f;

            if (Keyboard.current.dKey.isPressed ||
                Keyboard.current.rightArrowKey.isPressed)
                h = 1f;

            if (Keyboard.current.sKey.isPressed ||
                Keyboard.current.downArrowKey.isPressed)
                v = -1f;

            if (Keyboard.current.wKey.isPressed ||
                Keyboard.current.upArrowKey.isPressed)
                v = 1f;
        }

        animator.SetFloat("Horizontal", h, dampTime, Time.deltaTime);
        animator.SetFloat("Vertical", v, dampTime, Time.deltaTime);
    }
}
