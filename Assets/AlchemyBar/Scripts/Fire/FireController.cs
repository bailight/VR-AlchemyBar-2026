using System;
using UnityEngine;

namespace AlchemyBar.Fire
{
    public sealed class FireController : MonoBehaviour
    {
        private const float FuelEpsilon = 0.001f;

        [SerializeField, Min(0f)] private float maxFuel = 12f;
        [SerializeField, Min(0f)] private float currentFuel;
        [SerializeField, Min(0f)] private float burnRate = 0.25f;
        [SerializeField] private Transform flameVisual;
        [SerializeField] private Light fireLight;
        [SerializeField] private ParticleSystem fireParticles;
        [SerializeField, Min(0f)] private float maxLightIntensity = 4f;
        [SerializeField] private Vector3 minimumFlameScale = new Vector3(0.15f, 0.15f, 0.15f);

        private Vector3 baseFlameScale;
        public float CurrentFuel => currentFuel;
        public float MaxFuel => maxFuel;
        public float HeatIntensity => maxFuel <= 0f || currentFuel <= FuelEpsilon ? 0f : Mathf.Clamp01(currentFuel / maxFuel);
        public bool IsBurning => currentFuel > FuelEpsilon;

        public event Action<float> FuelChanged;

private void Awake()
        {
            if (flameVisual == null)
                flameVisual = transform.Find("Flame Placeholder");

            if (fireParticles == null)
                fireParticles = GetComponentInChildren<ParticleSystem>(true);

            baseFlameScale = flameVisual == null ? Vector3.one : flameVisual.localScale;
            ApplyVisuals();
        }

private void Update()
        {
            if (currentFuel <= FuelEpsilon)
            {
                if (currentFuel > 0f)
                {
                    currentFuel = 0f;
                    ApplyVisuals();
                    FuelChanged?.Invoke(currentFuel);
                }
                return;
            }

            currentFuel = Mathf.Max(0f, currentFuel - burnRate * Time.deltaTime);
            if (currentFuel <= FuelEpsilon)
                currentFuel = 0f;

            ApplyVisuals();
            FuelChanged?.Invoke(currentFuel);
        }

        public bool TryAddFirewood(Firewood firewood)
        {
            if (firewood == null || !firewood.Consume())
                return false;

            currentFuel = Mathf.Min(maxFuel, currentFuel + firewood.FuelValue);
            ApplyVisuals();
            FuelChanged?.Invoke(currentFuel);
            return true;
        }

        private void ApplyVisuals()
        {
            float intensity = HeatIntensity;
            if (flameVisual != null)
            {
                flameVisual.gameObject.SetActive(intensity > 0.001f);
                flameVisual.localScale = Vector3.Lerp(minimumFlameScale, baseFlameScale, intensity);
            }
            if (fireLight != null)
                fireLight.intensity = maxLightIntensity * intensity;
            if (fireParticles != null)
            {
                var emission = fireParticles.emission;
                emission.rateOverTime = 4f + intensity * 24f;
                if (intensity > 0.001f && !fireParticles.isPlaying)
                    fireParticles.Play();
                else if (intensity <= 0.001f && fireParticles.isPlaying)
                    fireParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }
        }

        private void OnValidate()
        {
            maxFuel = Mathf.Max(0f, maxFuel);
            currentFuel = Mathf.Clamp(currentFuel, 0f, maxFuel);
            burnRate = Mathf.Max(0f, burnRate);
        }
    }
}