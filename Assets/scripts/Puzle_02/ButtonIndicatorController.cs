using UnityEngine;
using UnityEngine.VFX;

public class ButtonIndicatorController : MonoBehaviour
{
    [Header("Animator Settings")]
    private Animator animator;
    private static readonly int PressHash = Animator.StringToHash("Press");

    [Header("VFX Settings")]
    [SerializeField] private VisualEffect orbitSingleVFX;
    [SerializeField] private VisualEffect orbitDualVFX;
    [SerializeField] private VisualEffect shockwaveRingVFX;

    [Header("VFX Scale")]
    [Tooltip("Escala del efecto de onda de impacto")]
    [SerializeField] private float shockwaveScale = 3f;

    [Header("VFX Properties")]
    private static readonly int PlayerColorID = Shader.PropertyToID("PlayerColor");

    [Header("Color Settings")]
    private SpriteRenderer spriteRenderer;
    private UnityEngine.UI.Image imageComponent;

    [Header("Pulse Effect (Optional)")]
    [SerializeField] private bool enablePulse = true;
    [SerializeField] private float pulseSpeed = 2f;
    [SerializeField] private float pulseAmount = 0.1f;

    [Header("Debug")]
    [SerializeField] private bool showDebugLogs = false;

    private Color currentPlayerColor = Color.white;
    private Vector3 originalScale;
    private int currentOrbitState = -1; 

    void Awake()
    {
        animator = GetComponent<Animator>();
        if (animator == null)
        {
            animator = GetComponentInChildren<Animator>();
        }

        spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        if (spriteRenderer == null)
        {
            imageComponent = GetComponentInChildren<UnityEngine.UI.Image>();
        }

        originalScale = transform.localScale;
    }

    void Start()
    {
        
        StopAllVFX();

        if (showDebugLogs)
        {

            LogVFXStatus();
        }
    }

    void Update()
    {
        if (enablePulse)
        {
            float pulse = 1f + Mathf.Sin(Time.time * pulseSpeed) * pulseAmount;
            transform.localScale = originalScale * pulse;
        }
    }

    
    
    
    public void TriggerPress()
    {
        if (animator != null)
        {
            animator.SetTrigger(PressHash);
        }
    }

    
    
    
    
    public void TriggerImpact(bool isFinalHit = false)
    {
        if (shockwaveRingVFX == null)
        {
            if (showDebugLogs)
            {

            }
            return;
        }

        
        float finalScale = isFinalHit ? shockwaveScale * 1.5f : shockwaveScale;

        Transform vfxTransform = shockwaveRingVFX.transform;
        vfxTransform.localScale = Vector3.one * finalScale;

        
        shockwaveRingVFX.Stop();
        shockwaveRingVFX.Play();

        if (showDebugLogs)
        {

        }
    }

    
    
    
    public void SetPlayerColor(Color playerColor)
    {
        currentPlayerColor = playerColor;

        
        if (spriteRenderer != null)
        {
            spriteRenderer.color = playerColor;
        }
        else if (imageComponent != null)
        {
            imageComponent.color = playerColor;
        }

        
        SetVFXColor(orbitSingleVFX, playerColor);
        SetVFXColor(orbitDualVFX, playerColor);
        SetVFXColor(shockwaveRingVFX, playerColor);

        if (showDebugLogs)
        {

        }
    }

    private void SetVFXColor(VisualEffect vfx, Color color)
    {
        if (vfx == null) return;

        if (vfx.HasVector4(PlayerColorID))
        {
            vfx.SetVector4(PlayerColorID, color);
        }
        else if (showDebugLogs)
        {

        }
    }

    
    
    
    
    public void SetOrbitState(int playerCount)
    {
        
        if (currentOrbitState == playerCount)
        {
            return;
        }

        currentOrbitState = playerCount;

        if (showDebugLogs)
        {

        }

        switch (playerCount)
        {
            case 0:
                
                StopOrbitVFX();

                break;

            case 1:
                
                if (orbitDualVFX != null)
                {
                    orbitDualVFX.Stop();

                }

                if (orbitSingleVFX != null)
                {
                    orbitSingleVFX.Reinit();
                    orbitSingleVFX.Play();

                }
                break;

            default:
                
                if (orbitSingleVFX != null)
                {
                    orbitSingleVFX.Stop();

                }

                if (orbitDualVFX != null)
                {
                    orbitDualVFX.Reinit();
                    orbitDualVFX.Play();

                }
                break;
        }
    }

    private void StopOrbitVFX()
    {
        if (orbitSingleVFX != null) orbitSingleVFX.Stop();
        if (orbitDualVFX != null) orbitDualVFX.Stop();
    }

    private void StopAllVFX()
    {
        StopOrbitVFX();
        if (shockwaveRingVFX != null) shockwaveRingVFX.Stop();
    }

    
    
    
    public void SetAlpha(float alpha)
    {
        if (spriteRenderer != null)
        {
            Color currentColor = spriteRenderer.color;
            currentColor.a = Mathf.Clamp01(alpha);
            spriteRenderer.color = currentColor;
        }
        else if (imageComponent != null)
        {
            Color currentColor = imageComponent.color;
            currentColor.a = Mathf.Clamp01(alpha);
            imageComponent.color = currentColor;
        }
    }

    private void LogVFXStatus()
    {




    }

    void OnDisable()
    {
        
        StopAllVFX();
        currentOrbitState = -1; 

        if (showDebugLogs)
        {

        }
    }

    void OnEnable()
    {
        if (showDebugLogs)
        {

        }
    }
}
