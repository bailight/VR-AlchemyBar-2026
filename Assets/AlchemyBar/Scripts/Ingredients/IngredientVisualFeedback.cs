using UnityEngine;

namespace AlchemyBar.Ingredients
{
    [RequireComponent(typeof(IngredientInstance))]
    public sealed class IngredientVisualFeedback : MonoBehaviour
    {
        [SerializeField] private Renderer[] renderers;
        [SerializeField, Range(0.1f, 1f)] private float groundHeightScale = 0.25f;
        [SerializeField, Range(1f, 1.5f)] private float groundWidthScale = 1.15f;

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        private IngredientInstance ingredient;
        private Vector3 rawScale;
        private MaterialPropertyBlock propertyBlock;

        private void Awake()
        {
            ingredient = GetComponent<IngredientInstance>();
            rawScale = transform.localScale;
            if (renderers == null || renderers.Length == 0)
                renderers = GetComponentsInChildren<Renderer>(true);
            propertyBlock = new MaterialPropertyBlock();
            ApplyProgress(ingredient.CurrentGrindingProgress);
        }

        private void OnEnable()
        {
            if (ingredient == null)
                ingredient = GetComponent<IngredientInstance>();
            ingredient.GrindingProgressUpdated += OnGrindingProgressUpdated;
            ingredient.PreparationStateChanged += OnPreparationStateChanged;
        }

        private void OnDisable()
        {
            if (ingredient == null)
                return;
            ingredient.GrindingProgressUpdated -= OnGrindingProgressUpdated;
            ingredient.PreparationStateChanged -= OnPreparationStateChanged;
        }

        private void OnGrindingProgressUpdated(IngredientInstance source, float progress)
        {
            ApplyProgress(progress);
        }

        private void OnPreparationStateChanged(IngredientInstance source, IngredientPreparationState state)
        {
            ApplyProgress(state == IngredientPreparationState.Ground ? 1f : source.CurrentGrindingProgress);
        }

        private void ApplyProgress(float progress)
        {
            progress = Mathf.Clamp01(progress);
            transform.localScale = new Vector3(
                rawScale.x * Mathf.Lerp(1f, groundWidthScale, progress),
                rawScale.y * Mathf.Lerp(1f, groundHeightScale, progress),
                rawScale.z * Mathf.Lerp(1f, groundWidthScale, progress));

            if (ingredient == null || ingredient.Definition == null || renderers == null)
                return;

            Color color = Color.Lerp(ingredient.Definition.BaseColor, ingredient.Definition.MagicalColor, progress);
            foreach (Renderer targetRenderer in renderers)
            {
                if (targetRenderer == null)
                    continue;

                targetRenderer.GetPropertyBlock(propertyBlock);
                propertyBlock.SetColor(BaseColorId, color);
                propertyBlock.SetColor(ColorId, color);
                targetRenderer.SetPropertyBlock(propertyBlock);
            }
        }
    }
}