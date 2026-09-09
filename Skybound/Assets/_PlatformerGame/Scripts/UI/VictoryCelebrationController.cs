using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

namespace PlatformerGame.UI
{
    /// <summary>
    /// Điều khiển Màn hình Phá Đảo / Chiến Thắng (Scene_Victory):
    /// - Nhân vật nhảy tung tăng ăn mừng
    /// - Text 'Chúc mừng bạn đã phá đảo' với hiệu ứng bùm bùm (punch bounce, scale pulse, color wave)
    /// - Hiệu ứng pháo hoa / confetti rực rỡ
    /// - Nút Chơi Lại và Về Trang Chủ
    /// </summary>
    public class VictoryCelebrationController : MonoBehaviour
    {
        [Header("--- UI References ---")]
        [SerializeField] private TextMeshProUGUI titleText;
        [SerializeField] private TextMeshProUGUI subtitleText;
        [SerializeField] private TextMeshProUGUI congratsBannerText;
        [SerializeField] private Button restartButton;
        [SerializeField] private Button homeButton;

        [Header("--- Scene Navigation ---")]
        [SerializeField] private string firstStageSceneName = "Stage_1";
        [SerializeField] private string homeSceneName = "Scene_Home";

        private Vector3 initialTitleScale = Vector3.one;

        private void Awake()
        {
            EnsureValidEventSystem();

            if (restartButton != null)
            {
                restartButton.onClick.AddListener(OnRestartClicked);
            }

            if (homeButton != null)
            {
                homeButton.onClick.AddListener(OnHomeClicked);
            }

            if (titleText != null)
            {
                initialTitleScale = titleText.transform.localScale;
            }
        }

        private void Start()
        {
            StartCoroutine(BoomBoomTextAnimation());
            StartCoroutine(ConfettiColorPulse());
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

        /// <summary>
        /// Hiệu ứng text bùm bùm: phóng to, nhịp đập phập phồng, xoay nhẹ ăn mừng
        /// </summary>
        private IEnumerator BoomBoomTextAnimation()
        {
            float timer = 0f;
            while (true)
            {
                timer += Time.unscaledDeltaTime * 4f;

                if (titleText != null)
                {
                    // Hiệu ứng đập bùm bùm (Elastic Scale Pulse)
                    float scalePunch = 1f + 0.12f * Mathf.Abs(Mathf.Sin(timer));
                    float rotZ = Mathf.Sin(timer * 0.5f) * 3f;
                    titleText.transform.localScale = initialTitleScale * scalePunch;
                    titleText.transform.localRotation = Quaternion.Euler(0f, 0f, rotZ);
                }

                if (congratsBannerText != null)
                {
                    float waveScale = 1f + 0.08f * Mathf.Cos(timer * 0.8f);
                    congratsBannerText.transform.localScale = Vector3.one * waveScale;
                }

                yield return null;
            }
        }

        private IEnumerator ConfettiColorPulse()
        {
            Color[] colors = new Color[]
            {
                new Color(1f, 0.85f, 0.2f),   // Vàng rực rỡ
                new Color(0.2f, 0.85f, 1f),   // Xanh dương
                new Color(1f, 0.4f, 0.7f),    // Hồng neon
                new Color(0.3f, 1f, 0.4f),    // Xanh lá
                new Color(1f, 0.5f, 0.1f)     // Cam ấm
            };

            int colorIndex = 0;
            while (true)
            {
                Color targetCol = colors[colorIndex % colors.Length];
                colorIndex++;

                if (subtitleText != null)
                {
                    float t = 0f;
                    Color startCol = subtitleText.color;
                    while (t < 1f)
                    {
                        t += Time.unscaledDeltaTime * 2.5f;
                        subtitleText.color = Color.Lerp(startCol, targetCol, t);
                        yield return null;
                    }
                }
                else
                {
                    yield return new WaitForSecondsRealtime(0.5f);
                }
            }
        }

        public void OnRestartClicked()
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene(firstStageSceneName);
        }

        public void OnHomeClicked()
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene(homeSceneName);
        }
    }
}