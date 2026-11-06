using UnityEngine;

public struct Pos
{
    public Vector3 p;
    public Quaternion r;
    public Pos(Vector3 pos, Quaternion rot)
    {
        p = pos;
        r = rot;
    }

    public Pos(Transform t)
    {
        p = t.position;
        r = t.rotation;
    }
}
