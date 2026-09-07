using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using PlatformerGame.Core;

namespace PlatformerGame.Managers
{
    public enum GameState
    {
        Countdown,
        Playing,
        GameOver,
        Victory
    }

    /// <summary>
    /// Quản lý vòng đời trò chơi Platformer:
    /// - Quản lý trạng thái: Đếm ngược (3-2-1) -> Chơi -> Thua (GameOver) / Thắng (Victory).
    /// - Quản lý điểm số, số Coins thu thập.
    /// - Điều khiển khóa/mở di chuyển Player.
    /// - Chuyển Scene (Restart, Load Home, Load Adventure).
    /// </summary>
    public class PlatformerGameManager : MonoBehaviour
    {
        public static PlatformerGameManager Instance { get; private set; }

        [Header("--- Quản lý Tham chiếu (References) ---")]
        [Tooltip("PlayerController trong màn chơi")]
        [SerializeField] private PlayerController2D player;

        [Header("--- Cài đặt trò chơi (Settings) ---")]
        [Tooltip("Tên scene Menu chính")]
        [SerializeField] private string homeSceneName = "Scene_Home";
        [Tooltip("Tên scene Phiêu lưu phiêu lưu chơi chính")]
        [SerializeField] private string adventureSceneName = "Scene_Adventure";
        [Tooltip("Thời gian đếm ngược trước khi bắt đầu (giây)")]
        [SerializeField] private float countdownDuration = 3f;

        [Header("--- Trạng thái hiện tại (Runtime Data) ---")]
        [SerializeField] private GameState currentState = GameState.Countdown;
        [SerializeField] private int coinsCollected = 0;
        [SerializeField] private int gemsCollected = 0;
        [SerializeField] private int score = 0;
        [SerializeField] private float startPlayerX;

        // Events
        public event Action<GameState> OnStateChanged;
        public event Action<int> OnCoinChanged;
        public event Action<int> OnGemChanged;
        public event Action<int> OnScoreChanged;
        public event Action<string> OnCountdownTick; // "3", "2", "1", "READY GO!", ""

        public GameState CurrentState => currentState;
        public int CoinsCollected => coinsCollected;
        public int GemsCollected => gemsCollected;
        public int Score => score;
        public PlayerController2D Player => player;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            else
            {
                Destroy(gameObject);
                return;
            }

            if (player == null)
            {
                player = FindObjectOfType<PlayerController2D>();
            }
        }

        private void Start()
        {
            if (player != null)
            {
                startPlayerX = player.transform.position.x;
            }

            StartCoroutine(GameStartCountdownRoutine());
        }

        private void Update()
        {
            if (currentState == GameState.Playing && player != null)
            {
                // Tính điểm dựa theo quãng đường di chuyển sang phải + số xu thu thập
                float distance = Mathf.Max(0, player.transform.position.x - startPlayerX);
                int calculatedScore = Mathf.FloorToInt(distance * 10f) + (coinsCollected * 50);

                if (calculatedScore != score)
                {
                    score = calculatedScore;
                    OnScoreChanged?.Invoke(score);
                }
            }
        }

        #region Game Loop & States

        private IEnumerator GameStartCountdownRoutine()
        {
            SetState(GameState.Countdown);
            if (player != null)
            {
                player.SetMovementLocked(true);
            }

            // Đếm ngược 3, 2, 1
            int seconds = Mathf.RoundToInt(countdownDuration);
            while (seconds > 0)
            {
                OnCountdownTick?.Invoke(seconds.ToString());
                yield return new WaitForSeconds(1f);
                seconds--;
            }

            // READY GO!
            OnCountdownTick?.Invoke("READY GO!");
            yield return new WaitForSeconds(0.6f);
            OnCountdownTick?.Invoke(string.Empty);

            // Bắt đầu chơi
            SetState(GameState.Playing);
            if (player != null)
            {
                player.SetMovementLocked(false);
            }
        }

        public void SetGameOver()
        {
            if (currentState == GameState.GameOver || currentState == GameState.Victory) return;

            SetState(GameState.GameOver);
            if (player != null)
            {
                player.SetMovementLocked(true);
            }
        }

        public void SetVictory()
        {
            if (currentState == GameState.GameOver || currentState == GameState.Victory) return;

            SetState(GameState.Victory);
            if (player != null)
            {
                player.SetMovementLocked(true);
            }
        }

        public void TriggerVictory()
        {
            SetVictory();
        }

        private void SetState(GameState newState)
        {
            currentState = newState;
            OnStateChanged?.Invoke(currentState);
        }

        #endregion

        #region Coins & Scoring

        /// <summary>
        /// Thêm số Coin thu thập được
        /// </summary>
        public void AddCoin(int amount = 1)
        {
            coinsCollected += amount;
            OnCoinChanged?.Invoke(coinsCollected);
        }

        /// <summary>
        /// Thêm số Kim Cương (Gem) thu thập được
        /// </summary>
        public void AddGem(int amount = 1)
        {
            gemsCollected += amount;
            OnGemChanged?.Invoke(gemsCollected);
        }

        #endregion

        #region Scene Navigation

        /// <summary>
        /// Chơi lại màn chơi hiện tại
        /// </summary>
        public void RestartGame()
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        /// <summary>
        /// Trở về màn hình chính Menu
        /// </summary>
        public void LoadHomeScene()
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene(homeSceneName);
        }

        /// <summary>
        /// Tải màn chơi Adventure
        /// </summary>
        public void LoadAdventureScene()
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene(adventureSceneName);
        }

        #endregion
    }
}
