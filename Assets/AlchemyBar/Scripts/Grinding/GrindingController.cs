using AlchemyBar.Ingredients;
using UnityEngine;

namespace AlchemyBar.Grinding
{
    public sealed class GrindingController : MonoBehaviour
    {
        [SerializeField] private MortarController mortar;
        [SerializeField] private PestleController pestle;
        [SerializeField] private ParticleSystem grindingParticles;
        [SerializeField] private ParticleSystem completionParticles;
        [SerializeField, Min(0.0001f)] private float minimumMovementDistance = 0.003f;
        [SerializeField, Min(0.001f)] private float maximumMovementDistance = 0.08f;
        [SerializeField, Min(0.01f)] private float grindingAmountMultiplier = 1f;
        [SerializeField, Min(0.005f)] private float particleDistanceInterval = 0.04f;

        [SerializeField] private bool pestleInGrindingZone;
        [SerializeField] private bool grindingActive;

        private Vector3 previousTipPosition;
        private bool hasPreviousTipPosition;
        private float particleDistanceAccumulator;

        public bool PestleInGrindingZone => pestleInGrindingZone;
        public bool GrindingActive => grindingActive;
        public float CurrentGrindingProgress => mortar != null && mortar.CurrentIngredient != null
            ? mortar.CurrentIngredient.CurrentGrindingProgress
            : 0f;

        private void Awake()
        {
            if (mortar == null)
                mortar = GetComponentInChildren<MortarController>(true);
            if (pestle == null)
                pestle = Object.FindFirstObjectByType<PestleController>(FindObjectsInactive.Include);
            if (grindingParticles == null)
                grindingParticles = FindParticleSystem("Grinding Particles");
            if (completionParticles == null)
                completionParticles = FindParticleSystem("Completion Particles");
        }

        private void Update()
        {
            if (pestle == null || mortar == null)
            {
                grindingActive = false;
                pestleInGrindingZone = false;
                hasPreviousTipPosition = false;
                return;
            }

            Vector3 currentTipPosition = pestle.TipPosition;
            if (!hasPreviousTipPosition)
            {
                previousTipPosition = currentTipPosition;
                hasPreviousTipPosition = true;
                return;
            }

            float movementDistance = Vector3.Distance(currentTipPosition, previousTipPosition);
            previousTipPosition = currentTipPosition;

            pestleInGrindingZone = mortar.IsPointInsideGrindingZone(currentTipPosition);
            bool validContext = pestle.IsHeld && pestleInGrindingZone && mortar.CanGrind;
            bool meaningfulMovement = movementDistance >= minimumMovementDistance
                && movementDistance <= maximumMovementDistance;

            grindingActive = validContext && meaningfulMovement;
            if (!grindingActive)
                return;

            IngredientInstance ingredient = mortar.CurrentIngredient;
            bool wasGround = ingredient.CurrentPreparationState == IngredientPreparationState.Ground;
            if (!ingredient.TryAddGrindingAmount(movementDistance * grindingAmountMultiplier))
                return;

            particleDistanceAccumulator += movementDistance;
            if (grindingParticles != null && particleDistanceAccumulator >= particleDistanceInterval)
            {
                particleDistanceAccumulator = 0f;
                grindingParticles.Emit(2);
            }

            bool isNowGround = ingredient.CurrentPreparationState == IngredientPreparationState.Ground;
            if (!wasGround && isNowGround && completionParticles != null)
                completionParticles.Emit(20);
        }

        private ParticleSystem FindParticleSystem(string childName)
        {
            ParticleSystem[] systems = GetComponentsInChildren<ParticleSystem>(true);
            foreach (ParticleSystem system in systems)
                if (system.name == childName)
                    return system;
            return null;
        }
    }
}