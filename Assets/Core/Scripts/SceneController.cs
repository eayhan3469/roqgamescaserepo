using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Core
{
    public class SceneController : MonoBehaviour
    {
        public static SceneController Instance { get; private set; }

        [Header("Juice & Transition Settings")]
        [SerializeField] private CanvasGroup transitionCanvasGroup;
        [SerializeField] private float fadeDuration = 0.25f;
        [SerializeField] private bool dontDestroyOnLoad = false;

        private bool isLoading = false;

        // Fallback mapping for scene names/aliases
        private static readonly Dictionary<string, string> SceneNameMap = new Dictionary<string, string>
        {
            { "1_FitTheShape", "FitTheShape" },
            { "Case1_FitTheShape", "FitTheShape" },
            { "FitTheShape", "FitTheShape" },
            { "2_BlockHole", "BlockHole" },
            { "Case2_BlockHole", "BlockHole" },
            { "BlockHole", "BlockHole" },
            { "3_Stickerdom", "Stickerdom" },
            { "Case3_Stickerdom", "Stickerdom" },
            { "Stickerdom", "Stickerdom" },
            { "4_Buca", "Buca" },
            { "Case4_Buca", "Buca" },
            { "Buca", "Buca" },
            { "MainMenu", "MainMenu" }
        };

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                if (dontDestroyOnLoad)
                {
                    DontDestroyOnLoad(gameObject);
                }
            }
            else if (Instance != this && dontDestroyOnLoad)
            {
                Destroy(gameObject);
            }
        }

        public void LoadScene(string sceneName)
        {
            if (isLoading) return;
            StartCoroutine(LoadSceneRoutine(sceneName));
        }

        private IEnumerator LoadSceneRoutine(string sceneName)
        {
            isLoading = true;

            string targetScene = sceneName;
            if (SceneNameMap.TryGetValue(sceneName, out string mappedName))
            {
                if (Application.CanStreamedLevelBeLoaded(mappedName))
                {
                    targetScene = mappedName;
                }
                else if (Application.CanStreamedLevelBeLoaded(sceneName))
                {
                    targetScene = sceneName;
                }
            }

            // Fade out if canvas group available
            if (transitionCanvasGroup != null)
            {
                transitionCanvasGroup.blocksRaycasts = true;
                float elapsed = 0f;
                while (elapsed < fadeDuration)
                {
                    elapsed += Time.unscaledDeltaTime;
                    transitionCanvasGroup.alpha = Mathf.Clamp01(elapsed / fadeDuration);
                    yield return null;
                }
                transitionCanvasGroup.alpha = 1f;
            }

            AsyncOperation asyncOp = SceneManager.LoadSceneAsync(targetScene);
            if (asyncOp != null)
            {
                while (!asyncOp.isDone)
                {
                    yield return null;
                }
            }
            else
            {
                Debug.LogWarning($"[SceneController] Could not load scene '{targetScene}' asynchronously. Trying fallback direct load.");
                SceneManager.LoadScene(targetScene);
            }

            // Fade in if canvas group available
            if (transitionCanvasGroup != null)
            {
                float elapsed = 0f;
                while (elapsed < fadeDuration)
                {
                    elapsed += Time.unscaledDeltaTime;
                    transitionCanvasGroup.alpha = 1f - Mathf.Clamp01(elapsed / fadeDuration);
                    yield return null;
                }
                transitionCanvasGroup.alpha = 0f;
                transitionCanvasGroup.blocksRaycasts = false;
            }

            isLoading = false;
        }
    }
}
