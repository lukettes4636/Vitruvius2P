using UnityEngine;

public class DialogueManager : MonoBehaviour
{
    private static DialogueManager instance;
    private bool dialogueActive = false;

    void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }
        instance = this;
    }

    public static bool IsDialogueActive()
    {
        return instance != null && instance.dialogueActive;
    }

    public static void SetDialogueActive(bool active)
    {
        if (instance == null) return;
        instance.dialogueActive = active;
    }
}
