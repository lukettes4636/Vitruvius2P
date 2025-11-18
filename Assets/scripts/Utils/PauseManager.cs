using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;
using TMPro; 

public class PauseManager : MonoBehaviour
{
    [SerializeField] private List<InputActionReference> pauseActions = new List<InputActionReference>();
    [SerializeField] private CanvasGroup pausePanelCanvasGroup; 
    [SerializeField] private TextMeshProUGUI pauseText; 

    private bool isPaused = false;

    void OnEnable()
    {
        foreach (var actionRef in pauseActions)
        {
            if (actionRef != null && actionRef.action != null)
            {
                actionRef.action.Enable();
                actionRef.action.performed += PerformPauseToggle;
            }
        }

        
        if (pausePanelCanvasGroup != null) pausePanelCanvasGroup.alpha = 0f;
        if (pauseText != null) pauseText.gameObject.SetActive(false);
    }

    void OnDisable()
    {
        foreach (var actionRef in pauseActions)
        {
            if (actionRef != null && actionRef.action != null)
            {
                actionRef.action.performed -= PerformPauseToggle;
                actionRef.action.Disable();
            }
        }
    }

    private void PerformPauseToggle(InputAction.CallbackContext context)
    {
        if (isPaused)
        {
            ResumeGame();
        }
        else
        {
            PauseGame();
        }
    }

    public void PauseGame()
    {
        Time.timeScale = 0f;
        isPaused = true;


        
        if (pausePanelCanvasGroup != null)
        {
            pausePanelCanvasGroup.alpha = 1f; 
            pausePanelCanvasGroup.interactable = true;
            pausePanelCanvasGroup.blocksRaycasts = true;
        }
        if (pauseText != null) pauseText.gameObject.SetActive(true);
    }

    public void ResumeGame()
    {
        Time.timeScale = 1f;
        isPaused = false;


        
        if (pausePanelCanvasGroup != null)
        {
            pausePanelCanvasGroup.alpha = 0f; 
            pausePanelCanvasGroup.interactable = false;
            pausePanelCanvasGroup.blocksRaycasts = false;
        }
        if (pauseText != null) pauseText.gameObject.SetActive(false);
    }
}
