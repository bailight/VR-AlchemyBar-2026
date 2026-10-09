using System;
using System.Collections.Generic;
using AlchemyBar.Fire;
using AlchemyBar.Ingredients;
using UnityEngine;

namespace AlchemyBar.Brewing
{
    [Serializable]
    public sealed class AddedIngredientRecord
    {
        [SerializeField] private IngredientDefinition definition;
        [SerializeField] private IngredientPreparationState preparationState;

        public IngredientDefinition Definition => definition;
        public IngredientPreparationState PreparationState => preparationState;

        public AddedIngredientRecord(IngredientInstance ingredient)
        {
            definition = ingredient.Definition;
            preparationState = ingredient.CurrentPreparationState;
        }
    }

    public sealed class BrewingCauldron : MonoBehaviour
    {
        [SerializeField] private HeatSource heatSource;
        [SerializeField] private CauldronLiquidVisual liquidVisual;
        [SerializeField] private float ambientTemperature = 20f;
        [SerializeField] private float maximumTemperature = 120f;
        [SerializeField] private float heatingRate = 18f;
        [SerializeField] private float coolingRate = 0.08f;
        [SerializeField] private float temperatureEventStep = 1f;
        [SerializeField] private List<AddedIngredientRecord> addedIngredients = new List<AddedIngredientRecord>();
        [SerializeField] private float currentTemperature = 20f;
        [SerializeField] private float currentHeatInput;
        [SerializeField] private float totalHeatingTime;

        private readonly HashSet<int> consumedIngredientInstanceIds = new HashSet<int>();
        
private float lastReportedTemperature;

        public IReadOnlyList<AddedIngredientRecord> AddedIngredients => addedIngredients;
        public int IngredientCount => addedIngredients.Count;
        public float CurrentTemperature => currentTemperature;
        public float AmbientTemperature => ambientTemperature;
        public float CurrentHeatInput => currentHeatInput;
        public float TotalHeatingTime => totalHeatingTime;
        public bool ContainsPotion => addedIngredients.Count > 0;
        public bool IsHeating => currentHeatInput > 0.001f;

        public event Action<IngredientInstance> IngredientAdded;
        public event Action<float> TemperatureChanged;
        public event Action HeatingStarted;
        public event Action HeatingStopped;

        private void Awake()
        {
            currentTemperature = Mathf.Clamp(currentTemperature, ambientTemperature, maximumTemperature);
            lastReportedTemperature = currentTemperature;
            if (heatSource == null)
                heatSource = FindFirstObjectByType<HeatSource>(FindObjectsInactive.Include);
            if (liquidVisual == null)
                liquidVisual = GetComponentInChildren<CauldronLiquidVisual>(true);
        }

        private void Update()
        {
            float previousHeat = currentHeatInput;
            currentHeatInput = heatSource != null && heatSource.IsHeating(transform.position) ? heatSource.HeatOutput : 0f;

            float heating = heatingRate * currentHeatInput;
            float cooling = coolingRate * (currentTemperature - ambientTemperature);
            currentTemperature = Mathf.Clamp(currentTemperature + (heating - cooling) * Time.deltaTime, ambientTemperature, maximumTemperature);

            if (currentHeatInput > 0.001f)
                totalHeatingTime += Time.deltaTime;

            if (previousHeat <= 0.001f && currentHeatInput > 0.001f)
                HeatingStarted?.Invoke();
            else if (previousHeat > 0.001f && currentHeatInput <= 0.001f)
                HeatingStopped?.Invoke();

            if (Mathf.Abs(currentTemperature - lastReportedTemperature) >= temperatureEventStep)
            {
                lastReportedTemperature = currentTemperature;
                TemperatureChanged?.Invoke(currentTemperature);
            }

            if (liquidVisual != null)
                liquidVisual.SetTemperature(currentTemperature, ambientTemperature, maximumTemperature);
        }

        public bool TryAddIngredient(IngredientInstance ingredient)
        {
            if (ingredient == null || ingredient.Definition == null)
                return false;

            if (consumedIngredientInstanceIds.Contains(ingredient.GetInstanceID()))
                return false;

            consumedIngredientInstanceIds.Add(ingredient.GetInstanceID());
            addedIngredients.Add(new AddedIngredientRecord(ingredient));
            liquidVisual?.BlendIngredient(ingredient);
            IngredientAdded?.Invoke(ingredient);
            ingredient.gameObject.SetActive(false);
            return true;
        }

        public bool HasIngredient(string ingredientId, IngredientPreparationState? state = null)
        {
            for (int i = 0; i < addedIngredients.Count; i++)
            {
                AddedIngredientRecord record = addedIngredients[i];
                if (record.Definition == null || record.Definition.Id != ingredientId)
                    continue;
                if (!state.HasValue || record.PreparationState == state.Value)
                    return true;
            }
            return false;
        }

        private void OnValidate()
        {
            ambientTemperature = Mathf.Max(0f, ambientTemperature);
            maximumTemperature = Mathf.Max(ambientTemperature, maximumTemperature);
            heatingRate = Mathf.Max(0f, heatingRate);
            coolingRate = Mathf.Max(0f, coolingRate);
        }
    }
}