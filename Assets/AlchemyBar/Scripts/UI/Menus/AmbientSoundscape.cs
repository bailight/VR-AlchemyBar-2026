using UnityEngine;

namespace AlchemyBar.UI.Menus
{
    [RequireComponent(typeof(AudioSource))]
    public sealed class AmbientSoundscape : MonoBehaviour
    {
        private const string ClipPath = "HexHearth/HexHearth_Ambient";

        [SerializeField, Range(0f, 1f)] private float volume = 0.8f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (FindAnyObjectByType<AmbientSoundscape>() != null) return;
            var clip = Resources.Load<AudioClip>(ClipPath);
            if (clip == null) return;

            var go = new GameObject("Ambient Soundscape");
            DontDestroyOnLoad(go);
            var source = go.AddComponent<AudioSource>();
            source.clip = clip;
            source.loop = true;
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            source.priority = 200;
            go.AddComponent<AmbientSoundscape>();
        }

        private void Start()
        {
            var source = GetComponent<AudioSource>();
            if (!TryGetComponent(out GameAudioChannel channel)) channel = gameObject.AddComponent<GameAudioChannel>();
            channel.Configure(GameAudioCategory.Ambient, volume);
            if (source.clip != null && !source.isPlaying) source.Play();
        }
    }
}
