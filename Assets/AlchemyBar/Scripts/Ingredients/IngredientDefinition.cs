using UnityEngine;

namespace AlchemyBar.Ingredients
{
    public enum IngredientType
    {
        Herb,
        Root,
        Fruit,
        Powder,
        Magical,
        Mineral
    }

    public enum IngredientPreparationState
    {
        Raw,
        Ground,
        Heated,
        Processed
    }

    [CreateAssetMenu(fileName = "Ingredient", menuName = "AlchemyBar/Ingredients/Ingredient Definition")]
    public sealed class IngredientDefinition : ScriptableObject
    {
        [SerializeField] private string id = "ingredient";
        [SerializeField] private string displayName = "Ingredient";
        [SerializeField] private IngredientType ingredientType = IngredientType.Herb;
        [SerializeField] private Color baseColor = Color.white;
        [SerializeField] private Color magicalColor = Color.cyan;
        [SerializeField] private bool grindable;
        [SerializeField, Min(0.01f)] private float requiredGrindingAmount = 1f;
        [SerializeField] private bool heatSensitive;

        public string Id => id;
        public string DisplayName => displayName;
        public IngredientType IngredientType => ingredientType;
        public Color BaseColor => baseColor;
        public Color MagicalColor => magicalColor;
        public bool Grindable => grindable;
        public float RequiredGrindingAmount => Mathf.Max(0.01f, requiredGrindingAmount);
        public bool HeatSensitive => heatSensitive;

        private void OnValidate()
        {
            requiredGrindingAmount = Mathf.Max(0.01f, requiredGrindingAmount);
            id = string.IsNullOrWhiteSpace(id) ? name.ToLowerInvariant() : id.Trim();
            displayName = string.IsNullOrWhiteSpace(displayName) ? name : displayName.Trim();
        }
    }
}