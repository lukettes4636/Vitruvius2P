using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(NPCProgressiveDialogue))]
public class NPCDialogueSystem : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private NPCPopupBillboard npcPopup;
    [SerializeField] private PlayerPopupBillboard player1Popup;
    [SerializeField] private PlayerPopupBillboard player2Popup;
    [SerializeField] private Animator npcAnimator;
    [SerializeField] private NPCProgressiveDialogue progressive;
    [SerializeField] private NPCFollowUpDialogue followUpDialogue;
    [SerializeField] private DialogueCameraController cameraController;

    [Header("Animation")]
    [SerializeField] private string talkParameter = "IsTalking";

    [Header("Interaction Range")]
    [SerializeField] private float interactionRange = 3f;

    [Header("Interaction Prompt")]
    [SerializeField] private GameObject interactPromptCanvas;

    [Header("Player Actions")]
    [SerializeField] private InputActionReference player1ActionButton;
    [SerializeField] private InputActionReference player2ActionButton;
    [SerializeField] private InputActionReference player1Navigate;
    [SerializeField] private InputActionReference player2Navigate;
    [SerializeField] private InputActionReference player1Submit;
    [SerializeField] private InputActionReference player2Submit;

    [Header("Visual")]
    [SerializeField] private Color selectedColor = new Color(1f, 0.84f, 0f);
    [SerializeField] private Color unselectedColor = Color.white;

    [Header("Acomodacion de Canvas")]
    [SerializeField] private bool autoArrangeCanvases = true;
    [SerializeField] private float minCanvasSeparation = 0.5f;
    [SerializeField] private float separationMultiplier = 1.2f;

    private List<DialogueNode> dialogueNodes = new List<DialogueNode>();
    private int currentNode = 0;
    private bool isTalking = false;
    private bool awaitingChoice = false;
    private bool waitingForInput = false;
    private int selectedOption = 0;
    private PlayerPopupBillboard currentPopup;
    private string currentPlayerTag;
    private PlayerInput currentPlayerInput;
    private float navigateCooldown = 0.25f;
    private float lastNavigateTime = 0f;
    private List<PlayerIdentifier> activePlayers = new List<PlayerIdentifier>();
    private System.Action<InputAction.CallbackContext> p1StartCb;
    private System.Action<InputAction.CallbackContext> p2StartCb;
    private System.Action<InputAction.CallbackContext> p1SubmitCb;
    private System.Action<InputAction.CallbackContext> p2SubmitCb;

    private GameObject player1Obj;
    private GameObject player2Obj;
    private Transform player1Tf;
    private Transform player2Tf;
    private PlayerInput player1PI;
    private PlayerInput player2PI;

    private Coroutine arrangeCoroutine; 

    void Start()
    {
        if (interactPromptCanvas != null) interactPromptCanvas.SetActive(false);
    }

    void Awake()
    {
        if (progressive == null)
            progressive = GetComponent<NPCProgressiveDialogue>();

        if (cameraController == null)
            cameraController = FindObjectOfType<DialogueCameraController>();

        player1Obj = GameObject.FindGameObjectWithTag("Player1");
        player2Obj = GameObject.FindGameObjectWithTag("Player2");
        if (player1Obj != null)
        {
            player1Tf = player1Obj.transform;
            player1PI = player1Obj.GetComponent<PlayerInput>();
        }
        if (player2Obj != null)
        {
            player2Tf = player2Obj.transform;
            player2PI = player2Obj.GetComponent<PlayerInput>();
        }
    }

    void OnEnable()
    {
        p1StartCb = ctx => TryStartDialogue("Player1");
        p2StartCb = ctx => TryStartDialogue("Player2");
        p1SubmitCb = ctx => TryProgress();
        p2SubmitCb = ctx => TryProgress();
        if (player1ActionButton?.action != null)
            player1ActionButton.action.performed += p1StartCb;
        if (player2ActionButton?.action != null)
            player2ActionButton.action.performed += p2StartCb;
        if (player1Submit?.action != null)
            player1Submit.action.performed += p1SubmitCb;
        if (player2Submit?.action != null)
            player2Submit.action.performed += p2SubmitCb;
    }

    void OnDisable()
    {
        if (player1ActionButton?.action != null && p1StartCb != null)
            player1ActionButton.action.performed -= p1StartCb;
        if (player2ActionButton?.action != null && p2StartCb != null)
            player2ActionButton.action.performed -= p2StartCb;
        if (player1Submit?.action != null && p1SubmitCb != null)
            player1Submit.action.performed -= p1SubmitCb;
        if (player2Submit?.action != null && p2SubmitCb != null)
            player2Submit.action.performed -= p2SubmitCb;
    }

    private void OnTriggerEnter(Collider other)
    {
        PlayerIdentifier playerIdentifier = other.GetComponent<PlayerIdentifier>();
        if (playerIdentifier == null) return;
        if (!activePlayers.Contains(playerIdentifier)) activePlayers.Add(playerIdentifier);
        ShowPrompt(true);
    }

    private void OnTriggerExit(Collider other)
    {
        PlayerIdentifier playerIdentifier = other.GetComponent<PlayerIdentifier>();
        if (playerIdentifier == null) return;
        if (activePlayers.Contains(playerIdentifier)) activePlayers.Remove(playerIdentifier);
        if (activePlayers.Count == 0) ShowPrompt(false);
    }

    private void TryStartDialogue(string playerTag)
    {
        if (isTalking) return;
        Transform pt = null;
        PlayerInput pi = null;
        if (playerTag == "Player1")
        {
            pt = player1Tf;
            pi = player1PI;
        }
        else if (playerTag == "Player2")
        {
            pt = player2Tf;
            pi = player2PI;
        }
        if (pt == null || pi == null) return;

        float distance = Vector3.Distance(transform.position, pt.position);
        if (distance > interactionRange)
        {
            return;
        }

        if (DialogueManager.IsDialogueActive())
        {
            if (playerTag == "Player1") player1Popup?.ShowMessage("The other player is talking...", 1.5f);
            if (playerTag == "Player2") player2Popup?.ShowMessage("The other player is talking...", 1.5f);
            return;
        }

        DialogueManager.SetDialogueActive(true);
        isTalking = true;
        currentPlayerTag = playerTag;
        currentPopup = playerTag == "Player1" ? player1Popup : player2Popup;
        currentPlayerInput = pi;
        currentPlayerInput.SwitchCurrentActionMap("UI");

        ShowPrompt(false);

        if (npcPopup != null)
        {
            npcPopup.SetActivePlayer(playerTag);
        }

        if (cameraController != null)
            cameraController.StartDialogueCamera(transform, pt);

        if (autoArrangeCanvases)
        {
            ArrangeCanvasesForDialogue();
        }

        if (progressive != null && progressive.HasInteractedOnce(currentPlayerTag) && followUpDialogue != null)
        {
            dialogueNodes = followUpDialogue.followUpDialogueNodes;
        }
        else
        {
            dialogueNodes = progressive != null ? progressive.GetCurrentDialogue(currentPlayerTag) : new List<DialogueNode>();
        }
        currentNode = 0;

        if (npcAnimator != null && AnimatorHasParameter(npcAnimator, talkParameter))
            npcAnimator.SetBool(talkParameter, true);

        progressive?.StartDialogue(this);
        ShowCurrentNode();
    }

    private void TryProgress()
    {
        if (!isTalking) return;

        if (waitingForInput && !awaitingChoice)
        {
            waitingForInput = false;
            AdvanceDialogue();
            return;
        }

        if (awaitingChoice)
        {
            ChooseOption(selectedOption);
            return;
        }
    }

    void Update()
    {
        if (!awaitingChoice || !isTalking) return;
        if (Time.time - lastNavigateTime < navigateCooldown) return;

        Vector2 nav = Vector2.zero;
        if (currentPlayerTag == "Player1" && player1Navigate != null)
            nav = player1Navigate.action.ReadValue<Vector2>();
        else if (currentPlayerTag == "Player2" && player2Navigate != null)
            nav = player2Navigate.action.ReadValue<Vector2>();

        if (Mathf.Abs(nav.y) > 0.5f)
        {
            int dir = nav.y > 0 ? -1 : 1;
            MoveOption(dir);
            lastNavigateTime = Time.time;
        }
    }

    private void MoveOption(int dir)
    {
        if (dialogueNodes == null || dialogueNodes.Count == 0) return;
        DialogueNode node = dialogueNodes[currentNode];
        if (node.options == null || node.options.Count == 0) return;
        int count = node.options.Count;
        selectedOption = Mathf.Clamp(selectedOption + dir, 0, count - 1);
        ShowOptions(node);
    }

    private void ShowCurrentNode()
    {
        if (dialogueNodes == null || currentNode < 0 || currentNode >= dialogueNodes.Count)
        {
            EndDialogue();
            return;
        }

        DialogueNode node = dialogueNodes[currentNode];
        awaitingChoice = false;
        waitingForInput = false;

        
        currentPopup?.Hide();
        npcPopup?.Hide();
        player1Popup?.Hide();
        player2Popup?.Hide();

        if (node.isNPC)
        {
            npcPopup?.ShowMessage(node.line, 999f);

            if (npcAnimator != null && AnimatorHasParameter(npcAnimator, talkParameter))
            {
                if (!npcAnimator.GetBool(talkParameter))
                    npcAnimator.SetBool(talkParameter, true);
            }
        }
        else
        {
            currentPopup?.ShowMessage(node.line, 999f);

            if (npcAnimator != null && AnimatorHasParameter(npcAnimator, talkParameter))
                npcAnimator.SetBool(talkParameter, false);
        }

        waitingForInput = true;
    }

    private void ShowOptions(DialogueNode node)
    {
        if (node.options == null || node.options.Count == 0) return;

        string selectedHex = ColorUtility.ToHtmlStringRGB(selectedColor);
        string unselectedHex = ColorUtility.ToHtmlStringRGB(unselectedColor);
        string optionsText = "";

        for (int i = 0; i < node.options.Count; i++)
        {
            if (i == selectedOption)
                optionsText += $"<color=#{selectedHex}>> {node.options[i].optionText}</color>\n";
            else
                optionsText += $"<color=#{unselectedHex}>  {node.options[i].optionText}</color>\n";
        }

        currentPopup?.ShowMessage(optionsText, 999f);
    }

    private void ChooseOption(int optionIndex)
    {
        if (dialogueNodes == null || dialogueNodes.Count == 0) { EndDialogue(); return; }

        DialogueNode node = dialogueNodes[currentNode];
        if (node.options == null || optionIndex < 0 || optionIndex >= node.options.Count)
        {
            EndDialogue();
            return;
        }

        currentPopup?.ShowMessage("", 0f);

        int nextIndex = node.options[optionIndex].nextNodeIndex;
        currentNode = nextIndex;
        awaitingChoice = false;
        ShowCurrentNode();
    }

    private void AdvanceDialogue()
    {
        if (dialogueNodes == null) { EndDialogue(); return; }

        DialogueNode node = dialogueNodes[currentNode];

        if (node.options != null && node.options.Count > 0)
        {
            awaitingChoice = true;
            selectedOption = 0;
            ShowOptions(node);
            return;
        }

        currentNode++;

        if (currentNode >= dialogueNodes.Count)
        {
            EndDialogue();
        }
        else
        {
            ShowCurrentNode();
        }
    }

    public void EndDialogue()
    {
        
        if (arrangeCoroutine != null)
        {
            StopCoroutine(arrangeCoroutine);
            arrangeCoroutine = null;
        }

        
        if (currentPopup != null)
        {
            currentPopup.Hide();
        }
        if (npcPopup != null)
        {
            npcPopup.Hide();
        }
        if (player1Popup != null)
        {
            player1Popup.Hide();
        }
        if (player2Popup != null)
        {
            player2Popup.Hide();
        }

        
        if (autoArrangeCanvases)
        {
            RestoreCanvasPositions();
        }

        
        DialogueManager.SetDialogueActive(false);
        isTalking = false;
        awaitingChoice = false;
        waitingForInput = false;
        currentNode = 0;

        
        if (cameraController != null)
            cameraController.EndDialogueCamera();

        
        if (npcAnimator != null && AnimatorHasParameter(npcAnimator, talkParameter))
            npcAnimator.SetBool(talkParameter, false);

        
        if (currentPlayerInput != null)
            currentPlayerInput.SwitchCurrentActionMap("Player");

        string playerTagToEnd = currentPlayerTag;

        currentPlayerInput = null;
        currentPlayerTag = "";
        currentPopup = null;

        
        if (progressive != null && !string.IsNullOrEmpty(playerTagToEnd))
            progressive.EndDialogue(playerTagToEnd);

        
        if (activePlayers.Count > 0)
            ShowPrompt(true);
    }

    private void ArrangeCanvasesForDialogue()
    {
        if (npcPopup == null || currentPopup == null) return;

        
        if (arrangeCoroutine != null)
        {
            StopCoroutine(arrangeCoroutine);
        }

        arrangeCoroutine = StartCoroutine(ArrangeCanvasesCoroutine());
    }

    private IEnumerator ArrangeCanvasesCoroutine()
    {
        yield return null;

        while (isTalking)
        {
            if (npcPopup == null || currentPopup == null) yield break;

            Camera cam = Camera.main;
            if (cam == null) yield break;

            Vector3 npcBasePos = transform.position;
            GameObject playerObj = GameObject.FindGameObjectWithTag(currentPlayerTag);
            if (playerObj == null) yield break;

            Vector3 playerBasePos = playerObj.transform.position;

            Vector2 npcCanvasSize = npcPopup.GetCanvasWorldSize();
            Vector2 playerCanvasSize = currentPopup.GetCanvasWorldSize();

            Vector3 npcToPlayer = playerBasePos - npcBasePos;
            npcToPlayer.y = 0f;

            if (npcToPlayer.magnitude < 0.01f)
            {
                yield return new WaitForSeconds(0.1f);
                continue;
            }

            npcToPlayer.Normalize();

            Vector3 cameraRight = cam.transform.right;
            cameraRight.y = 0f;
            cameraRight.Normalize();

            float dotProduct = Vector3.Dot(cameraRight, npcToPlayer);

            float requiredSeparation = (npcCanvasSize.x * 0.5f) + (playerCanvasSize.x * 0.5f) + minCanvasSeparation;
            requiredSeparation *= separationMultiplier;

            if (dotProduct > 0f)
            {
                npcPopup.SetLateralOffset(-requiredSeparation);
                currentPopup.SetLateralOffset(requiredSeparation);
            }
            else
            {
                npcPopup.SetLateralOffset(requiredSeparation);
                currentPopup.SetLateralOffset(-requiredSeparation);
            }

            yield return new WaitForSeconds(0.1f);
        }
    }

    private void RestoreCanvasPositions()
    {
        if (npcPopup != null)
            npcPopup.SetLateralOffset(0f);

        if (player1Popup != null)
            player1Popup.SetLateralOffset(0f);

        if (player2Popup != null)
            player2Popup.SetLateralOffset(0f);
    }

    private void ShowPrompt(bool state)
    {
        if (interactPromptCanvas == null) return;
        interactPromptCanvas.SetActive(state && activePlayers.Count > 0 && !DialogueManager.IsDialogueActive());
    }

    private bool AnimatorHasParameter(Animator a, string param)
    {
        if (a == null || string.IsNullOrEmpty(param)) return false;
        foreach (var p in a.parameters)
            if (p.name == param)
                return true;
        return false;
    }
}