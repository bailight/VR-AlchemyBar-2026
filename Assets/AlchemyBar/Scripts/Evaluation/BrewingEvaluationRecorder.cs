using System;
using System.Collections.Generic;
using AlchemyBar.Brewing;
using AlchemyBar.Recipes;
using AlchemyBar.Stirring;
using UnityEngine;

namespace AlchemyBar.Evaluation
{
    public sealed class BrewingEvaluationRecorder : MonoBehaviour
    {
        [Header("Observed Systems")]
        [SerializeField] private RecipeProgressTracker recipeTracker;
        [SerializeField] private BrewingCauldron brewingCauldron;
        [SerializeField] private StirringController stirringController;
        [SerializeField] private PotionEvaluator potionEvaluator;

        [Header("Runtime State (Read Only)")]
        [SerializeField] private bool isRecording;
        [SerializeField] private long currentSessionId;
        [SerializeField] private RecipeDefinition activeRecipe;
        [SerializeField] private int recordedIngredientCount;
        [SerializeField] private int recordedTemperatureStepCount;
        [SerializeField] private int recordedStirStepCount;

        private readonly List<IngredientEvaluationSample> ingredients =
            new List<IngredientEvaluationSample>();
        private readonly List<TemperatureEvaluationSample> temperatureSamples =
            new List<TemperatureEvaluationSample>();
        private readonly List<StirEvaluationSample> stirSamples =
            new List<StirEvaluationSample>();

        private static long nextSessionId;
        private int capturedCauldronIngredientCount;
        private TemperatureAccumulator activeTemperature;
        private int activeStirStepIndex = -1;
        private RecipeStepDefinition activeStirStep;
        private float stirBaselineClockwise;
        private float stirBaselineCounterClockwise;
        private bool subscribed;
        private BrewingEvaluationSession lastCompletedSession;

        public bool IsRecording => isRecording;
        public long CurrentSessionId => currentSessionId;
        public RecipeDefinition ActiveRecipe => activeRecipe;
        public BrewingEvaluationSession LastCompletedSession => lastCompletedSession;
        public PotionEvaluator Evaluator => potionEvaluator;

        private void Awake()
        {
            ResolveReferences();
        }

        private void OnEnable()
        {
            ResolveReferences();
            Subscribe();
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        private void Update()
        {
            if (!isRecording || activeTemperature == null || brewingCauldron == null)
                return;

            activeTemperature.Record(brewingCauldron.CurrentTemperature, Time.deltaTime);
        }

        private void OnRecipeStarted(RecipeDefinition recipe)
        {
            BeginSession(recipe);
        }

        private void OnRecipeCancelled(RecipeDefinition recipe)
        {
            DiscardSession();
        }

        private void OnRecipeStepStarted(int stepIndex, RecipeStepDefinition step)
        {
            if (!isRecording || step == null)
                return;

            if (step.StepType == RecipeStepType.HeatToTemperature)
            {
                activeTemperature = new TemperatureAccumulator(
                    stepIndex,
                    step.StepType,
                    step.TargetTemperature - step.TemperatureTolerance,
                    step.TargetTemperature + step.TemperatureTolerance,
                    step.TemperatureGracePeriod);
            }
            else if (step.StepType == RecipeStepType.MaintainTemperature)
            {
                activeTemperature = new TemperatureAccumulator(
                    stepIndex,
                    step.StepType,
                    step.MinimumTemperature,
                    step.MaximumTemperature,
                    step.TemperatureGracePeriod);
            }
            else if (step.StepType == RecipeStepType.Stir && stirringController != null)
            {
                activeStirStepIndex = stepIndex;
                activeStirStep = step;
                stirBaselineClockwise = stirringController.ClockwiseDegrees;
                stirBaselineCounterClockwise = stirringController.CounterClockwiseDegrees;
            }
        }

        private void OnRecipeStepCompleted(int stepIndex, RecipeStepDefinition step)
        {
            if (!isRecording || step == null)
                return;

            if (activeTemperature != null && activeTemperature.StepIndex == stepIndex)
                CompleteTemperatureStep();

            if (step.StepType == RecipeStepType.Stir && activeStirStepIndex == stepIndex)
                CompleteStirStep();
        }

        private void OnRecipeCompleted(RecipeDefinition recipe)
        {
            if (!isRecording || recipe == null || recipe != activeRecipe)
                return;

            CaptureNewIngredients();
            if (activeTemperature != null)
                CompleteTemperatureStep();
            if (activeStirStep != null)
                CompleteStirStep();

            BrewingEvaluationSession session = new BrewingEvaluationSession(
                currentSessionId,
                activeRecipe,
                ingredients,
                temperatureSamples,
                stirSamples);

            lastCompletedSession = session;
            isRecording = false;
            activeRecipe = null;
            activeTemperature = null;
            activeStirStep = null;
            activeStirStepIndex = -1;
            potionEvaluator?.EvaluateSession(session);
        }

        private void OnIngredientAdded(AlchemyBar.Ingredients.IngredientInstance ingredient)
        {
            if (isRecording)
                CaptureNewIngredients();
        }

        private void BeginSession(RecipeDefinition recipe)
        {
            ingredients.Clear();
            temperatureSamples.Clear();
            stirSamples.Clear();
            activeTemperature = null;
            activeStirStep = null;
            activeStirStepIndex = -1;
            lastCompletedSession = null;
            activeRecipe = recipe;
            currentSessionId = ++nextSessionId;
            capturedCauldronIngredientCount = brewingCauldron != null
                ? brewingCauldron.AddedIngredients.Count
                : 0;
            recordedIngredientCount = 0;
            recordedTemperatureStepCount = 0;
            recordedStirStepCount = 0;
            isRecording = recipe != null;
            potionEvaluator?.ClearResult();
        }

        private void DiscardSession()
        {
            isRecording = false;
            activeRecipe = null;
            activeTemperature = null;
            activeStirStep = null;
            activeStirStepIndex = -1;
            ingredients.Clear();
            temperatureSamples.Clear();
            stirSamples.Clear();
            recordedIngredientCount = 0;
            recordedTemperatureStepCount = 0;
            recordedStirStepCount = 0;
        }

        private void CaptureNewIngredients()
        {
            if (brewingCauldron == null)
                return;

            IReadOnlyList<AddedIngredientRecord> records = brewingCauldron.AddedIngredients;
            while (capturedCauldronIngredientCount < records.Count)
            {
                AddedIngredientRecord record = records[capturedCauldronIngredientCount++];
                if (record != null && record.Definition != null)
                {
                    ingredients.Add(new IngredientEvaluationSample(
                        record.Definition,
                        record.PreparationState));
                }
            }

            recordedIngredientCount = ingredients.Count;
        }

        private void CompleteTemperatureStep()
        {
            temperatureSamples.Add(activeTemperature.CreateSample());
            recordedTemperatureStepCount = temperatureSamples.Count;
            activeTemperature = null;
        }

        private void CompleteStirStep()
        {
            float clockwise = stirringController != null
                ? Mathf.Max(0f, stirringController.ClockwiseDegrees - stirBaselineClockwise)
                : 0f;
            float counterClockwise = stirringController != null
                ? Mathf.Max(0f, stirringController.CounterClockwiseDegrees - stirBaselineCounterClockwise)
                : 0f;

            stirSamples.Add(new StirEvaluationSample(
                activeStirStepIndex,
                activeStirStep.StirDirection,
                activeStirStep.RequiredStirringDegrees,
                clockwise,
                counterClockwise));
            recordedStirStepCount = stirSamples.Count;
            activeStirStep = null;
            activeStirStepIndex = -1;
        }

        private void ResolveReferences()
        {
            if (recipeTracker == null)
                recipeTracker = FindFirstObjectByType<RecipeProgressTracker>(FindObjectsInactive.Include);
            if (brewingCauldron == null)
                brewingCauldron = FindFirstObjectByType<BrewingCauldron>(FindObjectsInactive.Include);
            if (stirringController == null)
                stirringController = FindFirstObjectByType<StirringController>(FindObjectsInactive.Include);
            if (potionEvaluator == null)
                potionEvaluator = GetComponent<PotionEvaluator>();
            if (potionEvaluator == null)
                potionEvaluator = FindFirstObjectByType<PotionEvaluator>(FindObjectsInactive.Include);
        }

        private void Subscribe()
        {
            if (subscribed || recipeTracker == null)
                return;

            recipeTracker.RecipeStarted += OnRecipeStarted;
            recipeTracker.RecipeCancelled += OnRecipeCancelled;
            recipeTracker.RecipeStepStarted += OnRecipeStepStarted;
            recipeTracker.RecipeStepCompleted += OnRecipeStepCompleted;
            recipeTracker.RecipeCompleted += OnRecipeCompleted;

            if (brewingCauldron != null)
                brewingCauldron.IngredientAdded += OnIngredientAdded;

            subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!subscribed)
                return;

            if (recipeTracker != null)
            {
                recipeTracker.RecipeStarted -= OnRecipeStarted;
                recipeTracker.RecipeCancelled -= OnRecipeCancelled;
                recipeTracker.RecipeStepStarted -= OnRecipeStepStarted;
                recipeTracker.RecipeStepCompleted -= OnRecipeStepCompleted;
                recipeTracker.RecipeCompleted -= OnRecipeCompleted;
            }

            if (brewingCauldron != null)
                brewingCauldron.IngredientAdded -= OnIngredientAdded;

            subscribed = false;
        }

        private sealed class TemperatureAccumulator
        {
            private readonly RecipeStepType stepType;
            private readonly float minimum;
            private readonly float maximum;
            private readonly float gracePeriod;

            private float timeInRange;
            private float timeOutsideRange;
            private float harmfulOutsideTime;
            private float maximumBelowDeviation;
            private float maximumAboveDeviation;
            private float consecutiveOutsideTime;
            private int excursionCount;
            private bool excursionRegistered;

            public int StepIndex { get; }

            public TemperatureAccumulator(
                int stepIndex,
                RecipeStepType stepType,
                float minimum,
                float maximum,
                float gracePeriod)
            {
                StepIndex = stepIndex;
                this.stepType = stepType;
                this.minimum = minimum;
                this.maximum = maximum;
                this.gracePeriod = Mathf.Max(0f, gracePeriod);
            }

            public void Record(float temperature, float deltaTime)
            {
                float safeDeltaTime = Mathf.Max(0f, deltaTime);
                if (float.IsNaN(temperature) || float.IsInfinity(temperature))
                    return;

                if (temperature >= minimum && temperature <= maximum)
                {
                    timeInRange += safeDeltaTime;
                    consecutiveOutsideTime = 0f;
                    excursionRegistered = false;
                    return;
                }

                timeOutsideRange += safeDeltaTime;
                maximumBelowDeviation = Mathf.Max(
                    maximumBelowDeviation,
                    Mathf.Max(0f, minimum - temperature));
                maximumAboveDeviation = Mathf.Max(
                    maximumAboveDeviation,
                    Mathf.Max(0f, temperature - maximum));

                if (stepType != RecipeStepType.MaintainTemperature)
                    return;

                consecutiveOutsideTime += safeDeltaTime;
                if (consecutiveOutsideTime > gracePeriod)
                {
                    harmfulOutsideTime += safeDeltaTime;
                    if (!excursionRegistered)
                    {
                        excursionRegistered = true;
                        excursionCount++;
                    }
                }
            }

            public TemperatureEvaluationSample CreateSample()
            {
                return new TemperatureEvaluationSample(
                    StepIndex,
                    stepType,
                    minimum,
                    maximum,
                    timeInRange,
                    timeOutsideRange,
                    harmfulOutsideTime,
                    maximumBelowDeviation,
                    maximumAboveDeviation,
                    excursionCount);
            }
        }
    }
}
