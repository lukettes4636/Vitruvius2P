using UnityEngine;
using UnityEngine.SceneManagement;

namespace Michsky.UI.Dark
{
    public class SceneController : MonoBehaviour
    {
        [Header("SCENE SETTINGS")]
        public string gameSceneName = "Game";

        [Header("TRANSITION SETTINGS")]
        public float transitionDelay = 0.5f;

        private bool isTransitioning = false;

        public void LoadGameScene()
        {
            if (isTransitioning == false)
            {
                isTransitioning = true;
                Invoke("LoadGame", transitionDelay);
            }
        }

        public void LoadGameSceneImmediate()
        {
            LoadGame();
        }

        public void LoadSceneByName(string sceneName)
        {
            if (string.IsNullOrEmpty(sceneName) == false)
            {
                SceneManager.LoadScene(sceneName);
            }
            else
            {

            }
        }

        public void LoadSceneByIndex(int sceneIndex)
        {
            if (sceneIndex >= 0 && sceneIndex < SceneManager.sceneCountInBuildSettings)
            {
                SceneManager.LoadScene(sceneIndex);
            }
            else
            {

            }
        }

        public void RestartCurrentScene()
        {
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }

        public void QuitGame()
        {

            Application.Quit();
        }

        public void QuitGameWithDelay()
        {
            if (isTransitioning == false)
            {
                isTransitioning = true;
                Invoke("QuitGame", transitionDelay);
            }
        }

        private void LoadGame()
        {
            SceneManager.LoadScene(gameSceneName);
        }
    }
}
