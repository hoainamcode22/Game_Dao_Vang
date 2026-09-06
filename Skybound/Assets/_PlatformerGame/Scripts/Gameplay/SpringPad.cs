using System.Collections;
using UnityEngine;

namespace PlatformerGame.Gameplay
{
    /// <summary>
    /// Lò xo bật nhảy (Spring Pad) giúp nhân vật bật lên rất cao.
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public class SpringPad : MonoBehaviour
    {
        [Header("--- Lực Bật ---")]
        [Tooltip("Lực đẩy thẳng đứng khi giẫm lên lò xo")]
        [SerializeField] private float bounceForce = 22f;

        private Vector3 originalScale;
        private Coroutine squashRoutine;

        private void Start()
        {
            originalScale = transform.localScale;
            var col = GetComponent<Collider2D>();
            if (col != null) col.isTrigger = true;
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            var rb = other.GetComponent<Rigidbody2D>();
            if (rb != null)
            {
#if UNITY_6000_0_OR_NEWER
                rb.linearVelocity = new Vector2(rb.linearVelocity.x, bounceForce);
#else
                rb.velocity = new Vector2(rb.velocity.x, bounceForce);
#endif

                if (squashRoutine != null) StopCoroutine(squashRoutine);
                squashRoutine = StartCoroutine(AnimateBounce());
            }
        }

        private IEnumerator AnimateBounce()
        {
            // Nén lò xo
            transform.localScale = new Vector3(originalScale.x * 1.3f, originalScale.y * 0.5f, originalScale.z);
            yield return new WaitForSeconds(0.08f);

            // Giãn lò xo
            transform.localScale = new Vector3(originalScale.x * 0.8f, originalScale.y * 1.4f, originalScale.z);
            yield return new WaitForSeconds(0.12f);

            // Về trạng thái ban đầu
            transform.localScale = originalScale;
        }
    }
}
