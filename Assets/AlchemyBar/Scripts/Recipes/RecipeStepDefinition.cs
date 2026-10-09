using System;
using AlchemyBar.Ingredients;
using UnityEngine;

namespace AlchemyBar.Recipes
{
    public enum RecipeStepType
    {
        AddIngredient,
        AddPreparedIngredient,
        HeatToTemperature,
        MaintainTemperature,
        Stir,
        Wait,
        Complete
    }

    public enum RecipeStirDirection
    {
        Either,
        Clockwise,
        CounterClockwise
    }

    [Serializable]
    public sealed class RecipeStepDefinition
    {
        [SerializeField] private RecipeStepType stepType;
        [SerializeField] private IngredientDefinition ingredient;
        [SerializeField] private IngredientPreparationState requiredPreparationState = IngredientPreparationState.Raw;

        [Header("Temperature")]
        [SerializeField] private float targetTemperature = 70f;
        [SerializeField, Min(0f)] private float temperatureTolerance = 3f;
        [SerializeField] private float minimumTemperature = 65f;
        [SerializeField] private float maximumTemperature = 75f;
        [SerializeField, Min(0f)] private float requiredDuration = 10f;
        [SerializeField, Min(0f)] private float temperatureGracePeriod = 0.75f;

        [Header("Stirring")]
        [SerializeField] private RecipeStirDirection stirDirection = RecipeStirDirection.Clockwise;
        [SerializeField, Min(0f)] private float requiredStirringDegrees = 720f;

        public RecipeStepType StepType => stepType;
        public IngredientDefinition Ingredient => ingredient;
        public IngredientPreparationState RequiredPreparationState => requiredPreparationState;
        public float TargetTemperature => targetTemperature;
        public float TemperatureTolerance => Mathf.Max(0f, temperatureTolerance);
        public float MinimumTemperature => minimumTemperature;
        public float MaximumTemperature => maximumTemperature;
        public float RequiredDuration => Mathf.Max(0f, requiredDuration);
        public float TemperatureGracePeriod => Mathf.Max(0f, temperatureGracePeriod);
        public RecipeStirDirection StirDirection => stirDirection;
        public float RequiredStirringDegrees => Mathf.Max(0f, requiredStirringDegrees);

        public bool TryValidate(out string error)
        {
            switch (stepType)
            {
                case RecipeStepType.AddIngredient:
                case RecipeStepType.AddPreparedIngredient:
                    if (ingredient == null)
                    {
                        error = "Ingredient steps require an IngredientDefinition.";
                        return false;
                    }
                    break;

                case RecipeStepType.HeatToTemperature:
                    if (!IsFinite(targetTemperature) || !IsFinite(temperatureTolerance) || temperatureTolerance < 0f)
                    {
                        error = "HeatToTemperature requires finite values and a non-negative tolerance.";
                        return false;
                    }
                    break;

                case RecipeStepType.MaintainTemperature:
                    if (!IsFinite(minimumTemperature) || !IsFinite(maximumTemperature) ||
                        minimumTemperature > maximumTemperature)
                    {
                        error = "MaintainTemperature requires a valid minimum/maximum range.";
                        return false;
                    }

                    if (!IsFinite(requiredDuration) || requiredDuration <= 0f ||
                        !IsFinite(temperatureGracePeriod) || temperatureGracePeriod < 0f)
                    {
                        error = "MaintainTemperature requires a positive duration and non-negative grace period.";
                        return false;
                    }
                    break;

                case RecipeStepType.Stir:
                    if (!IsFinite(requiredStirringDegrees) || requiredStirringDegrees <= 0f)
                    {
                        error = "Stir requires a positive angular amount.";
                        return false;
                    }
                    break;

                case RecipeStepType.Wait:
                    if (!IsFinite(requiredDuration) || requiredDuration <= 0f)
                    {
                        error = "Wait requires a positive duration.";
                        return false;
                    }
                    break;
            }

            error = string.Empty;
            return true;
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
