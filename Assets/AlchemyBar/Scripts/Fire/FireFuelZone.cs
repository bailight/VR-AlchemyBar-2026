using UnityEngine;

namespace AlchemyBar.Fire
{
    [RequireComponent(typeof(Collider))]
    public sealed class FireFuelZone : MonoBehaviour
    {
        [SerializeField] private FireController fireController;

        private void Awake()
        {
            if (fireController == null)
                fireController = GetComponentInParent<FireController>();
            GetComponent<Collider>().isTrigger = true;
        }

        private void OnTriggerEnter(Collider other) => TryAddFuel(other);
        private void OnTriggerStay(Collider other) => TryAddFuel(other);

        private void TryAddFuel(Collider other)
        {
            Firewood firewood = other.GetComponentInParent<Firewood>();
            if (firewood != null)
                fireController?.TryAddFirewood(firewood);
        }
    }
}