using UnityEngine;

[RequireComponent(typeof(BoxCollider2D))]
public sealed class LiquidValve2D : MonoBehaviour
{
    [SerializeField] private LiquidSource2D source;
    private bool playerInside;


    private void Awake()
    {
        GetComponent<BoxCollider2D>().isTrigger = true;
    }


    private void OnTriggerEnter2D(Collider2D other)
    {
        if (playerInside ||
            other.GetComponentInParent<PlayerMotor2D>() == null)
        {
            return;
        }

        playerInside = true;
        Trigger();
    }
    

    private void OnTriggerExit2D(Collider2D other)
    {
        if (other.GetComponentInParent<PlayerMotor2D>() != null)
            playerInside = false;
    }


    //flip the attached source on or off
    public void Trigger()
    {
        if (source != null)
            source.Toggle();
    }
}