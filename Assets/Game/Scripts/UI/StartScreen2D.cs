using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class StartScreen2D : MonoBehaviour
{
    [SerializeField] private string gameSceneName = "Level_Alpha";

    public void StartGame()
    {
        Debug.Log("Start button clicked");

        Time.timeScale = 1f;
        SceneManager.LoadScene("Level_Alpha");
    }
}