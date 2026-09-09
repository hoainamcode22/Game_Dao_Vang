using UnityEngine;
using PlatformerGame.Managers;

namespace PlatformerGame.Gameplay
{
    /// <summary>
    /// Cửa chuyển màn (Stage Door / Castle Door).
    /// Yêu cầu có Chìa khóa tương ứng để mở và chuyển sang Stage tiếp theo hoặc Chiến Thắng ở Stage 8.
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public class StageDoor : MonoBehaviour
    {
        [Header("--- Cấu hình Cửa ---")]
        [Tooltip("Số thứ tự Stage hiện tại (1 đến 8)")]
        [SerializeField] private int stageIndex = 1;
        [Tooltip("Tên Scene tiếp theo cần chuyển đến (Ví dụ: Stage_2). Để trống nếu chạy chung 1 scene.")]
        [SerializeField] private string nextSceneName;
        [Tooltip("Có phải cửa cuối cùng của Game (Chiến thắng) hay không")]
        [SerializeField] private bool isFinalVictoryDoor = false;
        [Tooltip("Điểm xuất hiện của Player ở Stage tiếp theo (nếu chạy suốt trên cùng 1 scene)")]
        [SerializeField] private Transform nextStageSpawnPoint;

        [Header("--- Visual Sprites ---")]
        [SerializeField] private Sprite closedDoorSprite;
        [SerializeField] private Sprite openDoorSprite;
        [SerializeField] private SpriteRenderer spriteRenderer;

        private bool isOpen = false;

        private void Start()
        {
            var col = GetComponent<Collider2D>();
            if (col != null) col.isTrigger = true;

            if (spriteRenderer == null)
                spriteRenderer = GetComponent<SpriteRenderer>();

            UpdateDoorVisual();
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (!other.CompareTag("Player") && other.GetComponent<PlatformerGame.Core.PlayerController2D>() == null)
                return;

            if (StageManager.Instance != null && !StageManager.Instance.HasKey(stageIndex))
            {
                StageManager.Instance.ShowMessage("🔒 Cần tìm Chìa Khóa để mở Cửa!");
                return;
            }

            OpenAndPass(other.gameObject);
        }

        private void OpenAndPass(GameObject player)
        {
            if (isOpen) return;
            isOpen = true;
            UpdateDoorVisual();

            if (isFinalVictoryDoor || nextSceneName == "Scene_Victory" || stageIndex >= 3)
            {
                if (!string.IsNullOrEmpty(nextSceneName))
                {
                    UnityEngine.SceneManagement.SceneManager.LoadScene(nextSceneName);
                }
                else if (UnityEngine.SceneManagement.SceneUtility.GetBuildIndexByScenePath("Assets/Scenes/Scene_Victory.unity") >= 0 ||
                         UnityEngine.SceneManagement.SceneUtility.GetBuildIndexByScenePath("Scene_Victory") >= 0)
                {
                    UnityEngine.SceneManagement.SceneManager.LoadScene("Scene_Victory");
                }
                else if (StageManager.Instance != null)
                {
                    StageManager.Instance.TriggerVictory();
                }
                else if (PlatformerGameManager.Instance != null)
                {
                    PlatformerGameManager.Instance.SetVictory();
                }
            }
            else
            {
                if (!string.IsNullOrEmpty(nextSceneName))
                {
                    UnityEngine.SceneManagement.SceneManager.LoadScene(nextSceneName);
                }
                else if (StageManager.Instance != null)
                {
                    StageManager.Instance.AdvanceToStage(stageIndex + 1, nextStageSpawnPoint);
                }
            }
        }

        private void UpdateDoorVisual()
        {
            if (spriteRenderer == null) return;

            if (isOpen && openDoorSprite != null)
            {
                spriteRenderer.sprite = openDoorSprite;
            }
            else if (!isOpen && closedDoorSprite != null)
            {
                spriteRenderer.sprite = closedDoorSprite;
            }
        }

        public void SetNextSpawnPoint(Transform spawnPoint)
        {
            nextStageSpawnPoint = spawnPoint;
        }
    }
}
