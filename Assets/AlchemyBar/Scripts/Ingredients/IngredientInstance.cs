using System;
using UnityEngine;
using UnityEngine.Events;

namespace AlchemyBar.Ingredients
{
    public sealed class IngredientInstance : MonoBehaviour
    {
        [SerializeField] private IngredientDefinition definition;
        [SerializeField] private IngredientPreparationState currentPreparationState = IngredientPreparationState.Raw;
        [SerializeField, Min(0f)] private float accumulatedGrindingAmount;
        [SerializeField] private UnityEvent<float> grindingProgressChanged = new UnityEvent<float>();
        [SerializeField] private UnityEvent<IngredientPreparationState> preparationStateChanged = new UnityEvent<IngredientPreparationState>();

        public IngredientDefinition Definition => definition;
        public IngredientPreparationState CurrentPreparationState => currentPreparationState;
        public float CurrentGrindingProgress => definition == null ? 0f : Mathf.Clamp01(accumulatedGrindingAmount / definition.RequiredGrindingAmount);
        public bool CanGrind => definition != null && definition.Grindable && currentPreparationState == IngredientPreparationState.Raw;

        public event Action<IngredientInstance, float> GrindingProgressUpdated;
        public event Action<IngredientInstance, IngredientPreparationState> PreparationStateChanged;

        public bool TryAddGrindingAmount(float amount)
        {
            if (!CanGrind || amount <= 0f)
                return false;

            accumulatedGrindingAmount = Mathf.Min(accumulatedGrindingAmount + amount, definition.RequiredGrindingAmount);
            float progress = CurrentGrindingProgress;
            grindingProgressChanged.Invoke(progress);
            GrindingProgressUpdated?.Invoke(this, progress);

            if (progress >= 1f)
                SetPreparationState(IngredientPreparationState.Ground);

            return true;
        }

        public bool SetPreparationState(IngredientPreparationState newState)
        {
            if (currentPreparationState == newState)
                return false;

            currentPreparationState = newState;
            if (newState == IngredientPreparationState.Ground && definition != null)
                accumulatedGrindingAmount = definition.RequiredGrindingAmount;

            preparationStateChanged.Invoke(currentPreparationState);
            PreparationStateChanged?.Invoke(this, currentPreparationState);
            return true;
        }

        public void ResetRuntimeState()
        {
            accumulatedGrindingAmount = 0f;
            currentPreparationState = IngredientPreparationState.Raw;
            grindingProgressChanged.Invoke(0f);
            GrindingProgressUpdated?.Invoke(this, 0f);
            preparationStateChanged.Invoke(currentPreparationState);
            PreparationStateChanged?.Invoke(this, currentPreparationState);
        }

        private void OnValidate()
        {
            accumulatedGrindingAmount = Mathf.Max(0f, accumulatedGrindingAmount);
            if (definition != null)
                accumulatedGrindingAmount = Mathf.Min(accumulatedGrindingAmount, definition.RequiredGrindingAmount);
        }
    }
}