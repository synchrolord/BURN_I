using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public sealed class UnlockTrigger2D : MonoBehaviour
{
    [SerializeField]
    private PlayerUnlocks2D.Item item;

    [Header("Message")]
    [SerializeField] private GameObject visual;
    [SerializeField] private GameObject messageBox;

    [SerializeField, Min(0.1f)]
    private float messageTime = 1.5f;

    private Collider2D trigger;
    private bool collected;


    private void Awake()
    {
        trigger = GetComponent<Collider2D>();
        trigger.isTrigger = true;

        if (messageBox != null)
            messageBox.SetActive(false);
    }


    private void Start()
    {
        //hide pickups collected before a scene reload
        if (PlayerUnlocks2D.Has(item))
            gameObject.SetActive(false);
    }


    private void OnTriggerEnter2D(Collider2D other)
    {
        if (collected ||
            other.GetComponentInParent<PlayerUnlocks2D>() == null)
        {
            return;
        }

        collected = true;
        PlayerUnlocks2D.Add(item);

        //remove the pickup but leave its message briefly
        trigger.enabled = false;

        if (visual != null)
            visual.SetActive(false);

        if (messageBox != null)
        {
            messageBox.SetActive(true);
            StartCoroutine(CloseMessage());
        }
        else
        {
            gameObject.SetActive(false);
        }
    }


    private IEnumerator CloseMessage()
    {
        yield return new WaitForSeconds(messageTime);

        gameObject.SetActive(false);
    }
}