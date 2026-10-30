using System.Collections.Generic;
using UnityEngine;

public sealed class PlayerUnlocks2D : MonoBehaviour
{
    public enum Item
    {
        Water,
        Flame,
        Pilot,
        Jet,
        Wide,
        Grenade
    }

    [SerializeField]
    private Item[] startingItems =
    {
        Item.Water,
        Item.Flame,
        Item.Jet
    };

    private static readonly HashSet<Item> items =
        new HashSet<Item>();

    private static bool started;


    //reset unlocks only when starting a new game session
    [RuntimeInitializeOnLoadMethod(
        RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetItems()
    {
        items.Clear();
        started = false;
    }


    private void Awake()
    {
        //new players made after changing scenes use the same unlocks
        if (started)
            return;

        started = true;

        foreach (Item item in startingItems)
            items.Add(item);
    }


    public static bool Has(Item item)
    {
        return items.Contains(item);
    }


    public static void Add(Item item)
    {
        items.Add(item);
    }
}
