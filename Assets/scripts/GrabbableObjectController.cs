using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;
using System.Linq;

namespace Gameplay
{
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(Collider))]
    [RequireComponent(typeof(AudioSource))]
    public class GrabbableObjectController : MonoBehaviour
    {
        [Header("Input Settings")]
        [Tooltip("Action name for picking up/dropping the object")]
        public string ActionName = "Interact";

        [Tooltip("Input action reference for throwing (assign in Inspector)")]
        public InputActionReference ThrowActionReference;

        [Header("Interaction Settings")]
        [Tooltip("Maximum distance from player to allow pickup")]
        public float GrabRadius = 2.0f;

        [Header("Posicion y Rotacion (De Pie)")]
        [Tooltip("Posicion local cuando el jugador esta DE PIE")]
        public Vector3 HoldOffset = new Vector3(0.5f, 0.8f, 0.7f);

        [Tooltip("Rotacion local cuando el jugador esta DE PIE")]
        public Vector3 HoldRotation = Vector3.zero;

        [Header("Posicion y Rotacion (Agachado)")]
        [Tooltip("Posicion local cuando el jugador esta AGACHADO")]
        public Vector3 CrouchHoldOffset = new Vector3(0.5f, 0.45f, 0.7f);

        [Tooltip("Rotacion local cuando el jugador esta AGACHADO (Ej: inclinarlo un poco)")]
        public Vector3 CrouchHoldRotation = new Vector3(15f, 0f, 0f); 

        [Header("Suavizado")]
        [Tooltip("Velocidad de ajuste de posicion/rotacion (Agacharse/Levantarse)")]
        public float positionSmoothSpeed = 10f;

        [Tooltip("Layer mask for detecting players")]
        public LayerMask PlayerLayer = -1;

        [Header("Throw Settings")]
        [Tooltip("Horizontal force applied when throwing")]
        public float ThrowForce = 12f;

        [Tooltip("Upward force for arc trajectory")]
        public float ThrowUpwardForce = 3f;

        [Tooltip("Delay before releasing physics to sync with animation")]
        public float ThrowAnimationDelay = 0.15f;

        [Tooltip("How far forward the object moves visually before release")]
        public float VisualThrowDistance = 1.2f;

        [Header("Audio Settings")]
        [Tooltip("Sound played when object hits something")]
        public AudioClip CollisionSound;

        [Tooltip("Volume of the collision sound")]
        [Range(0f, 1f)]
        public float CollisionVolume = 1f;

        [Tooltip("Max distance at which the collision sound is audible")]
        public float NoiseRange = 15f;

        [Tooltip("Minimum impact velocity requried to play sound")]
        public float MinCollisionForce = 2f;

        [Header("Debug")]
        public bool VerboseLogging = false;

        protected Rigidbody rb;
        protected Collider col;
        protected AudioSource audioSource;
        protected Transform originalParent;
        protected Transform currentHolder;
        protected bool isHeld = false;
        protected bool isThrowingInProgress = false;

        protected Collider currentHolderCollider;
        protected Animator holderAnimator;

        public bool IsHeld => isHeld;
        public Transform CurrentHolder => currentHolder;

        protected virtual void Awake()
        {
            rb = GetComponent<Rigidbody>();
            col = GetComponent<Collider>();
            audioSource = GetComponent<AudioSource>();
            originalParent = transform.parent;

            SetupAudioSource();

            if (ThrowActionReference != null)
            {
                ThrowActionReference.action.Enable();
            }
        }

        protected void SetupAudioSource()
        {
            if (audioSource != null)
            {
                audioSource.playOnAwake = false;
                audioSource.spatialBlend = 0.2f;
                audioSource.rolloffMode = AudioRolloffMode.Linear;
                audioSource.maxDistance = NoiseRange;
            }
        }

        protected virtual void OnDestroy()
        {
            if (ThrowActionReference != null)
            {
                ThrowActionReference.action.Disable();
            }
        }

        protected virtual void Update()
        {
            if (isHeld)
            {
                HandleHeldState();
            }
            else
            {
                HandleIdleState();
            }
        }

        protected virtual void HandleIdleState()
        {
            Collider[] nearbyColliders = Physics.OverlapSphere(transform.position, GrabRadius, PlayerLayer);

            foreach (var nearbyCol in nearbyColliders)
            {
                PlayerInput playerInput = nearbyCol.GetComponentInParent<PlayerInput>();
                if (playerInput == null) continue;
                if (playerInput.actions == null) continue;

                if (IsPlayerHoldingSomething(playerInput.transform))
                {
                    continue;
                }

                InputAction interactAction = playerInput.actions.FindAction(ActionName);
                if (interactAction == null) continue;

                if (interactAction.WasPerformedThisFrame())
                {
                    Log($"Player {playerInput.name} picking up {gameObject.name}");
                    Pickup(playerInput.transform);
                    break;
                }
            }
        }

        protected virtual void HandleHeldState()
        {
            if (currentHolder == null)
            {
                Drop();
                return;
            }

            
            UpdateHeldPositionAndRotation();
            

            if (isThrowingInProgress) return;

            PlayerInput playerInput = currentHolder.GetComponent<PlayerInput>();
            if (playerInput == null) playerInput = currentHolder.GetComponentInParent<PlayerInput>();
            if (playerInput == null || playerInput.actions == null) return;

            InputAction interactAction = playerInput.actions.FindAction(ActionName);
            if (interactAction != null && interactAction.WasPerformedThisFrame())
            {
                Log($"Drop action triggered for {gameObject.name}");
                Drop();
                return;
            }

            if (ThrowActionReference != null && ThrowActionReference.action != null)
            {
                if (ThrowActionReference.action.WasPerformedThisFrame())
                {
                    Log($"Throw action triggered for {gameObject.name}");
                    StartCoroutine(ThrowSequence());
                    return;
                }
            }
        }

        
        protected void UpdateHeldPositionAndRotation()
        {
            Vector3 targetOffset = HoldOffset;
            Vector3 targetRotation = HoldRotation;

            
            if (holderAnimator != null)
            {
                bool isCrouching = holderAnimator.GetBool("IsCrouching");
                if (isCrouching)
                {
                    targetOffset = CrouchHoldOffset;
                    targetRotation = CrouchHoldRotation; 
                }
            }

            
            transform.localPosition = Vector3.Lerp(transform.localPosition, targetOffset, Time.deltaTime * positionSmoothSpeed);

            
            transform.localRotation = Quaternion.Slerp(transform.localRotation, Quaternion.Euler(targetRotation), Time.deltaTime * positionSmoothSpeed);
        }

        protected bool IsPlayerHoldingSomething(Transform playerTransform)
        {
            var heldObjects = playerTransform.GetComponentsInChildren<GrabbableObjectController>();
            return heldObjects.Any(obj => obj.isHeld && obj != this);
        }

        public virtual void Pickup(Transform holder)
        {
            Log($"{gameObject.name} picked up by {holder.name}");

            isHeld = true;
            isThrowingInProgress = false;
            currentHolder = holder;

            holderAnimator = holder.GetComponent<Animator>();
            if (holderAnimator == null) holderAnimator = holder.GetComponentInParent<Animator>();

            currentHolderCollider = holder.GetComponent<Collider>();
            if (currentHolderCollider == null) currentHolderCollider = holder.GetComponentInParent<Collider>();

            if (currentHolderCollider != null && col != null)
            {
                Physics.IgnoreCollision(col, currentHolderCollider, true);
            }

            rb.isKinematic = true;
            rb.velocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;

            transform.SetParent(holder);

            
            transform.localPosition = HoldOffset;
            transform.localEulerAngles = HoldRotation;

            var ikSystem = holder.GetComponent<ObjectCarryingSystem>();
            if (ikSystem == null) ikSystem = holder.GetComponentInParent<ObjectCarryingSystem>();

            if (ikSystem != null)
            {
                ikSystem.StartCarrying(this.gameObject);
            }
        }

        public virtual void Drop()
        {
            if (currentHolder != null)
            {
                var ikSystem = currentHolder.GetComponent<ObjectCarryingSystem>();
                if (ikSystem == null) ikSystem = currentHolder.GetComponentInParent<ObjectCarryingSystem>();

                if (ikSystem != null)
                {
                    ikSystem.StopCarrying();
                }
            }

            if (currentHolderCollider != null && col != null)
            {
                StartCoroutine(ResetCollisionDelay(col, currentHolderCollider, 0.5f));
            }

            Log($"{gameObject.name} dropped");

            isHeld = false;
            isThrowingInProgress = false;
            currentHolder = null;
            holderAnimator = null;

            transform.SetParent(originalParent);
            rb.isKinematic = false;
        }

        protected IEnumerator ThrowSequence()
        {
            if (currentHolder == null) yield break;
            isThrowingInProgress = true;

            var ikSystem = currentHolder.GetComponent<ObjectCarryingSystem>();
            if (ikSystem == null) ikSystem = currentHolder.GetComponentInParent<ObjectCarryingSystem>();

            if (ikSystem != null)
            {
                ikSystem.PlayThrowAnimation();
            }

            float elapsedTime = 0f;
            Vector3 startPos = transform.localPosition;

            
            Vector3 targetLocalPos = startPos + (Vector3.forward * VisualThrowDistance) + (Vector3.up * 0.2f);

            while (elapsedTime < ThrowAnimationDelay)
            {
                if (currentHolder == null) yield break;

                transform.localPosition = Vector3.Lerp(startPos, targetLocalPos, elapsedTime / ThrowAnimationDelay);

                elapsedTime += Time.deltaTime;
                yield return null;
            }

            ExecutePhysicsThrow();
        }

        protected void ExecutePhysicsThrow()
        {
            if (currentHolder == null)
            {
                Drop();
                return;
            }

            Vector3 throwDirection = currentHolder.forward;

            Log($"Throwing {gameObject.name} with force {ThrowForce}");

            Collider tempHolderCollider = currentHolderCollider;

            isHeld = false;
            isThrowingInProgress = false;
            currentHolder = null;
            holderAnimator = null;
            currentHolderCollider = null;

            transform.SetParent(originalParent);

            rb.isKinematic = false;

            Vector3 forceVector = (throwDirection * ThrowForce) + (Vector3.up * ThrowUpwardForce);
            rb.AddForce(forceVector, ForceMode.Impulse);

            Vector3 randomTorque = UnityEngine.Random.insideUnitSphere * 10f;
            rb.AddTorque(randomTorque, ForceMode.Impulse);

            if (tempHolderCollider != null && col != null)
            {
                StartCoroutine(ResetCollisionDelay(col, tempHolderCollider, 0.5f));
            }
        }

        private IEnumerator ResetCollisionDelay(Collider objectCol, Collider playerCol, float delay)
        {
            if (objectCol == null || playerCol == null) yield break;

            yield return new WaitForSeconds(delay);

            if (objectCol != null && playerCol != null)
            {
                Physics.IgnoreCollision(objectCol, playerCol, false);
            }
        }

        protected virtual void OnCollisionEnter(Collision collision)
        {
            if (isHeld) return;

            if (collision.relativeVelocity.magnitude >= MinCollisionForce)
            {
                PlayCollisionSound(collision.relativeVelocity.magnitude);
            }
        }

        protected void PlayCollisionSound(float impactMagnitude)
        {
            if (audioSource != null && CollisionSound != null)
            {
                float dynamicVolume = Mathf.Clamp(impactMagnitude / 10f, 0.2f, 1f) * CollisionVolume;
                audioSource.PlayOneShot(CollisionSound, dynamicVolume);
                Log($"Played collision sound with volume {dynamicVolume}");
            }
        }

        protected virtual void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, GrabRadius);

            if (audioSource != null)
            {
                Gizmos.color = Color.blue;
                Gizmos.DrawWireSphere(transform.position, NoiseRange);
            }
        }

        protected void Log(string message)
        {
            if (VerboseLogging)
            {

            }
        }

        protected void LogWarning(string message)
        {
            if (VerboseLogging)
            {

            }
        }
    }
}
