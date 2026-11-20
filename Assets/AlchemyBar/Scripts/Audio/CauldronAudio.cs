using AlchemyBar.Brewing;
using AlchemyBar.Ingredients;
using AlchemyBar.Stirring;
using AlchemyBar.UI.Menus;
using UnityEngine;

namespace AlchemyBar.Audio
{
    [RequireComponent(typeof(BrewingCauldron))]
    public sealed class CauldronAudio : MonoBehaviour
    {
        [SerializeField] private StirringController stirring;
        [SerializeField, Range(0f, 1f)] private float splashVolume = 0.8f;
        [SerializeField, Range(0f, 1f)] private float stirVolume = 0.6f;
        [SerializeField] private float fullStirSpeed = 360f;

        private BrewingCauldron cauldron;
        private AudioSource stirSource;
        private float stirLevel;

        private void Awake()
        {
            cauldron = GetComponent<BrewingCauldron>();
            if (stirring == null) stirring = FindAnyObjectByType<StirringController>();

            AudioClip[] loop = GameSfx.Get("StirLoop");
            if (loop.Length > 0)
            {
                var go = new GameObject("Stir Audio");
                go.transform.SetParent(transform, false);
                stirSource = go.AddComponent<AudioSource>();
                stirSource.clip = loop[0];
                stirSource.loop = true;
                stirSource.playOnAwake = false;
                stirSource.spatialBlend = 1f;
                stirSource.rolloffMode = AudioRolloffMode.Logarithmic;
                stirSource.minDistance = 0.6f;
                stirSource.maxDistance = 15f;
                stirSource.volume = 0f;
            }
        }

        private void OnEnable() => cauldron.IngredientAdded += OnIngredientAdded;
        private void OnDisable() => cauldron.IngredientAdded -= OnIngredientAdded;

        private void OnIngredientAdded(IngredientInstance ingredient)
        {
            Vector3 at = ingredient != null ? ingredient.transform.position : transform.position;
            GameSfx.PlayAt("Splash", at, splashVolume);
        }

        private void Update()
        {
            if (stirSource == null) return;

            float target = 0f;
            if (stirring != null && stirring.IsStirring && Time.timeScale > 0f)
                target = Mathf.Clamp01(Mathf.Abs(stirring.CurrentAngularSpeed) / fullStirSpeed);

            stirLevel = Mathf.MoveTowards(stirLevel, target, Time.unscaledDeltaTime * 3f);
            stirSource.volume = stirLevel * stirVolume * GameSettings.ToGain(GameSettings.SfxVolume);

            if (stirLevel > 0.001f && !stirSource.isPlaying) stirSource.Play();
            else if (stirLevel <= 0.001f && stirSource.isPlaying) stirSource.Pause();
        }
    }
}
