using UnityEngine;

[RequireComponent(typeof(BoxCollider2D))]
public sealed class ActivateTrigger2D : MonoBehaviour
{
    [SerializeField] private GameObject target;
    private bool activated;

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (activated ||
            other.GetComponentInParent<PlayerMotor2D>() == null)
        {
            return;
        }

        activated = true;

        //start the object when player reaches this area
        if (target != null)
            target.SetActive(true);
    }
}