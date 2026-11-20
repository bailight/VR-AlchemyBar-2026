using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace AlchemyBar.Audio
{
    [RequireComponent(typeof(XRGrabInteractable))]
    public sealed class GrabbableAudio : MonoBehaviour
    {
        private static readonly string[] GlassWords = { "glass", "bottle", "potion", "crystal", "vial", "flask", "jar" };

        [SerializeField] private bool glass;
        [SerializeField, Range(0f, 1f)] private float grabVolume = 0.6f;
        [SerializeField, Range(0f, 1f)] private float releaseVolume = 0.4f;
        [SerializeField] private float minImpactSpeed = 0.5f;
        [SerializeField] private float loudImpactSpeed = 4f;

        private XRGrabInteractable grab;
        private float enabledAt;
        private float nextImpact;

        private void Awake()
        {
            grab = GetComponent<XRGrabInteractable>();
            string n = name.ToLowerInvariant();
            foreach (string word in GlassWords)
                if (n.Contains(word)) glass = true;
        }

        private void OnEnable()
        {
            enabledAt = Time.time;
            grab.selectEntered.AddListener(OnGrab);
            grab.selectExited.AddListener(OnRelease);
        }

        private void OnDisable()
        {
            grab.selectEntered.RemoveListener(OnGrab);
            grab.selectExited.RemoveListener(OnRelease);
        }

        private void OnGrab(SelectEnterEventArgs args)
        {
            if (args.interactorObject is XRSocketInteractor) return;
            GameSfx.PlayAt("Pickup", transform.position, grabVolume);
        }

        private void OnRelease(SelectExitEventArgs args)
        {
            if (args.isCanceled || args.interactorObject is XRSocketInteractor) return;
            GameSfx.PlayAt("Release", transform.position, releaseVolume);
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (Time.timeScale <= 0f || Time.time - enabledAt < 1f || Time.time < nextImpact) return;
            float speed = collision.relativeVelocity.magnitude;
            if (speed < minImpactSpeed) return;

            nextImpact = Time.time + 0.08f;
            float loudness = Mathf.InverseLerp(minImpactSpeed, loudImpactSpeed, speed);
            Vector3 point = collision.contactCount > 0 ? collision.GetContact(0).point : transform.position;
            GameSfx.PlayAt(glass ? "ImpactGlass" : "ImpactWood", point, Mathf.Lerp(0.15f, 1f, loudness), 0.1f);
        }
    }
}
