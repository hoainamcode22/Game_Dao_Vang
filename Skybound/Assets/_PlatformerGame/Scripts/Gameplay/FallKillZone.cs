using UnityEngine;
using PlatformerGame.Core;

namespace PlatformerGame.Gameplay
{
    /// <summary>
    /// Vùng vực thẳm / Hố chết (Bottomless Pit).
    /// Khi nhân vật rơi xuống vượt qua ranh giới này, lập tức xử tử (Instant Kill / Game Over).
    /// </summary>
    [RequireComponent(typeof(BoxCollider2D))]
    public class FallKillZone : MonoBehaviour
    {
        [Tooltip("Số damage hạ sát ngay lập tức")]
        [SerializeField] private int instantKillDamage = 999;

        private void Awake()
        {
            BoxCollider2D box = GetComponent<BoxCollider2D>();
            if (box != null)
            {
                box.isTrigger = true;
            }
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            PlayerHealth health = other.GetComponent<PlayerHealth>();
            if (health == null)
            {
                health = other.GetComponentInParent<PlayerHealth>();
            }

            if (health != null && !health.IsDead)
            {
                health.TakeDamage(instantKillDamage, Vector2.zero);
            }
        }

        private void OnDrawGizmos()
        {
            BoxCollider2D box = GetComponent<BoxCollider2D>();
            if (box != null)
            {
                Gizmos.color = new Color(1f, 0f, 0f, 0.4f);
                Gizmos.DrawCube(transform.position + (Vector3)box.offset, box.size);
            }
        }
    }
}
