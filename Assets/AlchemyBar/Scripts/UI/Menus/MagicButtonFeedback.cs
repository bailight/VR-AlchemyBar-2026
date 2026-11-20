using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace AlchemyBar.UI.Menus
{
    [RequireComponent(typeof(RectTransform))]
    public sealed class MagicButtonFeedback : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
    {
        [SerializeField] private float hoverScale = 1.03f;
        [SerializeField] private float pressScale = 0.97f;
        [SerializeField] private float pressDim = 0.85f;
        [SerializeField] private float speed = 14f;
        [SerializeField] private Graphic visual;

        private Selectable selectable;
        private Vector3 baseScale;
        private Color baseColor = Color.white;
        private bool hovered;
        private bool pressed;
        private float t;

        public void SetVisual(Graphic graphic) => visual = graphic;

        private void Awake()
        {
            selectable = GetComponent<Selectable>();
            baseScale = transform.localScale;
            if (visual != null) baseColor = visual.color;
        }

        private void OnDisable()
        {
            hovered = pressed = false;
            t = 0f;
            transform.localScale = baseScale;
            if (visual != null) visual.color = baseColor;
        }

        public void OnPointerEnter(PointerEventData eventData) => hovered = true;
        public void OnPointerExit(PointerEventData eventData) { hovered = false; pressed = false; }
        public void OnPointerDown(PointerEventData eventData) => pressed = true;
        public void OnPointerUp(PointerEventData eventData) => pressed = false;

        private void Update()
        {
            bool interactable = selectable == null || selectable.IsInteractable();
            float target = !interactable ? 0f : pressed ? -1f : hovered ? 1f : 0f;
            t = Mathf.MoveTowards(t, target, Time.unscaledDeltaTime * speed);

            float scale = t >= 0f ? Mathf.Lerp(1f, hoverScale, t) : Mathf.Lerp(1f, pressScale, -t);
            transform.localScale = baseScale * scale;

            if (visual != null)
            {
                float k = t < 0f ? Mathf.Lerp(1f, pressDim, -t) : 1f;
                visual.color = new Color(baseColor.r * k, baseColor.g * k, baseColor.b * k, baseColor.a);
            }
        }
    }
}
