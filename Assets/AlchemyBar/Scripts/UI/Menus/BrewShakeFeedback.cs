using AlchemyBar.Brewing;
using AlchemyBar.Ingredients;
using AlchemyBar.Stirring;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Haptics;

namespace AlchemyBar.UI.Menus
{
    public sealed class BrewShakeFeedback : MonoBehaviour
    {
        [SerializeField] private BrewingCauldron cauldron;
        [SerializeField] private StirringController stirring;
        [SerializeField, Range(0f, 1f)] private float ingredientAmplitude = 0.6f;
        [SerializeField] private float ingredientDuration = 0.15f;
        [SerializeField, Range(0f, 1f)] private float stirAmplitude = 0.15f;
        [SerializeField] private float stirPulseInterval = 0.12f;

        private HapticImpulsePlayer[] players = new HapticImpulsePlayer[0];
        private float nextStirPulse;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            foreach (BrewingCauldron c in FindObjectsByType<BrewingCauldron>(FindObjectsSortMode.None))
                if (c.GetComponent<BrewShakeFeedback>() == null)
                    c.gameObject.AddComponent<BrewShakeFeedback>();
        }

        private void Awake()
        {
            if (cauldron == null) cauldron = GetComponent<BrewingCauldron>();
            if (stirring == null) stirring = FindAnyObjectByType<StirringController>();
            players = FindObjectsByType<HapticImpulsePlayer>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            GameSettings.EnsureLoaded();
        }

        private void OnEnable()
        {
            if (cauldron != null) cauldron.IngredientAdded += OnIngredientAdded;
            if (stirring != null) stirring.StirringProgressChanged += OnStirProgress;
        }

        private void OnDisable()
        {
            if (cauldron != null) cauldron.IngredientAdded -= OnIngredientAdded;
            if (stirring != null) stirring.StirringProgressChanged -= OnStirProgress;
        }

        private void OnIngredientAdded(IngredientInstance _) => Pulse(ingredientAmplitude, ingredientDuration);

        private void OnStirProgress(float _)
        {
            if (Time.time < nextStirPulse) return;
            nextStirPulse = Time.time + stirPulseInterval;
            Pulse(stirAmplitude, stirPulseInterval);
        }

        private void Pulse(float amplitude, float duration)
        {
            if (!GameSettings.BrewShakeFx || Time.timeScale == 0f) return;
            foreach (HapticImpulsePlayer p in players)
                if (p != null && p.isActiveAndEnabled) p.SendHapticImpulse(amplitude, duration);
        }
    }
}
