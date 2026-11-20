using AlchemyBar.Audio;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AlchemyBar.UI.Menus
{
    public sealed class SettingsView : MonoBehaviour
    {
        [SerializeField] private HexHearthMenuRouter router;
        [SerializeField] private SegmentedVolumeBar musicVolume;
        [SerializeField] private SegmentedVolumeBar spellEffectsVolume;
        [SerializeField] private MagicToggleSwitch ambientWhispers;
        [SerializeField] private MagicToggleSwitch brewShake;
        [SerializeField] private Button languageButton;
        [SerializeField] private TMP_Text languageValue;
        [SerializeField] private Button returnButton;
        [SerializeField] private Button saveButton;

        private int languageIndex;
        private float savedMusic;
        private float savedSfx;
        private bool savedAmbient;
        private bool savedShake;
        private bool saving;

        private void Awake()
        {
            if (router == null) router = GetComponentInParent<HexHearthMenuRouter>();
            if (languageButton != null) languageButton.onClick.AddListener(CycleLanguage);
            if (returnButton != null) returnButton.onClick.AddListener(Close);
            if (saveButton != null) saveButton.onClick.AddListener(SaveAndClose);
            if (musicVolume != null) musicVolume.OnValueChanged.AddListener(v => { GameSettings.MusicVolume = v; GameSettings.NotifyChanged(); });
            if (spellEffectsVolume != null) spellEffectsVolume.OnValueChanged.AddListener(v =>
            {
                GameSettings.SfxVolume = v;
                GameSettings.NotifyChanged();
                Camera cam = Camera.main;
                GameSfx.PlayAt("Pickup", cam != null ? cam.transform.position : Vector3.zero, 0.8f, 0.05f, 0f);
            });
            if (ambientWhispers != null) ambientWhispers.OnValueChanged.AddListener(on => { GameSettings.AmbientWhispers = on; GameSettings.NotifyChanged(); });
            if (brewShake != null) brewShake.OnValueChanged.AddListener(on => { GameSettings.BrewShakeFx = on; GameSettings.NotifyChanged(); });
        }

        private void OnEnable()
        {
            GameSettings.EnsureLoaded();
            saving = false;
            savedMusic = GameSettings.MusicVolume;
            savedSfx = GameSettings.SfxVolume;
            savedAmbient = GameSettings.AmbientWhispers;
            savedShake = GameSettings.BrewShakeFx;
            if (musicVolume != null) musicVolume.SetValueWithoutNotify(GameSettings.MusicVolume);
            if (spellEffectsVolume != null) spellEffectsVolume.SetValueWithoutNotify(GameSettings.SfxVolume);
            if (ambientWhispers != null) ambientWhispers.SetWithoutNotify(GameSettings.AmbientWhispers);
            if (brewShake != null) brewShake.SetWithoutNotify(GameSettings.BrewShakeFx);
            languageIndex = GameSettings.LanguageIndex;
            RefreshLanguage();
        }

        private void CycleLanguage()
        {
            languageIndex = (languageIndex + 1) % GameSettings.Languages.Length;
            RefreshLanguage();
            GameSettings.PreviewLanguage(languageIndex);
        }

        private void RefreshLanguage()
        {
            if (languageValue != null) languageValue.text = GameSettings.Languages[languageIndex];
        }

        private void SaveAndClose()
        {
            if (musicVolume != null) GameSettings.MusicVolume = musicVolume.Value;
            if (spellEffectsVolume != null) GameSettings.SfxVolume = spellEffectsVolume.Value;
            if (ambientWhispers != null) GameSettings.AmbientWhispers = ambientWhispers.IsOn;
            if (brewShake != null) GameSettings.BrewShakeFx = brewShake.IsOn;
            GameSettings.LanguageIndex = languageIndex;
            GameSettings.Save();
            saving = true;
            Close();
        }

        private void Close()
        {
            if (!saving)
            {
                GameSettings.MusicVolume = savedMusic;
                GameSettings.SfxVolume = savedSfx;
                GameSettings.AmbientWhispers = savedAmbient;
                GameSettings.BrewShakeFx = savedShake;
                GameSettings.NotifyChanged();
            }
            GameSettings.PreviewLanguage(GameSettings.LanguageIndex);
            if (router != null) router.Back();
            else gameObject.SetActive(false);
        }
    }
}
