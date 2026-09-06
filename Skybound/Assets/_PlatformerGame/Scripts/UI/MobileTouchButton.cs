using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace PlatformerGame.UI
{
    /// <summary>
    /// Nút cảm ứng ảo dành cho Mobile.
    /// Hỗ trợ bắt chính xác sự kiện Nhấn / Giữ / Thả / Trượt ngón tay ra ngoài mượt mà.
    /// </summary>
    public class MobileTouchButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        [Header("--- Hiệu ứng thị giác khi nhấn (Visual Feedback) ---")]
        [Tooltip("Tỷ lệ thu nhỏ khi bấm nút")]
        [SerializeField] private float pressedScale = 0.9f;
        [Tooltip("Màu sắc khi bấm nút")]
        [SerializeField] private Color pressedColor = new Color(0.85f, 0.85f, 0.85f, 1f);

        [Header("--- Events ---")]
        public UnityEvent onPointerDown;
        public UnityEvent onPointerUp;

        private bool isPressed;
        private Vector3 originalScale;
        private Graphic targetGraphic;
        private Color originalColor;

        public bool IsPressed => isPressed;

        public event Action OnButtonDown;
        public event Action OnButtonUp;

        private void Awake()
        {
            originalScale = transform.localScale;
            targetGraphic = GetComponent<Graphic>();
            if (targetGraphic != null)
            {
                originalColor = targetGraphic.color;
            }
        }

        private void OnDisable()
        {
            ResetButtonState();
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            isPressed = true;
            transform.localScale = originalScale * pressedScale;
            if (targetGraphic != null)
            {
                targetGraphic.color = pressedColor;
            }

            onPointerDown?.Invoke();
            OnButtonDown?.Invoke();
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            ResetButtonState();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            // Nếu người chơi vuốt trượt ngón tay ra khỏi nút -> nhả nút
            if (isPressed)
            {
                ResetButtonState();
            }
        }

        private void ResetButtonState()
        {
            if (!isPressed) return;

            isPressed = false;
            transform.localScale = originalScale;
            if (targetGraphic != null)
            {
                targetGraphic.color = originalColor;
            }

            onPointerUp?.Invoke();
            OnButtonUp?.Invoke();
        }
    }
}
