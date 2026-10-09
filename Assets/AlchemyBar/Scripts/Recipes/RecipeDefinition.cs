using System.Collections.Generic;
using UnityEngine;

namespace AlchemyBar.Recipes
{
    [CreateAssetMenu(fileName = "Recipe", menuName = "AlchemyBar/Recipe Definition")]
    public sealed class RecipeDefinition : ScriptableObject
    {
        [SerializeField] private string recipeId;
        [SerializeField] private string displayName;
        [SerializeField, TextArea(2, 5)] private string description;
        [SerializeField] private Color resultColor = Color.white;
        [SerializeField] private List<RecipeStepDefinition> steps = new List<RecipeStepDefinition>();

        public string RecipeId => recipeId;
        public string DisplayName => displayName;
        public string Description => description;
        public Color ResultColor => resultColor;
        public IReadOnlyList<RecipeStepDefinition> Steps => steps;
        public int ActionableStepCount
        {
            get
            {
                int count = 0;
                for (int i = 0; i < steps.Count; i++)
                {
                    if (steps[i] != null && steps[i].StepType != RecipeStepType.Complete)
                        count++;
                }

                return count;
            }
        }

        public bool TryValidate(out string error)
        {
            if (string.IsNullOrWhiteSpace(recipeId))
            {
                error = "Recipe ID is required.";
                return false;
            }

            if (steps == null || steps.Count == 0)
            {
                error = "A recipe must contain at least one step.";
                return false;
            }

            for (int i = 0; i < steps.Count; i++)
            {
                RecipeStepDefinition step = steps[i];
                if (step == null)
                {
                    error = "Step " + i + " is null.";
                    return false;
                }

                string stepError;
                if (!step.TryValidate(out stepError))
                {
                    error = "Step " + i + " is invalid: " + stepError;
                    return false;
                }

                if (step.StepType == RecipeStepType.Complete && i != steps.Count - 1)
                {
                    error = "Complete must be the final step.";
                    return false;
                }
            }

            if (steps[steps.Count - 1].StepType != RecipeStepType.Complete)
            {
                error = "The final step must be Complete.";
                return false;
            }

            if (ActionableStepCount == 0)
            {
                error = "A recipe needs at least one actionable step before Complete.";
                return false;
            }

            error = string.Empty;
            return true;
        }

        private void OnValidate()
        {
            recipeId = string.IsNullOrWhiteSpace(recipeId) ? string.Empty : recipeId.Trim();
            displayName = string.IsNullOrWhiteSpace(displayName) ? name : displayName.Trim();
        }
    }
}
