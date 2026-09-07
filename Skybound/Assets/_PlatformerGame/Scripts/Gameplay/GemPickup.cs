using System.Collections;
using UnityEngine;
using PlatformerGame.Core;
using PlatformerGame.Managers;
using PlatformerGame.UI;

namespace PlatformerGame.Gameplay
{
    public enum GemColorType
    {
        Blue_Sapphire, // Kim cương Xanh Dương: +5 Xu
        Green_Emerald, // Kim cương Xanh Lá: +10 Xu & Hồi +1 Tim Máu ❤️
        Red_Ruby,      // Kim cương Đỏ: +20 Xu (Đá quý hiếm)
        Yellow_Topaz   // Kim cương Vàng: +5 Xu
    }

    /// <summary>
    /// Hệ thống Kim Cương đa sắc màu (Xanh Dương, Xanh Lá, Đỏ, Vàng):
    /// - Nhàn rỗi: Lơ lửng nhấp nhô và nhịp thở phát sáng.
    /// - Khi chạm Player:
    ///   1. Nảy lên và hiện số điểm nổi tương ứng màu (+5, +10 kèm ❤️+1 Máu cho Green Gem).
    ///   2. Bay theo đường cong Bézier về góc HUD Coins.
    ///   3. Nảy biểu tượng HUD (Punch Scale) và cộng tiền vào GameManager!
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public class GemPickup : MonoBehaviour
    {
        [Header("--- Phân loại Kim Cương (Gem Type) ---")]
        [SerializeField] private GemColorType gemType = GemColorType.Blue_Sapphire;
        [SerializeField] private int value = 5;
        [SerializeField] private bool healPlayer = false;

        [Header("--- Hiệu ứng Lơ lửng (Idle Bobbing) ---")]
        [SerializeField] private float bobSpeed = 4f;
        [SerializeField] private float bobHeight = 0.2f;
        [SerializeField] private float idlePulseSpeed = 5f;
        [SerializeField] private float idlePulseAmount = 0.08f;

        [Header("--- Hoạt ảnh bay về HUD (Fly to HUD) ---")]
        [SerializeField] private float popHeight = 0.8f;
        [SerializeField] private float flyDuration = 0.6f;
        [SerializeField] private float rotationSpeed = 720f;

        [Header("--- Hiệu ứng Nam Châm Hút Gem (Magnet Attraction) ---")]
        [Tooltip("Khoảng cách bắt đầu kích hoạt hút kim cương")]
        [SerializeField] private float magnetRadius = 2.5f;
        [Tooltip("Tốc độ bay hút về phía người chơi")]
        [SerializeField] private float magnetSpeed = 8f;

        private Vector3 startPos;
        private Vector3 baseScale;
        private bool isCollected;
        private bool isAttracting;
        private Collider2D col;
        private SpriteRenderer sr;
        private Transform playerTransform;

        private void Awake()
        {
            col = GetComponent<Collider2D>();
            col.isTrigger = true;
            sr = GetComponentInChildren<SpriteRenderer>();
            baseScale = transform.localScale;
            if (baseScale == Vector3.zero) baseScale = Vector3.one;

            ApplyGemTypeDefaults();
        }

        private void ApplyGemTypeDefaults()
        {
            switch (gemType)
            {
                case GemColorType.Blue_Sapphire:
                    if (value <= 0) value = 5;
                    break;
                case GemColorType.Green_Emerald:
                    if (value <= 0) value = 10;
                    healPlayer = true;
                    break;
                case GemColorType.Red_Ruby:
                    if (value <= 0) value = 20;
                    break;
                case GemColorType.Yellow_Topaz:
                    if (value <= 0) value = 5;
                    break;
            }
        }

        private void Start()
        {
            startPos = transform.position;
            var player = FindObjectOfType<PlayerController2D>();
            if (player != null)
            {
                playerTransform = player.transform;
            }
        }

        private void Update()
        {
            if (isCollected) return;

            // 1. Nam châm hút về phía Player khi lại gần
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
                transform.position = Vector3.MoveTowards(transform.position, playerTransform.position, magnetSpeed * Time.deltaTime);
                magnetSpeed += 12f * Time.deltaTime;
                transform.Rotate(0f, 0f, 360f * Time.deltaTime);
            }
            else
            {
                // Nhấp nhô lơ lửng & nhịp thở khi chưa kích hoạt
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
                Collect(other.gameObject);
            }
        }

        public void Collect(GameObject playerObj = null)
        {
            if (isCollected) return;
            isCollected = true;
            if (col != null) col.enabled = false;

            // Xử lý hiệu ứng hồi máu cho Gem Xanh Lá
            if (healPlayer && playerObj != null)
            {
                PlayerHealth health = playerObj.GetComponent<PlayerHealth>();
                if (health != null)
                {
                    health.Heal(1);
                }
            }

            // Màu sắc và điểm nổi
            Color textColor = GetTextColor();
            if (UIManager_Platformer.Instance != null)
            {
                UIManager_Platformer.Instance.SpawnFloatingScore(transform.position, value, textColor);
            }

            // Chạy hoạt ảnh bay vút lên HUD Gem
            StartCoroutine(FlyToHUDRoutine());
        }

        private Color GetTextColor()
        {
            switch (gemType)
            {
                case GemColorType.Blue_Sapphire:
                    return new Color(0.25f, 0.85f, 1f); // Cyan blue
                case GemColorType.Green_Emerald:
                    return new Color(0.2f, 1f, 0.45f); // Bright emerald green
                case GemColorType.Red_Ruby:
                    return new Color(1f, 0.25f, 0.35f); // Ruby red
                default:
                    return new Color(1f, 0.85f, 0.15f); // Gold yellow
            }
        }

        private IEnumerator FlyToHUDRoutine()
        {
            Vector3 spawnPos = transform.position;
            Vector3 popTarget = spawnPos + Vector3.up * popHeight;

            // Giai đoạn 1: Nảy vọt lên trên một nhịp ngắn (Pop Jump)
            float popTimer = 0f;
            float popDuration = 0.12f;
            while (popTimer < popDuration)
            {
                popTimer += Time.deltaTime;
                float t = popTimer / popDuration;
                transform.position = Vector3.Lerp(spawnPos, popTarget, Mathf.Sin(t * Mathf.PI * 0.5f));
                transform.localScale = baseScale * Mathf.Lerp(1f, 1.4f, t);
                yield return null;
            }

            // Giai đoạn 2: Bay theo đường cong Bezier về góc trên bên phải HUD Gem
            Vector3 startFlyPos = transform.position;
            float flyTimer = 0f;

            if (sr != null)
            {
                sr.sortingOrder = 50;
            }

            while (flyTimer < flyDuration)
            {
                flyTimer += Time.deltaTime;
                float t = flyTimer / flyDuration;

                Vector3 screenTarget = UIManager_Platformer.Instance != null
                    ? UIManager_Platformer.Instance.GetGemIconScreenPosition()
                    : new Vector3(Screen.width - 60f, Screen.height - 60f, 0f);

                Camera mainCam = Camera.main;
                float targetZ = 10f;
                Vector3 worldTarget = (mainCam != null)
                    ? mainCam.ScreenToWorldPoint(new Vector3(screenTarget.x, screenTarget.y, targetZ))
                    : startFlyPos + new Vector3(8f, 6f, 0f);
                worldTarget.z = 0f;

                // Điểm uốn cong Bezier
                Vector3 controlPoint = new Vector3(
                    (startFlyPos.x + worldTarget.x) * 0.5f - 1.5f,
                    Mathf.Max(startFlyPos.y, worldTarget.y) + 2.5f,
                    0f
                );

                float u = 1f - t;
                float tt = t * t;
                float uu = u * u;
                float easedT = t * t * (3f - 2f * t);

                Vector3 currentPos = (uu * startFlyPos) + (2f * u * t * controlPoint) + (tt * worldTarget);
                transform.position = currentPos;

                transform.Rotate(Vector3.forward, rotationSpeed * Time.deltaTime);
                transform.localScale = Vector3.Lerp(baseScale * 1.4f, baseScale * 0.35f, easedT);

                yield return null;
            }

            // Giai đoạn 3: Chạm đích HUD Gem
            if (UIManager_Platformer.Instance != null)
            {
                UIManager_Platformer.Instance.PunchGemHUD();
            }

            if (PlatformerGameManager.Instance != null)
            {
                PlatformerGameManager.Instance.AddGem(1);
                PlatformerGameManager.Instance.AddCoin(value);
            }

            Destroy(gameObject);
        }

        public void SetupGem(GemColorType type, int coinValue, bool heal)
        {
            gemType = type;
            value = coinValue;
            healPlayer = heal;
        }
    }
}
