using UnityEngine;
using UnityEngine.InputSystem;
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
        
        [Tooltip("Position offset when held by the player")]
        public Vector3 HoldOffset = new Vector3(0.5f, 0.8f, 0.7f);
        
        [Tooltip("Rotation offset when held by the player")]
        public Vector3 HoldRotation = Vector3.zero;
        
        [Tooltip("Layer mask for detecting players")]
        public LayerMask PlayerLayer = -1;

        [Header("Throw Settings")]
        [Tooltip("Horizontal force applied when throwing (Impact force depends on Mass)")]
        public float ThrowForce = 12f;
        
        [Tooltip("Upward force for arc trajectory")]
        public float ThrowUpwardForce = 3f;

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
                    Throw();
                    return;
                }
            }
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
            currentHolder = holder;
            
            rb.isKinematic = true;
            rb.velocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            
            transform.SetParent(holder);
            transform.localPosition = HoldOffset;
            transform.localEulerAngles = HoldRotation;
        }

        public virtual void Drop()
        {
            Log($"{gameObject.name} dropped");
            
            isHeld = false;
            currentHolder = null;
            
            transform.SetParent(originalParent);
            rb.isKinematic = false;
        }

        public virtual void Throw()
        {
            if (currentHolder == null)
            {
                Drop();
                return;
            }

            Vector3 throwDirection = currentHolder.forward;
            
            Log($"Throwing {gameObject.name} with force {ThrowForce} in direction {throwDirection}");

            isHeld = false;
            currentHolder = null;
            
            transform.SetParent(originalParent);
            rb.isKinematic = false;

            
            
            Vector3 forceVector = (throwDirection * ThrowForce) + (Vector3.up * ThrowUpwardForce);
            rb.AddForce(forceVector, ForceMode.Impulse);

            
            Vector3 randomTorque = UnityEngine.Random.insideUnitSphere * 10f;
            rb.AddTorque(randomTorque, ForceMode.Impulse);
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
