using UnityEngine;
using UnityEngine.VFX;
using System.Reflection;
using UnityEngine.InputSystem;
using System.Linq;

[RequireComponent(typeof(CharacterController))]
public class PlayerNoiseEmitter : MonoBehaviour
{
    [Header("Radios de ruido (metros)")]
    public float idleNoiseRadius = 1f;
    public float crouchIdleNoiseRadius = 0.5f; 
    public float walkNoiseRadius = 3f;
    public float crouchNoiseRadius = 2f;
    public float runNoiseRadius = 6f;

    [Header("Visual Feedback (VFX)")]
    public VisualEffect noiseVFX;
    public string vfxRadiusProperty = "Radius";
    public string vfxPulseProperty = "PulseSpeed";
    public string vfxSpawnRateProperty = "SpawnRate";

    [Tooltip("Cantidad de partculas a emitir cuando hay ruido.")]
    public float activeSpawnRate = 100f;

    public float visualLerpSpeed = 5f;

    [Header("Configuracin de Pulsacin")]
    public float idlePulseSpeed = 2f;
    public float crouchIdlePulseSpeed = 1f; 
    public float walkPulseSpeed = 8f;
    public float crouchWalkPulseSpeed = 5f; 
    public float runPulseSpeed = 18f;

    [Header("Debug")]
    public bool showNoiseGizmo = true;
    public Color noiseColor = new Color(1f, 0.6f, 0f, 0.25f);
    public Color crouchColor = new Color(0f, 0.8f, 1f, 0.25f); 

    [HideInInspector] public float currentNoiseRadius = 0f;

    private CharacterController controller;
    private float visualRadius = 0f;
    private bool vfxDisplayEnabled = true;

    
    private object activeMovementScript;
    private FieldInfo isMovingField;
    private FieldInfo isRunningField;
    private FieldInfo isCrouchingField;
    private bool reflectionInitialized = false;

    void Awake()
    {
        controller = GetComponent<CharacterController>();
        InitializeReflection();

        if (noiseVFX != null)
        {
            noiseVFX.Play();
        }
    }

    void InitializeReflection()
    {
        Component[] components = GetComponents<Component>();
        activeMovementScript = components.FirstOrDefault(c =>
            c != null && (c.GetType().Name == "MovJugador1" || c.GetType().Name == "MovJugador2"));

        if (activeMovementScript != null)
        {
            var type = activeMovementScript.GetType();
            const BindingFlags flags = BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public;

            isMovingField = type.GetField("isMoving", flags);
            isRunningField = type.GetField("isRunningInput", flags);
            isCrouchingField = type.GetField("isCrouching", flags);

            reflectionInitialized = (isMovingField != null && isRunningField != null && isCrouchingField != null);

            if (!reflectionInitialized)
            {

            }
        }
        else
        {

        }
    }

    public void OnToggleNoiseVFX(InputValue value)
    {
        if (value.isPressed)
        {
            vfxDisplayEnabled = !vfxDisplayEnabled;
        }
    }

    void Update()
    {
        CalculateLogicRadius();
        UpdateVFX();
    }

    void CalculateLogicRadius()
    {
        bool isMoving = false;
        bool isRunning = false;
        bool isCrouching = false;

        
        if (reflectionInitialized)
        {
            try
            {
                isMoving = (bool)isMovingField.GetValue(activeMovementScript);
                isRunning = (bool)isRunningField.GetValue(activeMovementScript);
                isCrouching = (bool)isCrouchingField.GetValue(activeMovementScript);
            }
            catch (System.Exception e)
            {

                reflectionInitialized = false;
            }
        }

        
        if (!reflectionInitialized)
        {
            isMoving = controller.velocity.magnitude > 0.1f;
            
        }

        
        float targetRadius = idleNoiseRadius;

        if (isCrouching)
        {
            if (isMoving)
            {
                
                targetRadius = crouchNoiseRadius;
            }
            else
            {
                
                targetRadius = crouchIdleNoiseRadius;
            }
        }
        else
        {
            if (isMoving)
            {
                if (isRunning)
                {
                    
                    targetRadius = runNoiseRadius;
                }
                else
                {
                    
                    targetRadius = walkNoiseRadius;
                }
            }
            else
            {
                
                targetRadius = idleNoiseRadius;
            }
        }

        currentNoiseRadius = targetRadius;
    }

    void UpdateVFX()
    {
        if (noiseVFX == null) return;

        
        visualRadius = Mathf.Lerp(visualRadius, currentNoiseRadius, Time.deltaTime * visualLerpSpeed);
        noiseVFX.SetFloat(vfxRadiusProperty, visualRadius);

        
        float targetPulse = idlePulseSpeed;

        if (currentNoiseRadius >= runNoiseRadius - 0.1f)
        {
            targetPulse = runPulseSpeed;
        }
        else if (currentNoiseRadius >= walkNoiseRadius - 0.1f)
        {
            targetPulse = walkPulseSpeed;
        }
        else if (currentNoiseRadius >= crouchNoiseRadius - 0.1f)
        {
            targetPulse = crouchWalkPulseSpeed;
        }
        else if (currentNoiseRadius >= crouchIdleNoiseRadius - 0.1f && currentNoiseRadius < idleNoiseRadius)
        {
            targetPulse = crouchIdlePulseSpeed;
        }

        noiseVFX.SetFloat(vfxPulseProperty, targetPulse);

        
        float currentRate = 0f;

        if (vfxDisplayEnabled && visualRadius > 0.1f)
        {
            currentRate = activeSpawnRate;
        }
        else
        {
            currentRate = 0f;
        }

        noiseVFX.SetFloat(vfxSpawnRateProperty, currentRate);

        if (!noiseVFX.enabled) noiseVFX.enabled = true;
    }

    void OnDrawGizmosSelected()
    {
        if (!showNoiseGizmo) return;

        
        bool isCrouching = false;

        if (Application.isPlaying && reflectionInitialized && isCrouchingField != null)
        {
            try
            {
                isCrouching = (bool)isCrouchingField.GetValue(activeMovementScript);
            }
            catch
            {
                
            }
        }

        Gizmos.color = isCrouching ? crouchColor : noiseColor;
        Gizmos.DrawWireSphere(transform.position, currentNoiseRadius);

        
        if (isCrouching)
        {
            Gizmos.color = new Color(0f, 1f, 1f, 0.1f);
            Gizmos.DrawWireSphere(transform.position, crouchIdleNoiseRadius);
        }
    }
}
