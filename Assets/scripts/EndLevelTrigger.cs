using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using TMPro;

public class EndLevelTrigger : MonoBehaviour
{
    [Header("Referencias")]
    public CanvasGroup fadePanelCanvasGroup;
    public GameObject endDemoTextObject;
    public GameObject continueButtonObject;
    public Button continueButton;

    [Header("Player Input")]
    public PlayerInput playerOneInput;
    public PlayerInput playerTwoInput;

    [Header("Configuracin")]
    public string mainMenuScene = "Main Menu";
    public float fadeDuration = 1.5f;

    private bool triggered = false;

    private void Start()
    {
        
        if (fadePanelCanvasGroup != null)
        {
            fadePanelCanvasGroup.alpha = 0f;
        }
        if (continueButtonObject != null)
        {
            continueButtonObject.SetActive(false);
        }
        if (endDemoTextObject != null)
        {
            endDemoTextObject.SetActive(false); 
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!triggered && (other.GetComponent<CharacterController>() != null))
        {
            triggered = true;

            
            CharacterController cc = other.GetComponent<CharacterController>();
            if (cc != null)
            {
                cc.enabled = false;
            }

            StartCoroutine(FadeAndShowUI());
        }
    }

    private IEnumerator FadeAndShowUI()
    {
        
        float startTime = Time.time;
        float endAlpha = 1f; 

        while (fadePanelCanvasGroup.alpha < endAlpha)
        {
            float t = (Time.time - startTime) / fadeDuration;
            fadePanelCanvasGroup.alpha = Mathf.Lerp(0f, endAlpha, t);
            yield return null;
        }

        fadePanelCanvasGroup.alpha = endAlpha; 

        

        
        if (endDemoTextObject != null)
        {
            endDemoTextObject.SetActive(true);
        }

        
        if (continueButtonObject != null)
        {
            continueButtonObject.SetActive(true);
        }

        
        if (continueButton != null)
        {
            continueButton.Select();
        }

        
        if (playerOneInput != null)
        {
            playerOneInput.SwitchCurrentActionMap("UI");
        }
        if (playerTwoInput != null)
        {
            playerTwoInput.SwitchCurrentActionMap("UI");
        }

        
    }

    
    public void LoadMainMenuScene()
    {
        

        
        if (playerOneInput != null)
        {
            playerOneInput.SwitchCurrentActionMap("Player");
        }
        if (playerTwoInput != null)
        {
            playerTwoInput.SwitchCurrentActionMap("Player");
        }

        
        SceneManager.LoadScene(mainMenuScene);
    }
}