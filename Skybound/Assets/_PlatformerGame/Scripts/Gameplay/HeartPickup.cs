using UnityEngine;
using PlatformerGame.Core;
using PlatformerGame.Managers;

namespace PlatformerGame.Gameplay
{
    /// <summary>
    /// Trái tim / Kho máu nhặt trên đường đi để hồi máu.
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public class HeartPickup : MonoBehaviour
    {
        [Header("--- Hồi Máu ---")]
        [Tooltip("Lượng máu hồi phục (mặc định +1 tim)")]
        [SerializeField] private int healAmount = 1;
        [Tooltip("Hồi phục đầy 100% máu?")]
        [SerializeField] private bool fullHeal = false;

        [Header("--- Hiệu ứng bập bồng ---")]
        [SerializeField] private float bobSpeed = 3f;
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
            float newY = startPos.y + Mathf.Sin(Time.time * bobSpeed) * bobHeight;
            transform.position = new Vector3(startPos.x, newY, startPos.z);
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (isCollected) return;

            var playerHealth = other.GetComponent<PlayerHealth>();
            if (playerHealth == null) playerHealth = other.GetComponentInParent<PlayerHealth>();

            if (playerHealth != null)
            {
                if (playerHealth.CurrentHealth < playerHealth.MaxHealth)
                {
                    isCollected = true;
                    if (fullHeal)
                        playerHealth.ResetHealth();
                    else
                        playerHealth.Heal(healAmount);

                    if (StageManager.Instance != null)
                        StageManager.Instance.ShowMessage("❤️ Đã hồi máu!");

                    Destroy(gameObject);
                }
            }
        }
    }
}
