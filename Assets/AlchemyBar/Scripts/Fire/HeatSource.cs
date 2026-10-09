using UnityEngine;

namespace AlchemyBar.Fire
{
    public sealed class HeatSource : MonoBehaviour
    {
        [SerializeField] private Transform heatOrigin;
        
[SerializeField] private FireController fireController;
        [SerializeField, Min(0.1f)] private float heatRange = 1.5f;
        [SerializeField, Min(0f)] private float maximumHeatOutput = 1f;

        public float HeatOutput => fireController == null ? 0f : fireController.HeatIntensity * maximumHeatOutput;
        public bool IsHeating(Vector3 position) => HeatOutput > 0f && Vector3.Distance(HeatOriginPosition, position) <= heatRange;

        private Vector3 HeatOriginPosition => heatOrigin != null ? heatOrigin.position : transform.position;

        private void Awake()
        {
            
            if (heatOrigin == null)
                heatOrigin = transform.Find("Flame Placeholder");
if (fireController == null)
                fireController = GetComponent<FireController>();
        }
    }
}