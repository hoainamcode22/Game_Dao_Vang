using UnityEngine;
using PlatformerGame.Managers;

namespace PlatformerGame.Gameplay
{
    /// <summary>
    /// Vật phẩm Chìa Khóa để mở cửa qua màn (Stage Door).
    /// Khi Player chạm vào, phát hiệu ứng nhấp nháy, cộng chìa khóa và tự hủy.
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public class KeyPickup : MonoBehaviour
    {
        [Header("--- Cấu hình Chìa Khóa ---")]
        [Tooltip("Số thứ tự Stage áp dụng (1 đến 8)")]
        [SerializeField] private int stageIndex = 1;
        [Tooltip("Tốc độ bay bập bồng lên xuống")]
        [SerializeField] private float bobSpeed = 3f;
        [Tooltip("Độ cao bập bồng")]
        [SerializeField] private float bobHeight = 0.2f;

        private Vector3 startPos;
        private bool isCollected;

        private void Start()
        {
            startPos = transform.position;
            var col = GetComponent<Collider2D>();
            if (col != null) col.isTrigger = true;
        }

        private void Update()
        {
            if (isCollected) return;

            // Hiệu ứng bập bồng nhẹ nhàng
            float newY = startPos.y + Mathf.Sin(Time.time * bobSpeed) * bobHeight;
            transform.position = new Vector3(startPos.x, newY, startPos.z);
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (isCollected) return;

            if (other.CompareTag("Player") || other.GetComponent<PlatformerGame.Core.PlayerController2D>() != null)
            {
                Collect();
            }
        }

        private void Collect()
        {
            isCollected = true;

            if (StageManager.Instance != null)
            {
                StageManager.Instance.CollectKey(stageIndex);
            }

            Destroy(gameObject);
        }
    }
}
