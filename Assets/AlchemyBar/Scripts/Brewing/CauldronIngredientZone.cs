using AlchemyBar.Ingredients;
using UnityEngine;

namespace AlchemyBar.Brewing
{
    [RequireComponent(typeof(Collider))]
    public sealed class CauldronIngredientZone : MonoBehaviour
    {
        [SerializeField] private BrewingCauldron cauldron;
        [SerializeField] private Collider triggerZone;

        public Collider TriggerZone => triggerZone;

private void Awake()
        {
            if (cauldron == null)
                cauldron = GetComponentInParent<BrewingCauldron>();

            if (triggerZone == null)
            {
                Debug.LogError("CauldronIngredientZone requires an explicit trigger collider reference.", this);
                return;
            }

            triggerZone.isTrigger = true;
        }

private void OnValidate()
        {
            if (cauldron == null)
                cauldron = GetComponentInParent<BrewingCauldron>();

            if (triggerZone == null)
            {
                Collider[] colliders = GetComponents<Collider>();
                foreach (Collider candidate in colliders)
                {
                    if (candidate.isTrigger)
                    {
                        triggerZone = candidate;
                        break;
                    }
                }
            }
        }


        private void OnTriggerEnter(Collider other) => TryAdd(other);
        private void OnTriggerStay(Collider other) => TryAdd(other);

        private void TryAdd(Collider other)
        {
            IngredientInstance ingredient = other.GetComponentInParent<IngredientInstance>();
            if (ingredient != null)
                cauldron?.TryAddIngredient(ingredient);
        }
    }
}