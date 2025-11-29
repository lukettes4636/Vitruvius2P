using UnityEngine;
using UnityEngine.VFX; 

[RequireComponent(typeof(CharacterController))]
public class PlayerNoiseEmitter : MonoBehaviour
{
    [Header("Radios de ruido (metros)")]
    public float idleNoiseRadius = 1f;
    public float walkNoiseRadius = 3f;
    public float crouchNoiseRadius = 2f;
    public float runNoiseRadius = 6f;

    [Header("Visual Feedback")]
    public VisualEffect noiseVFX; 
    public string vfxRadiusProperty = "Radius"; 
    public float visualLerpSpeed = 5f; 

    [Header("Debug")]
    public bool showNoiseGizmo = true;
    public Color noiseColor = new Color(1f, 0.6f, 0f, 0.25f);

    [HideInInspector] public float currentNoiseRadius = 0f;

    private CharacterController controller;
    private float visualRadius = 0f; 

    
    private object activeMovementScript;
    private System.Reflection.FieldInfo isMovingField, isRunningField, isCrouchingField;
    private bool initialized = false;

    void Awake()
    {
        controller = GetComponent<CharacterController>();
        InitializeReflection();
    }

    void InitializeReflection()
    {
        var m1 = GetComponent("MovJugador1");
        var m2 = GetComponent("MovJugador2");
        if (m1 != null) activeMovementScript = m1;
        else if (m2 != null) activeMovementScript = m2;

        if (activeMovementScript != null)
        {
            var t = activeMovementScript.GetType();
            isMovingField = t.GetField("isMoving", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            isRunningField = t.GetField("isRunningInput", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            isCrouchingField = t.GetField("isCrouching", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            initialized = true;
        }
    }

    void Update()
    {
        CalculateLogicRadius();
        UpdateVFX();
    }

    void CalculateLogicRadius()
    {
        bool isMoving = false, isRunning = false, isCrouching = false;

        if (initialized)
        {
            if (isMovingField != null) isMoving = (bool)isMovingField.GetValue(activeMovementScript);
            if (isRunningField != null) isRunning = (bool)isRunningField.GetValue(activeMovementScript);
            if (isCrouchingField != null) isCrouching = (bool)isCrouchingField.GetValue(activeMovementScript);
        }
        else
        {
            isMoving = controller.velocity.magnitude > 0.1f;
        }

        float target = idleNoiseRadius;
        if (isMoving)
        {
            if (isRunning) target = runNoiseRadius;
            else if (isCrouching) target = crouchNoiseRadius;
            else target = walkNoiseRadius;
        }

        currentNoiseRadius = target;
    }

    void UpdateVFX()
    {
        if (noiseVFX == null) return;

        
        visualRadius = Mathf.Lerp(visualRadius, currentNoiseRadius, Time.deltaTime * visualLerpSpeed);

        
        noiseVFX.SetFloat(vfxRadiusProperty, visualRadius);

        
        
        if (currentNoiseRadius <= idleNoiseRadius)
        {
            noiseVFX.SetFloat("SpawnRate", 10); 
        }
        else
        {
            noiseVFX.SetFloat("SpawnRate", 100);
        }
    }

    void OnDrawGizmosSelected()
    {
        if (!showNoiseGizmo) return;
        Gizmos.color = noiseColor;
        Gizmos.DrawWireSphere(transform.position, currentNoiseRadius);
    }
}