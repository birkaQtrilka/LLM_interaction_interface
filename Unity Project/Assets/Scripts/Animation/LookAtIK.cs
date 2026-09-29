using System.Collections;
using UnityEngine;
using UnityEngine.Animations.Rigging;
using UnityEngine.XR;

public class LookAtIK : MonoBehaviour
{
    [SerializeField] private Rig aimConstraint;
    [SerializeField] private AnimationCurve lookCurve;
    [SerializeField] private Transform floater;
    public float turnDuration = .2f;

    public IEnumerator LookAt(Vector3 pos)
    {
        floater.position = pos;

        float elapsed = 0f;
        while (elapsed < turnDuration)
        {
            elapsed += Time.deltaTime;
            float rawT = Mathf.Clamp01(elapsed / turnDuration);
            float curvedT = lookCurve.Evaluate(rawT);

            if (aimConstraint != null)
            {
                aimConstraint.weight = curvedT;
            }

            yield return null;
        }
    }

    public IEnumerator StopLooking()
    {

        float elapsed = 0f;
        while (elapsed < turnDuration)
        {
            elapsed += Time.deltaTime;
            float rawT = Mathf.Clamp01(elapsed / turnDuration);
            float curvedT = lookCurve.Evaluate(rawT);
            curvedT = 1- curvedT;
            if (aimConstraint != null)
            {
                aimConstraint.weight = curvedT;
            }

            yield return null;
        }
    }
}
