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
    [SerializeField] private VisualEffect impactBurstVFX;
    [SerializeField] private VisualEffect shockwaveRingVFX;

    [Header("VFX Properties")]
    private static readonly int PlayerColorID = Shader.PropertyToID("PlayerColor");
    private static readonly int BurstCountID = Shader.PropertyToID("BurstCount");

    [Header("Color Settings")]
    private SpriteRenderer spriteRenderer;
    private UnityEngine.UI.Image imageComponent;

    [Header("Pulse Effect (Optional)")]
    [SerializeField] private bool enablePulse = true;
    [SerializeField] private float pulseSpeed = 2f;
    [SerializeField] private float pulseAmount = 0.1f;

    private Color currentPlayerColor = Color.white;
    private Vector3 originalScale;

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

    
    
    
    
    
    public void TriggerImpact(bool isSuccessful, bool isFinalHit = false)
    {
        if (!isSuccessful) return;

        
        int burstCount = isFinalHit ? 100 : 50;

        if (impactBurstVFX != null)
        {
            if (impactBurstVFX.HasInt(BurstCountID))
            {
                impactBurstVFX.SetInt(BurstCountID, burstCount);
            }
            impactBurstVFX.Play();
        }

        
        if (isFinalHit && shockwaveRingVFX != null)
        {
            shockwaveRingVFX.Play();
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
        SetVFXColor(impactBurstVFX, playerColor);
        SetVFXColor(shockwaveRingVFX, playerColor);
    }

    private void SetVFXColor(VisualEffect vfx, Color color)
    {
        if (vfx != null && vfx.HasVector4(PlayerColorID))
        {
            vfx.SetVector4(PlayerColorID, color);
        }
    }

    
    
    
    
    public void SetOrbitState(int playerCount)
    {
        switch (playerCount)
        {
            case 0:
                
                StopOrbitVFX();
                break;

            case 1:
                
                if (orbitDualVFX != null) orbitDualVFX.Stop();
                if (orbitSingleVFX != null) orbitSingleVFX.Play();
                break;

            default:
                
                if (orbitSingleVFX != null) orbitSingleVFX.Stop();
                if (orbitDualVFX != null) orbitDualVFX.Play();
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
        if (impactBurstVFX != null) impactBurstVFX.Stop();
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

    void OnDisable()
    {
        
        StopAllVFX();
    }
}