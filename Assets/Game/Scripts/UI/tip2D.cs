using TMPro;
using UnityEngine;

[RequireComponent(typeof(BoxCollider2D))]
public sealed class Tip2D : MonoBehaviour
{
    [SerializeField] private GameObject box;
    [SerializeField] private TMP_Text text;

    [TextArea]
    [SerializeField] private string message;


    private void Awake()
    {
        GetComponent<BoxCollider2D>().isTrigger = true;

        if (text != null)
            text.text = message;

        if (box != null)
            box.SetActive(false);
    }


    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.GetComponentInParent<PlayerMotor2D>() == null)
            return;

        if (box != null)
            box.SetActive(true);
    }


    private void OnTriggerExit2D(Collider2D other)
    {
        if (other.GetComponentInParent<PlayerMotor2D>() == null)
            return;

        if (box != null)
            box.SetActive(false);
    }
}