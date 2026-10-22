using System.Collections;
using UnityEngine;
using UnityEngine.Animations.Rigging;

public class LookAtIK : MonoBehaviour
{
    [SerializeField] private Rig aimConstraint;
    [SerializeField] private AnimationCurve lookCurve;
    [Tooltip("Create an empty transform in the world then assign it in the aim cosntraints of head")]
    [SerializeField, HideInInspector] Transform floater;
    public float turnDuration = .2f;
    private const string FloaterName = "LookAtFloater";

#if UNITY_EDITOR
    private void OnValidate()
    {
        // Resolve/create floater.
        if (floater == null)
        {
            var existing = GameObject.Find(FloaterName);

            if (existing != null)
            {
                floater = existing.transform;
            }
            else
            {
                var go = new GameObject(FloaterName);
                floater = go.transform;
            }
        }

        // Find all aim constraints.
        var aims = aimConstraint.GetComponentsInChildren<MultiAimConstraint>();

        foreach (var aim in aims)
        {
            var sources = aim.data.sourceObjects;

            bool hasFloater = false;
            int nullIndex = -1;

            for (int i = 0; i < sources.Count; i++)
            {
                var source = sources.GetTransform(i);

                if (source == floater)
                {
                    hasFloater = true;
                    break;
                }

                if (source == null && nullIndex == -1)
                {
                    nullIndex = i;
                }
            }

            // Already present, nothing to do.
            if (hasFloater)
                continue;

            // Prefer filling an existing null slot.
            if (nullIndex >= 0)
            {
                sources.SetTransform(nullIndex, floater);
                sources.SetWeight(nullIndex, 1f);
            }
            // Otherwise add a new source.
            else if (sources.Count < WeightedTransformArray.k_MaxLength)
            {
                sources.Add(new WeightedTransform(floater, 1f));
            }
            else
            {
                Debug.LogWarning(
                    $"Cannot add {FloaterName} to {aim.name}: source array is full.",
                    aim
                );

                continue;
            }

            aim.data.sourceObjects = sources;
        }
    }
#endif

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
