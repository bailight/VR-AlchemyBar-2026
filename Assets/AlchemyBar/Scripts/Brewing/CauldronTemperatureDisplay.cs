using UnityEngine;

namespace AlchemyBar.Brewing
{
    [RequireComponent(typeof(TextMesh))]
    public sealed class CauldronTemperatureDisplay : MonoBehaviour
    {
        [SerializeField] private BrewingCauldron cauldron;
        [SerializeField, Min(0.05f)] private float updateInterval = 0.2f;

        private TextMesh label;
        private float nextUpdateTime;

        private void Awake()
        {
            label = GetComponent<TextMesh>();
            if (cauldron == null)
                cauldron = FindFirstObjectByType<BrewingCauldron>(FindObjectsInactive.Include);
        }

        private void Update()
        {
            if (cauldron == null || Time.time < nextUpdateTime)
                return;
            nextUpdateTime = Time.time + updateInterval;
            label.text = "TEMP\n" + Mathf.RoundToInt(cauldron.CurrentTemperature) + " °C";
        }
    }
}