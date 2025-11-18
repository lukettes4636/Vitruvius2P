using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class FallenDoor : InteractiveObject
{
    [Header("Door Settings")]
    [SerializeField] private float maxLiftHeight = 2f;
    [SerializeField] private float liftSpeed = 1f;
    [SerializeField] private float dropSpeed = 2f;

    
    
    
    [Header("Crush Damage Settings")]
    [SerializeField] private int crushDamage = 50;
    [SerializeField] private LayerMask playerLayer;
    [Tooltip("Altura maxima (en metros desde el suelo) a la que la puerta puede infligir dano durante la caida.")]
    [SerializeField] private float crushHeightThreshold = 0.6f;
    

    [Header("Audio")]
    [SerializeField] private AudioClip liftSound;
    [SerializeField] private AudioClip dropSound;
    [SerializeField] private AudioClip maxHeightSound;
    private AudioSource audioSource;
    

    
    
    
    [Header("Haptic Feedback at Max Height")]
    [SerializeField] private float rumbleDuration = 0.5f;
    [SerializeField] private float lowFrequency = 0.5f;
    [SerializeField] private float highFrequency = 0.5f;

    [Header("Haptic Feedback - Drop IMPACT")]
    [SerializeField] private float dropRumbleDuration = 0.2f;
    [SerializeField] private float dropLowFrequency = 0.9f;
    [SerializeField] private float dropHighFrequency = 0.9f;

    private MovJugador1 playerLifter = null; 
    

    
    [Header("Outline Multiplayer")]
    [Tooltip("El ID del jugador que PUEDE activar el outline. 1 = Jugador 1.")]
    [SerializeField] private int requiredPlayerID = 1;
    [SerializeField] private string outlineColorProperty = "_Outline_Color";
    [SerializeField] private string outlineScaleProperty = "_Outline_Scale";
    [SerializeField] private float activeOutlineScale = 0.0125f;

    private Renderer meshRenderer;
    private MaterialPropertyBlock propertyBlock;
    private int outlineColorID;
    private int outlineScaleID;
    private Color originalOutlineColor = Color.black;
    private GameObject currentHoveringPlayer = null;
    

    private Vector3 initialPosition;
    private bool isBeingLifted = false;
    private bool isAtMaxHeight = false;
    private bool hasDropped = true;
    private bool isDropping = false; 

    
    
    
    [Header("UI Prompt Settings")]
    [SerializeField] private Canvas promptCanvas;
    [SerializeField] private TextMeshProUGUI promptText;
    

    private void Start()
    {
        initialPosition = transform.position;

        
        meshRenderer = GetComponent<Renderer>();
        if (meshRenderer != null)
        {
            propertyBlock = new MaterialPropertyBlock();

            outlineColorID = Shader.PropertyToID(outlineColorProperty);
            outlineScaleID = Shader.PropertyToID(outlineScaleProperty);

            SetOutlineState(Color.black, 0.0f);
        }

        if (GetComponent<Collider>() == null || !GetComponent<Collider>().isTrigger)
        {

        }

        
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
        }

        
        if (promptCanvas != null)
        {
            promptCanvas.enabled = false;
        }
    }

    private void Update()
    {
        HandleDoorMovement();
    }

    private void SetOutlineState(Color color, float scale)
    {
        if (meshRenderer != null && propertyBlock != null)
        {
            meshRenderer.GetPropertyBlock(propertyBlock);

            propertyBlock.SetColor(outlineColorID, color);
            propertyBlock.SetFloat(outlineScaleID, scale);

            meshRenderer.SetPropertyBlock(propertyBlock, 1);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (currentHoveringPlayer != null) return;

        PlayerIdentifier playerIdentifier = other.GetComponent<PlayerIdentifier>();

        if (playerIdentifier != null)
        {
            if (requiredPlayerID != 0 && playerIdentifier.playerID != requiredPlayerID)
            {
                return;
            }

            currentHoveringPlayer = other.gameObject;
            SetOutlineState(playerIdentifier.PlayerOutlineColor, activeOutlineScale);

            
            if (playerIdentifier.playerID == 1 && promptCanvas != null && promptText != null)
            {
                promptCanvas.enabled = true;
                promptText.text = "HOLD (X) TO LIFT THE DOOR";
            }
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.gameObject == currentHoveringPlayer)
        {
            SetOutlineState(originalOutlineColor, 0.0f);
            currentHoveringPlayer = null;

            
            if (promptCanvas != null)
            {
                promptCanvas.enabled = false;
            }
        }
    }

    
    public void StartLifting(MovJugador1 lifter)
    {
        isBeingLifted = true;
        playerLifter = lifter;
        hasDropped = false;
        isDropping = false;

        
        if (liftSound != null && audioSource != null)
        {
            audioSource.PlayOneShot(liftSound);
        }
    }

    public void StopLifting()
    {
        isBeingLifted = false;
        playerLifter = null;
        isDropping = true;

        
        if (dropSound != null && audioSource != null)
        {
            audioSource.PlayOneShot(dropSound);
        }
    }

    private void HandleDoorMovement()
    {
        float currentHeight = transform.position.y - initialPosition.y;

        if (isBeingLifted && currentHeight < maxLiftHeight)
        {
            
            transform.position += Vector3.up * (liftSpeed * Time.deltaTime);
            isDropping = false;

            if (transform.position.y >= initialPosition.y + maxLiftHeight)
            {
                transform.position = new Vector3(transform.position.x, initialPosition.y + maxLiftHeight, transform.position.z);

                if (!isAtMaxHeight)
                {
                    
                    if (maxHeightSound != null && audioSource != null)
                    {
                        audioSource.PlayOneShot(maxHeightSound);
                    }

                    
                    if (playerLifter != null)
                    {
                        playerLifter.StartCooperativeEffects(0f, 0f, lowFrequency, highFrequency, rumbleDuration);
                    }
                    isAtMaxHeight = true;
                }
            }
        }
        else if (!isBeingLifted && currentHeight > 0f)
        {
            
            transform.position -= Vector3.up * (dropSpeed * Time.deltaTime);
            isAtMaxHeight = false;

            
            
            
            if (isDropping && currentHeight <= crushHeightThreshold)
            {
                Collider[] hits = Physics.OverlapSphere(
                    transform.position + Vector3.up * 0.1f,
                    0.8f,
                    playerLayer
                );

                foreach (Collider hit in hits)
                {
                    PlayerIdentifier hitID = hit.GetComponent<PlayerIdentifier>();

                    if (hitID != null && playerLifter != null && hitID.gameObject != playerLifter.gameObject)
                    {
                        PlayerHealth victimHealth = hit.GetComponent<PlayerHealth>();

                        if (victimHealth != null && !victimHealth.IsDead)
                        {
                            victimHealth.SetLastDamageSource("FallenDoorCrush");
                            victimHealth.TakeDamage(crushDamage);

                            MovJugador1 otherPlayerScript1 = hit.GetComponent<MovJugador1>();

                            if (otherPlayerScript1 != null)
                            {
                                otherPlayerScript1.StartCooperativeEffects(0.3f, 0.25f, 0.7f, 0.8f, 0.3f);
                            }
                            else
                            {
                                MovJugador2 otherPlayerScript2 = hit.GetComponent<MovJugador2>();
                                if (otherPlayerScript2 != null)
                                {
                                    otherPlayerScript2.StartCooperativeEffects(0.3f, 0.25f, 0.7f, 0.8f, 0.3f);
                                }
                            }


                        }
                    }
                }
            }
            

            if (transform.position.y <= initialPosition.y)
            {
                transform.position = initialPosition;
                isDropping = false;
                playerLifter = null;

                if (!hasDropped)
                {
                    
                    if (currentHoveringPlayer != null)
                    {
                        MovJugador1 playerScript1 = currentHoveringPlayer.GetComponent<MovJugador1>();

                        if (playerScript1 != null)
                        {
                            playerScript1.StartCooperativeEffects(0f, 0f, dropLowFrequency, dropHighFrequency, dropRumbleDuration);
                        }
                        else
                        {
                            MovJugador2 playerScript2 = currentHoveringPlayer.GetComponent<MovJugador2>();
                            if (playerScript2 != null)
                            {
                                playerScript2.StartCooperativeEffects(0f, 0f, dropLowFrequency, dropHighFrequency, dropRumbleDuration);
                            }
                        }
                    }

                    hasDropped = true;
                }
            }
        }
        else if (currentHeight <= 0f)
        {
            hasDropped = true;
            isDropping = false;
            playerLifter = null;
        }
    }

    protected override void OnInteract()
    {

    }

    public float GetCurrentHeight()
    {
        return transform.position.y - initialPosition.y;
    }

    public float GetMaxLiftHeight()
    {
        return maxLiftHeight;
    }
}
