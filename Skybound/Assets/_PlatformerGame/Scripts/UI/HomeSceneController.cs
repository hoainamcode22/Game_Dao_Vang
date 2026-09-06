using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

namespace PlatformerGame.UI
{
    /// <summary>
    /// Điều khiển màn hình Menu chính (Scene_Home):
    /// - Nút Play Adventure: Bắt đầu vào màn chơi phiêu lưu
    /// - Nút Quit: Thoát game
    /// </summary>
    public class HomeSceneController : MonoBehaviour
    {
        [Header("--- Scene Configuration ---")]
        [Tooltip("Tên scene gameplay")]
        [SerializeField] private string adventureSceneName = "Scene_Adventure";

        [Header("--- UI Buttons ---")]
        [SerializeField] private Button playButton;
        [SerializeField] private Button quitButton;

        private void Awake()
        {
            EnsureValidEventSystem();

            if (playButton != null)
            {
                playButton.onClick.AddListener(OnPlayClicked);
            }

            if (quitButton != null)
            {
                quitButton.onClick.AddListener(OnQuitClicked);
            }
        }

        private void EnsureValidEventSystem()
        {
            var es = FindObjectOfType<UnityEngine.EventSystems.EventSystem>();
            if (es == null) return;

#if ENABLE_INPUT_SYSTEM
            var standalone = es.GetComponent<UnityEngine.EventSystems.StandaloneInputModule>();
            if (standalone != null)
            {
                Destroy(standalone);
            }
            if (es.GetComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>() == null)
            {
                es.gameObject.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
            }
#endif
        }

        public void OnPlayClicked()
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene(adventureSceneName);
        }

        public void OnQuitClicked()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
