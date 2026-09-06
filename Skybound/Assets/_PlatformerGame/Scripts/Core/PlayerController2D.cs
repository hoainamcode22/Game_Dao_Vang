using System;
using UnityEngine;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace PlatformerGame.Core
{
    /// <summary>
    /// Điều khiển di chuyển nhân vật 2D chuẩn phong cách Mario Platformer.
    /// Hỗ trợ mượt mà cả bàn phím PC (A/D, Mũi tên, Space) và Nút cảm ứng Mobile.
    /// Có tính năng: Variable Jump Height, Coyote Time, Jump Buffer, Ground Check chính xác.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(Collider2D))]
    public class PlayerController2D : MonoBehaviour
    {
        [Header("--- Di chuyển ngang (Movement) ---")]
        [Tooltip("Tốc độ chạy tối đa")]
        [SerializeField] private float moveSpeed = 8f;
        [Tooltip("Gia tốc khi bắt đầu chạy")]
        [SerializeField] private float acceleration = 60f;
        [Tooltip("Độ giảm tốc khi dừng lại")]
        [SerializeField] private float deceleration = 50f;

        [Header("--- Nhảy kiểu Mario (Jump Physics) ---")]
        [Tooltip("Lực nhảy ban đầu")]
        [SerializeField] private float jumpForce = 14f;
        [Tooltip("Hệ số trọng lực khi đang rơi tự do (giúp rơi nhanh, không bị floaty)")]
        [SerializeField] private float fallGravityMultiplier = 2.5f;
        [Tooltip("Hệ số trọng lực khi buông nút nhảy sớm (nhảy thấp/nhảy cao theo lực bấm)")]
        [SerializeField] private float lowJumpGravityMultiplier = 2f;
        [Tooltip("Thời gian ân huệ sau khi rời khỏi mặt đất vẫn nhảy được (Coyote Time)")]
        [SerializeField] private float coyoteTime = 0.15f;
        [Tooltip("Thời gian lưu đệm lệnh nhảy trước khi chạm đất (Jump Buffer)")]
        [SerializeField] private float jumpBufferTime = 0.15f;

        [Header("--- Kiểm tra chạm đất (Ground Check) ---")]
        [Tooltip("Transform điểm kiểm tra chân")]
        [SerializeField] private Transform groundCheckPoint;
        [Tooltip("Kích thước vùng Box kiểm tra đất")]
        [SerializeField] private Vector2 groundCheckSize = new Vector2(0.6f, 0.15f);
        [Tooltip("LayerMask chứa các lớp địa hình được tính là đất")]
        [SerializeField] private LayerMask groundLayer;

        [Header("--- Render & Visual ---")]
        [Tooltip("SpriteRenderer của nhân vật để lật mặt (Flip)")]
        [SerializeField] private SpriteRenderer spriteRenderer;

        // Components & State
        private Rigidbody2D rb;
        private float defaultGravityScale;
        private bool isGrounded;
        private bool wasGroundedLastFrame;

        // Input & Timers
        private float horizontalInput;
        private float virtualHorizontalInput;
        private bool isJumpHeld;
        private bool virtualJumpHeld;
        private float coyoteCounter;
        private float jumpBufferCounter;
        private bool isMovementLocked;

        // Events / Callbacks cho Animation & Sound Manager
        public event Action OnJump;
        public event Action OnLand;
        public event Action<bool> OnGroundStateChanged; // true: chạm đất, false: trên không
        public event Action<float> OnMove; // Tốc độ di chuyển hiện tại

        public bool IsGrounded => isGrounded;
        public Vector2 Velocity => GetLinearVelocity();
        public bool IsFacingRight { get; private set; } = true;

        private void Awake()
        {
            rb = GetComponent<Rigidbody2D>();
            if (spriteRenderer == null)
            {
                spriteRenderer = GetComponentInChildren<SpriteRenderer>();
            }

            defaultGravityScale = rb.gravityScale;
            if (defaultGravityScale <= 0) defaultGravityScale = 3f;
            rb.gravityScale = defaultGravityScale;

            // Đảm bảo không bị lật xoay Z khi va chạm
            rb.constraints = RigidbodyConstraints2D.FreezeRotation;
            rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

            // Tự tạo groundCheckPoint nếu chưa gán
            if (groundCheckPoint == null)
            {
                GameObject checkObj = new GameObject("GroundCheckPoint");
                checkObj.transform.SetParent(transform);
                checkObj.transform.localPosition = new Vector3(0f, -0.5f, 0f);
                groundCheckPoint = checkObj.transform;
            }
        }

        private void Update()
        {
            if (isMovementLocked)
            {
                horizontalInput = 0f;
                coyoteCounter = 0f;
                jumpBufferCounter = 0f;
                return;
            }

            GatherInputs();
            UpdateTimers();
            HandleSpriteFlip();

            // Thông báo tốc độ di chuyển
            OnMove?.Invoke(Mathf.Abs(GetLinearVelocity().x));
        }

        private void FixedUpdate()
        {
            CheckGrounded();
            ApplyHorizontalMovement();
            ApplyJumpPhysics();
        }

        #region Input Handling

        private void GatherInputs()
        {
            float rawHorizontal = 0f;
            bool jumpDown = false;
            bool jumpHolding = false;

            // 1. Đọc phím từ New Input System nếu có
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null)
            {
                if (Keyboard.current.aKey.isPressed || Keyboard.current.leftArrowKey.isPressed) rawHorizontal -= 1f;
                if (Keyboard.current.dKey.isPressed || Keyboard.current.rightArrowKey.isPressed) rawHorizontal += 1f;

                if (Keyboard.current.spaceKey.wasPressedThisFrame || Keyboard.current.wKey.wasPressedThisFrame || Keyboard.current.upArrowKey.wasPressedThisFrame)
                {
                    jumpDown = true;
                }
                if (Keyboard.current.spaceKey.isPressed || Keyboard.current.wKey.isPressed || Keyboard.current.upArrowKey.isPressed)
                {
                    jumpHolding = true;
                }
            }
#else
            // 2. Fallback sang Legacy Input Manager nếu New Input System tắt
            rawHorizontal = Input.GetAxisRaw("Horizontal");
            if (Input.GetButtonDown("Jump") || Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.UpArrow))
            {
                jumpDown = true;
            }
            jumpHolding = Input.GetButton("Jump") || Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow);
#endif

            // 3. Kết hợp với Mobile Virtual Input
            if (Mathf.Abs(virtualHorizontalInput) > 0.01f)
            {
                rawHorizontal = virtualHorizontalInput;
            }

            if (virtualJumpHeld)
            {
                jumpHolding = true;
            }

            horizontalInput = rawHorizontal;
            isJumpHeld = jumpHolding;

            // Nếu người chơi vừa bấm nút nhảy -> nạp jump buffer
            if (jumpDown)
            {
                TriggerJumpAction();
            }
        }

        /// <summary>
        /// API gọi từ Virtual Button trên UI Mobile để điều khiển sang trái/phải (-1, 0, 1).
        /// </summary>
        public void SetVirtualHorizontal(float value)
        {
            virtualHorizontalInput = Mathf.Clamp(value, -1f, 1f);
        }

        /// <summary>
        /// API gọi từ Virtual Jump Button trên Mobile khi bắt đầu nhấn xuống.
        /// </summary>
        public void SetVirtualJumpDown()
        {
            virtualJumpHeld = true;
            TriggerJumpAction();
        }

        /// <summary>
        /// API gọi từ Virtual Jump Button trên Mobile khi buông tay.
        /// </summary>
        public void SetVirtualJumpUp()
        {
            virtualJumpHeld = false;
        }

        /// <summary>
        /// Kích hoạt bộ đệm nhảy
        /// </summary>
        public void TriggerJumpAction()
        {
            if (isMovementLocked) return;
            jumpBufferCounter = jumpBufferTime;
        }

        #endregion

        #region Movement & Physics

        private void UpdateTimers()
        {
            // Đếm ngược Coyote Time
            if (isGrounded)
            {
                coyoteCounter = coyoteTime;
            }
            else
            {
                coyoteCounter -= Time.deltaTime;
            }

            // Đếm ngược Jump Buffer Time
            if (jumpBufferCounter > 0f)
            {
                jumpBufferCounter -= Time.deltaTime;
            }

            // Thực hiện nhảy khi cả Coyote Time và Jump Buffer đều hợp lệ
            if (jumpBufferCounter > 0f && coyoteCounter > 0f)
            {
                ExecuteJump();
            }
        }

        private void ExecuteJump()
        {
            Vector2 currentVel = GetLinearVelocity();
            SetLinearVelocity(new Vector2(currentVel.x, jumpForce));

            // Reset các bộ đếm sau khi nhảy
            jumpBufferCounter = 0f;
            coyoteCounter = 0f;

            OnJump?.Invoke();
        }

        private void ApplyHorizontalMovement()
        {
            float targetSpeed = horizontalInput * moveSpeed;
            Vector2 currentVel = GetLinearVelocity();

            // Tính gia tốc hoặc giảm tốc mượt mà
            float accelRate = (Mathf.Abs(targetSpeed) > 0.01f) ? acceleration : deceleration;
            float newSpeedX = Mathf.MoveTowards(currentVel.x, targetSpeed, accelRate * Time.fixedDeltaTime);

            SetLinearVelocity(new Vector2(newSpeedX, currentVel.y));
        }

        private void ApplyJumpPhysics()
        {
            Vector2 currentVel = GetLinearVelocity();

            // Mario Jump: Rơi nhanh hơn khi rơi xuống
            if (currentVel.y < 0f)
            {
                rb.gravityScale = defaultGravityScale * fallGravityMultiplier;
            }
            // Mario Jump: Nhảy thấp khi buông nút sớm lúc đang bay lên
            else if (currentVel.y > 0f && !isJumpHeld)
            {
                rb.gravityScale = defaultGravityScale * lowJumpGravityMultiplier;
            }
            else
            {
                rb.gravityScale = defaultGravityScale;
            }
        }

        private void CheckGrounded()
        {
            Vector2 checkPos = groundCheckPoint != null ? (Vector2)groundCheckPoint.position : (Vector2)transform.position - new Vector2(0f, 0.5f);
            
            // Dùng BoxCast để check va chạm mặt đất chính xác
            RaycastHit2D hit = Physics2D.BoxCast(checkPos, groundCheckSize, 0f, Vector2.down, 0.05f, groundLayer);
            isGrounded = hit.collider != null;

            // Xử lý sự kiện chạm đất / rời đất
            if (isGrounded != wasGroundedLastFrame)
            {
                OnGroundStateChanged?.Invoke(isGrounded);
                if (isGrounded)
                {
                    OnLand?.Invoke();
                }
            }

            wasGroundedLastFrame = isGrounded;
        }

        private void HandleSpriteFlip()
        {
            if (Mathf.Abs(horizontalInput) > 0.05f)
            {
                IsFacingRight = horizontalInput > 0f;
                if (spriteRenderer != null)
                {
                    spriteRenderer.flipX = !IsFacingRight;
                }
            }
        }

        #endregion

        #region Utilities & Controls

        /// <summary>
        /// Khóa hoặc mở khóa điều khiển nhân vật (khi đếm ngược 3-2-1 hoặc Game Over)
        /// </summary>
        public void SetMovementLocked(bool locked)
        {
            isMovementLocked = locked;
            if (locked)
            {
                SetLinearVelocity(Vector2.zero);
                horizontalInput = 0f;
                virtualHorizontalInput = 0f;
                virtualJumpHeld = false;
            }
        }

        /// <summary>
        /// Đặt vận tốc tương thích chuẩn với Unity 6 và các phiên bản cũ
        /// </summary>
        private void SetLinearVelocity(Vector2 vel)
        {
#if UNITY_6000_0_OR_NEWER
            rb.linearVelocity = vel;
#else
            rb.velocity = vel;
#endif
        }

        private Vector2 GetLinearVelocity()
        {
#if UNITY_6000_0_OR_NEWER
            return rb.linearVelocity;
#else
            return rb.velocity;
#endif
        }

        private void OnDrawGizmosSelected()
        {
            if (groundCheckPoint != null)
            {
                Gizmos.color = isGrounded ? Color.green : Color.red;
                Gizmos.DrawWireCube(groundCheckPoint.position + Vector3.down * 0.025f, groundCheckSize);
            }
        }

        #endregion
    }
}
