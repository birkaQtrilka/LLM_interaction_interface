using UnityEngine;

public static class PoseUtil
{
    public static Matrix4x4 Pose(Transform t) => Matrix4x4.TRS(t.position, t.rotation, Vector3.one);
    public static Matrix4x4 Matrix(Pos pos) => Matrix4x4.TRS(pos.p, pos.r, Vector3.one);
    public static Matrix4x4 Local(Vector3 pos, Vector3 euler) => Matrix4x4.TRS(pos, Quaternion.Euler(euler), Vector3.one);
    public static void ApplyTo(this Matrix4x4 m, Transform t) => t.SetPositionAndRotation(m.GetPosition(), m.rotation);
}