using UnityEngine;

public class ButtonIndicatorController : MonoBehaviour
{
    [Header("Animator Settings")]
    private Animator animator;
    private static readonly int PressHash = Animator.StringToHash("Press");

    [Header("Particle Effects (Optional)")]
    [SerializeField] private ParticleSystem pressParticles;

    [Header("Color Settings")]
    private SpriteRenderer spriteRenderer;
    private UnityEngine.UI.Image imageComponent;

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
    }

    
    
    
    public void TriggerPress()
    {
        if (animator != null)
        {
            animator.SetTrigger(PressHash);
        }

        
        if (pressParticles != null)
        {
            pressParticles.Play();
        }
    }

    
    
    
    
    public void SetPlayerColor(Color playerColor)
    {
        if (spriteRenderer != null)
        {
            spriteRenderer.color = playerColor;
        }
        else if (imageComponent != null)
        {
            imageComponent.color = playerColor;
        }
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
}