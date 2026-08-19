using UnityEngine;
using UnityEngine.UI;

namespace Core
{
    [RequireComponent(typeof(Button))]
    public class ReturnToMenu : MonoBehaviour
    {
        [Header("Target")]
        [SerializeField] private string mainMenuSceneName = "MainMenu";

        private Button button;

        private void Awake()
        {
            button = GetComponent<Button>();
            if (button != null)
            {
                button.onClick.AddListener(OnReturnClicked);
            }
        }

        private void OnDestroy()
        {
            if (button != null)
            {
                button.onClick.RemoveListener(OnReturnClicked);
            }
        }

        public void OnReturnClicked()
        {
            if (SceneController.Instance != null)
            {
                SceneController.Instance.LoadScene(mainMenuSceneName);
            }
            else
            {
                SceneController controller = FindFirstObjectByType<SceneController>();
                if (controller != null)
                {
                    controller.LoadScene(mainMenuSceneName);
                }
                else
                {
                    UnityEngine.SceneManagement.SceneManager.LoadScene(mainMenuSceneName);
                }
            }
        }
    }
}
