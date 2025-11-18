using System.Collections;
using UnityEngine;
using System.Collections.Generic; 

public class ElectricBox : MonoBehaviour
{
    [Header("Componentes de la Palanca")]
    [Tooltip("El GameObject completo que representa la palanca instalada (debe estar desactivado al inicio).")]
    [SerializeField] private GameObject fullLeverObject;
    [Tooltip("El Transform de la parte de la palanca que debe rotar (la parte movil).")]
    [SerializeField] private Transform leverToRotate;
    [SerializeField] private Vector3 rotationAngle = new Vector3(-60f, 0f, 0f); 
    [SerializeField] private float rotationDuration = 1.5f; 

    [Header("Efectos a desactivar")]
    [SerializeField] private GameObject electricityParticles; 

    [Header("Sonido")]
    [Tooltip("Sonido que se reproduce cuando se corta la electricidad.")]
    [SerializeField] private AudioClip powerCutSound;
    [SerializeField] private float powerCutVolume = 1.0f;

    [Header("Objetos Afectados")]
    [Tooltip("Arrastra aqui el script WarningDoor del Collider que quieres desactivar.")]
    [SerializeField] private WarningDoor doorBarrier; 

    [Header("Item Configuration")]
    [SerializeField] private string requiredItemID = "PalancaParte"; 

    
    
    
    [Header("Interaction Prompt")]
    [SerializeField] private GameObject interactPromptCanvas; 
    
    
    

    
    [Header("Outline Multiplayer")]
    [Tooltip("The color used when two or more players are in the trigger.")]
    [SerializeField] private Color cooperativeOutlineColor = Color.yellow;

    [Tooltip("The name of the 'Color' property in the Shader Graph.")]
    [SerializeField] private string outlineColorProperty = "_Outline_Color";
    [Tooltip("The name of the 'Scale' property in the Shader Graph.")]
    [SerializeField] private string outlineScaleProperty = "_Outline_Scale";
    [SerializeField] private float activeOutlineScale = 0.0125f;

    private List<PlayerIdentifier> activePlayers = new List<PlayerIdentifier>();
    private Renderer meshRenderer;
    private MaterialPropertyBlock propertyBlock;
    private int outlineColorID;
    private int outlineScaleID;
    private Color originalOutlineColor = Color.black;
    

    private bool isPowerOn = true;
    private bool isAnimating = false;

    private void Start()
    {
        if (fullLeverObject != null)
        {
            fullLeverObject.SetActive(false);
        }

        
        
        
        if (interactPromptCanvas != null)
        {
            interactPromptCanvas.SetActive(false);
        }
        
        
        

        
        meshRenderer = GetComponent<Renderer>();
        if (meshRenderer != null)
        {
            
            if (meshRenderer.sharedMaterials.Length < 2)
            {
                Material[] currentMaterials = meshRenderer.materials;
            }

            propertyBlock = new MaterialPropertyBlock();

            outlineColorID = Shader.PropertyToID(outlineColorProperty);
            outlineScaleID = Shader.PropertyToID(outlineScaleProperty);

            
            SetOutlineState(Color.black, 0.0f);
        }
        else
        {

        }
    }

    
    private void SetOutlineState(Color color, float scale)
    {
        if (meshRenderer != null && propertyBlock != null)
        {
            
            meshRenderer.GetPropertyBlock(propertyBlock, 1);

            propertyBlock.SetColor(outlineColorID, color);
            propertyBlock.SetFloat(outlineScaleID, scale);

            meshRenderer.SetPropertyBlock(propertyBlock, 1);
        }
    }

    private void UpdateOutlineVisuals()
    {
        if (activePlayers.Count == 0)
        {
            SetOutlineState(originalOutlineColor, 0.0f);
        }
        else if (activePlayers.Count == 1)
        {
            PlayerIdentifier singlePlayer = activePlayers[0];
            
            SetOutlineState(singlePlayer.PlayerOutlineColor, activeOutlineScale);
        }
        else
        {
            SetOutlineState(cooperativeOutlineColor, activeOutlineScale);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        
        PlayerIdentifier playerIdentifier = other.GetComponent<PlayerIdentifier>();

        if (playerIdentifier != null)
        {
            if (!activePlayers.Contains(playerIdentifier))
            {
                activePlayers.Add(playerIdentifier);
            }
            UpdateOutlineVisuals();

            
            
            
            PlayerInventory inventory = other.GetComponent<PlayerInventory>();
            if (inventory != null && inventory.HasItem(requiredItemID) && isPowerOn)
            {
                ShowPrompt(true);
            }
            
            
            
        }
    }

    private void OnTriggerExit(Collider other)
    {
        PlayerIdentifier playerIdentifier = other.GetComponent<PlayerIdentifier>();

        if (playerIdentifier != null)
        {
            if (activePlayers.Contains(playerIdentifier))
            {
                activePlayers.Remove(playerIdentifier);
            }
            UpdateOutlineVisuals();

            
            
            
            if (activePlayers.Count == 0)
            {
                ShowPrompt(false);
            }
            
            
        }
    }
    

    public void TryDeactivatePower(MonoBehaviour playerScript)
    {
        if (!isPowerOn || isAnimating)
        {
            return;
        }

        
        PlayerInventory inventory = playerScript.GetComponent<PlayerInventory>();
        PlayerUIController uiController = playerScript.GetComponent<PlayerUIController>();
        string playerName = playerScript.gameObject.name;

        if (inventory != null && inventory.HasItem(requiredItemID))
        {
            isAnimating = true;
            
            
            
            ShowPrompt(false);
            
            
            StartCoroutine(DeactivatePowerCoroutine(inventory, playerName, uiController));
        }
        else
        {
            if (uiController != null)
            {
                uiController.ShowNotification($"We need the other part of the {requiredItemID} to cut the power.");
            }
        }
    }

    private IEnumerator DeactivatePowerCoroutine(PlayerInventory inventory, string playerName, PlayerUIController uiController)
    {
        
        if (inventory.HasItem(requiredItemID))
        {
            inventory.UseItem(requiredItemID); 

        }

        
        if (fullLeverObject != null)
        {
            fullLeverObject.SetActive(true);
            yield return new WaitForSeconds(0.3f);
        }

        
        if (leverToRotate != null)
        {
            Quaternion startRotation = leverToRotate.localRotation;
            Quaternion endRotation = Quaternion.Euler(rotationAngle);
            float elapsed = 0f;

            while (elapsed < rotationDuration)
            {
                
                leverToRotate.localRotation = Quaternion.Slerp(startRotation, endRotation, elapsed / rotationDuration);
                elapsed += Time.deltaTime;
                yield return null;
            }
            leverToRotate.localRotation = endRotation;
        }

        
        yield return new WaitForSeconds(0.2f);

        if (electricityParticles != null)
        {
            electricityParticles.SetActive(false);

        }

        
        if (powerCutSound != null)
        {
            AudioSource.PlayClipAtPoint(powerCutSound, transform.position, powerCutVolume);
        }

        
        if (uiController != null)
        {
            uiController.ShowNotification($" The power is off now! ");
        }

        
        if (doorBarrier != null)
        {
            doorBarrier.DeactivateBarrier();
        }

        isPowerOn = false;
        isAnimating = false;
    }

    
    
    
    private void ShowPrompt(bool state)
    {
        if (interactPromptCanvas != null)
        {
            
            interactPromptCanvas.SetActive(state && isPowerOn && !isAnimating);
        }
    }
    
    
    
}
