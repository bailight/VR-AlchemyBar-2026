using AlchemyBar.UI.Menus;
using UnityEngine;

namespace AlchemyBar.Audio
{
    public sealed class BackgroundMusic : MonoBehaviour
    {
        private static readonly string[] ClipPaths =
        {
            "HexHearth/Music/HexHearth_Menu",
            "HexHearth/Music/HexHearth_Game",
            "HexHearth/Music/HexHearth_Pause"
        };

        [SerializeField, Range(0f, 1f)] private float menuVolume = 0.7f;
        [SerializeField, Range(0f, 1f)] private float gameplayVolume = 0.55f;
        [SerializeField, Range(0f, 1f)] private float pauseVolume = 0.7f;
        [SerializeField] private float crossfadeSeconds = 1.5f;
        [SerializeField] private float firstFadeInSeconds = 3f;

        private readonly AudioSource[] sources = new AudioSource[3];
        private readonly float[] levels = new float[3];
        private bool firstTrackStarted;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (FindAnyObjectByType<BackgroundMusic>() != null) return;
            var go = new GameObject("Background Music");
            DontDestroyOnLoad(go);
            go.AddComponent<BackgroundMusic>();
        }

        private void Awake()
        {
            GameSettings.EnsureLoaded();
            for (int i = 0; i < ClipPaths.Length; i++)
            {
                var clip = Resources.Load<AudioClip>(ClipPaths[i]);
                if (clip == null) continue;
                var s = gameObject.AddComponent<AudioSource>();
                s.clip = clip;
                s.loop = true;
                s.playOnAwake = false;
                s.spatialBlend = 0f;
                s.priority = 0;
                s.volume = 0f;
                sources[i] = s;
            }
        }

        private void Update()
        {
            int target = (int)HexHearthMenuRouter.Mood;
            float seconds = firstTrackStarted ? crossfadeSeconds : firstFadeInSeconds;
            float step = Time.unscaledDeltaTime / Mathf.Max(0.01f, seconds);

            for (int i = 0; i < sources.Length; i++)
            {
                AudioSource s = sources[i];
                if (s == null) continue;

                levels[i] = Mathf.MoveTowards(levels[i], i == target ? 1f : 0f, step);
                s.volume = levels[i] * levels[i] * TrackVolume(i) * GameSettings.ToGain(GameSettings.MusicVolume);

                if (levels[i] > 0f && !s.isPlaying)
                {
                    if (s.time > 0f) s.UnPause();
                    else s.Play();
                }
                else if (levels[i] <= 0f && s.isPlaying)
                {
                    s.Pause();
                }

                if (i == target && levels[i] >= 1f) firstTrackStarted = true;
            }
        }

        private float TrackVolume(int index)
        {
            switch ((MusicMood)index)
            {
                case MusicMood.MainMenu: return menuVolume;
                case MusicMood.Pause: return pauseVolume;
                default: return gameplayVolume;
            }
        }
    }
}
