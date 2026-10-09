using UnityEngine;

namespace AlchemyBar.Stirring
{
    [RequireComponent(typeof(Collider))]
    public sealed class CauldronStirZone : MonoBehaviour
    {
        [SerializeField, Min(0.01f)] private float radius = 0.3f;
        [SerializeField, Min(0.01f)] private float verticalHalfExtent = 0.18f;
        [SerializeField] private MagicWand currentWand;
        [SerializeField] private bool wandTipInside;

        public Vector3 Center => transform.position;
        public Vector3 PlaneNormal => transform.up;
        public float Radius => radius;
        public float VerticalHalfExtent => verticalHalfExtent;
        public MagicWand CurrentWand => currentWand;
        public bool IsWandInside => wandTipInside;

        private void Awake()
        {
            Collider zoneCollider = GetComponent<Collider>();
            if (zoneCollider != null)
                zoneCollider.isTrigger = true;
        }

        public bool ContainsPoint(Vector3 worldPosition)
        {
            Vector3 offset = worldPosition - Center;
            float verticalOffset = Vector3.Dot(offset, PlaneNormal);
            Vector3 planarOffset = offset - PlaneNormal * verticalOffset;
            return Mathf.Abs(verticalOffset) <= verticalHalfExtent
                && planarOffset.sqrMagnitude <= radius * radius;
        }

        public void SetWandState(MagicWand wand, bool isInside)
        {
            currentWand = isInside ? wand : null;
            wandTipInside = isInside;
        }

        private void OnValidate()
        {
            radius = Mathf.Max(0.01f, radius);
            verticalHalfExtent = Mathf.Max(0.01f, verticalHalfExtent);
        }
    }
}
