using UnityEngine;

namespace Core
{
    public class MainMenuManager : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private SceneController sceneController;

        private void Awake()
        {
            if (sceneController == null)
            {
                sceneController = FindFirstObjectByType<SceneController>();
            }
        }

        public void LoadFitTheShape()
        {
            LoadScene("1_FitTheShape");
        }

        public void LoadBlockHole()
        {
            LoadScene("2_BlockHole");
        }

        public void LoadStickerdom()
        {
            LoadScene("3_Stickerdom");
        }

        public void LoadBuca()
        {
            LoadScene("4_Buca");
        }

        public void LoadCase(string caseName)
        {
            LoadScene(caseName);
        }

        public void LoadScene(string sceneName)
        {
            if (sceneController != null)
            {
                sceneController.LoadScene(sceneName);
            }
            else if (SceneController.Instance != null)
            {
                SceneController.Instance.LoadScene(sceneName);
            }
            else
            {
                UnityEngine.SceneManagement.SceneManager.LoadScene(sceneName);
            }
        }
    }
}
