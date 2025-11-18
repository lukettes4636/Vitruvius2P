using UnityEngine;





[RequireComponent(typeof(CharacterController))]
public class PlayerNoiseEmitter : MonoBehaviour
{
    [Header("Radios de ruido (metros)")]
    public float idleNoiseRadius = 1f;
    public float walkNoiseRadius = 3f;
    public float crouchNoiseRadius = 2f;
    public float runNoiseRadius = 6f;

    [Header("Visual Debug")]
    public bool showNoiseGizmo = true;
    public Color noiseColor = new Color(1f, 0.6f, 0f, 0.25f);

    [HideInInspector] public float currentNoiseRadius = 0f;
    private CharacterController controller;

    
    private MovJugador1 mov1;
    private MovJugador2 mov2;

    void Awake()
    {
        controller = GetComponent<CharacterController>();
        mov1 = GetComponent<MovJugador1>();
        mov2 = GetComponent<MovJugador2>();
    }

    void Update()
    {
        UpdateNoiseRadius();
    }

    private void UpdateNoiseRadius()
    {
        bool isMoving = false;
        bool isRunning = false;
        bool isCrouching = false;

        if (mov1 != null)
        {
            
            isMoving = (bool)mov1.GetType()
                .GetField("isMoving", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.GetValue(mov1);
            isRunning = (bool)mov1.GetType()
                .GetField("isRunningInput", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.GetValue(mov1);
            isCrouching = (bool)mov1.GetType()
                .GetField("isCrouching", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.GetValue(mov1);
        }
        else if (mov2 != null)
        {
            
            isMoving = GetComponent<CharacterController>().velocity.magnitude > 0.1f;
            isRunning = (bool)mov2.GetType()
                .GetField("isRunningInput", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.GetValue(mov2);
            isCrouching = (bool)mov2.GetType()
                .GetField("isCrouching", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.GetValue(mov2);
        }

        
        if (!isMoving)
            currentNoiseRadius = idleNoiseRadius;
        else if (isCrouching)
            currentNoiseRadius = crouchNoiseRadius;
        else if (isRunning)
            currentNoiseRadius = runNoiseRadius;
        else
            currentNoiseRadius = walkNoiseRadius;
    }

    void OnDrawGizmosSelected()
    {
        if (!showNoiseGizmo) return;
        Gizmos.color = noiseColor;
        Gizmos.DrawWireSphere(transform.position, currentNoiseRadius);
    }
}
