using System.Collections;
using UnityEngine;
using UnityEngine.Animations.Rigging;

public class LookAtIK : MonoBehaviour
{
    [SerializeField] private Rig aimConstraint;
    [SerializeField] private AnimationCurve lookCurve;
    [SerializeField] private float turnDuration = 0.2f;

    // Optional: assign a pre-existing target in the prefab/inspector.
    // If left empty, one will be created dynamically at runtime.
    [SerializeField] private Transform floater;

    private Coroutine activeLookCoroutine;

    private void Awake()
    {
        EnsureFloaterExists();
    }

    private void EnsureFloaterExists()
    {
        if (floater != null) return;

        // Create the floater at runtime
        string floaterName = $"LookAtFloater_{gameObject.name}";
        var go = new GameObject(floaterName);
        floater = go.transform;

        // Position it in front of this object by default
        floater.position = transform.position + transform.forward * 2f;

        // Register to the MultiAimConstraints at runtime
        if (aimConstraint != null)
        {
            var aims = aimConstraint.GetComponentsInChildren<MultiAimConstraint>();
            foreach (var aim in aims)
            {
                var sources = aim.data.sourceObjects;

                // Check if already registered
                bool alreadyPresent = false;
                for (int i = 0; i < sources.Count; i++)
                {
                    if (sources.GetTransform(i) == floater)
                    {
                        alreadyPresent = true;
                        break;
                    }
                }

                if (!alreadyPresent)
                {
                    sources.Add(new WeightedTransform(floater, 1f));
                    aim.data.sourceObjects = sources;
                }
            }

            // Rebuild the rig graph so runtime changes take effect
            var rigBuilder = GetComponentInParent<RigBuilder>();
            if (rigBuilder != null)
            {
                rigBuilder.Build();
            }
        }
    }

    private void OnDestroy()
    {
        // Clean up the created GameObject when this character is destroyed
        if (floater != null && floater.gameObject != null)
        {
            Destroy(floater.gameObject);
        }
    }

    public void StartLookAt(Vector3 pos)
    {
        if (activeLookCoroutine != null) StopCoroutine(activeLookCoroutine);
        activeLookCoroutine = StartCoroutine(LookAtRoutine(pos));
    }

    public void StartStopLooking()
    {
        if (activeLookCoroutine != null) StopCoroutine(activeLookCoroutine);
        activeLookCoroutine = StartCoroutine(StopLookingRoutine());
    }

    public IEnumerator LookAtRoutine(Vector3 pos)
    {
        if (floater == null) EnsureFloaterExists();
        floater.position = pos;

        float elapsed = 0f;
        while (elapsed < turnDuration)
        {
            elapsed += Time.deltaTime;
            float t = lookCurve.Evaluate(Mathf.Clamp01(elapsed / turnDuration));

            if (aimConstraint != null)
                aimConstraint.weight = t;

            yield return null;
        }

        if (aimConstraint != null) aimConstraint.weight = 1f;
    }

    public IEnumerator StopLookingRoutine()
    {
        float elapsed = 0f;
        float startWeight = aimConstraint != null ? aimConstraint.weight : 1f;

        while (elapsed < turnDuration)
        {
            elapsed += Time.deltaTime;
            float t = lookCurve.Evaluate(Mathf.Clamp01(elapsed / turnDuration));

            if (aimConstraint != null)
                aimConstraint.weight = Mathf.Lerp(startWeight, 0f, t);

            yield return null;
        }

        if (aimConstraint != null) aimConstraint.weight = 0f;
    }
}