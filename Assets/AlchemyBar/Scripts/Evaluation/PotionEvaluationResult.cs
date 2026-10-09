using System;
using System.Collections.Generic;
using AlchemyBar.Ingredients;
using AlchemyBar.Recipes;
using UnityEngine;

namespace AlchemyBar.Evaluation
{
    public enum PotionQualityGrade
    {
        Perfect,
        Successful,
        Failed
    }

    [Serializable]
    public sealed class IngredientEvaluationSample
    {
        [SerializeField] private IngredientDefinition definition;
        [SerializeField] private IngredientPreparationState preparationState;

        public IngredientDefinition Definition => definition;
        public IngredientPreparationState PreparationState => preparationState;

        public IngredientEvaluationSample(IngredientDefinition definition, IngredientPreparationState preparationState)
        {
            this.definition = definition;
            this.preparationState = preparationState;
        }
    }

    [Serializable]
    public sealed class TemperatureEvaluationSample
    {
        [SerializeField] private int stepIndex;
        [SerializeField] private RecipeStepType stepType;
        [SerializeField] private float targetMinimum;
        [SerializeField] private float targetMaximum;
        [SerializeField] private float timeInValidRange;
        [SerializeField] private float timeOutsideRange;
        [SerializeField] private float harmfulOutsideTime;
        [SerializeField] private float maximumBelowDeviation;
        [SerializeField] private float maximumAboveDeviation;
        [SerializeField] private int meaningfulExcursionCount;

        public int StepIndex => stepIndex;
        public RecipeStepType StepType => stepType;
        public float TargetMinimum => targetMinimum;
        public float TargetMaximum => targetMaximum;
        public float TimeInValidRange => timeInValidRange;
        public float TimeOutsideRange => timeOutsideRange;
        public float HarmfulOutsideTime => harmfulOutsideTime;
        public float MaximumBelowDeviation => maximumBelowDeviation;
        public float MaximumAboveDeviation => maximumAboveDeviation;
        public int MeaningfulExcursionCount => meaningfulExcursionCount;

        public TemperatureEvaluationSample(
            int stepIndex,
            RecipeStepType stepType,
            float targetMinimum,
            float targetMaximum,
            float timeInValidRange,
            float timeOutsideRange,
            float harmfulOutsideTime,
            float maximumBelowDeviation,
            float maximumAboveDeviation,
            int meaningfulExcursionCount)
        {
            this.stepIndex = stepIndex;
            this.stepType = stepType;
            this.targetMinimum = targetMinimum;
            this.targetMaximum = targetMaximum;
            this.timeInValidRange = Mathf.Max(0f, timeInValidRange);
            this.timeOutsideRange = Mathf.Max(0f, timeOutsideRange);
            this.harmfulOutsideTime = Mathf.Max(0f, harmfulOutsideTime);
            this.maximumBelowDeviation = Mathf.Max(0f, maximumBelowDeviation);
            this.maximumAboveDeviation = Mathf.Max(0f, maximumAboveDeviation);
            this.meaningfulExcursionCount = Mathf.Max(0, meaningfulExcursionCount);
        }
    }

    [Serializable]
    public sealed class StirEvaluationSample
    {
        [SerializeField] private int stepIndex;
        [SerializeField] private RecipeStirDirection requiredDirection;
        [SerializeField] private float requiredDegrees;
        [SerializeField] private float clockwiseDegrees;
        [SerializeField] private float counterClockwiseDegrees;

        public int StepIndex => stepIndex;
        public RecipeStirDirection RequiredDirection => requiredDirection;
        public float RequiredDegrees => requiredDegrees;
        public float ClockwiseDegrees => clockwiseDegrees;
        public float CounterClockwiseDegrees => counterClockwiseDegrees;
        public float TotalDegrees => clockwiseDegrees + counterClockwiseDegrees;

        public StirEvaluationSample(
            int stepIndex,
            RecipeStirDirection requiredDirection,
            float requiredDegrees,
            float clockwiseDegrees,
            float counterClockwiseDegrees)
        {
            this.stepIndex = stepIndex;
            this.requiredDirection = requiredDirection;
            this.requiredDegrees = Mathf.Max(0f, requiredDegrees);
            this.clockwiseDegrees = Mathf.Max(0f, clockwiseDegrees);
            this.counterClockwiseDegrees = Mathf.Max(0f, counterClockwiseDegrees);
        }
    }

    public sealed class BrewingEvaluationSession
    {
        private readonly List<IngredientEvaluationSample> ingredients;
        private readonly List<TemperatureEvaluationSample> temperatureSamples;
        private readonly List<StirEvaluationSample> stirSamples;

        public long SessionId { get; }
        public RecipeDefinition Recipe { get; }
        public IReadOnlyList<IngredientEvaluationSample> Ingredients => ingredients;
        public IReadOnlyList<TemperatureEvaluationSample> TemperatureSamples => temperatureSamples;
        public IReadOnlyList<StirEvaluationSample> StirSamples => stirSamples;

        public BrewingEvaluationSession(
            long sessionId,
            RecipeDefinition recipe,
            IEnumerable<IngredientEvaluationSample> ingredients,
            IEnumerable<TemperatureEvaluationSample> temperatureSamples,
            IEnumerable<StirEvaluationSample> stirSamples)
        {
            SessionId = sessionId;
            Recipe = recipe;
            this.ingredients = ingredients != null
                ? new List<IngredientEvaluationSample>(ingredients)
                : new List<IngredientEvaluationSample>();
            this.temperatureSamples = temperatureSamples != null
                ? new List<TemperatureEvaluationSample>(temperatureSamples)
                : new List<TemperatureEvaluationSample>();
            this.stirSamples = stirSamples != null
                ? new List<StirEvaluationSample>(stirSamples)
                : new List<StirEvaluationSample>();
        }
    }

    [Serializable]
    public sealed class PotionEvaluationResult
    {
        [SerializeField] private RecipeDefinition recipe;
        [SerializeField, Range(0f, 100f)] private float overallScore;
        [SerializeField, Range(0f, 100f)] private float ingredientScore;
        [SerializeField, Range(0f, 100f)] private float temperatureScore;
        [SerializeField, Range(0f, 100f)] private float stirringScore;
        [SerializeField] private bool hasIngredientEvaluation;
        [SerializeField] private bool hasTemperatureEvaluation;
        [SerializeField] private bool hasStirringEvaluation;
        [SerializeField] private PotionQualityGrade grade;
        [SerializeField] private int unexpectedIngredientCount;
        [SerializeField] private int wrongPreparationCount;
        [SerializeField] private int temperatureExcursionCount;
        [SerializeField] private float reverseStirringDegrees;
        [SerializeField] private List<string> feedbackMessages;

        public RecipeDefinition Recipe => recipe;
        public float OverallScore => overallScore;
        public float IngredientScore => ingredientScore;
        public float TemperatureScore => temperatureScore;
        public float StirringScore => stirringScore;
        public bool HasIngredientEvaluation => hasIngredientEvaluation;
        public bool HasTemperatureEvaluation => hasTemperatureEvaluation;
        public bool HasStirringEvaluation => hasStirringEvaluation;
        public PotionQualityGrade Grade => grade;
        public int UnexpectedIngredientCount => unexpectedIngredientCount;
        public int WrongPreparationCount => wrongPreparationCount;
        public int TemperatureExcursionCount => temperatureExcursionCount;
        public float ReverseStirringDegrees => reverseStirringDegrees;
        public IReadOnlyList<string> FeedbackMessages => feedbackMessages;

        public PotionEvaluationResult(
            RecipeDefinition recipe,
            float overallScore,
            float ingredientScore,
            float temperatureScore,
            float stirringScore,
            bool hasIngredientEvaluation,
            bool hasTemperatureEvaluation,
            bool hasStirringEvaluation,
            PotionQualityGrade grade,
            int unexpectedIngredientCount,
            int wrongPreparationCount,
            int temperatureExcursionCount,
            float reverseStirringDegrees,
            IEnumerable<string> feedbackMessages)
        {
            this.recipe = recipe;
            this.overallScore = Mathf.Clamp(overallScore, 0f, 100f);
            this.ingredientScore = Mathf.Clamp(ingredientScore, 0f, 100f);
            this.temperatureScore = Mathf.Clamp(temperatureScore, 0f, 100f);
            this.stirringScore = Mathf.Clamp(stirringScore, 0f, 100f);
            this.hasIngredientEvaluation = hasIngredientEvaluation;
            this.hasTemperatureEvaluation = hasTemperatureEvaluation;
            this.hasStirringEvaluation = hasStirringEvaluation;
            this.grade = grade;
            this.unexpectedIngredientCount = Mathf.Max(0, unexpectedIngredientCount);
            this.wrongPreparationCount = Mathf.Max(0, wrongPreparationCount);
            this.temperatureExcursionCount = Mathf.Max(0, temperatureExcursionCount);
            this.reverseStirringDegrees = Mathf.Max(0f, reverseStirringDegrees);
            this.feedbackMessages = feedbackMessages != null
                ? new List<string>(feedbackMessages)
                : new List<string>();
        }
    }
}
