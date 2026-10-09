using System;
using System.Collections.Generic;
using AlchemyBar.Brewing;
using AlchemyBar.Recipes;
using UnityEngine;

namespace AlchemyBar.UI
{
    public sealed class MagicRecipeBook : MonoBehaviour
    {
        [SerializeField] private RecipeProgressTracker recipeTracker;
        [SerializeField] private BrewingCauldron brewingCauldron;
        [SerializeField] private List<RecipeDefinition> availableRecipes = new List<RecipeDefinition>();
        [SerializeField] private RecipeDefinition selectedRecipe;
        [SerializeField, TextArea] private string statusMessage;

        public RecipeProgressTracker RecipeTracker => recipeTracker;
        public BrewingCauldron BrewingCauldron => brewingCauldron;
        public IReadOnlyList<RecipeDefinition> AvailableRecipes => availableRecipes;
        public RecipeDefinition SelectedRecipe => selectedRecipe;
        public string StatusMessage => statusMessage;
        public bool CanStartSelectedRecipe =>
            selectedRecipe != null &&
            recipeTracker != null &&
            !recipeTracker.IsRecipeActive &&
            brewingCauldron != null &&
            !brewingCauldron.ContainsPotion;

        public event Action<RecipeDefinition> SelectionChanged;
        public event Action<string> StatusChanged;
        public event Action RecipeListRequested;

        private void Awake()
        {
            ResolveReferences();
        }

        private void OnEnable()
        {
            if (selectedRecipe == null)
                SelectFirstAvailableRecipe();
        }

        public bool SelectRecipeByIndex(int index)
        {
            if (recipeTracker != null && recipeTracker.IsRecipeActive)
            {
                SetStatus("Finish or cancel the active recipe before selecting another.");
                return false;
            }

            if (index < 0 || index >= availableRecipes.Count || availableRecipes[index] == null)
            {
                SetStatus("That recipe is not available.");
                return false;
            }

            selectedRecipe = availableRecipes[index];
            SetStatus(string.Empty);
            SelectionChanged?.Invoke(selectedRecipe);
            return true;
        }

        public bool SelectRecipe(RecipeDefinition recipe)
        {
            int index = availableRecipes.IndexOf(recipe);
            return SelectRecipeByIndex(index);
        }

        public bool StartSelectedRecipe()
        {
            ResolveReferences();

            if (selectedRecipe == null)
            {
                SetStatus("Select a recipe first.");
                return false;
            }

            if (recipeTracker == null)
            {
                SetStatus("Recipe tracker is unavailable.");
                return false;
            }

            if (recipeTracker.IsRecipeActive)
            {
                SetStatus("A recipe is already active.");
                return false;
            }

            if (brewingCauldron == null)
            {
                SetStatus("Brewing cauldron is unavailable.");
                return false;
            }

            if (brewingCauldron.ContainsPotion)
            {
                SetStatus("Reset brewing station before starting a new recipe.");
                return false;
            }

            if (!recipeTracker.StartRecipe(selectedRecipe))
            {
                SetStatus(string.IsNullOrWhiteSpace(recipeTracker.LastValidationError)
                    ? "The selected recipe could not be started."
                    : recipeTracker.LastValidationError);
                return false;
            }

            SetStatus(string.Empty);
            return true;
        }

        public void BackToRecipes()
        {
            if (recipeTracker != null && recipeTracker.IsRecipeActive)
            {
                SetStatus("The active recipe must be completed or cancelled first.");
                return;
            }

            if (brewingCauldron != null && brewingCauldron.ContainsPotion)
                SetStatus("Reset brewing station before starting a new recipe.");
            else
                SetStatus(string.Empty);

            RecipeListRequested?.Invoke();
            SelectionChanged?.Invoke(selectedRecipe);
        }

        private void SelectFirstAvailableRecipe()
        {
            for (int i = 0; i < availableRecipes.Count; i++)
            {
                if (availableRecipes[i] != null)
                {
                    selectedRecipe = availableRecipes[i];
                    SelectionChanged?.Invoke(selectedRecipe);
                    return;
                }
            }
        }

        private void SetStatus(string message)
        {
            statusMessage = message ?? string.Empty;
            StatusChanged?.Invoke(statusMessage);
        }

        private void ResolveReferences()
        {
            if (recipeTracker == null)
                recipeTracker = FindFirstObjectByType<RecipeProgressTracker>(FindObjectsInactive.Include);
            if (brewingCauldron == null)
                brewingCauldron = FindFirstObjectByType<BrewingCauldron>(FindObjectsInactive.Include);
        }
    }
}
