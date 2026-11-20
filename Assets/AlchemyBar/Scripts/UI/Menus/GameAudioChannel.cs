using UnityEngine;

namespace AlchemyBar.UI.Menus
{
    public enum GameAudioCategory
    {
        Music,
        SpellEffects,
        Ambient
    }

    [RequireComponent(typeof(AudioSource))]
    public sealed class GameAudioChannel : MonoBehaviour
    {
        [SerializeField] private GameAudioCategory category = GameAudioCategory.SpellEffects;
        [SerializeField, Range(0f, 1f)] private float baseVolume = 1f;

        private AudioSource source;

        private void Awake() => source = GetComponent<AudioSource>();

        public void Configure(GameAudioCategory newCategory, float newBaseVolume)
        {
            category = newCategory;
            baseVolume = newBaseVolume;
            if (source != null) Apply();
        }

        private void OnEnable()
        {
            GameSettings.EnsureLoaded();
            GameSettings.Changed += Apply;
            Apply();
        }

        private void OnDisable() => GameSettings.Changed -= Apply;

        private void Apply()
        {
            float v;
            switch (category)
            {
                case GameAudioCategory.Music: v = GameSettings.ToGain(GameSettings.MusicVolume); break;
                case GameAudioCategory.Ambient: v = GameSettings.AmbientWhispers ? GameSettings.ToGain(GameSettings.SfxVolume) : 0f; break;
                default: v = GameSettings.ToGain(GameSettings.SfxVolume); break;
            }
            source.volume = baseVolume * v;
        }
    }
}
