using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

[RequireComponent(typeof(Collider2D))]
public sealed class SoundTrigger2D : MonoBehaviour
{
    [Header("Sound Settings")]
    [SerializeField] private AudioClip sound;

    [SerializeField, Range(0f, 10f)]
    private float volume = 5f;

    [SerializeField] private bool use2DSound = true;

    [Header("Scene Reset")]
    [SerializeField] private bool resetCurrentScene;
    [SerializeField, Min(0f)] private float extraResetDelay;

    private bool hasTriggered;

    private void Awake()
    {
        GetComponent<Collider2D>().isTrigger = true;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (hasTriggered ||
            other.GetComponentInParent<PlayerMotor2D>() == null)
            return;

        hasTriggered = true;

        if (sound != null)
        {
            GameObject audioObject = new GameObject("LOUD Trigger Sound");
            audioObject.transform.position = transform.position;

            AudioSource source = audioObject.AddComponent<AudioSource>();

            source.clip = sound;
            source.spatialBlend = use2DSound ? 0f : 1f;
            source.volume = 1f;
            source.playOnAwake = false;

            // Allow volume values above the normal range
            source.PlayOneShot(sound, volume);

            Destroy(audioObject, sound.length + 1f);
        }

        if (resetCurrentScene)
            StartCoroutine(ReloadAfterSound());
    }

    private IEnumerator ReloadAfterSound()
    {
        float delay = (sound != null ? sound.length : 0f)
                      + extraResetDelay;

        yield return new WaitForSeconds(delay);

        SceneManager.LoadScene(
            SceneManager.GetActiveScene().buildIndex
        );
    }
}