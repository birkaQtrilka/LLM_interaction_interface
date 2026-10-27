using System;
using UnityEngine;

public class GrabReceiver : MonoBehaviour
{
    public event Action OnGrabPoint;

    public void GrabPoint() => OnGrabPoint?.Invoke();


    public event Action<string> EventRelay;
    public void RelayEvent(string eventName) => EventRelay?.Invoke(eventName);
}
