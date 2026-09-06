using System;
using System.Collections;
using UnityEngine;
using PlatformerGame.Managers;

namespace PlatformerGame.Core
{
    /// <summary>
    /// Quản lý lượng máu của nhân vật (Mặc định 5 tim/máu),
    /// xử lý sát thương kèm lực đẩy lùi (Knockback),
    /// thời gian bất tử chớp nháy (I-Frames), và thông báo Game Over khi hết máu.
    /// </summary>
    [RequireComponent(typeof(PlayerController2D))]
    [RequireComponent(typeof(Rigidbody2D))]
    public class PlayerHealth : MonoBehaviour
    {
        [Header("--- Chỉ số máu (Health Stats) ---")]
        [Tooltip("Số máu tối đa (Mặc định 5 Tim)")]
        [SerializeField] private int maxHealth = 5;
        [Tooltip("Số máu hiện tại")]
        [SerializeField] private int currentHealth;

        [Header("--- Bất tử & Chớp nháy (I-Frames) ---")]
        [Tooltip("Thời gian bất tử sau khi nhận sát thương (giây)")]
        [SerializeField] private float invincibilityDuration = 1.5f;
        [Tooltip("Tần suất nhấp nháy sprite (giây/lần)")]
        [SerializeField] private float flashInterval = 0.1f;
        [Tooltip("Màu khi bị dính đòn (thường là màu đỏ hoặc làm mờ)")]
        [SerializeField] private Color hurtFlashColor = new Color(1f, 0.3f, 0.3f, 0.5f);

        [Header("--- Lực đẩy lùi (Knockback) ---")]
        [Tooltip("Độ mạnh của lực đẩy lùi")]
        [SerializeField] private float defaultKnockbackForce = 8f;
        [Tooltip("Thời gian vô hiệu hóa di chuyển người chơi khi bị knockback")]
        [SerializeField] private float knockbackStunTime = 0.2f;

        [Header("--- Visual References ---")]
        [SerializeField] private SpriteRenderer spriteRenderer;

        // Components & State
        private Rigidbody2D rb;
        private PlayerController2D controller;
        private bool isInvincible;
        private bool isDead;
        private Color originalColor = Color.white;
        private Coroutine flashCoroutine;
        private Coroutine knockbackCoroutine;

        // Events
        public event Action<int, int> OnHealthChanged; // (currentHealth, maxHealth)
        public event Action OnDamaged;
        public event Action OnDeath;

        public int MaxHealth => maxHealth;
        public int CurrentHealth => currentHealth;
        public bool IsDead => isDead;
        public bool IsInvincible => isInvincible;

        private void Awake()
        {
            rb = GetComponent<Rigidbody2D>();
            controller = GetComponent<PlayerController2D>();
            if (spriteRenderer == null)
            {
                spriteRenderer = GetComponentInChildren<SpriteRenderer>();
            }

            if (spriteRenderer != null)
            {
                originalColor = spriteRenderer.color;
            }

            currentHealth = maxHealth;
        }

        private void Start()
        {
            // Cập nhật trạng thái máu ban đầu lên UI
            OnHealthChanged?.Invoke(currentHealth, maxHealth);
        }

        /// <summary>
        /// Nhận sát thương và chịu lực đẩy lùi theo hướng truyền vào.
        /// </summary>
        /// <param name="damage">Lượng sát thương</param>
        /// <param name="knockbackDirection">Hướng đẩy lùi (đã chuẩn hóa hoặc Vector2)</param>
        public void TakeDamage(int damage, Vector2 knockbackDirection)
        {
            if (isDead || isInvincible) return;

            currentHealth -= damage;
            if (currentHealth < 0) currentHealth = 0;

            OnHealthChanged?.Invoke(currentHealth, maxHealth);
            OnDamaged?.Invoke();

            if (currentHealth <= 0)
            {
                Die();
                return;
            }

            // Áp dụng lực đẩy lùi Knockback
            if (knockbackDirection.sqrMagnitude > 0.01f)
            {
                ApplyKnockback(knockbackDirection.normalized * defaultKnockbackForce);
            }

            // Kích hoạt thời gian bất tử
            if (gameObject.activeInHierarchy)
            {
                if (flashCoroutine != null) StopCoroutine(flashCoroutine);
                flashCoroutine = StartCoroutine(InvincibilityFlashRoutine());
            }
        }

        /// <summary>
        /// Hồi máu cho người chơi
        /// </summary>
        public void Heal(int amount)
        {
            if (isDead || amount <= 0) return;

            currentHealth = Mathf.Min(currentHealth + amount, maxHealth);
            OnHealthChanged?.Invoke(currentHealth, maxHealth);
        }

        /// <summary>
        /// Khôi phục đầy đủ máu
        /// </summary>
        public void ResetHealth()
        {
            isDead = false;
            currentHealth = maxHealth;
            if (spriteRenderer != null)
            {
                spriteRenderer.color = originalColor;
            }
            OnHealthChanged?.Invoke(currentHealth, maxHealth);
        }

        private void ApplyKnockback(Vector2 knockbackVelocity)
        {
            if (knockbackCoroutine != null) StopCoroutine(knockbackCoroutine);
            knockbackCoroutine = StartCoroutine(KnockbackRoutine(knockbackVelocity));
        }

        private IEnumerator KnockbackRoutine(Vector2 knockbackVelocity)
        {
            controller.SetMovementLocked(true);

#if UNITY_6000_0_OR_NEWER
            rb.linearVelocity = knockbackVelocity;
#else
            rb.velocity = knockbackVelocity;
#endif

            yield return new WaitForSeconds(knockbackStunTime);

            // Mở lại di chuyển nếu game vẫn đang chơi
            if (!isDead && PlatformerGameManager.Instance != null && PlatformerGameManager.Instance.CurrentState == GameState.Playing)
            {
                controller.SetMovementLocked(false);
            }
        }

        private IEnumerator InvincibilityFlashRoutine()
        {
            isInvincible = true;
            float elapsed = 0f;

            while (elapsed < invincibilityDuration)
            {
                if (spriteRenderer != null)
                {
                    spriteRenderer.color = hurtFlashColor;
                }
                yield return new WaitForSeconds(flashInterval);
                elapsed += flashInterval;

                if (spriteRenderer != null)
                {
                    spriteRenderer.color = originalColor;
                }
                yield return new WaitForSeconds(flashInterval);
                elapsed += flashInterval;
            }

            if (spriteRenderer != null)
            {
                spriteRenderer.color = originalColor;
            }
            isInvincible = false;
            flashCoroutine = null;
        }

        private void Die()
        {
            if (isDead) return;
            isDead = true;

            controller.SetMovementLocked(true);
            OnDeath?.Invoke();

            // Gọi Game Manager xử lý Game Over
            if (PlatformerGameManager.Instance != null)
            {
                PlatformerGameManager.Instance.SetGameOver();
            }
        }
    }
}
