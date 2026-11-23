using System.Collections;
using UnityEngine;
using UnityEngine.Animations.Rigging;

public class LookAtIK : MonoBehaviour
{
    [SerializeField] private Rig aimConstraint;
    [SerializeField] private float targetSmoothTime = 0.1f;
    private Vector3 floaterVelocity;
    private Coroutine weightRoutine;
    private float weightGoal = -1f;
    Transform floater;


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

    /// Call every frame. Snaps while the look isn't engaged, smooths otherwise.
    public void SetTarget(Vector3 pos)
    {
        EnsureFloaterExists();
        bool snap = aimConstraint == null || aimConstraint.weight < 0.05f;
        if (snap) { floater.position = pos; floaterVelocity = Vector3.zero; }
        else floater.position = Vector3.SmoothDamp(floater.position, pos, ref floaterVelocity, targetSmoothTime);
    }

    public void Engage(float duration, AnimationCurve curve) => BlendTo(1f, duration, curve);
    public void Disengage(float duration, AnimationCurve curve) => BlendTo(0f, duration, curve);

    private void LateUpdate()
    {
        if (aimConstraint != null && weightRoutine == null && weightGoal >= 0f)
            aimConstraint.weight = weightGoal;
    }

    private void BlendTo(float goal, float duration, AnimationCurve curve)
    {
        bool atGoal = aimConstraint == null || Mathf.Approximately(aimConstraint.weight, goal);
        if (Mathf.Approximately(weightGoal, goal) && atGoal) return;

        weightGoal = goal;
        if (weightRoutine != null) StopCoroutine(weightRoutine);
        weightRoutine = StartCoroutine(BlendRoutine(goal, Mathf.Max(0.01f, duration), curve));
    }

    private IEnumerator BlendRoutine(float goal, float duration, AnimationCurve curve)
    {
        if (aimConstraint == null) yield break;
        float start = aimConstraint.weight, elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float k = Mathf.Clamp01(elapsed / duration);
            float t = curve != null ? curve.Evaluate(k) : k;
            aimConstraint.weight = Mathf.Lerp(start, goal, t);
            yield return null;
        }
        aimConstraint.weight = goal;
        weightRoutine = null;
    }
}