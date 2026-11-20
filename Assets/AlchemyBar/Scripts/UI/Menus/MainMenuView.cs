using AlchemyBar.Core;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace AlchemyBar.UI.Menus
{
    public sealed class MainMenuView : MonoBehaviour
    {
        [SerializeField] private HexHearthMenuRouter router;
        [SerializeField] private Button startButton;
        [SerializeField] private Button settingsButton;
        [SerializeField] private Button exitButton;

        [Tooltip("Hook gameplay start here (load scene, enable rig, start recipe...). The menu hides itself afterwards.")]
        [SerializeField] private UnityEvent onStartRequested = new UnityEvent();

        public UnityEvent OnStartRequested => onStartRequested;

        private void Awake()
        {
            if (router == null) router = GetComponentInParent<HexHearthMenuRouter>();
            if (startButton != null) startButton.onClick.AddListener(HandleStart);
            if (settingsButton != null) settingsButton.onClick.AddListener(() => router?.ShowSettings());
            if (exitButton != null) exitButton.onClick.AddListener(Quit);
        }

        private void HandleStart()
        {
            SessionStats.ResetSession();
            onStartRequested.Invoke();
            router?.HideAll();
        }

        private static void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
