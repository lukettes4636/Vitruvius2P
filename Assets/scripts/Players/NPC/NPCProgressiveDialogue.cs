using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class DialogueSet
{
    public string setName;
    public List<DialogueNode> dialogueNodes;
}

public class NPCProgressiveDialogue : MonoBehaviour
{
    [Header("Dialogue Sets")]
    public List<DialogueSet> dialogueSets = new List<DialogueSet>();

    
    private Dictionary<string, int> playerDialogueSetIndex = new Dictionary<string, int>();
    private Dictionary<string, bool> playerHasInteractedOnce = new Dictionary<string, bool>();

    public List<DialogueNode> GetCurrentDialogue(string playerTag)
    {
        if (dialogueSets == null || dialogueSets.Count == 0)
            return new List<DialogueNode>();

        
        int setIndex = 0;
        if (playerDialogueSetIndex.ContainsKey(playerTag))
        {
            setIndex = playerDialogueSetIndex[playerTag];
        }
        else
        {
            
            playerDialogueSetIndex[playerTag] = 0;
            setIndex = 0;
        }

        return dialogueSets[Mathf.Clamp(setIndex, 0, dialogueSets.Count - 1)].dialogueNodes;
    }

    public void AdvanceDialogueSet(string playerTag)
    {
        if (!playerDialogueSetIndex.ContainsKey(playerTag))
            playerDialogueSetIndex[playerTag] = 0;

        int currentIndex = playerDialogueSetIndex[playerTag];
        if (currentIndex < dialogueSets.Count - 1)
        {
            playerDialogueSetIndex[playerTag] = currentIndex + 1;
        }
    }

    public void ResetDialogueSet(string playerTag)
    {
        playerDialogueSetIndex[playerTag] = 0;
        playerHasInteractedOnce[playerTag] = false;
    }

    public void StartDialogue(NPCDialogueSystem system)
    {
        
    }

    public void EndDialogue(string playerTag)
    {
        AdvanceDialogueSet(playerTag);
        playerHasInteractedOnce[playerTag] = true; 
    }

    public bool HasInteractedOnce(string playerTag)
    {
        if (!playerHasInteractedOnce.ContainsKey(playerTag))
            return false;
        return playerHasInteractedOnce[playerTag];
    }
    
    
    public List<DialogueNode> GetCurrentDialogue()
    {
        return GetCurrentDialogue("Player1"); 
    }
    
    public bool HasInteractedOnce()
    {
        return HasInteractedOnce("Player1"); 
    }
}
