using System.Collections;
using UnityEngine;
using UnityEngine.Animations.Rigging;

public class GrabIK : MonoBehaviour
{
    [SerializeField] Transform test;
    [SerializeField] Transform handTarget;
    [SerializeField] Rig grabRig;
    public float grabSpeed;

    //[ContextMenu("Grab Test")]
    //public void GrabTest()
    //{
    //    StartCoroutine( Grab(test));
    //}
    //// prolly needs to return enumerator
    //public IEnumerator Grab(Transform t)
    //{
    //    Vector3 startPos = handTarget.position;
    //    handTarget.position = 
    //}
}
