using System.Collections.Generic;
using AlchemyBar.UI.Menus;
using UnityEngine;

namespace AlchemyBar.Audio
{
    public static class GameSfx
    {
        private const string Root = "HexHearth/Sfx/";
        private const int PoolSize = 16;

        private static readonly Dictionary<string, AudioClip[]> Clips = new Dictionary<string, AudioClip[]>();
        private static readonly Dictionary<string, int> LastIndex = new Dictionary<string, int>();
        private static AudioSource[] pool;
        private static int next;

        public static AudioClip[] Get(string category)
        {
            if (!Clips.TryGetValue(category, out AudioClip[] clips))
            {
                clips = Resources.LoadAll<AudioClip>(Root + category);
                Clips[category] = clips;
            }
            return clips;
        }

        public static void PlayAt(string category, Vector3 position, float volume = 1f, float pitchJitter = 0.08f, float spatialBlend = 1f)
        {
            AudioClip[] clips = Get(category);
            if (clips.Length == 0) return;

            GameSettings.EnsureLoaded();
            float finalVolume = volume * GameSettings.ToGain(GameSettings.SfxVolume);
            if (finalVolume <= 0.001f) return;

            int index = Random.Range(0, clips.Length);
            if (clips.Length > 1 && LastIndex.TryGetValue(category, out int last) && last == index)
                index = (index + 1) % clips.Length;
            LastIndex[category] = index;

            AudioSource source = NextSource();
            source.transform.position = position;
            source.clip = clips[index];
            source.volume = finalVolume;
            source.pitch = 1f + Random.Range(-pitchJitter, pitchJitter);
            source.spatialBlend = spatialBlend;
            source.Play();
        }

        private static AudioSource NextSource()
        {
            if (pool == null || pool[0] == null)
            {
                var root = new GameObject("Game Sfx");
                Object.DontDestroyOnLoad(root);
                pool = new AudioSource[PoolSize];
                for (int i = 0; i < PoolSize; i++)
                {
                    var go = new GameObject("Sfx " + i);
                    go.transform.SetParent(root.transform, false);
                    var s = go.AddComponent<AudioSource>();
                    s.playOnAwake = false;
                    s.rolloffMode = AudioRolloffMode.Logarithmic;
                    s.minDistance = 0.6f;
                    s.maxDistance = 20f;
                    s.dopplerLevel = 0f;
                    pool[i] = s;
                }
            }

            for (int i = 0; i < PoolSize; i++)
            {
                AudioSource candidate = pool[(next + i) % PoolSize];
                if (!candidate.isPlaying)
                {
                    next = (next + i + 1) % PoolSize;
                    return candidate;
                }
            }

            AudioSource oldest = pool[next];
            next = (next + 1) % PoolSize;
            return oldest;
        }
    }
}
