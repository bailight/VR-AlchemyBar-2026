using System;
using System.Collections.Generic;
using AlchemyBar.Ingredients;
using AlchemyBar.Recipes;
using UnityEngine;

namespace AlchemyBar.Evaluation
{
    public sealed class PotionEvaluator : MonoBehaviour
    {
        [Header("Category Weights")]
        [SerializeField, Min(0f)] private float ingredientWeight = 40f;
        [SerializeField, Min(0f)] private float temperatureWeight = 30f;
        [SerializeField, Min(0f)] private float stirringWeight = 30f;

        [Header("Grade Thresholds")]
        [SerializeField, Range(0f, 100f)] private float perfectThreshold = 90f;
        [SerializeField, Range(0f, 100f)] private float successfulThreshold = 65f;

        [Header("Ingredient Scoring")]
        [SerializeField, Range(0f, 1f)] private float wrongPreparationCredit = 0.35f;
        [SerializeField, Min(0f)] private float unexpectedIngredientPenalty = 15f;
        [SerializeField, Min(0f)] private float wrongPreparationPenalty = 10f;

        [Header("Temperature Scoring")]
        [SerializeField, Min(0f)] private float heatOvershootPenaltyPerDegree = 4f;
        [SerializeField, Min(0f)] private float maintainExcursionPenalty = 12f;
        [SerializeField, Min(0f)] private float maintainOutsideSecondPenalty = 4f;
        [SerializeField, Min(0f)] private float maintainDeviationPenaltyPerDegree = 2.5f;

        [Header("Stirring Scoring")]
        [SerializeField, Range(0f, 1f)] private float freeOvershootFraction = 0.1f;
        [SerializeField, Min(0f)] private float overshootPenaltyScale = 25f;
        [SerializeField, Min(0f)] private float maximumOvershootPenalty = 15f;

        [Header("Feedback")]
        [SerializeField, Range(1, 8)] private int maximumFeedbackMessages = 5;

        [Header("Runtime Result (Read Only)")]
        [SerializeField] private PotionEvaluationResult lastResult;

        private long lastEvaluatedSessionId = long.MinValue;

        public PotionEvaluationResult LastResult => lastResult;
        public bool HasResult => lastResult != null;
        public float PerfectThreshold => perfectThreshold;
        public float SuccessfulThreshold => successfulThreshold;

        public event Action<PotionEvaluationResult> PotionEvaluated;

        public PotionEvaluationResult EvaluateSession(BrewingEvaluationSession session)
        {
            if (session == null || session.Recipe == null)
                return null;

            if (lastResult != null && session.SessionId == lastEvaluatedSessionId)
                return lastResult;

            int unexpectedCount;
            int wrongPreparationCount;
            bool hasIngredientEvaluation;
            float ingredientScore = ScoreIngredients(
                session,
                out hasIngredientEvaluation,
                out unexpectedCount,
                out wrongPreparationCount);

            int excursionCount;
            bool hasTemperatureEvaluation;
            float temperatureScore = ScoreTemperature(
                session,
                out hasTemperatureEvaluation,
                out excursionCount);

            float reverseDegrees;
            bool hasStirringEvaluation;
            float stirringScore = ScoreStirring(
                session,
                out hasStirringEvaluation,
                out reverseDegrees);

            float weightedScore = 0f;
            float appliedWeight = 0f;
            if (hasIngredientEvaluation)
            {
                weightedScore += ingredientScore * ingredientWeight;
                appliedWeight += ingredientWeight;
            }

            if (hasTemperatureEvaluation)
            {
                weightedScore += temperatureScore * temperatureWeight;
                appliedWeight += temperatureWeight;
            }

            if (hasStirringEvaluation)
            {
                weightedScore += stirringScore * stirringWeight;
                appliedWeight += stirringWeight;
            }

            float overallScore = appliedWeight > Mathf.Epsilon
                ? weightedScore / appliedWeight
                : 100f;
            overallScore = Mathf.Clamp(overallScore, 0f, 100f);

            PotionQualityGrade grade = overallScore >= perfectThreshold
                ? PotionQualityGrade.Perfect
                : overallScore >= successfulThreshold
                    ? PotionQualityGrade.Successful
                    : PotionQualityGrade.Failed;

            List<string> feedback = BuildFeedback(
                hasIngredientEvaluation,
                ingredientScore,
                unexpectedCount,
                wrongPreparationCount,
                hasTemperatureEvaluation,
                temperatureScore,
                excursionCount,
                hasStirringEvaluation,
                stirringScore,
                reverseDegrees);

            lastEvaluatedSessionId = session.SessionId;
            lastResult = new PotionEvaluationResult(
                session.Recipe,
                overallScore,
                ingredientScore,
                temperatureScore,
                stirringScore,
                hasIngredientEvaluation,
                hasTemperatureEvaluation,
                hasStirringEvaluation,
                grade,
                unexpectedCount,
                wrongPreparationCount,
                excursionCount,
                reverseDegrees,
                feedback);

            PotionEvaluated?.Invoke(lastResult);
            return lastResult;
        }

        public void ClearResult()
        {
            lastResult = null;
        }

        private float ScoreIngredients(
            BrewingEvaluationSession session,
            out bool applicable,
            out int unexpectedCount,
            out int wrongPreparationCount)
        {
            List<IngredientRequirement> requirements = BuildIngredientRequirements(session.Recipe);
            IReadOnlyList<IngredientEvaluationSample> actual = session.Ingredients;
            applicable = requirements.Count > 0 || actual.Count > 0;
            unexpectedCount = 0;
            wrongPreparationCount = 0;

            if (!applicable)
                return 0f;

            bool[] used = new bool[actual.Count];
            float fulfilledQuality = 0f;

            for (int requirementIndex = 0; requirementIndex < requirements.Count; requirementIndex++)
            {
                IngredientRequirement requirement = requirements[requirementIndex];
                int match = FindExactMatch(actual, used, requirement);
                if (match >= 0)
                {
                    used[match] = true;
                    fulfilledQuality += 1f;
                    continue;
                }

                if (!requirement.RequiresPreparation)
                    continue;

                int wrongPreparationMatch = FindIngredientMatch(actual, used, requirement.Definition);
                if (wrongPreparationMatch >= 0)
                {
                    used[wrongPreparationMatch] = true;
                    fulfilledQuality += wrongPreparationCredit;
                    wrongPreparationCount++;
                }
            }

            for (int i = 0; i < actual.Count; i++)
            {
                if (used[i])
                    continue;

                unexpectedCount++;
                if (IsWrongPreparationForAnyRequirement(actual[i], requirements))
                    wrongPreparationCount++;
            }

            float baseScore = requirements.Count > 0
                ? 100f * fulfilledQuality / requirements.Count
                : 100f;
            float score = baseScore
                          - unexpectedCount * unexpectedIngredientPenalty
                          - wrongPreparationCount * wrongPreparationPenalty;
            return Mathf.Clamp(score, 0f, 100f);
        }

        private float ScoreTemperature(
            BrewingEvaluationSession session,
            out bool applicable,
            out int excursionCount)
        {
            List<int> temperatureStepIndices = new List<int>();
            for (int i = 0; i < session.Recipe.Steps.Count; i++)
            {
                RecipeStepDefinition step = session.Recipe.Steps[i];
                if (step != null &&
                    (step.StepType == RecipeStepType.HeatToTemperature ||
                     step.StepType == RecipeStepType.MaintainTemperature))
                    temperatureStepIndices.Add(i);
            }

            applicable = temperatureStepIndices.Count > 0;
            excursionCount = 0;
            if (!applicable)
                return 0f;

            float total = 0f;
            for (int i = 0; i < temperatureStepIndices.Count; i++)
            {
                TemperatureEvaluationSample sample =
                    FindTemperatureSample(session.TemperatureSamples, temperatureStepIndices[i]);
                if (sample == null)
                    continue;

                if (sample.StepType == RecipeStepType.HeatToTemperature)
                {
                    total += Mathf.Clamp(
                        100f - sample.MaximumAboveDeviation * heatOvershootPenaltyPerDegree,
                        0f,
                        100f);
                }
                else
                {
                    excursionCount += sample.MeaningfulExcursionCount;
                    float maximumDeviation = Mathf.Max(
                        sample.MaximumBelowDeviation,
                        sample.MaximumAboveDeviation);
                    float penalty =
                        sample.MeaningfulExcursionCount * maintainExcursionPenalty +
                        sample.HarmfulOutsideTime * maintainOutsideSecondPenalty +
                        maximumDeviation * maintainDeviationPenaltyPerDegree;
                    total += Mathf.Clamp(100f - penalty, 0f, 100f);
                }
            }

            return Mathf.Clamp(total / temperatureStepIndices.Count, 0f, 100f);
        }

        private float ScoreStirring(
            BrewingEvaluationSession session,
            out bool applicable,
            out float reverseDegrees)
        {
            List<int> stirStepIndices = new List<int>();
            for (int i = 0; i < session.Recipe.Steps.Count; i++)
            {
                RecipeStepDefinition step = session.Recipe.Steps[i];
                if (step != null && step.StepType == RecipeStepType.Stir)
                    stirStepIndices.Add(i);
            }

            applicable = stirStepIndices.Count > 0;
            reverseDegrees = 0f;
            if (!applicable)
                return 0f;

            float totalScore = 0f;
            for (int i = 0; i < stirStepIndices.Count; i++)
            {
                StirEvaluationSample sample = FindStirSample(session.StirSamples, stirStepIndices[i]);
                if (sample == null)
                    continue;

                float correct;
                float wrong;
                switch (sample.RequiredDirection)
                {
                    case RecipeStirDirection.Clockwise:
                        correct = sample.ClockwiseDegrees;
                        wrong = sample.CounterClockwiseDegrees;
                        break;
                    case RecipeStirDirection.CounterClockwise:
                        correct = sample.CounterClockwiseDegrees;
                        wrong = sample.ClockwiseDegrees;
                        break;
                    default:
                        correct = sample.TotalDegrees;
                        wrong = 0f;
                        break;
                }

                reverseDegrees += wrong;
                float directionAccuracy = correct + wrong > Mathf.Epsilon
                    ? 100f * correct / (correct + wrong)
                    : 0f;

                float freeOvershoot = sample.RequiredDegrees * freeOvershootFraction;
                float excessiveOvershoot = Mathf.Max(
                    0f,
                    correct - sample.RequiredDegrees - freeOvershoot);
                float overshootPenalty = sample.RequiredDegrees > Mathf.Epsilon
                    ? Mathf.Min(
                        maximumOvershootPenalty,
                        excessiveOvershoot / sample.RequiredDegrees * overshootPenaltyScale)
                    : 0f;

                totalScore += Mathf.Clamp(directionAccuracy - overshootPenalty, 0f, 100f);
            }

            return Mathf.Clamp(totalScore / stirStepIndices.Count, 0f, 100f);
        }

        private List<string> BuildFeedback(
            bool hasIngredients,
            float ingredientScore,
            int unexpectedCount,
            int wrongPreparationCount,
            bool hasTemperature,
            float temperatureScore,
            int excursionCount,
            bool hasStirring,
            float stirringScore,
            float reverseDegrees)
        {
            var feedback = new List<string>();

            if (hasIngredients)
            {
                if (ingredientScore >= 95f)
                    AddFeedback(feedback, "Ingredients were prepared perfectly.");
                else
                {
                    if (unexpectedCount > 0)
                        AddFeedback(feedback, "Unexpected additions: " + unexpectedCount + ".");
                    if (wrongPreparationCount > 0)
                        AddFeedback(feedback, "Some ingredients were prepared incorrectly.");
                    if (unexpectedCount == 0 && wrongPreparationCount == 0)
                        AddFeedback(feedback, "Some required ingredients were missing.");
                }
            }

            if (hasTemperature)
            {
                if (temperatureScore >= 90f)
                    AddFeedback(feedback, "Temperature control was stable.");
                else if (excursionCount > 0 || temperatureScore < 70f)
                    AddFeedback(feedback, "Temperature control had significant deviations.");
                else
                    AddFeedback(feedback, "Temperature control had a minor overshoot.");
            }

            if (hasStirring)
            {
                if (stirringScore >= 90f)
                    AddFeedback(feedback, "Stirring direction was accurate.");
                else if (reverseDegrees > 1f)
                    AddFeedback(feedback, "Too much reverse stirring reduced quality.");
                else
                    AddFeedback(feedback, "Excessive stirring reduced quality.");
            }

            return feedback;
        }

        private void AddFeedback(List<string> feedback, string message)
        {
            if (feedback.Count < maximumFeedbackMessages)
                feedback.Add(message);
        }

        private static List<IngredientRequirement> BuildIngredientRequirements(RecipeDefinition recipe)
        {
            var requirements = new List<IngredientRequirement>();
            for (int i = 0; i < recipe.Steps.Count; i++)
            {
                RecipeStepDefinition step = recipe.Steps[i];
                if (step == null || step.Ingredient == null)
                    continue;

                if (step.StepType == RecipeStepType.AddIngredient)
                {
                    requirements.Add(new IngredientRequirement(step.Ingredient, false, IngredientPreparationState.Raw));
                }
                else if (step.StepType == RecipeStepType.AddPreparedIngredient)
                {
                    requirements.Add(new IngredientRequirement(
                        step.Ingredient,
                        true,
                        step.RequiredPreparationState));
                }
            }

            return requirements;
        }

        private static int FindExactMatch(
            IReadOnlyList<IngredientEvaluationSample> actual,
            bool[] used,
            IngredientRequirement requirement)
        {
            for (int i = 0; i < actual.Count; i++)
            {
                if (used[i] || !IngredientMatches(actual[i].Definition, requirement.Definition))
                    continue;

                if (!requirement.RequiresPreparation ||
                    actual[i].PreparationState == requirement.PreparationState)
                    return i;
            }

            return -1;
        }

        private static int FindIngredientMatch(
            IReadOnlyList<IngredientEvaluationSample> actual,
            bool[] used,
            IngredientDefinition definition)
        {
            for (int i = 0; i < actual.Count; i++)
            {
                if (!used[i] && IngredientMatches(actual[i].Definition, definition))
                    return i;
            }

            return -1;
        }

        private static bool IsWrongPreparationForAnyRequirement(
            IngredientEvaluationSample sample,
            List<IngredientRequirement> requirements)
        {
            if (sample == null)
                return false;

            for (int i = 0; i < requirements.Count; i++)
            {
                IngredientRequirement requirement = requirements[i];
                if (requirement.RequiresPreparation &&
                    IngredientMatches(sample.Definition, requirement.Definition) &&
                    sample.PreparationState != requirement.PreparationState)
                    return true;
            }

            return false;
        }

        private static bool IngredientMatches(IngredientDefinition actual, IngredientDefinition expected)
        {
            if (actual == null || expected == null)
                return false;
            if (actual == expected)
                return true;
            return !string.IsNullOrEmpty(actual.Id) &&
                   string.Equals(actual.Id, expected.Id, StringComparison.Ordinal);
        }

        private static TemperatureEvaluationSample FindTemperatureSample(
            IReadOnlyList<TemperatureEvaluationSample> samples,
            int stepIndex)
        {
            for (int i = 0; i < samples.Count; i++)
            {
                if (samples[i].StepIndex == stepIndex)
                    return samples[i];
            }

            return null;
        }

        private static StirEvaluationSample FindStirSample(
            IReadOnlyList<StirEvaluationSample> samples,
            int stepIndex)
        {
            for (int i = 0; i < samples.Count; i++)
            {
                if (samples[i].StepIndex == stepIndex)
                    return samples[i];
            }

            return null;
        }

        private void OnValidate()
        {
            ingredientWeight = Mathf.Max(0f, ingredientWeight);
            temperatureWeight = Mathf.Max(0f, temperatureWeight);
            stirringWeight = Mathf.Max(0f, stirringWeight);
            perfectThreshold = Mathf.Clamp(perfectThreshold, 0f, 100f);
            successfulThreshold = Mathf.Clamp(successfulThreshold, 0f, perfectThreshold);
            wrongPreparationCredit = Mathf.Clamp01(wrongPreparationCredit);
            maximumFeedbackMessages = Mathf.Clamp(maximumFeedbackMessages, 1, 8);
        }

        private sealed class IngredientRequirement
        {
            public IngredientDefinition Definition { get; }
            public bool RequiresPreparation { get; }
            public IngredientPreparationState PreparationState { get; }

            public IngredientRequirement(
                IngredientDefinition definition,
                bool requiresPreparation,
                IngredientPreparationState preparationState)
            {
                Definition = definition;
                RequiresPreparation = requiresPreparation;
                PreparationState = preparationState;
            }
        }
    }
}
