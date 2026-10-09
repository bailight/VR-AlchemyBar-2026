using System;
using System.Collections.Generic;
using AlchemyBar.Brewing;
using AlchemyBar.Evaluation;
using AlchemyBar.Recipes;
using AlchemyBar.Stirring;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace AlchemyBar.UI
{
    public enum RecipeBookView
    {
        Selection,
        ActiveRecipe,
        Completion
    }

    public sealed class RecipeBookUI : MonoBehaviour
    {
        [Header("State Providers")]
        [SerializeField] private MagicRecipeBook magicRecipeBook;
        [SerializeField] private RecipeProgressTracker recipeTracker;
        [SerializeField] private BrewingCauldron brewingCauldron;
        [SerializeField] private StirringController stirringController;
        [SerializeField] private PotionEvaluator potionEvaluator;

        [Header("Views")]
        [SerializeField] private GameObject recipeSelectionPanel;
        [SerializeField] private GameObject activeRecipePanel;
        [SerializeField] private GameObject completionPanel;

        [Header("Selection")]
        [SerializeField] private Button[] recipeButtons;
        [SerializeField] private TMP_Text[] recipeButtonLabels;
        [SerializeField] private Button startRecipeButton;
        [SerializeField] private TMP_Text previewRecipeName;
        [SerializeField] private TMP_Text previewDescription;
        [SerializeField] private TMP_Text previewIngredients;
        [SerializeField] private TMP_Text previewSteps;

        [Header("Active Recipe")]
        [SerializeField] private TMP_Text recipeNameText;
        [SerializeField] private TMP_Text recipeDescriptionText;
        [SerializeField] private TMP_Text ingredientListText;
        [SerializeField] private TMP_Text stepListText;
        [SerializeField] private TMP_Text currentStepTitleText;
        [SerializeField] private TMP_Text currentStepDetailText;
        [SerializeField] private TMP_Text temperatureText;
        [SerializeField] private GameObject temperaturePanel;
        [SerializeField] private TMP_Text stirringText;
        [SerializeField] private GameObject stirringPanel;
        [SerializeField] private Image currentStepProgressFill;
        [SerializeField] private TMP_Text currentStepProgressText;
        [SerializeField] private Image overallProgressFill;
        [SerializeField] private TMP_Text overallProgressText;

        [Header("Completion")]
        [SerializeField] private TMP_Text completionText;
        [SerializeField] private Button backToRecipesButton;

        [Header("Shared")]
        [SerializeField] private TMP_Text statusText;
        [SerializeField, Min(0.05f)] private float liveRefreshInterval = 0.2f;
        [SerializeField] private Color normalRecipeButtonColor = new Color(0.28f, 0.20f, 0.32f, 1f);
        [SerializeField] private Color selectedRecipeButtonColor = new Color(0.36f, 0.24f, 0.62f, 1f);

        private readonly List<UnityAction> recipeButtonActions = new List<UnityAction>();
        private UnityAction startRecipeAction;
        private UnityAction backToRecipesAction;
        private float nextLiveRefreshTime;
        private bool subscribed;

        public RecipeBookView CurrentView { get; private set; }
        public string RenderedStepList => stepListText != null ? stepListText.text : string.Empty;
        public string RenderedIngredientList => ingredientListText != null ? ingredientListText.text : string.Empty;
        public string RenderedCurrentStep => currentStepTitleText != null ? currentStepTitleText.text : string.Empty;
        public string RenderedTemperature => temperatureText != null ? temperatureText.text : string.Empty;
        public string RenderedStirring => stirringText != null ? stirringText.text : string.Empty;
        public string RenderedEvaluation => completionText != null ? completionText.text : string.Empty;

        private void Awake()
        {
            ResolveReferences();
            ConfigureButtonHandlers();
        }

        private void OnEnable()
        {
            ResolveReferences();
            Subscribe();
            RefreshRecipeButtons();
            RefreshStatus(magicRecipeBook != null ? magicRecipeBook.StatusMessage : string.Empty);

            if (recipeTracker != null && recipeTracker.IsRecipeComplete)
                ShowCompletion(recipeTracker.ActiveRecipe);
            else if (recipeTracker != null && recipeTracker.IsRecipeActive)
                ShowActiveRecipe(recipeTracker.ActiveRecipe);
            else
                ShowSelection();
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        private void OnDestroy()
        {
            RemoveButtonHandlers();
        }

        private void Update()
        {
            if (CurrentView != RecipeBookView.ActiveRecipe || Time.unscaledTime < nextLiveRefreshTime)
                return;

            nextLiveRefreshTime = Time.unscaledTime + liveRefreshInterval;
            RefreshDynamicState();
        }

        public void ShowSelection()
        {
            CurrentView = RecipeBookView.Selection;
            SetViewActive(recipeSelectionPanel, true);
            SetViewActive(activeRecipePanel, false);
            SetViewActive(completionPanel, false);
            RefreshRecipeButtons();
            RefreshPreview(magicRecipeBook != null ? magicRecipeBook.SelectedRecipe : null);
            RefreshStatus(magicRecipeBook != null ? magicRecipeBook.StatusMessage : string.Empty);
        }

        private void ShowActiveRecipe(RecipeDefinition recipe)
        {
            CurrentView = RecipeBookView.ActiveRecipe;
            SetViewActive(recipeSelectionPanel, false);
            SetViewActive(activeRecipePanel, true);
            SetViewActive(completionPanel, false);
            RefreshActiveStatic(recipe);
            RefreshDynamicState();
            RefreshStatus(string.Empty);
            nextLiveRefreshTime = Time.unscaledTime + liveRefreshInterval;
        }

private void ShowCompletion(RecipeDefinition recipe)
        {
            if (potionEvaluator != null &&
                potionEvaluator.HasResult &&
                potionEvaluator.LastResult != null &&
                potionEvaluator.LastResult.Recipe == recipe)
            {
                ShowEvaluation(potionEvaluator.LastResult);
                return;
            }

            CurrentView = RecipeBookView.Completion;
            SetViewActive(recipeSelectionPanel, false);
            SetViewActive(activeRecipePanel, false);
            SetViewActive(completionPanel, true);

            if (completionText != null)
            {
                string displayName = recipe != null ? recipe.DisplayName : "POTION";
                completionText.text = displayName.ToUpperInvariant() +
                                      "\n<size=130%>POTION COMPLETE</size>" +
                                      "\n<size=65%>Evaluating brew...</size>";
            }

            RefreshStatus(magicRecipeBook != null ? magicRecipeBook.StatusMessage : string.Empty);
        }

private void ShowEvaluation(PotionEvaluationResult result)
        {
            if (result == null)
                return;

            CurrentView = RecipeBookView.Completion;
            SetViewActive(recipeSelectionPanel, false);
            SetViewActive(activeRecipePanel, false);
            SetViewActive(completionPanel, true);

            if (completionText != null)
            {
                completionText.text = FormatEvaluation(result);
                completionText.color = GradeColor(result);
            }

            RefreshStatus(magicRecipeBook != null ? magicRecipeBook.StatusMessage : string.Empty);
        }

private static string FormatEvaluation(PotionEvaluationResult result)
        {
            var lines = new List<string>();
            string recipeName = result.Recipe != null ? result.Recipe.DisplayName : "Potion";
            lines.Add("<b>" + recipeName.ToUpperInvariant() + "</b>");
            lines.Add("<size=115%>POTION COMPLETE</size>");
            lines.Add("QUALITY: <b>" + result.Grade.ToString().ToUpperInvariant() + "</b>");
            lines.Add("<size=120%><b>" + Mathf.RoundToInt(result.OverallScore) + " / 100</b></size>");
            lines.Add(string.Empty);

            if (result.HasIngredientEvaluation)
                lines.Add("Ingredients     " + Mathf.RoundToInt(result.IngredientScore));
            if (result.HasTemperatureEvaluation)
                lines.Add("Temperature     " + Mathf.RoundToInt(result.TemperatureScore));
            if (result.HasStirringEvaluation)
                lines.Add("Stirring        " + Mathf.RoundToInt(result.StirringScore));

            if (result.FeedbackMessages.Count > 0)
            {
                lines.Add(string.Empty);
                for (int i = 0; i < result.FeedbackMessages.Count; i++)
                    lines.Add("• " + result.FeedbackMessages[i]);
            }

            return string.Join("\n", lines);
        }

private static Color GradeColor(PotionEvaluationResult result)
        {
            if (result == null)
                return new Color(0.28f, 0.18f, 0.38f, 1f);

            switch (result.Grade)
            {
                case PotionQualityGrade.Perfect:
                    return Color.Lerp(
                        new Color(0.34f, 0.18f, 0.55f, 1f),
                        result.Recipe != null ? result.Recipe.ResultColor : Color.white,
                        0.25f);
                case PotionQualityGrade.Failed:
                    return new Color(0.55f, 0.12f, 0.18f, 1f);
                default:
                    return new Color(0.26f, 0.20f, 0.42f, 1f);
            }
        }




        private void RefreshPreview(RecipeDefinition recipe)
        {
            SetText(previewRecipeName, recipe != null ? recipe.DisplayName.ToUpperInvariant() : "SELECT A RECIPE");
            SetText(previewDescription, recipe != null ? recipe.Description : "Choose a recipe to preview its ingredients and steps.");
            SetText(previewIngredients, recipe != null ? BuildIngredientList(recipe, false) : string.Empty);
            SetText(previewSteps, recipe != null ? BuildStepList(recipe, false) : string.Empty);

            if (startRecipeButton != null)
                startRecipeButton.interactable = recipe != null && (recipeTracker == null || !recipeTracker.IsRecipeActive);

            UpdateRecipeButtonVisuals();
        }

        private void RefreshActiveStatic(RecipeDefinition recipe)
        {
            SetText(recipeNameText, recipe != null ? recipe.DisplayName.ToUpperInvariant() : "NO ACTIVE RECIPE");
            SetText(recipeDescriptionText, recipe != null ? recipe.Description : string.Empty);
            SetText(ingredientListText, recipe != null ? BuildIngredientList(recipe, true) : string.Empty);
            SetText(stepListText, recipe != null ? BuildStepList(recipe, true) : string.Empty);
            RefreshCurrentStepPresentation();
        }

        private void RefreshDynamicState()
        {
            if (recipeTracker == null)
                return;

            if (recipeTracker.IsRecipeComplete)
            {
                ShowCompletion(recipeTracker.ActiveRecipe);
                return;
            }

            if (!recipeTracker.IsRecipeActive)
                return;

            SetFill(currentStepProgressFill, recipeTracker.CurrentStepProgress);
            SetFill(overallProgressFill, recipeTracker.NormalizedRecipeProgress);
            SetText(currentStepProgressText, Mathf.RoundToInt(recipeTracker.CurrentStepProgress * 100f) + "%");
            SetText(overallProgressText, "Overall " + Mathf.RoundToInt(recipeTracker.NormalizedRecipeProgress * 100f) + "%");
            RefreshCurrentStepPresentation();
        }

        private void RefreshCurrentStepPresentation()
        {
            RecipeStepDefinition step = recipeTracker != null ? recipeTracker.CurrentStep : null;
            if (step == null)
            {
                SetText(currentStepTitleText, "Waiting for recipe");
                SetText(currentStepDetailText, string.Empty);
                SetViewActive(temperaturePanel, false);
                SetViewActive(stirringPanel, false);
                return;
            }

            SetText(currentStepTitleText, FormatStep(step));
            SetViewActive(temperaturePanel,
                step.StepType == RecipeStepType.HeatToTemperature ||
                step.StepType == RecipeStepType.MaintainTemperature);
            SetViewActive(stirringPanel, step.StepType == RecipeStepType.Stir);

            float progress = recipeTracker != null ? recipeTracker.CurrentStepProgress : 0f;
            switch (step.StepType)
            {
                case RecipeStepType.AddIngredient:
                case RecipeStepType.AddPreparedIngredient:
                    SetText(currentStepDetailText, "Status: Waiting for ingredient");
                    break;
                case RecipeStepType.HeatToTemperature:
                    SetText(currentStepDetailText, "Bring the potion into the target range.");
                    break;
                case RecipeStepType.MaintainTemperature:
                    SetText(currentStepDetailText,
                        FormatNumber(progress * step.RequiredDuration) + " / " +
                        FormatNumber(step.RequiredDuration) + " s");
                    break;
                case RecipeStepType.Stir:
                    SetText(currentStepDetailText,
                        FormatNumber(progress * step.RequiredStirringDegrees) + "° / " +
                        FormatNumber(step.RequiredStirringDegrees) + "°");
                    break;
                case RecipeStepType.Wait:
                    SetText(currentStepDetailText,
                        FormatNumber(progress * step.RequiredDuration) + " / " +
                        FormatNumber(step.RequiredDuration) + " s");
                    break;
                case RecipeStepType.Complete:
                    SetText(currentStepDetailText, "Finishing potion...");
                    break;
            }

            RefreshTemperature(step);
            RefreshStirring(step, progress);
        }

        private void RefreshTemperature(RecipeStepDefinition step)
        {
            if (temperatureText == null ||
                (step.StepType != RecipeStepType.HeatToTemperature &&
                 step.StepType != RecipeStepType.MaintainTemperature))
                return;

            float current = brewingCauldron != null ? brewingCauldron.CurrentTemperature : 0f;
            float minimum;
            float maximum;
            string target;

            if (step.StepType == RecipeStepType.HeatToTemperature)
            {
                minimum = step.TargetTemperature - step.TemperatureTolerance;
                maximum = step.TargetTemperature + step.TemperatureTolerance;
                target = "Target: " + FormatNumber(step.TargetTemperature) + " ±" +
                         FormatNumber(step.TemperatureTolerance) + "°C";
            }
            else
            {
                minimum = step.MinimumTemperature;
                maximum = step.MaximumTemperature;
                target = "Target Range: " + FormatNumber(minimum) + "–" + FormatNumber(maximum) + "°C";
            }

            string feedback = current < minimum
                ? "Too Cold"
                : current > maximum
                    ? "Too Hot"
                    : "Correct Temperature";

            temperatureText.text = "Current: " + FormatNumber(current) + "°C\n" + target + "\n" + feedback;
        }

        private void RefreshStirring(RecipeStepDefinition step, float progress)
        {
            if (stirringText == null || step.StepType != RecipeStepType.Stir)
                return;

            string currentDirection = stirringController != null
                ? FormatLiveDirection(stirringController.CurrentDirection)
                : "None";
            stirringText.text =
                "Required: " + FormatRecipeDirection(step.StirDirection) + "\n" +
                "Progress: " + FormatNumber(progress * step.RequiredStirringDegrees) + "° / " +
                FormatNumber(step.RequiredStirringDegrees) + "°\n" +
                "Current Direction: " + currentDirection;
        }

        private string BuildIngredientList(RecipeDefinition recipe, bool includeState)
        {
            var lines = new List<string>();
            for (int i = 0; i < recipe.Steps.Count; i++)
            {
                RecipeStepDefinition step = recipe.Steps[i];
                if (step == null ||
                    (step.StepType != RecipeStepType.AddIngredient &&
                     step.StepType != RecipeStepType.AddPreparedIngredient) ||
                    step.Ingredient == null)
                    continue;

                string requirement = step.StepType == RecipeStepType.AddPreparedIngredient
                    ? step.RequiredPreparationState.ToString()
                    : "Any";
                bool completed = includeState && recipeTracker != null &&
                                 (recipeTracker.IsRecipeComplete || i < recipeTracker.CurrentStepIndex);
                string marker = includeState ? (completed ? "✓ " : "○ ") : "• ";
                lines.Add(marker + step.Ingredient.DisplayName + " — " + requirement);
            }

            return lines.Count > 0 ? string.Join("\n", lines) : "No physical ingredients";
        }

        private string BuildStepList(RecipeDefinition recipe, bool includeState)
        {
            var lines = new List<string>();
            for (int i = 0; i < recipe.Steps.Count; i++)
            {
                RecipeStepDefinition step = recipe.Steps[i];
                if (step == null)
                    continue;

                string marker = "○";
                if (includeState && recipeTracker != null)
                {
                    if (recipeTracker.IsRecipeComplete || i < recipeTracker.CurrentStepIndex)
                        marker = "✓";
                    else if (recipeTracker.IsRecipeActive && i == recipeTracker.CurrentStepIndex)
                        marker = "→";
                }

                lines.Add((i + 1) + ". " + FormatStep(step) + "   " + marker);
            }

            return string.Join("\n", lines);
        }

        public static string FormatStep(RecipeStepDefinition step)
        {
            if (step == null)
                return "Unknown step";

            switch (step.StepType)
            {
                case RecipeStepType.AddIngredient:
                    return "Add " + IngredientName(step);
                case RecipeStepType.AddPreparedIngredient:
                    return "Add " + step.RequiredPreparationState + " " + IngredientName(step);
                case RecipeStepType.HeatToTemperature:
                    return "Heat to " + FormatNumber(step.TargetTemperature) + "°C";
                case RecipeStepType.MaintainTemperature:
                    return "Maintain " + FormatNumber(step.MinimumTemperature) + "–" +
                           FormatNumber(step.MaximumTemperature) + "°C for " +
                           FormatNumber(step.RequiredDuration) + " s";
                case RecipeStepType.Stir:
                    return "Stir " + FormatRecipeDirection(step.StirDirection).ToLowerInvariant() + " " +
                           FormatNumber(step.RequiredStirringDegrees) + "°";
                case RecipeStepType.Wait:
                    return "Wait " + FormatNumber(step.RequiredDuration) + " s";
                case RecipeStepType.Complete:
                    return "Finish potion";
                default:
                    return step.StepType.ToString();
            }
        }

        private void RefreshRecipeButtons()
        {
            if (recipeButtons == null || magicRecipeBook == null)
                return;

            IReadOnlyList<RecipeDefinition> recipes = magicRecipeBook.AvailableRecipes;
            for (int i = 0; i < recipeButtons.Length; i++)
            {
                bool available = i < recipes.Count && recipes[i] != null;
                if (recipeButtons[i] != null)
                {
                    recipeButtons[i].gameObject.SetActive(available);
                    recipeButtons[i].interactable = available;
                }

                if (recipeButtonLabels != null && i < recipeButtonLabels.Length && recipeButtonLabels[i] != null)
                    recipeButtonLabels[i].text = available ? recipes[i].DisplayName : string.Empty;
            }

            UpdateRecipeButtonVisuals();
        }

        private void UpdateRecipeButtonVisuals()
        {
            if (recipeButtons == null || magicRecipeBook == null)
                return;

            IReadOnlyList<RecipeDefinition> recipes = magicRecipeBook.AvailableRecipes;
            for (int i = 0; i < recipeButtons.Length; i++)
            {
                if (recipeButtons[i] == null || recipeButtons[i].targetGraphic == null)
                    continue;

                bool selected = i < recipes.Count && recipes[i] == magicRecipeBook.SelectedRecipe;
                recipeButtons[i].targetGraphic.color = selected ? selectedRecipeButtonColor : normalRecipeButtonColor;
            }
        }

        private void ConfigureButtonHandlers()
        {
            if (recipeButtons != null)
            {
                for (int i = 0; i < recipeButtons.Length; i++)
                {
                    int capturedIndex = i;
                    UnityAction action = () =>
                    {
                        if (magicRecipeBook != null)
                            magicRecipeBook.SelectRecipeByIndex(capturedIndex);
                    };
                    recipeButtonActions.Add(action);
                    if (recipeButtons[i] != null)
                        recipeButtons[i].onClick.AddListener(action);
                }
            }

            startRecipeAction = () =>
            {
                if (magicRecipeBook != null)
                    magicRecipeBook.StartSelectedRecipe();
            };
            if (startRecipeButton != null)
                startRecipeButton.onClick.AddListener(startRecipeAction);

            backToRecipesAction = () =>
            {
                if (magicRecipeBook != null)
                    magicRecipeBook.BackToRecipes();
            };
            if (backToRecipesButton != null)
                backToRecipesButton.onClick.AddListener(backToRecipesAction);
        }

        private void RemoveButtonHandlers()
        {
            if (recipeButtons != null)
            {
                for (int i = 0; i < recipeButtons.Length && i < recipeButtonActions.Count; i++)
                {
                    if (recipeButtons[i] != null)
                        recipeButtons[i].onClick.RemoveListener(recipeButtonActions[i]);
                }
            }

            if (startRecipeButton != null && startRecipeAction != null)
                startRecipeButton.onClick.RemoveListener(startRecipeAction);
            if (backToRecipesButton != null && backToRecipesAction != null)
                backToRecipesButton.onClick.RemoveListener(backToRecipesAction);
        }

private void Subscribe()
        {
            if (subscribed)
                return;

            if (magicRecipeBook != null)
            {
                magicRecipeBook.SelectionChanged += OnSelectionChanged;
                magicRecipeBook.StatusChanged += RefreshStatus;
                magicRecipeBook.RecipeListRequested += ShowSelection;
            }

            if (recipeTracker != null)
            {
                recipeTracker.RecipeStarted += OnRecipeStarted;
                recipeTracker.RecipeCancelled += OnRecipeCancelled;
                recipeTracker.RecipeStepStarted += OnRecipeStepChanged;
                recipeTracker.RecipeStepCompleted += OnRecipeStepChanged;
                recipeTracker.RecipeProgressChanged += OnRecipeProgressChanged;
                recipeTracker.RecipeCompleted += OnRecipeCompleted;
            }

            if (potionEvaluator != null)
                potionEvaluator.PotionEvaluated += OnPotionEvaluated;

            subscribed = true;
        }

private void Unsubscribe()
        {
            if (!subscribed)
                return;

            if (magicRecipeBook != null)
            {
                magicRecipeBook.SelectionChanged -= OnSelectionChanged;
                magicRecipeBook.StatusChanged -= RefreshStatus;
                magicRecipeBook.RecipeListRequested -= ShowSelection;
            }

            if (recipeTracker != null)
            {
                recipeTracker.RecipeStarted -= OnRecipeStarted;
                recipeTracker.RecipeCancelled -= OnRecipeCancelled;
                recipeTracker.RecipeStepStarted -= OnRecipeStepChanged;
                recipeTracker.RecipeStepCompleted -= OnRecipeStepChanged;
                recipeTracker.RecipeProgressChanged -= OnRecipeProgressChanged;
                recipeTracker.RecipeCompleted -= OnRecipeCompleted;
            }

            if (potionEvaluator != null)
                potionEvaluator.PotionEvaluated -= OnPotionEvaluated;

            subscribed = false;
        }

        private void OnSelectionChanged(RecipeDefinition recipe)
        {
            if (CurrentView == RecipeBookView.Selection)
                RefreshPreview(recipe);
        }

        private void OnRecipeStarted(RecipeDefinition recipe)
        {
            ShowActiveRecipe(recipe);
        }

        private void OnRecipeCancelled(RecipeDefinition recipe)
        {
            ShowSelection();
        }

        private void OnRecipeStepChanged(int index, RecipeStepDefinition step)
        {
            if (recipeTracker != null)
            {
                RefreshActiveStatic(recipeTracker.ActiveRecipe);
                RefreshDynamicState();
            }
        }

        private void OnRecipeProgressChanged(float stepProgress, float overallProgress)
        {
            if (CurrentView == RecipeBookView.ActiveRecipe)
                RefreshDynamicState();
        }

        private void OnRecipeCompleted(RecipeDefinition recipe)
        {
            ShowCompletion(recipe);
        }

private void OnPotionEvaluated(PotionEvaluationResult result)
        {
            ShowEvaluation(result);
        }


        private void RefreshStatus(string message)
        {
            if (statusText == null)
                return;

            statusText.text = message ?? string.Empty;
            statusText.gameObject.SetActive(!string.IsNullOrWhiteSpace(statusText.text));
        }

private void ResolveReferences()
        {
            if (magicRecipeBook == null)
                magicRecipeBook = GetComponentInParent<MagicRecipeBook>();
            if (recipeTracker == null)
                recipeTracker = FindFirstObjectByType<RecipeProgressTracker>(FindObjectsInactive.Include);
            if (brewingCauldron == null)
                brewingCauldron = FindFirstObjectByType<BrewingCauldron>(FindObjectsInactive.Include);
            if (stirringController == null)
                stirringController = FindFirstObjectByType<StirringController>(FindObjectsInactive.Include);
            if (potionEvaluator == null)
                potionEvaluator = FindFirstObjectByType<PotionEvaluator>(FindObjectsInactive.Include);
        }

        private static string IngredientName(RecipeStepDefinition step)
        {
            return step.Ingredient != null ? step.Ingredient.DisplayName : "ingredient";
        }

        private static string FormatRecipeDirection(RecipeStirDirection direction)
        {
            return direction == RecipeStirDirection.CounterClockwise ? "Counter-clockwise" : direction.ToString();
        }

        private static string FormatLiveDirection(StirDirection direction)
        {
            return direction == StirDirection.CounterClockwise ? "Counter-clockwise" : direction.ToString();
        }

        private static string FormatNumber(float value)
        {
            return value.ToString("0.#");
        }

        private static void SetText(TMP_Text target, string value)
        {
            if (target != null)
                target.text = value ?? string.Empty;
        }

        private static void SetFill(Image target, float value)
        {
            if (target != null)
                target.fillAmount = Mathf.Clamp01(value);
        }

        private static void SetViewActive(GameObject target, bool active)
        {
            if (target != null && target.activeSelf != active)
                target.SetActive(active);
        }
    }
}
