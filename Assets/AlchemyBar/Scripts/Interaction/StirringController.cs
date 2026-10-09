using System;
using AlchemyBar.Brewing;
using UnityEngine;

namespace AlchemyBar.Stirring
{
    public enum StirDirection
    {
        None,
        Clockwise,
        CounterClockwise
    }

    public sealed class StirringController : MonoBehaviour
    {
        [SerializeField] private MagicWand wand;
        [SerializeField] private CauldronStirZone stirZone;
        [SerializeField] private BrewingCauldron cauldron;
        [SerializeField] private StirringFeedback feedback;
        [SerializeField, Min(1f)] private float requiredTotalAngle = 720f;
        [SerializeField, Min(0f)] private float minimumRadius = 0.08f;
        [SerializeField, Min(0.01f)] private float maximumRadius = 0.32f;
        [SerializeField, Min(0f)] private float minimumTipMovement = 0.004f;
        [SerializeField, Min(0f)] private float minimumAngularDelta = 0.75f;
        [SerializeField, Min(1f)] private float maximumAngularDelta = 35f;
        [SerializeField, Min(0.1f)] private float maximumTipSpeed = 3f;
        [SerializeField, Min(0f)] private float maximumVerticalDelta = 0.06f;
        [SerializeField, Min(0f)] private float directionConfirmationAngle = 8f;
        [SerializeField, Min(0.01f)] private float stirringStopDelay = 0.18f;
        [SerializeField, Min(0.01f)] private float progressEventStep = 0.01f;
        [SerializeField, Min(0.1f)] private float angularSpeedSmoothing = 10f;

        [SerializeField] private bool isWandInside;
        [SerializeField] private bool isStirring;
        [SerializeField] private StirDirection currentDirection;
        [SerializeField] private float currentAngularSpeed;
        [SerializeField] private float clockwiseDegrees;
        [SerializeField] private float counterClockwiseDegrees;
        [SerializeField] private float totalStirringDegrees;
        [SerializeField] private float normalizedProgress;

        private bool hasPreviousSample;
        private Vector3 previousTipPosition;
        private Vector3 previousRadial;
        private float timeSinceValidMovement;
        private float lastReportedProgress;
        private StirDirection pendingDirection;
        private float pendingDirectionDegrees;

        public bool IsWandInside => isWandInside;
        public bool IsStirring => isStirring;
        public StirDirection CurrentDirection => currentDirection;
        public float CurrentAngularSpeed => currentAngularSpeed;
        public float ClockwiseDegrees => clockwiseDegrees;
        public float CounterClockwiseDegrees => counterClockwiseDegrees;
        public float TotalStirringDegrees => totalStirringDegrees;
        public float NormalizedProgress => normalizedProgress;

        public event Action StirringStarted;
        public event Action StirringStopped;
        public event Action<StirDirection> DirectionChanged;
        public event Action<float> StirringProgressChanged;

        private void Awake()
        {
            if (wand == null)
                wand = FindFirstObjectByType<MagicWand>(FindObjectsInactive.Include);
            if (stirZone == null)
                stirZone = FindFirstObjectByType<CauldronStirZone>(FindObjectsInactive.Include);
            if (cauldron == null)
                cauldron = FindFirstObjectByType<BrewingCauldron>(FindObjectsInactive.Include);
            if (feedback == null)
                TryGetComponent(out feedback);
        }

        private void Update()
        {
            if (wand == null || stirZone == null || cauldron == null)
            {
                StopStirring(true);
                return;
            }

            Vector3 tipPosition = wand.TipWorldPosition;
            bool tipInside = stirZone.ContainsPoint(tipPosition);
            stirZone.SetWandState(wand, tipInside);
            ProcessTipSample(tipPosition, Time.deltaTime, tipInside, cauldron.ContainsPotion);
            feedback?.SetFeedback(isStirring, currentAngularSpeed, currentDirection);
        }

        public bool ProcessTipSample(Vector3 tipWorldPosition, float deltaTime, bool tipInside, bool potionPresent)
        {
            isWandInside = tipInside;
            if (!tipInside || !potionPresent || stirZone == null || deltaTime <= Mathf.Epsilon)
            {
                ResetSampling();
                StopStirring(true);
                return false;
            }

            Vector3 normal = stirZone.PlaneNormal.normalized;
            Vector3 centerOffset = tipWorldPosition - stirZone.Center;
            float height = Vector3.Dot(centerOffset, normal);
            Vector3 radial = centerOffset - normal * height;
            float radius = radial.magnitude;

            if (!hasPreviousSample)
            {
                StoreSample(tipWorldPosition, radial);
                return false;
            }

            Vector3 movement = tipWorldPosition - previousTipPosition;
            float verticalMovement = Mathf.Abs(Vector3.Dot(movement, normal));
            Vector3 planarMovementVector = movement - normal * Vector3.Dot(movement, normal);
            float planarMovement = planarMovementVector.magnitude;
            float speed = movement.magnitude / deltaTime;

            bool radiusValid = radius >= minimumRadius && radius <= maximumRadius
                && previousRadial.magnitude >= minimumRadius
                && previousRadial.magnitude <= maximumRadius;
            bool movementValid = planarMovement >= minimumTipMovement
                && verticalMovement <= maximumVerticalDelta
                && speed <= maximumTipSpeed;

            float signedAngle = radiusValid
                ? Vector3.SignedAngle(previousRadial, radial, normal)
                : 0f;
            float absoluteAngle = Mathf.Abs(signedAngle);
            bool angleValid = absoluteAngle >= minimumAngularDelta
                && absoluteAngle <= maximumAngularDelta;

            StoreSample(tipWorldPosition, radial);

            if (!radiusValid || !movementValid || !angleValid)
            {
                AdvanceIdleTimer(deltaTime);
                return false;
            }

            StirDirection sampleDirection = DirectionFromSignedAngle(signedAngle, minimumAngularDelta);
            if (sampleDirection == StirDirection.None)
            {
                AdvanceIdleTimer(deltaTime);
                return false;
            }

            if (sampleDirection == StirDirection.Clockwise)
                clockwiseDegrees += absoluteAngle;
            else
                counterClockwiseDegrees += absoluteAngle;

            totalStirringDegrees += absoluteAngle;
            normalizedProgress = Mathf.Clamp01(totalStirringDegrees / requiredTotalAngle);

            float targetSpeed = absoluteAngle / deltaTime;
            float smoothing = 1f - Mathf.Exp(-angularSpeedSmoothing * deltaTime);
            currentAngularSpeed = Mathf.Lerp(currentAngularSpeed, targetSpeed, smoothing);
            timeSinceValidMovement = 0f;
            SetStirring(true);
            UpdateDirection(sampleDirection, absoluteAngle);
            ReportProgressIfNeeded();
            return true;
        }

        public void ResetProgress()
        {
            clockwiseDegrees = 0f;
            counterClockwiseDegrees = 0f;
            totalStirringDegrees = 0f;
            normalizedProgress = 0f;
            lastReportedProgress = 0f;
            ResetSampling();
            StopStirring(true);
            StirringProgressChanged?.Invoke(0f);
        }

        public static StirDirection DirectionFromSignedAngle(float signedAngle, float threshold = 0.01f)
        {
            if (signedAngle > threshold)
                return StirDirection.Clockwise;
            if (signedAngle < -threshold)
                return StirDirection.CounterClockwise;
            return StirDirection.None;
        }

        private void UpdateDirection(StirDirection sampleDirection, float angle)
        {
            if (sampleDirection == currentDirection)
            {
                pendingDirection = StirDirection.None;
                pendingDirectionDegrees = 0f;
                return;
            }

            if (sampleDirection != pendingDirection)
            {
                pendingDirection = sampleDirection;
                pendingDirectionDegrees = angle;
            }
            else
            {
                pendingDirectionDegrees += angle;
            }

            if (pendingDirectionDegrees < directionConfirmationAngle)
                return;

            currentDirection = sampleDirection;
            pendingDirection = StirDirection.None;
            pendingDirectionDegrees = 0f;
            DirectionChanged?.Invoke(currentDirection);
        }

        private void AdvanceIdleTimer(float deltaTime)
        {
            timeSinceValidMovement += deltaTime;
            currentAngularSpeed = Mathf.MoveTowards(currentAngularSpeed, 0f, 720f * deltaTime);
            if (timeSinceValidMovement >= stirringStopDelay)
                StopStirring(false);
        }

        private void StoreSample(Vector3 position, Vector3 radial)
        {
            previousTipPosition = position;
            previousRadial = radial;
            hasPreviousSample = true;
        }

        private void ResetSampling()
        {
            hasPreviousSample = false;
            previousRadial = Vector3.zero;
            timeSinceValidMovement = 0f;
            currentAngularSpeed = 0f;
        }

        private void SetStirring(bool value)
        {
            if (isStirring == value)
                return;
            isStirring = value;
            if (value)
                StirringStarted?.Invoke();
            else
                StirringStopped?.Invoke();
        }

        private void StopStirring(bool clearDirection)
        {
            SetStirring(false);
            currentAngularSpeed = 0f;
            if (clearDirection && currentDirection != StirDirection.None)
            {
                currentDirection = StirDirection.None;
                pendingDirection = StirDirection.None;
                pendingDirectionDegrees = 0f;
                DirectionChanged?.Invoke(currentDirection);
            }
        }

        private void ReportProgressIfNeeded()
        {
            if (normalizedProgress < 1f
                && normalizedProgress - lastReportedProgress < progressEventStep)
                return;

            lastReportedProgress = normalizedProgress;
            StirringProgressChanged?.Invoke(normalizedProgress);
        }

        private void OnValidate()
        {
            requiredTotalAngle = Mathf.Max(1f, requiredTotalAngle);
            minimumRadius = Mathf.Max(0f, minimumRadius);
            maximumRadius = Mathf.Max(minimumRadius + 0.01f, maximumRadius);
            minimumTipMovement = Mathf.Max(0f, minimumTipMovement);
            minimumAngularDelta = Mathf.Max(0f, minimumAngularDelta);
            maximumAngularDelta = Mathf.Max(minimumAngularDelta + 0.01f, maximumAngularDelta);
            maximumTipSpeed = Mathf.Max(0.1f, maximumTipSpeed);
            maximumVerticalDelta = Mathf.Max(0f, maximumVerticalDelta);
        }
    }
}
