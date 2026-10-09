using System;
using AlchemyBar.Brewing;
using AlchemyBar.Ingredients;
using AlchemyBar.Stirring;
using UnityEngine;

namespace AlchemyBar.Recipes
{
    public sealed class RecipeProgressTracker : MonoBehaviour
    {
        [Header("Gameplay State Providers")]
        [SerializeField] private BrewingCauldron brewingCauldron;
        [SerializeField] private StirringController stirringController;

        [Header("Development Recipe")]
        [SerializeField] private RecipeDefinition defaultRecipe;
        [SerializeField] private bool autoStartRecipe = true;

        [Header("Progress Events")]
        [SerializeField, Range(0.001f, 0.25f)] private float progressEventThreshold = 0.01f;

        [Header("Runtime State (Read Only)")]
        [SerializeField] private RecipeDefinition activeRecipe;
        [SerializeField] private int currentStepIndex = -1;
        [SerializeField] private RecipeStepType currentStepType;
        [SerializeField, Range(0f, 1f)] private float currentStepProgress;
        [SerializeField, Range(0f, 1f)] private float normalizedRecipeProgress;
        [SerializeField] private bool isRecipeActive;
        [SerializeField] private bool isRecipeComplete;
        [SerializeField] private string lastValidationError;

        private float stepElapsedTime;
        private float outOfRangeTime;
        private float stirBaselineClockwise;
        private float stirBaselineCounterClockwise;
        private float stirBaselineTotal;
        private float lastProgressEventValue = -1f;
        private bool subscribed;

        public RecipeDefinition ActiveRecipe => activeRecipe;
        public RecipeDefinition DefaultRecipe => defaultRecipe;
        public int CurrentStepIndex => currentStepIndex;
        public RecipeStepDefinition CurrentStep
        {
            get
            {
                if (activeRecipe == null || currentStepIndex < 0 || currentStepIndex >= activeRecipe.Steps.Count)
                    return null;

                return activeRecipe.Steps[currentStepIndex];
            }
        }

        public RecipeStepType CurrentStepType => currentStepType;
        public bool IsRecipeActive => isRecipeActive;
        public bool IsRecipeComplete => isRecipeComplete;
        public float CurrentStepProgress => currentStepProgress;
        public float NormalizedRecipeProgress => normalizedRecipeProgress;
        public string LastValidationError => lastValidationError;

        public event Action<RecipeDefinition> RecipeStarted;
        public event Action<RecipeDefinition> RecipeCancelled;
        public event Action<int, RecipeStepDefinition> RecipeStepStarted;
        public event Action<int, RecipeStepDefinition> RecipeStepCompleted;
        public event Action<float, float> RecipeProgressChanged;
        public event Action<RecipeDefinition> RecipeCompleted;

        private void Awake()
        {
            ResolveReferences();
        }

        private void OnEnable()
        {
            ResolveReferences();
            Subscribe();
        }

        private void Start()
        {
            if (autoStartRecipe && defaultRecipe != null && !isRecipeActive && !isRecipeComplete)
                StartRecipe(defaultRecipe);
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        private void Update()
        {
            Tick(Time.deltaTime);
        }

        public bool StartRecipe(RecipeDefinition recipe)
        {
            string validationError;
            if (recipe == null)
            {
                lastValidationError = "Cannot start a null recipe.";
                return false;
            }

            if (!recipe.TryValidate(out validationError))
            {
                lastValidationError = validationError;
                return false;
            }

            if (isRecipeActive)
                CancelRecipe();

            BeginRecipe(recipe);
            return true;
        }

        public void CancelRecipe()
        {
            if (!isRecipeActive && activeRecipe == null)
                return;

            RecipeDefinition cancelledRecipe = activeRecipe;
            isRecipeActive = false;
            isRecipeComplete = false;
            activeRecipe = null;
            currentStepIndex = -1;
            currentStepProgress = 0f;
            normalizedRecipeProgress = 0f;
            ResetStepRuntime();
            PublishProgress(true);
            RecipeCancelled?.Invoke(cancelledRecipe);
        }

        public bool RestartRecipe()
        {
            RecipeDefinition recipe = activeRecipe != null ? activeRecipe : defaultRecipe;
            if (recipe == null)
            {
                lastValidationError = "No active or default recipe is available to restart.";
                return false;
            }

            string validationError;
            if (!recipe.TryValidate(out validationError))
            {
                lastValidationError = validationError;
                return false;
            }

            BeginRecipe(recipe);
            return true;
        }

        private void BeginRecipe(RecipeDefinition recipe)
        {
            activeRecipe = recipe;
            currentStepIndex = 0;
            isRecipeActive = true;
            isRecipeComplete = false;
            normalizedRecipeProgress = 0f;
            lastProgressEventValue = -1f;
            lastValidationError = string.Empty;
            ResetStepRuntime();

            RecipeStarted?.Invoke(activeRecipe);
            BeginCurrentStep();
        }

        private void BeginCurrentStep()
        {
            RecipeStepDefinition step = CurrentStep;
            if (step == null)
            {
                CompleteRecipe();
                return;
            }

            ResetStepRuntime();
            currentStepType = step.StepType;

            if (step.StepType == RecipeStepType.Stir && stirringController != null)
            {
                stirBaselineClockwise = stirringController.ClockwiseDegrees;
                stirBaselineCounterClockwise = stirringController.CounterClockwiseDegrees;
                stirBaselineTotal = stirringController.TotalStirringDegrees;
            }

            RecipeStepStarted?.Invoke(currentStepIndex, step);
            SetCurrentStepProgress(0f, true);

            if (step.StepType == RecipeStepType.Complete)
                CompleteCurrentStep();
        }

        private void Tick(float deltaTime)
        {
            if (!isRecipeActive || activeRecipe == null)
                return;

            RecipeStepDefinition step = CurrentStep;
            if (step == null)
            {
                CompleteRecipe();
                return;
            }

            float safeDeltaTime = Mathf.Max(0f, deltaTime);
            switch (step.StepType)
            {
                case RecipeStepType.HeatToTemperature:
                    TickHeatToTemperature(step);
                    break;
                case RecipeStepType.MaintainTemperature:
                    TickMaintainTemperature(step, safeDeltaTime);
                    break;
                case RecipeStepType.Stir:
                    TickStir(step);
                    break;
                case RecipeStepType.Wait:
                    TickWait(step, safeDeltaTime);
                    break;
            }
        }

        private void OnIngredientAdded(IngredientInstance ingredient)
        {
            if (!isRecipeActive || ingredient == null || brewingCauldron == null)
                return;

            RecipeStepDefinition step = CurrentStep;
            if (step == null ||
                (step.StepType != RecipeStepType.AddIngredient &&
                 step.StepType != RecipeStepType.AddPreparedIngredient))
                return;

            AddedIngredientRecord snapshot = GetLatestSnapshot();
            if (snapshot == null || snapshot.Definition == null || !IngredientMatches(snapshot.Definition, step.Ingredient))
                return;

            if (step.StepType == RecipeStepType.AddPreparedIngredient &&
                snapshot.PreparationState != step.RequiredPreparationState)
                return;

            CompleteCurrentStep();
        }

        private AddedIngredientRecord GetLatestSnapshot()
        {
            var ingredients = brewingCauldron.AddedIngredients;
            return ingredients != null && ingredients.Count > 0 ? ingredients[ingredients.Count - 1] : null;
        }

        private static bool IngredientMatches(IngredientDefinition actual, IngredientDefinition required)
        {
            if (actual == null || required == null)
                return false;

            if (actual == required)
                return true;

            return !string.IsNullOrEmpty(actual.Id) &&
                   string.Equals(actual.Id, required.Id, StringComparison.Ordinal);
        }

        private void TickHeatToTemperature(RecipeStepDefinition step)
        {
            if (brewingCauldron == null)
                return;

            float minimum = step.TargetTemperature - step.TemperatureTolerance;
            float maximum = step.TargetTemperature + step.TemperatureTolerance;
            bool inRange = brewingCauldron.CurrentTemperature >= minimum &&
                           brewingCauldron.CurrentTemperature <= maximum;
            SetCurrentStepProgress(inRange ? 1f : 0f);

            if (inRange)
                CompleteCurrentStep();
        }

        private void TickMaintainTemperature(RecipeStepDefinition step, float deltaTime)
        {
            if (brewingCauldron == null)
                return;

            float temperature = brewingCauldron.CurrentTemperature;
            bool inRange = temperature >= step.MinimumTemperature && temperature <= step.MaximumTemperature;
            if (inRange)
            {
                outOfRangeTime = 0f;
                stepElapsedTime += deltaTime;
            }
            else
            {
                outOfRangeTime += deltaTime;
                if (outOfRangeTime > step.TemperatureGracePeriod)
                    stepElapsedTime = 0f;
            }

            float progress = step.RequiredDuration > 0f ? stepElapsedTime / step.RequiredDuration : 1f;
            SetCurrentStepProgress(progress);
            if (stepElapsedTime >= step.RequiredDuration)
                CompleteCurrentStep();
        }

        private void TickStir(RecipeStepDefinition step)
        {
            if (stirringController == null)
                return;

            float degrees;
            switch (step.StirDirection)
            {
                case RecipeStirDirection.Clockwise:
                    degrees = Mathf.Max(0f, stirringController.ClockwiseDegrees - stirBaselineClockwise);
                    break;
                case RecipeStirDirection.CounterClockwise:
                    degrees = Mathf.Max(0f, stirringController.CounterClockwiseDegrees - stirBaselineCounterClockwise);
                    break;
                default:
                    degrees = Mathf.Max(0f, stirringController.TotalStirringDegrees - stirBaselineTotal);
                    break;
            }

            float progress = step.RequiredStirringDegrees > 0f ? degrees / step.RequiredStirringDegrees : 1f;
            SetCurrentStepProgress(progress);
            if (degrees >= step.RequiredStirringDegrees)
                CompleteCurrentStep();
        }

        private void TickWait(RecipeStepDefinition step, float deltaTime)
        {
            stepElapsedTime += deltaTime;
            float progress = step.RequiredDuration > 0f ? stepElapsedTime / step.RequiredDuration : 1f;
            SetCurrentStepProgress(progress);

            if (stepElapsedTime >= step.RequiredDuration)
                CompleteCurrentStep();
        }

        private void CompleteCurrentStep()
        {
            if (!isRecipeActive)
                return;

            RecipeStepDefinition completedStep = CurrentStep;
            if (completedStep == null)
            {
                CompleteRecipe();
                return;
            }

            SetCurrentStepProgress(1f, true);
            RecipeStepCompleted?.Invoke(currentStepIndex, completedStep);

            if (completedStep.StepType == RecipeStepType.Complete ||
                currentStepIndex >= activeRecipe.Steps.Count - 1)
            {
                CompleteRecipe();
                return;
            }

            currentStepIndex++;
            BeginCurrentStep();
        }

        private void CompleteRecipe()
        {
            if (isRecipeComplete)
                return;

            isRecipeActive = false;
            isRecipeComplete = true;
            currentStepProgress = 1f;
            normalizedRecipeProgress = 1f;
            PublishProgress(true);
            RecipeCompleted?.Invoke(activeRecipe);
        }

        private void SetCurrentStepProgress(float value, bool forceEvent = false)
        {
            currentStepProgress = Mathf.Clamp01(value);
            normalizedRecipeProgress = CalculateNormalizedRecipeProgress();
            PublishProgress(forceEvent);
        }

        private float CalculateNormalizedRecipeProgress()
        {
            if (activeRecipe == null)
                return 0f;

            int actionableSteps = activeRecipe.ActionableStepCount;
            if (actionableSteps <= 0)
                return isRecipeComplete ? 1f : 0f;

            int completedActionableSteps = 0;
            for (int i = 0; i < currentStepIndex && i < activeRecipe.Steps.Count; i++)
            {
                RecipeStepDefinition step = activeRecipe.Steps[i];
                if (step != null && step.StepType != RecipeStepType.Complete)
                    completedActionableSteps++;
            }

            RecipeStepDefinition current = CurrentStep;
            float activeContribution = current != null && current.StepType != RecipeStepType.Complete
                ? currentStepProgress
                : 0f;

            return Mathf.Clamp01((completedActionableSteps + activeContribution) / actionableSteps);
        }

        private void PublishProgress(bool force)
        {
            if (!force && lastProgressEventValue >= 0f &&
                Mathf.Abs(normalizedRecipeProgress - lastProgressEventValue) < progressEventThreshold)
                return;

            lastProgressEventValue = normalizedRecipeProgress;
            RecipeProgressChanged?.Invoke(currentStepProgress, normalizedRecipeProgress);
        }

        private void ResetStepRuntime()
        {
            currentStepProgress = 0f;
            stepElapsedTime = 0f;
            outOfRangeTime = 0f;
            stirBaselineClockwise = 0f;
            stirBaselineCounterClockwise = 0f;
            stirBaselineTotal = 0f;
        }

        private void ResolveReferences()
        {
            if (brewingCauldron == null)
                brewingCauldron = FindFirstObjectByType<BrewingCauldron>(FindObjectsInactive.Include);

            if (stirringController == null)
                stirringController = FindFirstObjectByType<StirringController>(FindObjectsInactive.Include);
        }

        private void Subscribe()
        {
            if (subscribed || brewingCauldron == null)
                return;

            brewingCauldron.IngredientAdded += OnIngredientAdded;
            subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!subscribed)
                return;

            if (brewingCauldron != null)
                brewingCauldron.IngredientAdded -= OnIngredientAdded;

            subscribed = false;
        }
    }
}
