using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

namespace PlatformerGame.UI
{
    /// <summary>
    /// Điều khiển màn hình Menu chính (Scene_Home):
    /// - Nút Play Adventure: Bắt đầu vào màn chơi phiêu lưu (Stage_1)
    /// - Nút Chọn Màn: Vào màn chơi
    /// - Nút Audio: Bật/Tắt âm thanh
    /// - Nút Quit: Thoát game
    /// </summary>
    public class HomeSceneController : MonoBehaviour
    {
        [Header("--- Scene Configuration ---")]
        [Tooltip("Tên scene gameplay")]
        [SerializeField] private string adventureSceneName = "Stage_1";

        [Header("--- UI Buttons ---")]
        [SerializeField] private Button playButton;
        [SerializeField] private Button selectStageButton;
        [SerializeField] private Button quitButton;
        [SerializeField] private Button audioButton;

        [Header("--- Audio Toggle ---")]
        [SerializeField] private Image audioIconImage;

        private bool isAudioMuted = false;

        private void Awake()
        {
            EnsureValidEventSystem();

            if (playButton != null)
            {
                playButton.onClick.AddListener(OnPlayClicked);
            }

            if (selectStageButton != null)
            {
                selectStageButton.onClick.AddListener(OnPlayClicked);
            }

            if (quitButton != null)
            {
                quitButton.onClick.AddListener(OnQuitClicked);
            }

            if (audioButton != null)
            {
                audioButton.onClick.AddListener(OnAudioToggleClicked);
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

        public void OnAudioToggleClicked()
        {
            isAudioMuted = !isAudioMuted;
            AudioListener.pause = isAudioMuted;
            if (audioIconImage != null)
            {
                audioIconImage.color = isAudioMuted ? new Color(1f, 0.4f, 0.4f, 0.6f) : Color.white;
            }
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