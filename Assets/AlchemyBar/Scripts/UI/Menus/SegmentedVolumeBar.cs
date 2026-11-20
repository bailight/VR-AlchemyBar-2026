using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace AlchemyBar.UI.Menus
{
    [RequireComponent(typeof(RectTransform))]
    public sealed class SegmentedVolumeBar : MonoBehaviour, IPointerDownHandler, IDragHandler
    {
        [System.Serializable]
        public sealed class FloatEvent : UnityEvent<float> { }

        [SerializeField] private Image[] segments;
        [Tooltip("Colour of each lit segment, left to right.")]
        [SerializeField] private Color[] activeColors;
        [SerializeField] private Color inactiveColor = new Color(0.2f, 0.15f, 0.3f, 1f);
        [SerializeField, Range(0, 5)] private int level = 4;
        [SerializeField] private FloatEvent onValueChanged = new FloatEvent();

        public FloatEvent OnValueChanged => onValueChanged;
        public int SegmentCount => segments != null ? segments.Length : 0;

        public float Value
        {
            get => SegmentCount == 0 ? 0f : (float)level / SegmentCount;
            set => SetLevel(Mathf.RoundToInt(Mathf.Clamp01(value) * SegmentCount), true);
        }

        public void SetValueWithoutNotify(float value) => SetLevel(Mathf.RoundToInt(Mathf.Clamp01(value) * SegmentCount), false);

        public void Configure(Image[] segmentImages, Color[] litColors, Color unlitColor, int startLevel)
        {
            segments = segmentImages;
            activeColors = litColors;
            inactiveColor = unlitColor;
            level = startLevel;
            Refresh();
        }

        private void OnEnable() => Refresh();
        private void OnValidate() => Refresh();

        public void OnPointerDown(PointerEventData eventData) => Pick(eventData.pointerPressRaycast.worldPosition, eventData);
        public void OnDrag(PointerEventData eventData) => Pick(eventData.pointerCurrentRaycast.worldPosition, eventData);

        private void Pick(Vector3 worldPosition, PointerEventData eventData)
        {
            var rt = (RectTransform)transform;
            Vector2 local;
            if (worldPosition != Vector3.zero)
                local = rt.InverseTransformPoint(worldPosition);
            else if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(rt, eventData.position, eventData.pressEventCamera, out local))
                return;

            Rect r = rt.rect;
            float u = Mathf.InverseLerp(r.xMin, r.xMax, local.x);

            int newLevel = u < 0.04f ? 0 : Mathf.Clamp(Mathf.CeilToInt(u * SegmentCount), 1, SegmentCount);
            SetLevel(newLevel, true);
        }

        private void SetLevel(int newLevel, bool notify)
        {
            newLevel = Mathf.Clamp(newLevel, 0, SegmentCount);
            bool changed = newLevel != level;
            level = newLevel;
            Refresh();
            if (changed && notify) onValueChanged.Invoke(Value);
        }

        private void Refresh()
        {
            if (segments == null) return;
            for (int i = 0; i < segments.Length; i++)
            {
                if (segments[i] == null) continue;
                bool lit = i < level;
                Color lc = activeColors != null && activeColors.Length > 0 ? activeColors[Mathf.Min(i, activeColors.Length - 1)] : Color.white;
                segments[i].color = lit ? lc : inactiveColor;
            }
        }
    }
}
