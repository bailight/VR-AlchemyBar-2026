using AlchemyBar.Core;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace AlchemyBar.UI.Menus
{
    public sealed class PauseMenuView : MonoBehaviour
    {
        [SerializeField] private HexHearthMenuRouter router;
        [SerializeField] private Button resumeButton;
        [SerializeField] private Button settingsButton;
        [SerializeField] private Button saveAndReturnButton;
        [SerializeField] private Button abandonButton;

        [Header("Session info")]
        [SerializeField] private TMP_Text potionsBrewed;
        [SerializeField] private TMP_Text goldEarned;
        [SerializeField] private TMP_Text sessionTime;

        [SerializeField] private UnityEvent onResume = new UnityEvent();
        [Tooltip("Save game state here. Afterwards the main menu is shown.")]
        [SerializeField] private UnityEvent onSaveAndReturn = new UnityEvent();
        [Tooltip("Discard the current brew here. Afterwards the main menu is shown.")]
        [SerializeField] private UnityEvent onAbandon = new UnityEvent();

        public UnityEvent OnResume => onResume;
        public UnityEvent OnSaveAndReturn => onSaveAndReturn;
        public UnityEvent OnAbandon => onAbandon;

        private void Awake()
        {
            if (router == null) router = GetComponentInParent<HexHearthMenuRouter>();
            if (resumeButton != null) resumeButton.onClick.AddListener(() => { onResume.Invoke(); router?.HideAll(); });
            if (settingsButton != null) settingsButton.onClick.AddListener(() => router?.ShowSettings());
            if (saveAndReturnButton != null) saveAndReturnButton.onClick.AddListener(() => { onSaveAndReturn.Invoke(); router?.ShowMainMenu(); });
            if (abandonButton != null) abandonButton.onClick.AddListener(() => { onAbandon.Invoke(); router?.ShowMainMenu(); });
        }

        private void OnEnable() => RefreshSessionInfo();

        public void RefreshSessionInfo() =>
            SetSessionInfo(SessionStats.PotionsBrewed, SessionStats.GoldEarned, SessionStats.SessionSeconds);

        public void SetSessionInfo(int potions, int gold, float seconds)
        {
            if (potionsBrewed != null) potionsBrewed.text = potions.ToString();
            if (goldEarned != null) goldEarned.text = gold.ToString();
            if (sessionTime != null)
            {
                int s = Mathf.Max(0, Mathf.FloorToInt(seconds));
                sessionTime.text = s >= 3600 ? $"{s / 3600}:{s / 60 % 60:00}:{s % 60:00}" : $"{s / 60}:{s % 60:00}";
            }
        }
    }
}
