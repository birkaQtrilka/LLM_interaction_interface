using UnityEngine;

public class GrabIKTest : MonoBehaviour
{
    [SerializeField] GrabIK grab;
    [SerializeField] Transform testTarget;
    Vector3 testTargetInitPos;

    private void Awake()
    {
        grab.OnGrab.AddListener(OnGrab);
        SetTestPosition();
    }

    void OnGrab()
    {
        testTarget.SetParent( grab.hand);
        testTarget.localPosition = Vector3.zero;
        testTarget.localEulerAngles = Vector3.zero;
    }                                           

    [ContextMenu("Grab")]
    public void TestGrab()
    {
        ResetTestPosition();
        grab.TriggerGrab(testTarget);
    }

    [ContextMenu("Set test position")]
    public void SetTestPosition()
    {
        testTargetInitPos = testTarget.position;
    }

    [ContextMenu("Reset test position")]
    public void ResetTestPosition()
    {
        testTarget.SetParent(null);
        testTarget.position = testTargetInitPos;
    }
}
