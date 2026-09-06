using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using PlatformerGame.Core;

namespace PlatformerGame.Managers
{
    /// <summary>
    /// Quản lý tiến trình 8 Màn chơi liên hoàn (Stage 1 -> Stage 8),
    /// xử lý Chìa khóa từng màn, hiển thị Tên Stage, dịch chuyển nhân vật và kích hoạt Chiến Thắng.
    /// </summary>
    public class StageManager : MonoBehaviour
    {
        public static StageManager Instance { get; private set; }

        [Header("--- Tiến trình Màn chơi ---")]
        [Tooltip("Stage hiện tại (1 đến 8)")]
        [SerializeField] private int currentStageIndex = 1;
        [Tooltip("Đã nhặt chìa khóa của màn hiện tại chưa")]
        [SerializeField] private bool hasKey = false;

        [Header("--- Danh sách Tên 8 Màn Chơi ---")]
        [SerializeField] private string[] stageNames = new string[]
        {
            "Stage 1: Thung Lũng Cỏ Xanh",
            "Stage 2: Hang Đất Nâu & Bục Gỗ",
            "Stage 3: Thung Lũng Lò Xo",
            "Stage 4: Hẻm Núi Chông Gai",
            "Stage 5: Đỉnh Mây Trời",
            "Stage 6: Hầm Ngục Cưa Xoay",
            "Stage 7: Mê Cung Ống Nước",
            "Stage 8: Pháo Đài Tối Thượng"
        };

        // Events
        public event Action<int, string> OnStageChanged; // (stageIndex, stageName)
        public event Action<bool> OnKeyStatusChanged;    // (hasKey)
        public event Action<string> OnMessageDisplayed;  // (message)

        public int CurrentStageIndex => currentStageIndex;
        public bool HasKeyCurrentStage => hasKey;
        public string CurrentStageName => (currentStageIndex >= 1 && currentStageIndex <= stageNames.Length) 
            ? stageNames[currentStageIndex - 1] 
            : $"Stage {currentStageIndex}";

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            else
            {
                Destroy(gameObject);
            }
        }

        private void Start()
        {
            // Bắt đầu từ Stage 1
            hasKey = false;
            OnKeyStatusChanged?.Invoke(false);
            ShowStageBanner(currentStageIndex);
        }

        /// <summary>
        /// Nhặt chìa khóa của màn hiện tại
        /// </summary>
        public void CollectKey(int stageIndex)
        {
            hasKey = true;
            OnKeyStatusChanged?.Invoke(true);
            ShowMessage("🔑 ĐÃ NHẶT ĐƯỢC CHÌA KHÓA! Hãy đến Cửa để qua màn.");
        }

        /// <summary>
        /// Kiểm tra xem người chơi có chìa khóa cho stage này không
        /// </summary>
        public bool HasKey(int stageIndex)
        {
            return hasKey;
        }

        /// <summary>
        /// Tiến sang Stage tiếp theo
        /// </summary>
        public void AdvanceToStage(int nextStageIndex, Transform nextSpawnPoint)
        {
            currentStageIndex = nextStageIndex;
            hasKey = false;
            OnKeyStatusChanged?.Invoke(false);

            // Dịch chuyển Player đến điểm xuất phát của Map mới
            var player = GameObject.FindGameObjectWithTag("Player");
            if (player != null && nextSpawnPoint != null)
            {
                player.transform.position = nextSpawnPoint.position;
                var rb = player.GetComponent<Rigidbody2D>();
                if (rb != null)
                {
#if UNITY_6000_0_OR_NEWER
                    rb.linearVelocity = Vector2.zero;
#else
                    rb.velocity = Vector2.zero;
#endif
                }
            }

            ShowStageBanner(currentStageIndex);
            OnStageChanged?.Invoke(currentStageIndex, CurrentStageName);
        }

        /// <summary>
        /// Kích hoạt chiến thắng toàn bộ 8 màn chơi
        /// </summary>
        public void TriggerVictory()
        {
            ShowMessage("🏆 CHÚC MỪNG! BẠN ĐÃ PHÁ ĐẢO TOÀN BỘ 8 MÀN CHƠI!");
            if (PlatformerGameManager.Instance != null)
            {
                PlatformerGameManager.Instance.TriggerVictory();
            }
        }

        /// <summary>
        /// Hiển thị thông báo nổi trên HUD
        /// </summary>
        public void ShowMessage(string message)
        {
            Debug.Log($"[StageManager] {message}");
            OnMessageDisplayed?.Invoke(message);
        }

        private void ShowStageBanner(int stage)
        {
            string name = CurrentStageName;
            ShowMessage($"🚩 {name}");
        }
    }
}
