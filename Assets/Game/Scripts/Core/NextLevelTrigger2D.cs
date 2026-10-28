using UnityEngine;
using UnityEngine.SceneManagement;

[RequireComponent(typeof(Collider2D))]
public sealed class NextLevelTrigger2D : MonoBehaviour
{
    [SerializeField] private string nextScene;

    private bool loading;


    private void Awake()
    {
        GetComponent<Collider2D>().isTrigger = true;
    }


    private void OnTriggerEnter2D(Collider2D other)
    {
        if (loading ||
            string.IsNullOrWhiteSpace(nextScene) ||
            other.GetComponentInParent<PlayerMotor2D>() == null)
        {
            return;
        }

        loading = true;
        SceneManager.LoadScene(nextScene);
    }


    private void OnDrawGizmos()
    {
        Gizmos.color = Color.green;
        Gizmos.DrawWireCube(transform.position, new Vector3(2f, 4f, 0f));
    }
}
