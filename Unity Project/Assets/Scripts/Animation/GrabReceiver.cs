using System;
using UnityEngine;

public class GrabReceiver : MonoBehaviour
{
    public event Action OnGrabPoint;

    public void GrabPoint() => OnGrabPoint?.Invoke();
}
