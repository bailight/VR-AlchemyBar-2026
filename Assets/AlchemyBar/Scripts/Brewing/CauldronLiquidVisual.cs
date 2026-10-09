using AlchemyBar.Ingredients;
using UnityEngine;

namespace AlchemyBar.Brewing
{
    [RequireComponent(typeof(Renderer))]
    public sealed class CauldronLiquidVisual : MonoBehaviour
    {
        [SerializeField] private Renderer liquidRenderer;
        [SerializeField] private ParticleSystem steamParticles;
        [SerializeField] private Color baseLiquidColor = new Color(0.12f, 0.08f, 0.22f, 1f);
        [SerializeField, Range(0f, 1f)] private float rawBlendStrength = 0.35f;
        [SerializeField, Range(0f, 1f)] private float groundBlendStrength = 0.55f;

        [Header("Steam Thresholds (°C)")]
        [SerializeField, Min(0f)] private float lowSteamTemperature = 50f;
        [SerializeField, Min(0f)] private float mediumSteamTemperature = 80f;
        [SerializeField, Min(0f)] private float strongSteamTemperature = 100f;

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");
        private MaterialPropertyBlock propertyBlock;
        private float currentHeatNormalized;
        private float stirringIntensity;
        
private Color currentColor;

        public Color CurrentColor => currentColor;

        private void Awake()
        {
            if (liquidRenderer == null)
                liquidRenderer = GetComponent<Renderer>();
            if (steamParticles == null)
                steamParticles = GetComponentInChildren<ParticleSystem>(true);
            propertyBlock = new MaterialPropertyBlock();
            currentColor = baseLiquidColor;
            ApplyVisuals(0f);
        }

public void BlendIngredient(IngredientInstance ingredient)
        {
            if (ingredient == null || ingredient.Definition == null)
                return;
            float strength = ingredient.CurrentPreparationState == IngredientPreparationState.Ground
                ? groundBlendStrength
                : rawBlendStrength;
            currentColor = Color.Lerp(currentColor, ingredient.Definition.MagicalColor, strength);
            ApplyVisuals(currentHeatNormalized);
        }

public void SetTemperature(float temperature, float ambient, float maximum)
        {
            currentHeatNormalized = Mathf.InverseLerp(ambient, maximum, temperature);
            ApplyVisuals(currentHeatNormalized);
            UpdateSteam(temperature);
        }

public void SetStirringFeedback(float intensity)
        {
            stirringIntensity = Mathf.Clamp01(intensity);
            ApplyVisuals(currentHeatNormalized);
        }


private void ApplyVisuals(float heat)
        {
            if (liquidRenderer == null)
                return;
            Color visible = Color.Lerp(currentColor, Color.white, heat * 0.25f);
            float emissionStrength = heat * 1.5f + stirringIntensity * 2f;
            liquidRenderer.GetPropertyBlock(propertyBlock);
            propertyBlock.SetColor(BaseColorId, visible);
            propertyBlock.SetColor(ColorId, visible);
            propertyBlock.SetColor(EmissionColorId, visible * emissionStrength);
            liquidRenderer.SetPropertyBlock(propertyBlock);
        }

private void UpdateSteam(float temperature)
        {
            if (steamParticles == null)
                return;

            float rate = temperature < lowSteamTemperature
                ? 0f
                : temperature < mediumSteamTemperature
                    ? 4f
                    : temperature < strongSteamTemperature
                        ? 10f
                        : 18f;

            var emission = steamParticles.emission;
            emission.rateOverTime = rate;
            if (rate > 0f && !steamParticles.isPlaying)
                steamParticles.Play();
            else if (rate <= 0f && steamParticles.isPlaying)
                steamParticles.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }

private void OnValidate()
        {
            lowSteamTemperature = Mathf.Max(0f, lowSteamTemperature);
            mediumSteamTemperature = Mathf.Max(lowSteamTemperature, mediumSteamTemperature);
            strongSteamTemperature = Mathf.Max(mediumSteamTemperature, strongSteamTemperature);
        }

    }
}