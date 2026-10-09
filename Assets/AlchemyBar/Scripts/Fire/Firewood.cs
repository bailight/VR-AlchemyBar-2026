using UnityEngine;

namespace AlchemyBar.Fire
{
    public sealed class Firewood : MonoBehaviour
    {
        [SerializeField, Min(0.01f)] private float fuelValue = 3f;
        [SerializeField] private bool consumed;

        public float FuelValue => fuelValue;
        public bool IsConsumed => consumed;

        public bool Consume()
        {
            if (consumed)
                return false;
            consumed = true;
            gameObject.SetActive(false);
            return true;
        }
    }
}