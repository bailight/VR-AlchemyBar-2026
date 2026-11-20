using Unity.XR.CoreUtils;
using UnityEngine;

namespace AlchemyBar.Audio
{
    public sealed class FootstepAudio : MonoBehaviour
    {
        [SerializeField] private float strideLength = 0.65f;
        [SerializeField] private float minSpeed = 0.35f;
        [SerializeField] private float teleportDistance = 0.6f;
        [SerializeField, Range(0f, 1f)] private float volume = 0.55f;

        private Transform head;
        private Vector3 lastPosition;
        private bool hasLast;
        private float travelled;

        private void Start()
        {
            var origin = GetComponent<XROrigin>();
            head = origin != null && origin.Camera != null ? origin.Camera.transform : Camera.main != null ? Camera.main.transform : null;
        }

        private void OnDisable() => hasLast = false;

        private void LateUpdate()
        {
            if (head == null || Time.timeScale <= 0f || Time.deltaTime <= 0f)
            {
                hasLast = false;
                return;
            }

            Vector3 position = head.position;
            if (!hasLast)
            {
                lastPosition = position;
                hasLast = true;
                return;
            }

            Vector3 delta = position - lastPosition;
            delta.y = 0f;
            lastPosition = position;
            float moved = delta.magnitude;

            if (moved > teleportDistance)
            {
                travelled = 0f;
                return;
            }

            if (moved / Time.deltaTime < minSpeed)
            {
                travelled = strideLength * 0.6f;
                return;
            }

            travelled += moved;
            if (travelled < strideLength) return;
            travelled -= strideLength;

            Vector3 feet = new Vector3(position.x, transform.position.y, position.z);
            GameSfx.PlayAt("Footstep", feet, volume, 0.1f, 0.5f);
        }
    }
}
