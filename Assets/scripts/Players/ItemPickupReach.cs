using UnityEngine;
using UnityEngine.Animations.Rigging;
using System.Collections;





public class ItemPickupReach : MonoBehaviour
{
    [Header("Rig References")]
    [Tooltip("El Rig que controla el IK de la mano")]
    [SerializeField] private Rig handReachRig;

    [Tooltip("Target del Two Bone IK Constraint para la mano")]
    [SerializeField] private Transform handIKTarget;

    [Tooltip("Transform de la mano real del personaje")]
    [SerializeField] private Transform handBone;

    [Header("Animation Settings")]
    [Tooltip("Duracion de la animacion de alcanzar")]
    [SerializeField] private float reachDuration = 0.3f;

    [Tooltip("Duracion de la animacion de regresar")]
    [SerializeField] private float returnDuration = 0.25f;

    [Tooltip("Curva de animacion para suavizar el movimiento")]
    [SerializeField] private AnimationCurve reachCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

    [Tooltip("Offset desde el centro del objeto hacia donde alcanzar")]
    [SerializeField] private Vector3 grabOffset = new Vector3(0, 0.1f, 0);

    [Header("Advanced Settings")]
    [Tooltip("Distancia maxima para activar la animacion")]
    [SerializeField] private float maxReachDistance = 1.5f;

    [Tooltip("Si esta activa la animacion de alcanzar")]
    [SerializeField] private bool enableReachAnimation = true;

    private Vector3 restPosition;
    private Quaternion restRotation;
    private bool isReaching = false;
    private Coroutine currentReachCoroutine;

    private void Start()
    {
        
        if (handIKTarget != null)
        {
            restPosition = handIKTarget.localPosition;
            restRotation = handIKTarget.localRotation;
        }

        
        if (handReachRig != null)
        {
            handReachRig.weight = 0f;
        }
    }

    
    
    
    
    public void ReachForItem(GameObject targetObject)
    {
        if (!enableReachAnimation || targetObject == null || handIKTarget == null)
            return;

        float distance = Vector3.Distance(transform.position, targetObject.transform.position);
        if (distance > maxReachDistance)
            return;

        
        if (currentReachCoroutine != null)
        {
            StopCoroutine(currentReachCoroutine);
        }

        Vector3 targetPosition = targetObject.transform.position + grabOffset;
        currentReachCoroutine = StartCoroutine(ReachCoroutine(targetPosition));
    }

    
    
    
    public void ReturnToRest()
    {
        if (!enableReachAnimation || handIKTarget == null)
            return;

        if (currentReachCoroutine != null)
        {
            StopCoroutine(currentReachCoroutine);
        }

        currentReachCoroutine = StartCoroutine(ReturnCoroutine());
    }

    private IEnumerator ReachCoroutine(Vector3 worldTargetPosition)
    {
        isReaching = true;
        float elapsed = 0f;

        Vector3 startPos = handIKTarget.position;
        Quaternion startRot = handIKTarget.rotation;

        
        Vector3 directionToTarget = (worldTargetPosition - handIKTarget.position).normalized;
        Quaternion targetRotation = Quaternion.LookRotation(directionToTarget, Vector3.up);

        while (elapsed < reachDuration)
        {
            elapsed += Time.deltaTime;
            float normalizedTime = elapsed / reachDuration;
            float curveValue = reachCurve.Evaluate(normalizedTime);

            
            handIKTarget.position = Vector3.Lerp(startPos, worldTargetPosition, curveValue);
            handIKTarget.rotation = Quaternion.Slerp(startRot, targetRotation, curveValue);

            
            if (handReachRig != null)
            {
                handReachRig.weight = Mathf.Lerp(0f, 1f, curveValue);
            }

            yield return null;
        }

        
        handIKTarget.position = worldTargetPosition;
        handIKTarget.rotation = targetRotation;

        if (handReachRig != null)
        {
            handReachRig.weight = 1f;
        }

        currentReachCoroutine = null;
    }

    private IEnumerator ReturnCoroutine()
    {
        isReaching = false;
        float elapsed = 0f;

        Vector3 startPos = handIKTarget.position;
        Quaternion startRot = handIKTarget.rotation;

        
        Vector3 worldRestPosition = handIKTarget.parent != null
            ? handIKTarget.parent.TransformPoint(restPosition)
            : restPosition;

        Quaternion worldRestRotation = handIKTarget.parent != null
            ? handIKTarget.parent.rotation * restRotation
            : restRotation;

        while (elapsed < returnDuration)
        {
            elapsed += Time.deltaTime;
            float normalizedTime = elapsed / returnDuration;
            float curveValue = reachCurve.Evaluate(normalizedTime);

            
            handIKTarget.position = Vector3.Lerp(startPos, worldRestPosition, curveValue);
            handIKTarget.rotation = Quaternion.Slerp(startRot, worldRestRotation, curveValue);

            
            if (handReachRig != null)
            {
                handReachRig.weight = Mathf.Lerp(1f, 0f, curveValue);
            }

            yield return null;
        }

        
        handIKTarget.localPosition = restPosition;
        handIKTarget.localRotation = restRotation;

        if (handReachRig != null)
        {
            handReachRig.weight = 0f;
        }

        currentReachCoroutine = null;
    }

    
    
    
    public void OnItemCollected()
    {
        
        StartCoroutine(DelayedReturn());
    }

    private IEnumerator DelayedReturn()
    {
        yield return new WaitForSeconds(0.1f);
        ReturnToRest();
    }

    
    public bool IsReaching => isReaching;

    public void SetEnableReachAnimation(bool enable)
    {
        enableReachAnimation = enable;

        if (!enable && handReachRig != null)
        {
            handReachRig.weight = 0f;
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (handIKTarget != null)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(handIKTarget.position, 0.05f);

            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, maxReachDistance);
        }
    }
}