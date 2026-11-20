using System;
using UnityEngine;

namespace AlchemyBar.UI.Menus
{
    public static class GameSettings
    {
        private const string Prefix = "AlchemyBar.Settings.";

        public static readonly string[] Languages = { "English", "Русский" };

        public static float MusicVolume { get; set; } = 0.8f;
        public static float SfxVolume { get; set; } = 0.6f;
        public static bool AmbientWhispers { get; set; } = true;
        public static bool BrewShakeFx { get; set; }
        public static int LanguageIndex { get; set; }

        public static string Language => Languages[Mathf.Clamp(LanguageIndex, 0, Languages.Length - 1)];

        public static event Action Changed;

        public static int DisplayLanguageIndex { get; private set; }

        public static event Action<int> LanguageChanged;

        public static void PreviewLanguage(int index)
        {
            index = Mathf.Clamp(index, 0, Languages.Length - 1);
            if (index == DisplayLanguageIndex) return;
            DisplayLanguageIndex = index;
            LanguageChanged?.Invoke(index);
        }

        private static bool loaded;

        public static void EnsureLoaded()
        {
            if (loaded) return;
            loaded = true;
            MusicVolume = PlayerPrefs.GetFloat(Prefix + "MusicVolume", MusicVolume);
            SfxVolume = PlayerPrefs.GetFloat(Prefix + "SfxVolume", SfxVolume);
            AmbientWhispers = PlayerPrefs.GetInt(Prefix + "AmbientWhispers", AmbientWhispers ? 1 : 0) == 1;
            BrewShakeFx = PlayerPrefs.GetInt(Prefix + "BrewShakeFx", BrewShakeFx ? 1 : 0) == 1;
            LanguageIndex = PlayerPrefs.GetInt(Prefix + "Language", LanguageIndex);
            DisplayLanguageIndex = Mathf.Clamp(LanguageIndex, 0, Languages.Length - 1);
        }

        public static float ToGain(float sliderValue) => sliderValue * sliderValue;

        public static void NotifyChanged() => Changed?.Invoke();

        public static void Save()
        {
            PlayerPrefs.SetFloat(Prefix + "MusicVolume", MusicVolume);
            PlayerPrefs.SetFloat(Prefix + "SfxVolume", SfxVolume);
            PlayerPrefs.SetInt(Prefix + "AmbientWhispers", AmbientWhispers ? 1 : 0);
            PlayerPrefs.SetInt(Prefix + "BrewShakeFx", BrewShakeFx ? 1 : 0);
            PlayerPrefs.SetInt(Prefix + "Language", LanguageIndex);
            PlayerPrefs.Save();
            PreviewLanguage(LanguageIndex);
            Changed?.Invoke();
        }
    }
}
