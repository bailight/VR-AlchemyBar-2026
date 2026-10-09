using System;
using AlchemyBar.Ingredients;
using UnityEngine;

namespace AlchemyBar.Grinding
{
    [RequireComponent(typeof(Collider))]
    public sealed class MortarController : MonoBehaviour
    {
        [SerializeField] private Collider grindingZone;
        [SerializeField] private IngredientInstance currentIngredient;

        public IngredientInstance CurrentIngredient => currentIngredient;
        public bool CanGrind => currentIngredient != null && currentIngredient.CanGrind;

        public event Action<IngredientInstance> IngredientEntered;
        public event Action<IngredientInstance> IngredientExited;

        private void Awake()
        {
            if (grindingZone == null)
                grindingZone = GetComponent<Collider>();
            grindingZone.isTrigger = true;
        }

        public bool IsPointInsideGrindingZone(Vector3 worldPosition)
        {
            if (grindingZone == null || !grindingZone.enabled)
                return false;

            Vector3 closestPoint = grindingZone.ClosestPoint(worldPosition);
            return (closestPoint - worldPosition).sqrMagnitude < 0.000004f;
        }

        private void OnTriggerEnter(Collider other)
        {
            TryRegister(other);
        }

        private void OnTriggerStay(Collider other)
        {
            if (currentIngredient == null)
                TryRegister(other);
        }

        private void OnTriggerExit(Collider other)
        {
            IngredientInstance ingredient = other.GetComponentInParent<IngredientInstance>();
            if (ingredient == null || ingredient != currentIngredient)
                return;

            currentIngredient = null;
            IngredientExited?.Invoke(ingredient);
        }

        private void TryRegister(Collider other)
        {
            IngredientInstance ingredient = other.GetComponentInParent<IngredientInstance>();
            if (ingredient == null || ingredient.Definition == null || !ingredient.Definition.Grindable)
                return;
            if (currentIngredient != null && currentIngredient != ingredient)
                return;
            if (currentIngredient == ingredient)
                return;

            currentIngredient = ingredient;
            IngredientEntered?.Invoke(ingredient);
        }

        private void OnValidate()
        {
            if (grindingZone == null)
                grindingZone = GetComponent<Collider>();
        }
    }
}