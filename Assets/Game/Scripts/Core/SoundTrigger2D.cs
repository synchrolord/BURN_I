using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

[RequireComponent(typeof(Collider2D))]
public sealed class SoundTrigger2D : MonoBehaviour
{
    [SerializeField] private AudioClip sound;
    [SerializeField] private bool resetCurrentScene;
    [SerializeField, Min(0f)] private float extraResetDelay;

    private bool hasTriggered;

    private void Awake()
    {
        GetComponent<Collider2D>().isTrigger = true;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (hasTriggered || other.GetComponentInParent<PlayerMotor2D>() == null)
            return;

        hasTriggered = true;

        if (sound != null)
            AudioSource.PlayClipAtPoint(sound, transform.position);

        if (resetCurrentScene)
            StartCoroutine(ReloadAfterSound());
    }

    private IEnumerator ReloadAfterSound()
    {
        float delay = (sound != null ? sound.length : 0f) + extraResetDelay;
        yield return new WaitForSeconds(delay);
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }
}
