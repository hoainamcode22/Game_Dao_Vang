using UnityEngine;

namespace PlatformerGame.CameraSystem
{
    /// <summary>
    /// Camera 2D theo dõi mượt mà theo nhân vật (Player).
    /// Hỗ trợ SmoothDamp / Lerp, Offset nhìn trước, giới hạn Min Y để không rơi theo hố vực.
    /// </summary>
    public class CameraFollow2D : MonoBehaviour
    {
        [Header("--- Mục tiêu theo dõi (Target) ---")]
        [Tooltip("Transform của Player hoặc nhân vật chính")]
        [SerializeField] private Transform target;

        [Header("--- Căn chỉnh & Độ mượt (Smooth & Offset) ---")]
        [Tooltip("Khoảng cách lệch tâm (X: nhìn trước, Y: nâng cao góc nhìn, Z: khoảng cách camera)")]
        [SerializeField] private Vector3 offset = new Vector3(2f, 1.5f, -10f);
        [Tooltip("Thời gian làm mượt chuyển động")]
        [SerializeField] private float smoothTime = 0.2f;

        [Header("--- Giới hạn biên (Camera Bounds) ---")]
        [Tooltip("Có bật giới hạn Min Y (ngăn camera rơi xuống hố chết) không")]
        [SerializeField] private bool useMinYLimit = true;
        [Tooltip("Độ cao Y tối thiểu mà Camera được phép hạ xuống")]
        [SerializeField] private float minY = -1f;

        [Tooltip("Có bật giới hạn Min X (không cho lùi lại quá xa) không")]
        [SerializeField] private bool useMinXLimit = true;
        [Tooltip("Tọa độ X tối thiểu")]
        [SerializeField] private float minX = -5f;

        private Vector3 currentVelocity = Vector3.zero;

        private void LateUpdate()
        {
            if (target == null) return;

            // Tính vị trí đích
            Vector3 targetPosition = target.position + offset;

            // Áp dụng giới hạn biên
            if (useMinYLimit && targetPosition.y < minY)
            {
                targetPosition.y = minY;
            }

            if (useMinXLimit && targetPosition.x < minX)
            {
                targetPosition.x = minX;
            }

            // Luôn giữ khoảng cách Z camera
            targetPosition.z = offset.z;

            // Di chuyển mượt bằng SmoothDamp
            transform.position = Vector3.SmoothDamp(transform.position, targetPosition, ref currentVelocity, smoothTime);
        }

        /// <summary>
        /// Gán mục tiêu mới cho Camera theo dõi
        /// </summary>
        public void SetTarget(Transform newTarget)
        {
            target = newTarget;
        }

        /// <summary>
        /// Đặt camera ngay tức thì tới vị trí mục tiêu mà không chờ làm mượt (dùng khi bắt đầu màn chơi)
        /// </summary>
        public void SnapToTarget()
        {
            if (target == null) return;

            Vector3 targetPos = target.position + offset;
            if (useMinYLimit && targetPos.y < minY) targetPos.y = minY;
            if (useMinXLimit && targetPos.x < minX) targetPos.x = minX;
            targetPos.z = offset.z;

            transform.position = targetPos;
            currentVelocity = Vector3.zero;
        }
    }
}
