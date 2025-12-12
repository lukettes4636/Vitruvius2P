using UnityEngine;
using UnityEngine.Animations.Rigging;
using System.Collections;

public class ObjectCarryingSystem : MonoBehaviour
{
    [Header("Rigging Setup")]
    public Rig carryingRig;
    public Transform leftHandTarget;
    public Transform rightHandTarget;

    [Header("Settings")]
    public float transitionSpeed = 8f;

    [Header("Throw Animation Settings")]
    [Tooltip("Distancia hacia adelante desde el CUERPO del personaje")]
    public float throwForwardOffset = 1.2f;

    [Tooltip("Altura relativa al punto de origen de las manos")]
    public float throwUpwardOffset = 0.0f; 

    [Tooltip("Velocidad del empuje")]
    public float throwAnimationSpeed = 15f;

    
    private Transform _gripL;
    private Transform _gripR;

    private bool _isCarrying = false;
    private bool _isThrowing = false;
    private float _targetWeight = 0f;

    
    private Vector3 _throwTargetPosL;
    private Vector3 _throwTargetPosR;
    private Quaternion _throwTargetRotL;
    private Quaternion _throwTargetRotR;

    private void Start()
    {
        if (carryingRig != null) carryingRig.weight = 0f;
    }

    private void LateUpdate()
    {
        if (carryingRig == null) return;

        
        carryingRig.weight = Mathf.Lerp(carryingRig.weight, _targetWeight, Time.deltaTime * transitionSpeed);

        if (_isThrowing)
        {
            
            leftHandTarget.position = Vector3.Lerp(leftHandTarget.position, _throwTargetPosL, Time.deltaTime * throwAnimationSpeed);
            rightHandTarget.position = Vector3.Lerp(rightHandTarget.position, _throwTargetPosR, Time.deltaTime * throwAnimationSpeed);

            leftHandTarget.rotation = Quaternion.Lerp(leftHandTarget.rotation, _throwTargetRotL, Time.deltaTime * throwAnimationSpeed);
            rightHandTarget.rotation = Quaternion.Lerp(rightHandTarget.rotation, _throwTargetRotR, Time.deltaTime * throwAnimationSpeed);
        }
        else if (_isCarrying && _gripL != null && _gripR != null)
        {
            
            leftHandTarget.position = _gripL.position;
            leftHandTarget.rotation = _gripL.rotation;

            rightHandTarget.position = _gripR.position;
            rightHandTarget.rotation = _gripR.rotation;
        }
    }

    public void StartCarrying(GameObject heldObject)
    {
        Transform foundGripL = heldObject.transform.Find("Grip_L");
        Transform foundGripR = heldObject.transform.Find("Grip_R");

        if (foundGripL != null && foundGripR != null)
        {
            _gripL = foundGripL;
            _gripR = foundGripR;
            _isCarrying = true;
            _isThrowing = false;
            _targetWeight = 1f;
        }
        else
        {
            _isCarrying = false;
            _targetWeight = 0f;
        }
    }

    public void StopCarrying()
    {
        if (!_isThrowing)
        {
            _isCarrying = false;
            _targetWeight = 0f;
            _gripL = null;
            _gripR = null;
        }
    }

    public void PlayThrowAnimation()
    {
        _isThrowing = true;
        _targetWeight = 1f;

        
        
        Vector3 forwardDir = transform.forward;
        Vector3 upDir = transform.up;

        
        Vector3 displacement = (forwardDir * throwForwardOffset) + (upDir * throwUpwardOffset);

        
        
        
        _throwTargetPosL = leftHandTarget.position + displacement;
        _throwTargetPosR = rightHandTarget.position + displacement;

        
        _throwTargetRotL = leftHandTarget.rotation;
        _throwTargetRotR = rightHandTarget.rotation;

        StartCoroutine(StopThrowingRoutine());
    }

    private IEnumerator StopThrowingRoutine()
    {
        yield return new WaitForSeconds(0.4f);

        _isThrowing = false;
        _isCarrying = false;
        _targetWeight = 0f;
        _gripL = null;
        _gripR = null;
    }
}