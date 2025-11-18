using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public class InventoryHotbarUI : MonoBehaviour
{
    [Header("Referencias")]
    public PlayerInventory playerInventory;
    public Image[] hotbarSlots; // los 4 slots de la UI

    [Header("Sprites de ítems")]
    public Sprite cardIcon;
    public Sprite leverIcon;
    public Sprite keyIcon;
    public Sprite emptyIcon;

    private Dictionary<string, Sprite> itemSprites = new Dictionary<string, Sprite>();

    private void Awake()
    {
        // Crear el diccionario de íconos
        itemSprites["Card"] = cardIcon;
        itemSprites["Lever"] = leverIcon;
        itemSprites["Key"] = keyIcon;
    }

    private void Start()
    {
        RefreshHotbar();
    }

    // Este método será llamado desde PlayerInventory cuando se actualice
    public void RefreshHotbar()
    {
        if (playerInventory == null) return;
        if (hotbarSlots == null || hotbarSlots.Length == 0) return;

        var items = playerInventory.GetCollectedItems();
        var keyCards = playerInventory.GetCollectedKeyCards();

        // Combinar ambos tipos de ítems
        List<string> allItems = new List<string>();
        allItems.AddRange(items);
        allItems.AddRange(keyCards);

        for (int i = 0; i < hotbarSlots.Length; i++)
        {
            if (i < allItems.Count)
            {
                string itemID = allItems[i];
                if (itemSprites.ContainsKey(itemID))
                    hotbarSlots[i].sprite = itemSprites[itemID];
                else
                    hotbarSlots[i].sprite = emptyIcon;

                hotbarSlots[i].enabled = true;
            }
            else
            {
                hotbarSlots[i].sprite = emptyIcon;
                hotbarSlots[i].enabled = true;
            }
        }
    }
}
