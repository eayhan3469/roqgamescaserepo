using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

namespace Core
{
    [RequireComponent(typeof(Button))]
    public class RestartSceneButton : MonoBehaviour
    {
        private Button button;

        private void Awake()
        {
            button = GetComponent<Button>();
            if (button != null)
            {
                button.onClick.AddListener(OnRestartClicked);
            }
        }

        private void OnDestroy()
        {
            if (button != null)
            {
                button.onClick.RemoveListener(OnRestartClicked);
            }
        }

        public void OnRestartClicked()
        {
            string currentSceneName = SceneManager.GetActiveScene().name;
            if (SceneController.Instance != null)
            {
                SceneController.Instance.LoadScene(currentSceneName);
            }
            else
            {
                SceneController controller = FindFirstObjectByType<SceneController>();
                if (controller != null)
                {
                    controller.LoadScene(currentSceneName);
                }
                else
                {
                    SceneManager.LoadScene(currentSceneName);
                }
            }
        }
    }
}
