using System.Collections;
using UnityEngine;
using PlatformerGame.Core;
using PlatformerGame.Managers;

namespace PlatformerGame.Gameplay
{
    /// <summary>
    /// Đồng xu / Vật phẩm có thể thu thập được.
    /// Khi Player chạm vào: cộng xu vào GameManager, phát âm thanh,
    /// và thực hiện hoạt ảnh nảy lên / phóng to / mờ dần rồi tự hủy.
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public class CollectibleCoin : MonoBehaviour
    {
        [Header("--- Giá trị & Cài đặt (Coin Settings) ---")]
        [Tooltip("Số coin được cộng khi ăn")]
        [SerializeField] private int coinValue = 1;

        [Header("--- Hoạt ảnh khi ăn (Collect Animation) ---")]
        [Tooltip("Độ cao đồng xu nảy lên khi được nhặt")]
        [SerializeField] private float bounceHeight = 1.0f;
        [Tooltip("Thời gian diễn ra hiệu ứng thu thập (giây)")]
        [SerializeField] private float collectAnimDuration = 0.35f;

        [Header("--- Hiệu ứng Âm thanh (Audio) ---")]
        [SerializeField] private AudioClip collectSound;
        [SerializeField] private float soundVolume = 0.8f;

        private bool isCollected;
        private Collider2D col;
        private SpriteRenderer spriteRenderer;

        private void Awake()
        {
            col = GetComponent<Collider2D>();
            col.isTrigger = true;
            spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (isCollected) return;

            // Kiểm tra xem đối tượng va chạm có phải là Player hay không
            if (other.CompareTag("Player") || other.GetComponent<PlayerController2D>() != null)
            {
                Collect();
            }
        }

        private void Collect()
        {
            isCollected = true;
            col.enabled = false;

            // Cộng tiền vào GameManager
            if (PlatformerGameManager.Instance != null)
            {
                PlatformerGameManager.Instance.AddCoin(coinValue);
            }

            // Phát âm thanh
            if (collectSound != null)
            {
                AudioSource.PlayClipAtPoint(collectSound, transform.position, soundVolume);
            }

            // Chạy hiệu ứng bay lên rồi biến mất
            StartCoroutine(CollectAnimationRoutine());
        }

        private IEnumerator CollectAnimationRoutine()
        {
            Vector3 startPos = transform.position;
            Vector3 targetPos = startPos + Vector3.up * bounceHeight;
            Vector3 startScale = transform.localScale;
            Vector3 peakScale = startScale * 1.3f;

            Color originalColor = spriteRenderer != null ? spriteRenderer.color : Color.white;

            float elapsed = 0f;
            while (elapsed < collectAnimDuration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / collectAnimDuration;

                // Di chuyển lên trên
                transform.position = Vector3.Lerp(startPos, targetPos, Mathf.Sin(t * Mathf.PI * 0.5f));

                // Phóng to rồi thu nhỏ
                transform.localScale = Vector3.Lerp(peakScale, Vector3.zero, t);

                // Mờ dần
                if (spriteRenderer != null)
                {
                    Color c = originalColor;
                    c.a = Mathf.Lerp(1f, 0f, t);
                    spriteRenderer.color = c;
                }

                yield return null;
            }

            Destroy(gameObject);
        }
    }
}
