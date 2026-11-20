using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace AlchemyBar.UI.Menus
{
    public sealed class MagicToggleSwitch : MonoBehaviour, IPointerClickHandler
    {
        [System.Serializable]
        public sealed class BoolEvent : UnityEvent<bool> { }

        [SerializeField] private bool isOn = true;
        [SerializeField] private Image track;
        [SerializeField] private RectTransform knob;
        [SerializeField] private Image knobImage;

        [Header("On")]
        [SerializeField] private Sprite trackOn;
        [SerializeField] private Color knobOn = Color.white;
        [SerializeField] private float knobXOn = 22f;

        [Header("Off")]
        [SerializeField] private Sprite trackOff;
        [SerializeField] private Color knobOff = Color.gray;
        [SerializeField] private float knobXOff = 2f;

        [SerializeField] private float speed = 10f;
        [SerializeField] private BoolEvent onValueChanged = new BoolEvent();

        private float position;

        public BoolEvent OnValueChanged => onValueChanged;

        public bool IsOn
        {
            get => isOn;
            set => Set(value, true);
        }

        public void Set(bool value, bool notify)
        {
            if (isOn == value) return;
            isOn = value;
            if (track != null) track.sprite = isOn ? trackOn : trackOff;
            if (notify) onValueChanged.Invoke(isOn);
        }

        public void SetWithoutNotify(bool value)
        {
            Set(value, false);
            Snap();
        }

        public void OnPointerClick(PointerEventData eventData) => IsOn = !isOn;

        private void OnEnable() => Snap();

        private void Snap()
        {
            position = isOn ? 1f : 0f;
            ApplyVisuals();
        }

        private void Update()
        {
            float target = isOn ? 1f : 0f;
            if (Mathf.Approximately(position, target)) return;
            position = Mathf.MoveTowards(position, target, Time.unscaledDeltaTime * speed);
            ApplyVisuals();
        }

        private void ApplyVisuals()
        {
            float eased = Mathf.SmoothStep(0f, 1f, position);
            if (knob != null)
            {
                Vector2 p = knob.anchoredPosition;
                p.x = Mathf.Lerp(knobXOff, knobXOn, eased);
                knob.anchoredPosition = p;
            }
            if (knobImage != null) knobImage.color = Color.Lerp(knobOff, knobOn, eased);
            if (track != null) track.sprite = isOn ? trackOn : trackOff;
        }
    }
}
