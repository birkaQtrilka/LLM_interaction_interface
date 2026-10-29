
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;

public class RaycastHitEvents : MonoBehaviour
{
    [Header("Raycast Settings")]
    [SerializeField] private float rayDistance = 100f;
    [SerializeField] private LayerMask layerMask = ~0;

    [Header("Events")]
    public TransformEvent onHitEveryFrame;
    public TransformEvent onSpacePressed;

    [Header("Debug")]
    [SerializeField] private bool drawRay = true;

    // The empty GameObject placed at the hit position.
    [field: SerializeField] public GameObject HitPointObject { get; private set; }

    // Custom event type that passes a Transform.
    [System.Serializable]
    public class TransformEvent : UnityEvent<Transform> { }

    private void Awake()
    {
        if(HitPointObject == null) HitPointObject = new GameObject("Raycast Hit Point");
        HitPointObject.SetActive(false);
    }

    private void Update()
    {
        Ray ray = new Ray(transform.position, transform.forward);

        bool hasHit = Physics.Raycast(
            ray,
            out RaycastHit hit,
            rayDistance,
            layerMask
        );

        if (drawRay)
        {
            Debug.DrawRay(
                transform.position,
                transform.forward * (hasHit ? hit.distance : rayDistance),
                hasHit ? Color.green : Color.red
            );
        }

        if (hasHit)
        {
            // Move the empty object to the hit position.
            HitPointObject.SetActive(true);
            HitPointObject.transform.position = hit.point;
            HitPointObject.transform.rotation = Quaternion.LookRotation(hit.normal);

            // Fire every frame while the ray hits something.
            onHitEveryFrame?.Invoke(HitPointObject.transform);

            // Fire only on the frame Space is pressed.
            if (Keyboard.current != null &&
                Keyboard.current.spaceKey.wasPressedThisFrame)
            {
                onSpacePressed?.Invoke(HitPointObject.transform);
            }
        }
        else
        {
            HitPointObject.SetActive(false);
        }
    }

    private void OnDestroy()
    {
        if (HitPointObject != null)
        {
            Destroy(HitPointObject);
        }
    }
}
