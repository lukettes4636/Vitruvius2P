using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class DialogueOption
{
    public string optionText;
    public int nextNodeIndex;
}

[System.Serializable]
public class DialogueNode
{
    [TextArea(2, 4)]
    public string line;
    public bool isNPC = true;
    public List<DialogueOption> options = new List<DialogueOption>();
}
