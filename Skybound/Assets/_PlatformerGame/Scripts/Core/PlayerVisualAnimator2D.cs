using UnityEngine;

namespace PlatformerGame.Core
{
    /// <summary>
    /// Điều khiển Visual Animation cho nhân vật người chơi trong Gameplay:
    /// - Khi bấm A / D (chạy): Đổi luân phiên sprite walk1 & walk2 với nhịp bước chân mượt mà.
    /// - Khi bấm W / Space (nhảy): Đổi sang sprite jump, kèm hiệu ứng giãn người (stretch).
    /// - Khi tiếp đất: Hiệu ứng nhún (squash) đàn hồi chuẩn phong cách hoạt hình Mario.
    /// - Khi đứng yên: Đổi về sprite idle và thở nhẹ (breathing bounce).
    /// </summary>
    [RequireComponent(typeof(PlayerController2D))]
    public class PlayerVisualAnimator2D : MonoBehaviour
    {
        [Header("--- Sprites Kenney ---")]
        [SerializeField] private Sprite idleSprite;
        [SerializeField] private Sprite walk1Sprite;
        [SerializeField] private Sprite walk2Sprite;
        [SerializeField] private Sprite jumpSprite;
        [SerializeField] private Sprite happySprite;

        [Header("--- Cài đặt Animation ---")]
        [SerializeField] private float walkFrameRate = 0.12f;
        [SerializeField] private float moveThreshold = 0.15f;

        [Header("--- Squash & Stretch (Độ nảy) ---")]
        [SerializeField] private Transform visualTransform;
        [SerializeField] private float jumpStretchAmount = 0.25f;
        [SerializeField] private float landSquashAmount = 0.3f;
        [SerializeField] private float recoverSpeed = 12f;

        private PlayerController2D controller;
        private SpriteRenderer spriteRenderer;
        private Rigidbody2D rb;

        private float walkTimer;
        private int walkIndex;
        private Vector3 currentVisualScale = Vector3.one;
        private bool wasGrounded;

        private void Awake()
        {
            controller = GetComponent<PlayerController2D>();
            rb = GetComponent<Rigidbody2D>();
            spriteRenderer = GetComponentInChildren<SpriteRenderer>();

            if (visualTransform == null && spriteRenderer != null)
            {
                visualTransform = spriteRenderer.transform;
            }
        }

        private void OnEnable()
        {
            if (controller != null)
            {
                controller.OnJump += HandleJump;
                controller.OnLand += HandleLand;
            }
        }

        private void OnDisable()
        {
            if (controller != null)
            {
                controller.OnJump -= HandleJump;
                controller.OnLand -= HandleLand;
            }
        }

        private void Start()
        {
            if (visualTransform != null)
            {
                currentVisualScale = Vector3.one;
            }
        }

        private void Update()
        {
            UpdateAnimationState();
            UpdateScaleRecovery();
        }

        private void UpdateAnimationState()
        {
            if (spriteRenderer == null || controller == null) return;

            bool isGrounded = controller.IsGrounded;
            float horizontalSpeed = Mathf.Abs(controller.Velocity.x);

            if (!isGrounded)
            {
                // Đang ở trên không (Nhảy / Rơi)
                if (jumpSprite != null)
                {
                    spriteRenderer.sprite = jumpSprite;
                }
            }
            else
            {
                // Đang ở trên mặt đất
                if (horizontalSpeed > moveThreshold)
                {
                    // Đang chạy ngang (A / D)
                    walkTimer += Time.deltaTime;
                    if (walkTimer >= walkFrameRate)
                    {
                        walkTimer = 0f;
                        walkIndex = (walkIndex + 1) % 2;
                    }

                    spriteRenderer.sprite = (walkIndex == 0) ? (walk1Sprite ?? idleSprite) : (walk2Sprite ?? idleSprite);
                }
                else
                {
                    // Đứng yên
                    walkTimer = 0f;
                    walkIndex = 0;
                    if (idleSprite != null)
                    {
                        spriteRenderer.sprite = idleSprite;
                    }
                }
            }
        }

        private void HandleJump()
        {
            // Kéo giãn nhân vật khi bật nhảy
            currentVisualScale = new Vector3(1f - jumpStretchAmount * 0.5f, 1f + jumpStretchAmount, 1f);
        }

        private void HandleLand()
        {
            // Nhún dẹp nhân vật khi tiếp đất
            currentVisualScale = new Vector3(1f + landSquashAmount, 1f - landSquashAmount * 0.7f, 1f);
        }

        private void UpdateScaleRecovery()
        {
            if (visualTransform == null) return;

            // Hồi phục tỉ lệ về Vector3.one theo thời gian
            currentVisualScale = Vector3.Lerp(currentVisualScale, Vector3.one, Time.deltaTime * recoverSpeed);

            // Giữ nguyên dấu X của sprite renderer nếu cần lật hướng
            Vector3 finalScale = currentVisualScale;
            visualTransform.localScale = finalScale;
        }

        public void SetSprites(Sprite idle, Sprite walk1, Sprite walk2, Sprite jump, Sprite happy)
        {
            idleSprite = idle;
            walk1Sprite = walk1;
            walk2Sprite = walk2;
            jumpSprite = jump;
            happySprite = happy;
        }
    }
}
