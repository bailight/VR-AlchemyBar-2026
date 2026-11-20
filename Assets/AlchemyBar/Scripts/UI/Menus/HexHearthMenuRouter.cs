using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UnityEngine.XR;
using UnityEngine.XR.Interaction.Toolkit.Inputs;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Simulation;

namespace AlchemyBar.UI.Menus
{
    public enum HexHearthScreen { None, MainMenu, Settings, Pause }

    public enum MusicMood { MainMenu, Gameplay, Pause }

    public enum MenuPresentation
    {
        Auto,
        FollowHead,
        FixedInWorld,
        ScreenOverlay
    }

    public sealed class HexHearthMenuRouter : MonoBehaviour
    {
        [SerializeField] private GameObject mainMenu;
        [SerializeField] private GameObject settings;
        [SerializeField] private GameObject pause;

        [SerializeField] private HexHearthScreen startScreen = HexHearthScreen.MainMenu;

        [Header("Pause")]
        [Tooltip("Optional: e.g. XRI Left/Menu button. Toggles the pause menu during gameplay.")]
        [SerializeField] private InputActionReference pauseAction;
        [Tooltip("Esc (keyboard) and Start (gamepad): open pause in gameplay, go back in menus, close pause.")]
        [SerializeField] private bool escapeOpensPause = true;
        [Tooltip("XR Device Simulator's \"Stop Manipulation\" is moved off Esc to this binding so Esc stays the pause key.")]
        [SerializeField] private string simulatorStopManipulationBinding = "<Keyboard>/backquote";
        [Tooltip("Time.timeScale = 0 while any menu is open: physics, animations, particles and the simulator body stop.")]
        [SerializeField] private bool freezeWorldWhileMenuOpen = true;
        [Tooltip("Disable the XRI locomotion input actions (move, turn, teleport...) while any menu is open.")]
        [SerializeField] private bool lockLocomotionWhileMenuOpen = true;
        [Tooltip("Screen overlay: colour around the 16:9 frame on other aspect ratios (matches the backdrop's edge).")]
        [SerializeField] private Color screenFillColor = new Color(22f / 255f, 14f / 255f, 36f / 255f, 1f);

        [Header("Presentation")]
        [SerializeField] private MenuPresentation presentation = MenuPresentation.Auto;
        [SerializeField] private float distance = 1.1f;
        [SerializeField] private float verticalOffset = -0.05f;
        [Tooltip("FollowHead: the menu starts moving once the gaze is this many degrees away from it.")]
        [SerializeField] private float recenterAngle = 20f;
        [SerializeField] private float followSmoothTime = 0.25f;
        [Tooltip("Menu canvas resolution (Full HD). Screen overlay fits it to the screen.")]
        [SerializeField] private Vector2 referenceResolution = new Vector2(1920f, 1080f);
        [Tooltip("World-space size of one canvas pixel: 0.72 m / 1080 px gives a 1.28 m x 0.72 m panel.")]
        [SerializeField] private float worldUnitsPerPixel = 0.72f / 1080f;

        public static MusicMood Mood { get; private set; } = MusicMood.Gameplay;
        public static event System.Action<MusicMood> MoodChanged;

        private readonly Stack<HexHearthScreen> history = new Stack<HexHearthScreen>();
        private HexHearthScreen current = HexHearthScreen.None;
        private float timeScaleBeforePause = 1f;
        private bool frozen;

        private MenuPresentation activeMode;
        private bool following;
        private Vector3 followVelocity;
        private bool needsInitialPlacement;
        private readonly List<InputAction> lockedActions = new List<InputAction>();
        private readonly List<GameObject> screenFills = new List<GameObject>();

        public HexHearthScreen Current => current;
        public bool IsInGameplay => current == HexHearthScreen.None;
        public MenuPresentation ActiveMode => activeMode;

        private void Awake()
        {
            WrapCanvasContent();
            GameSettings.EnsureLoaded();
            foreach (Canvas canvas in GetComponentsInChildren<Canvas>(true))
                if (canvas.isRootCanvas && canvas.GetComponent<MenuLocalizer>() == null)
                    canvas.gameObject.AddComponent<MenuLocalizer>();
            SetActive(HexHearthScreen.None);
        }

        private void OnDestroy()
        {
            if (frozen) Time.timeScale = timeScaleBeforePause;
            UnlockLocomotion();
        }

        private void Start()
        {
            ApplyPresentation(presentation);
            Show(startScreen, false);
        }

        private void OnEnable()
        {
            if (pauseAction == null) return;
            pauseAction.action.performed += OnPausePressed;
            pauseAction.action.Enable();
        }

        private void OnDisable()
        {
            if (pauseAction != null) pauseAction.action.performed -= OnPausePressed;
        }

        private void OnPausePressed(InputAction.CallbackContext _) => HandleBackKey();

        private void Update()
        {
            if (!escapeOpensPause) return;
            MoveSimulatorOffEscape();
            bool pressed = Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame
                           || Gamepad.current != null && Gamepad.current.startButton.wasPressedThisFrame;
            if (pressed) HandleBackKey();
        }

        private XRDeviceSimulator reboundSimulator;
        private float nextSimulatorCheck;

        private void MoveSimulatorOffEscape()
        {
            if (reboundSimulator != null || Time.unscaledTime < nextSimulatorCheck) return;
            nextSimulatorCheck = Time.unscaledTime + 0.5f;

            XRDeviceSimulator simulator = FindAnyObjectByType<XRDeviceSimulator>();
            if (simulator == null || simulator.stopManipulationAction == null) return;
            InputAction action = simulator.stopManipulationAction.action;
            if (action == null) return;

            for (int i = 0; i < action.bindings.Count; i++)
                if (string.Equals(action.bindings[i].effectivePath, "<Keyboard>/escape", System.StringComparison.OrdinalIgnoreCase))
                    action.ApplyBindingOverride(i, simulatorStopManipulationBinding);
            reboundSimulator = simulator;
        }

        public void HandleBackKey()
        {
            switch (current)
            {
                case HexHearthScreen.None: ShowPause(); break;
                case HexHearthScreen.Pause: HideAll(); break;
                case HexHearthScreen.Settings: Back(); break;
            }
        }

        public void ShowMainMenu() { history.Clear(); Show(HexHearthScreen.MainMenu, false); }
        public void ShowSettings() => Show(HexHearthScreen.Settings, true);
        public void ShowPause() { history.Clear(); Show(HexHearthScreen.Pause, false); }

        public void Back()
        {
            if (history.Count > 0) Show(history.Pop(), false);
            else HideAll();
        }

        public void HideAll()
        {
            history.Clear();
            Show(HexHearthScreen.None, false);
        }

        public void SetPresentation(MenuPresentation mode)
        {
            presentation = mode;
            ApplyPresentation(mode);
        }

        private void Show(HexHearthScreen screen, bool remember)
        {
            if (remember && current != HexHearthScreen.None && current != screen) history.Push(current);
            bool wasHidden = current == HexHearthScreen.None;
            current = screen;
            SetActive(screen);
            UpdateMood();
            UpdateTimeScale();
            if (wasHidden && screen != HexHearthScreen.None) needsInitialPlacement = true;
        }

        private void UpdateMood()
        {
            MusicMood mood = Mood;
            switch (current)
            {
                case HexHearthScreen.None: mood = MusicMood.Gameplay; break;
                case HexHearthScreen.MainMenu: mood = MusicMood.MainMenu; break;
                case HexHearthScreen.Pause: mood = MusicMood.Pause; break;
                case HexHearthScreen.Settings:
                    if (history.Contains(HexHearthScreen.Pause)) mood = MusicMood.Pause;
                    else if (history.Contains(HexHearthScreen.MainMenu)) mood = MusicMood.MainMenu;
                    break;
            }
            if (mood == Mood) return;
            Mood = mood;
            MoodChanged?.Invoke(mood);
        }

        private void SetActive(HexHearthScreen screen)
        {
            if (mainMenu != null) mainMenu.SetActive(screen == HexHearthScreen.MainMenu);
            if (settings != null) settings.SetActive(screen == HexHearthScreen.Settings);
            if (pause != null) pause.SetActive(screen == HexHearthScreen.Pause);
        }

        private void UpdateTimeScale()
        {
            bool menuOpen = current != HexHearthScreen.None;

            bool shouldFreeze = freezeWorldWhileMenuOpen && menuOpen;
            if (shouldFreeze && !frozen) { timeScaleBeforePause = Time.timeScale; Time.timeScale = 0f; frozen = true; }
            else if (!shouldFreeze && frozen) { Time.timeScale = timeScaleBeforePause; frozen = false; }

            if (lockLocomotionWhileMenuOpen && menuOpen) LockLocomotion();
            else UnlockLocomotion();

            UpdateCursor(menuOpen);
        }

        private bool cursorReleased;
        private CursorLockMode cursorLockBefore;
        private bool cursorVisibleBefore;

        private void UpdateCursor(bool menuOpen)
        {
            bool release = menuOpen && activeMode == MenuPresentation.ScreenOverlay;
            if (release && !cursorReleased)
            {
                cursorLockBefore = Cursor.lockState;
                cursorVisibleBefore = Cursor.visible;
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
                cursorReleased = true;
            }
            else if (!release && cursorReleased)
            {
                Cursor.lockState = cursorLockBefore;
                Cursor.visible = cursorVisibleBefore;
                cursorReleased = false;
            }
        }

        private void LockLocomotion()
        {
            if (lockedActions.Count > 0) return;
            foreach (InputActionManager manager in FindObjectsByType<InputActionManager>(FindObjectsSortMode.None))
            {
                if (manager.actionAssets == null) continue;
                foreach (InputActionAsset asset in manager.actionAssets)
                {
                    if (asset == null) continue;
                    foreach (InputActionMap map in asset.actionMaps)
                    {
                        if (map.name.IndexOf("Locomotion", System.StringComparison.OrdinalIgnoreCase) < 0) continue;
                        foreach (InputAction action in map.actions)
                        {
                            if (!action.enabled) continue;
                            action.Disable();
                            lockedActions.Add(action);
                        }
                    }
                }
            }
        }

        private void UnlockLocomotion()
        {
            foreach (InputAction action in lockedActions)
                if (action != null) action.Enable();
            lockedActions.Clear();
        }

        #region Presentation

        private void WrapCanvasContent()
        {
            foreach (Canvas canvas in GetComponentsInChildren<Canvas>(true))
            {
                if (!canvas.isRootCanvas || canvas.transform.Find("Frame") != null) continue;
                var root = (RectTransform)canvas.transform;

                var children = new List<Transform>();
                foreach (Transform child in root) children.Add(child);

                var fill = new GameObject("ScreenFill", typeof(RectTransform), typeof(Image));
                var fillRt = (RectTransform)fill.transform;
                fillRt.SetParent(root, false);
                fillRt.anchorMin = Vector2.zero;
                fillRt.anchorMax = Vector2.one;
                fillRt.offsetMin = fillRt.offsetMax = Vector2.zero;
                var fillImage = fill.GetComponent<Image>();
                fillImage.color = screenFillColor;
                fillImage.raycastTarget = true;
                fill.layer = canvas.gameObject.layer;
                screenFills.Add(fill);

                var frame = new GameObject("Frame", typeof(RectTransform), typeof(RectMask2D));
                var frameRt = (RectTransform)frame.transform;
                frameRt.SetParent(root, false);
                frameRt.anchorMin = frameRt.anchorMax = frameRt.pivot = new Vector2(0.5f, 0.5f);
                frameRt.anchoredPosition = Vector2.zero;
                frameRt.sizeDelta = referenceResolution;
                frame.layer = canvas.gameObject.layer;

                foreach (Transform child in children) child.SetParent(frameRt, false);
            }
        }

        private void ApplyPresentation(MenuPresentation requested)
        {
            activeMode = requested == MenuPresentation.Auto
                ? (XRSettings.isDeviceActive ? MenuPresentation.FollowHead : MenuPresentation.ScreenOverlay)
                : requested;

            foreach (GameObject fill in screenFills)
                if (fill != null) fill.SetActive(activeMode == MenuPresentation.ScreenOverlay);

            Camera cam = Camera.main;
            foreach (Canvas canvas in GetComponentsInChildren<Canvas>(true))
            {
                if (!canvas.isRootCanvas) continue;
                var rt = (RectTransform)canvas.transform;
                var scaler = canvas.GetComponent<CanvasScaler>();

                if (activeMode == MenuPresentation.ScreenOverlay)
                {
                    canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                    canvas.sortingOrder = 100;
                    if (scaler != null)
                    {
                        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                        scaler.referenceResolution = referenceResolution;
                        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
                    }
                }
                else
                {
                    canvas.renderMode = RenderMode.WorldSpace;
                    canvas.worldCamera = cam;
                    if (scaler != null) scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
                    rt.sizeDelta = referenceResolution;
                    rt.localPosition = Vector3.zero;
                    rt.localRotation = Quaternion.identity;
                    rt.localScale = Vector3.one * worldUnitsPerPixel;
                }
            }

            needsInitialPlacement = true;
        }

        private void LateUpdate()
        {
            if (current == HexHearthScreen.None || activeMode == MenuPresentation.ScreenOverlay) return;
            Camera cam = Camera.main;
            if (cam == null) return;

            Transform head = cam.transform;
            Vector3 forward = Vector3.ProjectOnPlane(head.forward, Vector3.up);
            if (forward.sqrMagnitude < 1e-4f) forward = Vector3.ProjectOnPlane(head.up, Vector3.up);
            forward.Normalize();
            Vector3 targetPos = head.position + forward * distance + Vector3.up * verticalOffset;

            if (needsInitialPlacement)
            {
                if (head.localPosition == Vector3.zero && XRSettings.isDeviceActive) return;
                transform.SetPositionAndRotation(targetPos, Quaternion.LookRotation(forward, Vector3.up));
                needsInitialPlacement = false;
                following = false;
                return;
            }

            if (activeMode != MenuPresentation.FollowHead) return;

            Vector3 toMenu = Vector3.ProjectOnPlane(transform.position - head.position, Vector3.up);
            float angle = Vector3.Angle(forward, toMenu);
            float distError = Mathf.Abs(toMenu.magnitude - distance);
            if (!following && (angle > recenterAngle || distError > 0.35f)) following = true;

            if (following)
            {
                transform.position = Vector3.SmoothDamp(transform.position, targetPos, ref followVelocity,
                                                        followSmoothTime, Mathf.Infinity, Time.unscaledDeltaTime);
                Vector3 look = Vector3.ProjectOnPlane(transform.position - head.position, Vector3.up);
                if (look.sqrMagnitude > 1e-4f)
                    transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(look, Vector3.up),
                                                          1f - Mathf.Exp(-10f * Time.unscaledDeltaTime));
                if ((transform.position - targetPos).sqrMagnitude < 0.0004f) following = false;
            }
        }

        #endregion
    }
}
