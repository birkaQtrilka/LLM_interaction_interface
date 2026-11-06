using System;
using UnityEngine;
using UnityEngine.Animations.Rigging;

[Serializable]
public class GrabIKCrouchSettings
{
    public Rig rig;
    [Tooltip("OverrideTransform on the hips bone. Space MUST be set to Pivot in the inspector.")]
    public OverrideTransform hipsOverride;
    public Transform leftFootTarget, rightFootTarget;
    public Transform leftFoot, rightFoot;
    public Transform leftKneeHint, rightKneeHint;
    [Tooltip("Non-animated transform the hints are kept relative to. Defaults to root.")]
    public Transform hintAnchor;
    public float reachMargin = 0f;
    public float maxDepth = 0.6f;
    public float minDepth = 0.05f;
}
