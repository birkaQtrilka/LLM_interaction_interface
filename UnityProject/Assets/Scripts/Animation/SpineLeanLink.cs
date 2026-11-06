using System;
using UnityEngine;
using UnityEngine.Animations.Rigging;

/// <summary>
/// One vertebra that takes part in the spine lean. Each link needs its own
/// MultiAimConstraint and its own aim target so the bend can accumulate along the chain.
/// </summary>
[Serializable]
public class SpineLeanLink
{
    [Tooltip("MultiAimConstraint on this vertebra. Its source object should be Aim Target below.")]
    public MultiAimConstraint constraint;
    [Tooltip("Each vertebra needs its own target (positioned relative to its own bone).")]
    public Transform aimTarget;
    [Tooltip("Share of the global lean weight this bone receives. Typical: lower bones less, top bone 1.")]
    [Range(0f, 1f)] public float weightScale = 1f;

    [NonSerialized] public Vector3 axis;
    [NonSerialized] public Vector3 neutralAim;

    public Transform Bone => constraint != null ? constraint.data.constrainedObject : null;
    public bool IsValid => constraint != null && constraint.isActiveAndEnabled
                           && aimTarget != null && Bone != null;
}