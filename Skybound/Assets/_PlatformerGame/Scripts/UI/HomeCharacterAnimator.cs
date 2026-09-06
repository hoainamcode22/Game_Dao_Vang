using System.Collections;
using UnityEngine;

namespace PlatformerGame.UI
{
    /// <summary>
    /// Tự động tạo hiệu ứng cho nhân vật ở Scene_Home:
    /// - Tự động chạy qua lại (Patrol) đổi frame bước chân (walk1, walk2).
    /// - Tự động nhún nhảy, bật nhảy lên cao (jump, happy) theo chu kỳ.
    /// - Hiệu ứng Squash & Stretch (co giãn mượt mà) tạo cảm giác cực kỳ sống động và vui nhộn.
    /// </summary>
    public class HomeCharacterAnimator : MonoBehaviour
    {
        [Header("--- Sprites ---")]
        [SerializeField] private Sprite idleSprite;
        [SerializeField] private Sprite walk1Sprite;
        [SerializeField] private Sprite walk2Sprite;
        [SerializeField] private Sprite jumpSprite;
        [SerializeField] private Sprite happySprite;

        [Header("--- Tuần tra & Di chuyển tự động ---")]
        [SerializeField] private bool canMove = true;
        [SerializeField] private float minX = -8f;
        [SerializeField] private float maxX = -2f;
        [SerializeField] private float moveSpeed = 2.5f;
        [SerializeField] private float walkFrameRate = 0.15f;

        [Header("--- Bật nhảy tự động (Auto Hop / Jump) ---")]
        [SerializeField] private bool canJump = true;
        [SerializeField] private float minJumpInterval = 1.8f;
        [SerializeField] private float maxJumpInterval = 3.5f;
        [SerializeField] private float jumpHeight = 1.8f;
        [SerializeField] private float jumpDuration = 0.6f;

        [Header("--- Squash & Stretch (Nhún nhảy) ---")]
        [SerializeField] private float baseScale = 1.3f;
        [SerializeField] private float idleBounceSpeed = 4f;
        [SerializeField] private float idleBounceAmount = 0.08f;

        private SpriteRenderer spriteRenderer;
        private Vector3 startPos;
        private float groundY;
        private int movingDirection = 1; // 1: sang phải, -1: sang trái
        private bool isJumping = false;
        private float walkTimer = 0f;
        private int walkFrameIndex = 0;
        private float nextJumpTime = 0f;

        private void Awake()
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
            if (spriteRenderer == null)
            {
                spriteRenderer = GetComponentInChildren<SpriteRenderer>();
            }
        }

        private void Start()
        {
            startPos = transform.position;
            groundY = transform.position.y;
            ScheduleNextJump();

            // Random hướng ban đầu
            if (Random.value > 0.5f) movingDirection = -1;
            UpdateFacing();
        }

        private void Update()
        {
            if (isJumping) return;

            // 1. Tự động di chuyển qua lại
            if (canMove)
            {
                Vector3 pos = transform.position;
                pos.x += movingDirection * moveSpeed * Time.deltaTime;

                if (pos.x >= maxX)
                {
                    pos.x = maxX;
                    movingDirection = -1;
                    UpdateFacing();
                }
                else if (pos.x <= minX)
                {
                    pos.x = minX;
                    movingDirection = 1;
                    UpdateFacing();
                }

                transform.position = pos;

                // Animate bước chân
                walkTimer += Time.deltaTime;
                if (walkTimer >= walkFrameRate)
                {
                    walkTimer = 0f;
                    walkFrameIndex = (walkFrameIndex + 1) % 2;
                    if (spriteRenderer != null)
                    {
                        spriteRenderer.sprite = (walkFrameIndex == 0) ? (walk1Sprite ?? idleSprite) : (walk2Sprite ?? idleSprite);
                    }
                }

                // Hiệu ứng nhún nhẹ khi bước chân
                float stepBob = Mathf.Abs(Mathf.Sin(Time.time * 12f)) * 0.1f;
                ApplyScale(1f + stepBob * 0.2f, 1f - stepBob * 0.2f);
            }
            else
            {
                // Đứng yên nhún nhảy nhẹ nhàng (Breathing Bounce)
                float breath = Mathf.Sin(Time.time * idleBounceSpeed) * idleBounceAmount;
                ApplyScale(1f - breath, 1f + breath);
                if (spriteRenderer != null && idleSprite != null)
                {
                    spriteRenderer.sprite = idleSprite;
                }
            }

            // 2. Kiểm tra đến thời điểm nhảy
            if (canJump && Time.time >= nextJumpTime)
            {
                StartCoroutine(PerformJumpRoutine());
            }
        }

        private void UpdateFacing()
        {
            Vector3 scale = transform.localScale;
            scale.x = Mathf.Abs(baseScale) * (movingDirection >= 0 ? 1f : -1f);
            scale.y = Mathf.Abs(baseScale);
            transform.localScale = scale;
        }

        private void ApplyScale(float scaleXMult, float scaleYMult)
        {
            float signX = movingDirection >= 0 ? 1f : -1f;
            transform.localScale = new Vector3(baseScale * signX * scaleXMult, baseScale * scaleYMult, 1f);
        }

        private void ScheduleNextJump()
        {
            nextJumpTime = Time.time + Random.Range(minJumpInterval, maxJumpInterval);
        }

        private IEnumerator PerformJumpRoutine()
        {
            isJumping = true;

            // Giai đoạn 1: Chuẩn bị nhún xuống trước khi bật lên (Pre-jump Squash)
            if (spriteRenderer != null && idleSprite != null) spriteRenderer.sprite = idleSprite;
            float preJumpTimer = 0f;
            float preJumpDuration = 0.12f;
            while (preJumpTimer < preJumpDuration)
            {
                preJumpTimer += Time.deltaTime;
                float t = preJumpTimer / preJumpDuration;
                ApplyScale(1.25f, 0.75f); // Nhún lùn sang 2 bên
                yield return null;
            }

            // Giai đoạn 2: Bật nhảy lên không trung (Jump Arc với Stretch)
            if (spriteRenderer != null)
            {
                spriteRenderer.sprite = jumpSprite ?? happySprite ?? idleSprite;
            }

            float jumpTimer = 0f;
            float startX = transform.position.x;
            float targetX = startX + (canMove ? movingDirection * 1.2f : 0f);
            targetX = Mathf.Clamp(targetX, minX, maxX);

            while (jumpTimer < jumpDuration)
            {
                jumpTimer += Time.deltaTime;
                float t = jumpTimer / jumpDuration; // 0 -> 1

                // Parabol độ cao Y = 4 * jumpHeight * t * (1 - t)
                float currentY = groundY + (4f * jumpHeight * t * (1f - t));
                float currentX = Mathf.Lerp(startX, targetX, t);
                transform.position = new Vector3(currentX, currentY, transform.position.z);

                // Lên đỉnh đổi sprite Happy
                if (t > 0.4f && t < 0.7f && happySprite != null && spriteRenderer != null)
                {
                    spriteRenderer.sprite = happySprite;
                }
                else if (t >= 0.7f && jumpSprite != null && spriteRenderer != null)
                {
                    spriteRenderer.sprite = jumpSprite;
                }

                // Co giãn theo chiều cao
                float stretch = Mathf.Sin(t * Mathf.PI);
                ApplyScale(1f - stretch * 0.2f, 1f + stretch * 0.35f);

                yield return null;
            }

            // Giai đoạn 3: Tiếp đất (Landing Squash)
            transform.position = new Vector3(targetX, groundY, transform.position.z);
            if (spriteRenderer != null)
            {
                spriteRenderer.sprite = happySprite ?? idleSprite;
            }

            float landTimer = 0f;
            float landDuration = 0.15f;
            while (landTimer < landDuration)
            {
                landTimer += Time.deltaTime;
                float t = landTimer / landDuration;
                float squash = Mathf.Sin(t * Mathf.PI);
                ApplyScale(1f + squash * 0.3f, 1f - squash * 0.25f);
                yield return null;
            }

            // Khôi phục scale chuẩn
            ApplyScale(1f, 1f);
            isJumping = false;
            ScheduleNextJump();
        }

        public void SetupPatrolBounds(float min, float max, float speed, float scale, bool jump)
        {
            minX = min;
            maxX = max;
            moveSpeed = speed;
            baseScale = scale;
            canJump = jump;
            groundY = transform.position.y;
            UpdateFacing();
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
