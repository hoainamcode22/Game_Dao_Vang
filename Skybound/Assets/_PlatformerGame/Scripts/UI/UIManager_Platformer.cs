using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using PlatformerGame.Core;
using PlatformerGame.Managers;

namespace PlatformerGame.UI
{
    /// <summary>
    /// Quản lý toàn bộ giao diện Canvas HUD và Popups trong màn chơi Platformer:
    /// - 5 Tim hiển thị máu
    /// - Đếm số Coin thu thập
    /// - Hiển thị điểm / khoảng cách
    /// - Đếm ngược 3, 2, 1, READY GO! với hiệu ứng Scale & Fade
    /// - Nút điều khiển ảo Mobile: Trái, Phải, Nhảy
    /// - Popup Game Over & Victory (Hiện điểm, xu, nút Chơi lại, nút Về Menu)
    /// </summary>
    public class UIManager_Platformer : MonoBehaviour
    {
        [Header("--- Tham chiếu Đối tượng (References) ---")]
        [SerializeField] private PlayerController2D playerController;
        [SerializeField] private PlayerHealth playerHealth;

        [Header("--- HUD Tim máu (Hearts HUD) ---")]
        [Tooltip("Danh sách các Image hiển thị tim (5 tim)")]
        [SerializeField] private List<Image> heartImages = new List<Image>();
        [Tooltip("Sprite tim đầy (đỏ)")]
        [SerializeField] private Sprite fullHeartSprite;
        [Tooltip("Sprite tim rỗng hoặc mờ (khi mất máu)")]
        [SerializeField] private Sprite emptyHeartSprite;

        [Header("--- HUD Coins & Score ---")]
        [SerializeField] private TMP_Text coinText;
        [SerializeField] private TMP_Text scoreText;

        [Header("--- HUD Chìa Khóa & Thông báo Stage ---")]
        [SerializeField] private Image keyIconImage;
        [SerializeField] private Sprite keyActiveSprite;
        [SerializeField] private Sprite keyInactiveSprite;
        [SerializeField] private GameObject bannerContainer;
        [SerializeField] private TMP_Text bannerText;

        [Header("--- Đếm ngược (Countdown) ---")]
        [SerializeField] private TMP_Text countdownText;
        [SerializeField] private GameObject countdownContainer;

        [Header("--- Điều khiển Mobile (Mobile Controls) ---")]
        [SerializeField] private GameObject mobileControlsPanel;
        [SerializeField] private MobileTouchButton leftButton;
        [SerializeField] private MobileTouchButton rightButton;
        [SerializeField] private MobileTouchButton jumpButton;

        [Header("--- Popup Game Over ---")]
        [SerializeField] private GameObject gameOverPanel;
        [SerializeField] private TMP_Text gameOverScoreText;
        [SerializeField] private TMP_Text gameOverCoinText;
        [SerializeField] private Button gameOverRestartButton;
        [SerializeField] private Button gameOverHomeButton;

        [Header("--- Popup Victory ---")]
        [SerializeField] private GameObject victoryPanel;
        [SerializeField] private TMP_Text victoryScoreText;
        [SerializeField] private TMP_Text victoryCoinText;
        [SerializeField] private Button victoryRestartButton;
        [SerializeField] private Button victoryHomeButton;

        private Coroutine countdownCoroutine;

        private void Awake()
        {
            // Tự động tìm Player nếu chưa gán
            if (playerController == null) playerController = FindObjectOfType<PlayerController2D>();
            if (playerHealth == null) playerHealth = FindObjectOfType<PlayerHealth>();

            // Đảm bảo EventSystem tương thích với New Input System
            EnsureValidEventSystem();

            // Gán sự kiện cho các nút Game Over / Victory
            if (gameOverRestartButton != null)
                gameOverRestartButton.onClick.AddListener(OnRestartClicked);
            if (gameOverHomeButton != null)
                gameOverHomeButton.onClick.AddListener(OnHomeClicked);

            if (victoryRestartButton != null)
                victoryRestartButton.onClick.AddListener(OnRestartClicked);
            if (victoryHomeButton != null)
                victoryHomeButton.onClick.AddListener(OnHomeClicked);

            // Ẩn các popup khi khởi tạo
            if (gameOverPanel != null) gameOverPanel.SetActive(false);
            if (victoryPanel != null) victoryPanel.SetActive(false);
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

        private void Start()
        {
            // Đăng ký sự kiện từ PlayerHealth
            if (playerHealth != null)
            {
                playerHealth.OnHealthChanged += UpdateHeartsHUD;
                UpdateHeartsHUD(playerHealth.CurrentHealth, playerHealth.MaxHealth);
            }

            // Đăng ký sự kiện từ PlatformerGameManager
            if (PlatformerGameManager.Instance != null)
            {
                PlatformerGameManager.Instance.OnCoinChanged += UpdateCoinHUD;
                PlatformerGameManager.Instance.OnScoreChanged += UpdateScoreHUD;
                PlatformerGameManager.Instance.OnStateChanged += HandleGameStateChanged;
                PlatformerGameManager.Instance.OnCountdownTick += ShowCountdownTick;

                UpdateCoinHUD(PlatformerGameManager.Instance.CoinsCollected);
                UpdateScoreHUD(PlatformerGameManager.Instance.Score);
            }

            // Đăng ký sự kiện từ StageManager
            if (StageManager.Instance != null)
            {
                StageManager.Instance.OnKeyStatusChanged += UpdateKeyHUD;
                StageManager.Instance.OnMessageDisplayed += ShowBannerMessage;
                UpdateKeyHUD(StageManager.Instance.HasKeyCurrentStage);
            }

            // Đăng ký sự kiện nút bấm ảo Mobile
            SetupMobileButtons();
        }

        private void Update()
        {
            HandleMobileInputPolling();
        }

        private void OnDestroy()
        {
            if (playerHealth != null)
            {
                playerHealth.OnHealthChanged -= UpdateHeartsHUD;
            }

            if (PlatformerGameManager.Instance != null)
            {
                PlatformerGameManager.Instance.OnCoinChanged -= UpdateCoinHUD;
                PlatformerGameManager.Instance.OnScoreChanged -= UpdateScoreHUD;
                PlatformerGameManager.Instance.OnStateChanged -= HandleGameStateChanged;
                PlatformerGameManager.Instance.OnCountdownTick -= ShowCountdownTick;
            }

            if (StageManager.Instance != null)
            {
                StageManager.Instance.OnKeyStatusChanged -= UpdateKeyHUD;
                StageManager.Instance.OnMessageDisplayed -= ShowBannerMessage;
            }
        }

        #region Hearts HUD

        public void UpdateHeartsHUD(int currentHealth, int maxHealth)
        {
            for (int i = 0; i < heartImages.Count; i++)
            {
                if (heartImages[i] == null) continue;

                if (i < maxHealth)
                {
                    heartImages[i].gameObject.SetActive(true);
                    if (i < currentHealth)
                    {
                        // Tim còn sống
                        if (fullHeartSprite != null)
                        {
                            heartImages[i].sprite = fullHeartSprite;
                            heartImages[i].color = Color.white;
                        }
                        else
                        {
                            heartImages[i].color = Color.red;
                        }
                    }
                    else
                    {
                        // Tim đã mất
                        if (emptyHeartSprite != null)
                        {
                            heartImages[i].sprite = emptyHeartSprite;
                            heartImages[i].color = Color.white;
                        }
                        else
                        {
                            heartImages[i].color = new Color(0.3f, 0.3f, 0.3f, 0.35f);
                        }
                    }
                }
                else
                {
                    heartImages[i].gameObject.SetActive(false);
                }
            }
        }

        #endregion

        #region Coins & Score HUD

        public void UpdateCoinHUD(int coins)
        {
            if (coinText != null)
            {
                coinText.text = coins.ToString("D2");
            }
        }

        public void UpdateScoreHUD(int score)
        {
            if (scoreText != null)
            {
                scoreText.text = $"SCORE: {score:N0}";
            }
        }

        #endregion

        #region Key & Banner HUD

        private Coroutine bannerCoroutine;

        public void UpdateKeyHUD(bool hasKey)
        {
            if (keyIconImage != null)
            {
                if (hasKey)
                {
                    if (keyActiveSprite != null) keyIconImage.sprite = keyActiveSprite;
                    keyIconImage.color = Color.white;
                    keyIconImage.transform.localScale = Vector3.one * 1.2f;
                }
                else
                {
                    if (keyInactiveSprite != null) keyIconImage.sprite = keyInactiveSprite;
                    keyIconImage.color = new Color(0.3f, 0.3f, 0.3f, 0.4f);
                    keyIconImage.transform.localScale = Vector3.one;
                }
            }
        }

        public void ShowBannerMessage(string message)
        {
            if (bannerText != null)
            {
                bannerText.text = message;
            }
            if (bannerContainer != null)
            {
                if (bannerCoroutine != null) StopCoroutine(bannerCoroutine);
                bannerCoroutine = StartCoroutine(BannerRoutine());
            }
        }

        private IEnumerator BannerRoutine()
        {
            bannerContainer.SetActive(true);
            yield return new WaitForSeconds(3f);
            bannerContainer.SetActive(false);
        }

        #endregion

        #region Countdown Animation

        public void ShowCountdownTick(string text)
        {
            if (countdownText == null) return;

            if (string.IsNullOrEmpty(text))
            {
                if (countdownContainer != null) countdownContainer.SetActive(false);
                else countdownText.gameObject.SetActive(false);
                return;
            }

            if (countdownContainer != null) countdownContainer.SetActive(true);
            else countdownText.gameObject.SetActive(true);

            countdownText.text = text;

            if (countdownCoroutine != null) StopCoroutine(countdownCoroutine);
            countdownCoroutine = StartCoroutine(AnimateCountdownText());
        }

        private IEnumerator AnimateCountdownText()
        {
            Transform textTransform = countdownText.transform;
            Vector3 startScale = Vector3.one * 1.5f;
            Vector3 targetScale = Vector3.one * 1.0f;
            textTransform.localScale = startScale;

            Color originalColor = countdownText.color;
            originalColor.a = 1f;
            countdownText.color = originalColor;

            float duration = 0.5f;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                textTransform.localScale = Vector3.Lerp(startScale, targetScale, Mathf.SmoothStep(0, 1, t));
                yield return null;
            }

            textTransform.localScale = targetScale;
        }

        #endregion

        #region Mobile Controls

        private void SetupMobileButtons()
        {
            if (jumpButton != null)
            {
                jumpButton.OnButtonDown += () =>
                {
                    if (playerController != null) playerController.SetVirtualJumpDown();
                };
                jumpButton.OnButtonUp += () =>
                {
                    if (playerController != null) playerController.SetVirtualJumpUp();
                };
            }
        }

        private void HandleMobileInputPolling()
        {
            if (playerController == null) return;

            float horizontal = 0f;
            if (leftButton != null && leftButton.IsPressed) horizontal -= 1f;
            if (rightButton != null && rightButton.IsPressed) horizontal += 1f;

            playerController.SetVirtualHorizontal(horizontal);
        }

        #endregion

        #region Game State & Popups

        private void HandleGameStateChanged(GameState state)
        {
            switch (state)
            {
                case GameState.GameOver:
                    ShowGameOverPopup();
                    break;
                case GameState.Victory:
                    ShowVictoryPopup();
                    break;
            }
        }

        private void ShowGameOverPopup()
        {
            if (gameOverPanel != null)
            {
                gameOverPanel.SetActive(true);

                if (gameOverScoreText != null && PlatformerGameManager.Instance != null)
                    gameOverScoreText.text = $"SCORE: {PlatformerGameManager.Instance.Score:N0}";

                if (gameOverCoinText != null && PlatformerGameManager.Instance != null)
                    gameOverCoinText.text = $"COINS: {PlatformerGameManager.Instance.CoinsCollected}";
            }
        }

        private void ShowVictoryPopup()
        {
            if (victoryPanel != null)
            {
                victoryPanel.SetActive(true);

                if (victoryScoreText != null && PlatformerGameManager.Instance != null)
                    victoryScoreText.text = $"SCORE: {PlatformerGameManager.Instance.Score:N0}";

                if (victoryCoinText != null && PlatformerGameManager.Instance != null)
                    victoryCoinText.text = $"COINS: {PlatformerGameManager.Instance.CoinsCollected}";
            }
        }

        private void OnRestartClicked()
        {
            if (PlatformerGameManager.Instance != null)
            {
                PlatformerGameManager.Instance.RestartGame();
            }
        }

        private void OnHomeClicked()
        {
            if (PlatformerGameManager.Instance != null)
            {
                PlatformerGameManager.Instance.LoadHomeScene();
            }
        }

        #endregion
    }
}
