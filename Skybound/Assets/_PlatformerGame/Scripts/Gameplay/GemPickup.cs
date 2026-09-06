using UnityEngine;
using PlatformerGame.Managers;

namespace PlatformerGame.Gameplay
{
    /// <summary>
    /// Kim cương nhặt trong game (tương đương 5 điểm/xu hoặc điểm thưởng lớn).
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public class GemPickup : MonoBehaviour
    {
        [SerializeField] private int value = 5;
        [SerializeField] private float bobSpeed = 4f;
        [SerializeField] private float bobHeight = 0.25f;

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

            if (other.CompareTag("Player") || other.GetComponent<PlatformerGame.Core.PlayerController2D>() != null)
            {
                isCollected = true;
                if (PlatformerGameManager.Instance != null)
                {
                    PlatformerGameManager.Instance.AddCoin(value);
                }
                Destroy(gameObject);
            }
        }
    }
}
