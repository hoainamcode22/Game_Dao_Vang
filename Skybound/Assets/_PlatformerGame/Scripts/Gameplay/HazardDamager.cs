using UnityEngine;
using PlatformerGame.Core;

namespace PlatformerGame.Gameplay
{
    /// <summary>
    /// Chướng ngại vật / Bãi gai gây sát thương cho nhân vật.
    /// Khi Player va chạm hoặc đi vào vùng Trigger -> gây damage kèm lực hất văng (Knockback).
    /// </summary>
    public class HazardDamager : MonoBehaviour
    {
        [Header("--- Sát thương (Damage Settings) ---")]
        [Tooltip("Số máu bị trừ khi va chạm")]
        [SerializeField] private int damage = 1;
        [Tooltip("Góc hất văng ngược lại khi dính bẫy")]
        [SerializeField] private Vector2 knockbackDirection = new Vector2(0f, 1f);

        [Header("--- Hiệu ứng âm thanh (Audio) ---")]
        [SerializeField] private AudioClip hitSound;

        private void OnTriggerEnter2D(Collider2D other)
        {
            CheckAndDamagePlayer(other.gameObject);
        }

        private void OnCollisionEnter2D(Collision2D collision)
        {
            CheckAndDamagePlayer(collision.gameObject);
        }

        private void CheckAndDamagePlayer(GameObject target)
        {
            PlayerHealth health = target.GetComponent<PlayerHealth>();
            if (health == null)
            {
                health = target.GetComponentInParent<PlayerHealth>();
            }

            if (health != null && !health.IsDead && !health.IsInvincible)
            {
                // Tính toán hướng đẩy lùi (hất lên và văng ra xa tâm bẫy)
                Vector2 diff = (target.transform.position - transform.position);
                Vector2 finalKnockback = new Vector2(
                    Mathf.Sign(diff.x != 0 ? diff.x : 1f) * Mathf.Abs(knockbackDirection.x),
                    Mathf.Max(0.5f, knockbackDirection.y)
                );

                if (hitSound != null)
                {
                    AudioSource.PlayClipAtPoint(hitSound, transform.position, 1f);
                }

                health.TakeDamage(damage, finalKnockback);
            }
        }
    }
}
