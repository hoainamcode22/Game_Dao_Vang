using System.Collections;
using UnityEngine;
using PlatformerGame.Core;
using PlatformerGame.Managers;
using PlatformerGame.UI;

namespace PlatformerGame.Gameplay
{
    /// <summary>
    /// Đồng xu vàng tròn có thể thu thập được:
    /// - Nhàn rỗi: Lơ lửng nhấp nhô nhẹ nhàng & nhịp thở phát sáng.
    /// - Nam châm thu hút (Magnet Attraction): Khi Player chạy tới gần (khoảng cách 2.5 ô), đồng xu tự động bay hút về phía Player.
    /// - Khi chạm Player:
    ///   1. Hiện số điểm nổi bay lên (+1 Xu vàng).
    ///   2. Bay theo đường cong Bézier uốn lượn hướng thẳng về Khung Coin HUD góc trên màn hình.
    ///   3. Rung nảy biểu tượng Coin HUD (Punch Scale) và cộng tiền vào GameManager!
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public class CollectibleCoin : MonoBehaviour
    {
        [Header("--- Giá trị & Cài đặt (Coin Settings) ---")]
        [Tooltip("Số coin được cộng khi ăn")]
        [SerializeField] private int coinValue = 1;

        [Header("--- Hiệu ứng Nam Châm Hút Vàng (Magnet Attraction) ---")]
        [Tooltip("Khoảng cách bắt đầu kích hoạt hút đồng xu")]
        [SerializeField] private float magnetRadius = 2.5f;
        [Tooltip("Tốc độ bay hút về phía người chơi")]
        [SerializeField] private float magnetSpeed = 8f;

        [Header("--- Hiệu ứng Lơ lửng (Idle Bobbing) ---")]
        [SerializeField] private float bobSpeed = 4f;
        [SerializeField] private float bobHeight = 0.15f;
        [SerializeField] private float idlePulseSpeed = 5f;
        [SerializeField] private float idlePulseAmount = 0.08f;

        [Header("--- Hoạt ảnh bay về HUD (Fly to HUD) ---")]
        [SerializeField] private float popHeight = 0.8f;
        [SerializeField] private float flyDuration = 0.55f;
        [SerializeField] private float rotationSpeed = 720f;

        [Header("--- Hiệu ứng Âm thanh (Audio) ---")]
        [SerializeField] private AudioClip collectSound;
        [SerializeField] private float soundVolume = 0.8f;

        private Vector3 startPos;
        private Vector3 baseScale;
        private bool isCollected;
        private bool isAttracting;
        private Collider2D col;
        private SpriteRenderer spriteRenderer;
        private Transform playerTransform;

        private void Awake()
        {
            col = GetComponent<Collider2D>();
            col.isTrigger = true;
            spriteRenderer = GetComponentInChildren<SpriteRenderer>();
            baseScale = transform.localScale;
            if (baseScale == Vector3.zero) baseScale = Vector3.one;
        }

        private void Start()
        {
            startPos = transform.position;
            
            // Tìm Player trong màn chơi
            var player = FindObjectOfType<PlayerController2D>();
            if (player != null)
            {
                playerTransform = player.transform;
            }
        }

        private void Update()
        {
            if (isCollected) return;

            // 1. Kiểm tra khoảng cách nam châm hút vàng
            if (playerTransform != null)
            {
                float dist = Vector2.Distance(transform.position, playerTransform.position);
                if (dist <= magnetRadius)
                {
                    isAttracting = true;
                }
            }

            if (isAttracting && playerTransform != null)
            {
                // Bay mượt mà bám sát về phía Player
                transform.position = Vector3.MoveTowards(transform.position, playerTransform.position, magnetSpeed * Time.deltaTime);
                magnetSpeed += 12f * Time.deltaTime; // Tăng tốc dần khi bay gần
                transform.Rotate(0f, 0f, 360f * Time.deltaTime);
            }
            else
            {
                // Nhấp nhô lơ lửng nhịp thở khi chưa kích hoạt nam châm
                float newY = startPos.y + Mathf.Sin(Time.time * bobSpeed) * bobHeight;
                transform.position = new Vector3(startPos.x, newY, startPos.z);

                float pulse = Mathf.Sin(Time.time * idlePulseSpeed) * idlePulseAmount;
                transform.localScale = baseScale * (1f + pulse);
            }
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (isCollected) return;

            if (other.CompareTag("Player") || other.GetComponent<PlayerController2D>() != null)
            {
                Collect();
            }
        }

        private void Collect()
        {
            isCollected = true;
            col.enabled = false;

            // Phát âm thanh thu thập
            if (collectSound != null)
            {
                AudioSource.PlayClipAtPoint(collectSound, transform.position, soundVolume);
            }

            // Hiện chữ nổi +1 vàng tại vị trí ăn
            if (UIManager_Platformer.Instance != null)
            {
                UIManager_Platformer.Instance.SpawnFloatingScore(transform.position, coinValue, new Color(1f, 0.9f, 0.2f));
            }

            // Chạy chuỗi hoạt ảnh bay vòng cung uốn lượn lên HUD
            StartCoroutine(FlyToHUDRoutine());
        }

        private IEnumerator FlyToHUDRoutine()
        {
            Vector3 originPos = transform.position;
            Vector3 popTarget = originPos + Vector3.up * popHeight;

            // Pha 1: Nảy tưng lên trên
            float popTime = 0.12f;
            float elapsed = 0f;
            while (elapsed < popTime)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / popTime;
                transform.position = Vector3.Lerp(originPos, popTarget, Mathf.Sin(t * Mathf.PI * 0.5f));
                transform.localScale = baseScale * (1f + 0.35f * Mathf.Sin(t * Mathf.PI));
                yield return null;
            }

            // Pha 2: Bay theo đường cong Bézier lên biểu tượng Coin trên HUD
            Camera mainCam = Camera.main;
            Vector3 startFlyPos = transform.position;
            elapsed = 0f;

            while (elapsed < flyDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / flyDuration);
                float easeT = t * t * (3f - 2f * t);

                // Lấy tọa độ Screen của icon Coin HUD
                Vector3 targetScreenPos = (UIManager_Platformer.Instance != null)
                    ? UIManager_Platformer.Instance.GetCoinIconScreenPosition()
                    : new Vector3(Screen.width - 200f, Screen.height - 60f, 0f);

                Vector3 targetWorldPos = startFlyPos + Vector3.up * 5f;
                if (mainCam != null)
                {
                    float distanceToCam = Mathf.Abs(mainCam.transform.position.z - transform.position.z);
                    targetWorldPos = mainCam.ScreenToWorldPoint(new Vector3(targetScreenPos.x, targetScreenPos.y, distanceToCam));
                }

                // Điểm điều khiển Bézier tạo đường cong mềm mại
                Vector3 controlPoint = (startFlyPos + targetWorldPos) * 0.5f + Vector3.left * 1.5f + Vector3.up * 2f;

                // Công thức Quadratic Bézier
                Vector3 currentPos = Mathf.Pow(1 - easeT, 2) * startFlyPos +
                                     2 * (1 - easeT) * easeT * controlPoint +
                                     Mathf.Pow(easeT, 2) * targetWorldPos;

                transform.position = currentPos;
                transform.Rotate(0f, 0f, rotationSpeed * Time.deltaTime);

                // Thu nhỏ dần khi tiếp cận HUD
                transform.localScale = Vector3.Lerp(baseScale * 1.2f, baseScale * 0.35f, easeT);

                yield return null;
            }

            // Pha 3: Chạm đích HUD
            if (PlatformerGameManager.Instance != null)
            {
                PlatformerGameManager.Instance.AddCoin(coinValue);
            }

            if (UIManager_Platformer.Instance != null)
            {
                UIManager_Platformer.Instance.PunchCoinHUD();
            }

            Destroy(gameObject);
        }
    }
}
