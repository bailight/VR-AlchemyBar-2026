using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace AlchemyBar.Stirring
{
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(XRGrabInteractable))]
    public sealed class MagicWand : MonoBehaviour
    {
        [SerializeField] private Transform wandTip;
        [SerializeField] private Rigidbody wandRigidbody;
        [SerializeField] private XRGrabInteractable grabInteractable;

        private Vector3 previousTipPosition;
        private Vector3 tipVelocity;

        public Transform WandTip => wandTip;
        public Vector3 TipWorldPosition => wandTip != null ? wandTip.position : transform.position;
        public Vector3 TipVelocity => tipVelocity;
        public bool IsGrabbed => grabInteractable != null && grabInteractable.isSelected;

        private void Awake()
        {
            if (wandTip == null)
                wandTip = transform.Find("WandTip");
            if (wandRigidbody == null)
                wandRigidbody = GetComponent<Rigidbody>();
            if (grabInteractable == null)
                grabInteractable = GetComponent<XRGrabInteractable>();

            previousTipPosition = TipWorldPosition;
        }

private void FixedUpdate()
        {
            Vector3 currentPosition = TipWorldPosition;
            if (wandRigidbody != null)
            {
                tipVelocity = wandRigidbody.GetPointVelocity(currentPosition);
            }
            else
            {
                float deltaTime = Time.fixedDeltaTime;
                tipVelocity = deltaTime > Mathf.Epsilon
                    ? (currentPosition - previousTipPosition) / deltaTime
                    : Vector3.zero;
            }

            previousTipPosition = currentPosition;
        }

        private void OnDisable()
        {
            tipVelocity = Vector3.zero;
        }
    }
}
