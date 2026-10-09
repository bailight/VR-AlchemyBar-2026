using AlchemyBar.Brewing;
using UnityEngine;

namespace AlchemyBar.Stirring
{
    public sealed class StirringFeedback : MonoBehaviour
    {
        [SerializeField] private CauldronLiquidVisual liquidVisual;
        [SerializeField] private Transform liquidTransform;
        [SerializeField] private ParticleSystem magicalParticles;
        [SerializeField] private Renderer wandTipRenderer;
        [SerializeField, Min(1f)] private float fullIntensityAngularSpeed = 360f;
        [SerializeField, Min(0f)] private float liquidRotationSpeed = 45f;
        [SerializeField] private Color idleTipColor = new Color(0.35f, 0.2f, 0.75f, 1f);
        [SerializeField] private Color activeTipColor = new Color(0.25f, 0.9f, 1f, 1f);

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");
        private MaterialPropertyBlock tipPropertyBlock;

        private void Awake()
        {
            tipPropertyBlock = new MaterialPropertyBlock();
            ApplyTipVisual(0f);
            SetParticleEmission(false, 0f);
        }

        public void SetFeedback(bool active, float angularSpeed, StirDirection direction)
        {
            float intensity = active
                ? Mathf.Clamp01(angularSpeed / fullIntensityAngularSpeed)
                : 0f;

            if (active && liquidTransform != null)
            {
                float sign = direction == StirDirection.CounterClockwise ? -1f : 1f;
                liquidTransform.Rotate(Vector3.up, liquidRotationSpeed * sign * Mathf.Lerp(0.3f, 1f, intensity) * Time.deltaTime, Space.World);
            }

            liquidVisual?.SetStirringFeedback(intensity);
            SetParticleEmission(active, intensity);
            ApplyTipVisual(intensity);
        }

        private void SetParticleEmission(bool active, float intensity)
        {
            if (magicalParticles == null)
                return;

            var emission = magicalParticles.emission;
            emission.rateOverTime = active ? Mathf.Lerp(4f, 28f, intensity) : 0f;

            if (active && !magicalParticles.isPlaying)
                magicalParticles.Play();
            else if (!active && magicalParticles.isPlaying)
                magicalParticles.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }

        private void ApplyTipVisual(float intensity)
        {
            if (wandTipRenderer == null)
                return;

            Color color = Color.Lerp(idleTipColor, activeTipColor, intensity);
            wandTipRenderer.GetPropertyBlock(tipPropertyBlock);
            tipPropertyBlock.SetColor(BaseColorId, color);
            tipPropertyBlock.SetColor(ColorId, color);
            tipPropertyBlock.SetColor(EmissionColorId, color * Mathf.Lerp(0.6f, 3f, intensity));
            wandTipRenderer.SetPropertyBlock(tipPropertyBlock);
        }
    }
}
