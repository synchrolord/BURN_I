using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class StartScreen2D : MonoBehaviour
{
    [SerializeField] private string gameSceneName = "Floor_01";

    public void StartGame()
    {
        Debug.Log("Start button clicked");

        Time.timeScale = 1f;
        if (!Application.CanStreamedLevelBeLoaded(gameSceneName))
        {
            Debug.LogError("Start scene is missing from the build: " + gameSceneName, this);
            return;
        }
        SceneManager.LoadScene(gameSceneName);
    }
}
