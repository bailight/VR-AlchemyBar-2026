using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace AlchemyBar.Grinding
{
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(XRGrabInteractable))]
    public sealed class PestleController : MonoBehaviour
    {
        [SerializeField] private Transform grindingTip;
        [SerializeField] private Rigidbody body;
        [SerializeField] private XRGrabInteractable grabInteractable;

        public Transform GrindingTip => grindingTip;
        public Vector3 TipPosition => grindingTip != null ? grindingTip.position : transform.position;
        public bool IsHeld => grabInteractable != null && grabInteractable.isSelected;
        public Rigidbody Body => body;

        private void Awake()
        {
            if (body == null)
                body = GetComponent<Rigidbody>();
            if (grabInteractable == null)
                grabInteractable = GetComponent<XRGrabInteractable>();
            if (grindingTip == null)
            {
                Transform candidate = transform.Find("Grinding Tip");
                grindingTip = candidate != null ? candidate : transform;
            }
        }

        private void OnValidate()
        {
            if (body == null)
                body = GetComponent<Rigidbody>();
            if (grabInteractable == null)
                grabInteractable = GetComponent<XRGrabInteractable>();
            if (grindingTip == null)
                grindingTip = transform.Find("Grinding Tip");
        }
    }
}