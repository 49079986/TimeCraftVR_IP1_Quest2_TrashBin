using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;

namespace TimeCraft.IP1
{
    /// <summary>
    /// Lightweight OpenXR controller rig for the standalone Quest 2 build.
    /// The desktop mouse/keyboard workflow remains available when XR is not active.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public sealed class Quest2VRAdapter : MonoBehaviour
    {
        private sealed class HandState
        {
            public XRNode node;
            public InputDevice device;
            public Transform visual;
            public LineRenderer ray;
            public bool previousTrigger;
            public bool previousPrimary;
            public bool previousSecondary;
            public bool previousGrip;
            public float dragDistance = 1.5f;
            public float stickCooldown;
            public bool dragging;
            public bool gripHolding;
            public bool currentGrip;
            public bool gripActionStarted;
            public float gripPressedTime;
            public Vector3 previousGripPosition;
            public Vector3 throwVelocity;
        }

        private static readonly Color IdleRayColor = new Color(0.1f, 0.82f, 1f, 0.85f);
        private static readonly Color HitRayColor = new Color(0.2f, 1f, 0.45f, 0.95f);
        private static readonly Color DragRayColor = new Color(1f, 0.68f, 0.12f, 0.98f);
        private static readonly Color CrumpleRayColor = new Color(1f, 0.15f, 0.28f, 0.98f);

        private readonly HandState left = new HandState { node = XRNode.LeftHand };
        private readonly HandState right = new HandState { node = XRNode.RightHand };
        private readonly List<XRInputSubsystem> inputSubsystems = new List<XRInputSubsystem>();

        private TimeCraftPrototypeManager manager;
        private Transform trackingOrigin;
        private Camera xrCamera;
        private Material rayMaterial;
        private HandState activeDragHand;
        private bool twoHandTrimActive;
        private bool twoHandTrimConsumed;
        private float twoHandStartDistance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (Application.platform != RuntimePlatform.Android && !XRSettings.isDeviceActive)
            {
                return;
            }

            if (FindFirstObjectByType<Quest2VRAdapter>() == null)
            {
                new GameObject("Quest 2 OpenXR Rig").AddComponent<Quest2VRAdapter>();
            }
        }

        private IEnumerator Start()
        {
            manager = FindFirstObjectByType<TimeCraftPrototypeManager>();
            if (manager == null)
            {
                enabled = false;
                yield break;
            }

            manager.SetXRMode(true);
            Application.targetFrameRate = 72;
            QualitySettings.vSyncCount = 0;

            xrCamera = manager.MainCamera;
            if (xrCamera == null)
            {
                enabled = false;
                yield break;
            }

            BuildTrackingRig();
            DisableDesktopOverlayCanvas();
            CreateWorldInstructions();

            for (var attempt = 0; attempt < 120; attempt++)
            {
                ConfigureFloorTracking();
                RefreshDevice(left);
                RefreshDevice(right);
                if (left.device.isValid || right.device.isValid)
                {
                    break;
                }

                yield return null;
            }
        }

        private void BuildTrackingRig()
        {
            trackingOrigin = new GameObject("XR Origin (Floor)").transform;
            trackingOrigin.position = new Vector3(0f, 0f, -2.35f);
            trackingOrigin.rotation = Quaternion.identity;
            trackingOrigin.SetParent(transform, true);

            xrCamera.transform.SetParent(trackingOrigin, false);
            xrCamera.transform.localPosition = new Vector3(0f, 1.65f, 0f);
            xrCamera.transform.localRotation = Quaternion.identity;
            xrCamera.nearClipPlane = 0.05f;
            xrCamera.farClipPlane = 100f;
            xrCamera.allowHDR = false;
            xrCamera.allowDynamicResolution = true;

            var shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null)
            {
                shader = Shader.Find("Sprites/Default");
            }
            rayMaterial = new Material(shader) { name = "Quest 2 Controller Ray" };

            left.visual = CreateHandVisual("Left Controller", new Color(0.18f, 0.65f, 1f), out left.ray);
            right.visual = CreateHandVisual("Right Controller", new Color(1f, 0.45f, 0.15f), out right.ray);
        }

        private Transform CreateHandVisual(string name, Color color, out LineRenderer line)
        {
            var hand = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            hand.name = name;
            hand.transform.SetParent(trackingOrigin, false);
            hand.transform.localScale = Vector3.one * 0.055f;

            var collider = hand.GetComponent<Collider>();
            if (collider != null)
            {
                Destroy(collider);
            }

            var renderer = hand.GetComponent<Renderer>();
            renderer.sharedMaterial = new Material(rayMaterial);
            renderer.sharedMaterial.color = color;

            line = hand.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.positionCount = 2;
            line.startWidth = 0.006f;
            line.endWidth = 0.002f;
            line.numCapVertices = 4;
            line.sharedMaterial = new Material(rayMaterial);
            SetLineColor(line, IdleRayColor);
            return hand.transform;
        }

        private void Update()
        {
            if (trackingOrigin == null || manager == null)
            {
                return;
            }

            UpdateHeadPose();
            UpdateHand(left);
            UpdateHand(right);
            UpdateTwoHandTrimGesture();
        }

        private void UpdateHeadPose()
        {
            var head = InputDevices.GetDeviceAtXRNode(XRNode.CenterEye);
            if (head.TryGetFeatureValue(CommonUsages.devicePosition, out var position))
            {
                xrCamera.transform.localPosition = position;
            }
            if (head.TryGetFeatureValue(CommonUsages.deviceRotation, out var rotation))
            {
                xrCamera.transform.localRotation = rotation;
            }
        }

        private void UpdateHand(HandState hand)
        {
            if (!hand.device.isValid)
            {
                RefreshDevice(hand);
            }

            var tracked = hand.device.isValid && UpdateTrackedPose(hand);
            hand.visual.gameObject.SetActive(tracked);
            if (!tracked)
            {
                return;
            }

            var interactionRay = new Ray(hand.visual.position, hand.visual.forward);
            var displayDistance = 6f;
            var lineColor = IdleRayColor;
            if (Physics.Raycast(interactionRay, out var hoverHit, displayDistance))
            {
                displayDistance = hoverHit.distance;
                if (hoverHit.collider.GetComponentInParent<TimeCraftInteractable>() != null ||
                    hoverHit.collider.GetComponentInParent<TimeCraftFloatingPreview>() != null ||
                    hoverHit.collider.GetComponentInParent<TimeCraftTrimPiece>() != null ||
                    hoverHit.collider.GetComponentInParent<TimeCraftTimelineScrubber>() != null ||
                    hoverHit.collider.GetComponentInParent<TimeCraftPlaybackButton>() != null)
                {
                    lineColor = HitRayColor;
                }
            }

            var trigger = ReadButton(hand.device, CommonUsages.triggerButton);
            if (trigger && !hand.previousTrigger && activeDragHand == null)
            {
                if (manager.BeginXRDrag(interactionRay, out var hitPoint))
                {
                    hand.dragDistance = Mathf.Clamp(Vector3.Distance(interactionRay.origin, hitPoint), 0.4f, 6f);
                    hand.dragging = true;
                    activeDragHand = hand;
                    Pulse(hand.device, 0.45f, 0.06f);
                }
            }

            if (hand.dragging && activeDragHand == hand)
            {
                displayDistance = hand.dragDistance;
                lineColor = DragRayColor;
                if (trigger)
                {
                    manager.MoveXRDrag(interactionRay.GetPoint(hand.dragDistance));
                }
                else
                {
                    manager.EndXRDrag();
                    hand.dragging = false;
                    activeDragHand = null;
                    Pulse(hand.device, 0.25f, 0.045f);
                }
            }

            var grip = ReadButton(hand.device, CommonUsages.gripButton);
            hand.currentGrip = grip;
            if (grip && !hand.previousGrip && (activeDragHand == null || activeDragHand == hand))
            {
                if (hand.dragging)
                {
                    manager.EndXRDrag();
                    hand.dragging = false;
                    activeDragHand = null;
                }
                hand.gripPressedTime = Time.unscaledTime;
                hand.gripActionStarted = false;
            }

            if (grip && !hand.gripActionStarted && !twoHandTrimActive && !twoHandTrimConsumed &&
                Time.unscaledTime - hand.gripPressedTime >= 0.22f)
            {
                hand.gripActionStarted = true;
                if (manager.CrumpleSelected())
                {
                    hand.gripHolding = true;
                    hand.previousGripPosition = hand.visual.position;
                    hand.throwVelocity = Vector3.zero;
                    Pulse(hand.device, 0.75f, 0.1f);
                }
            }

            if (hand.gripHolding)
            {
                lineColor = CrumpleRayColor;
                var holdPosition = hand.visual.position + hand.visual.forward * 0.14f;
                if (grip)
                {
                    var instantaneous = (hand.visual.position - hand.previousGripPosition) / Mathf.Max(Time.unscaledDeltaTime, 0.001f);
                    hand.throwVelocity = Vector3.Lerp(hand.throwVelocity, instantaneous, 0.42f);
                    hand.previousGripPosition = hand.visual.position;
                    manager.HoldSelectedCrumpled(holdPosition, hand.visual.rotation);
                }
                else
                {
                    manager.ThrowSelectedCrumpled(hand.throwVelocity * 1.25f + hand.visual.forward * 0.35f);
                    hand.gripHolding = false;
                    hand.gripActionStarted = false;
                    Pulse(hand.device, 0.5f, 0.06f);
                }
            }

            var primary = ReadButton(hand.device, CommonUsages.primaryButton);
            if (primary && !hand.previousPrimary)
            {
                Pulse(hand.device, 0.15f, 0.025f);
            }

            var secondary = ReadButton(hand.device, CommonUsages.secondaryButton);
            if (secondary && !hand.previousSecondary)
            {
                manager.ToggleSequencePlayback();
                Pulse(hand.device, 0.18f, 0.03f);
            }

            if (hand.node == XRNode.RightHand)
            {
                UpdateSpeedStick(hand);
            }

            hand.previousTrigger = trigger;
            hand.previousPrimary = primary;
            hand.previousSecondary = secondary;
            hand.previousGrip = grip;

            hand.ray.SetPosition(0, interactionRay.origin);
            hand.ray.SetPosition(1, interactionRay.GetPoint(displayDistance));
            SetLineColor(hand.ray, lineColor);
        }

        private void UpdateTwoHandTrimGesture()
        {
            var selected = manager.SelectedInteractable;
            var canTear = selected != null &&
                          selected.Kind == InteractableKind.MediaClip &&
                          !selected.IsPaperSheet &&
                          !selected.IsCrumpled;

            if (left.currentGrip && right.currentGrip && canTear && !twoHandTrimConsumed)
            {
                var distance = Vector3.Distance(left.visual.position, right.visual.position);
                if (!twoHandTrimActive)
                {
                    twoHandTrimActive = true;
                    twoHandStartDistance = distance;
                    left.gripActionStarted = true;
                    right.gripActionStarted = true;
                }
                else if (distance - twoHandStartDistance > 0.28f)
                {
                    manager.ToggleSelectedTrim();
                    twoHandTrimActive = false;
                    twoHandTrimConsumed = true;
                    Pulse(left.device, 0.65f, 0.1f);
                    Pulse(right.device, 0.65f, 0.1f);
                }
                return;
            }

            if (!left.currentGrip && !right.currentGrip)
            {
                twoHandTrimActive = false;
                twoHandTrimConsumed = false;
                left.gripActionStarted = false;
                right.gripActionStarted = false;
            }
        }

        private bool UpdateTrackedPose(HandState hand)
        {
            var hasPosition = hand.device.TryGetFeatureValue(CommonUsages.devicePosition, out var position);
            var hasRotation = hand.device.TryGetFeatureValue(CommonUsages.deviceRotation, out var rotation);
            if (hasPosition)
            {
                hand.visual.localPosition = position;
            }
            if (hasRotation)
            {
                hand.visual.localRotation = rotation;
            }
            return hasPosition || hasRotation;
        }

        private void UpdateSpeedStick(HandState hand)
        {
            hand.stickCooldown = Mathf.Max(0f, hand.stickCooldown - Time.unscaledDeltaTime);
            if (!hand.device.TryGetFeatureValue(CommonUsages.primary2DAxis, out var stick) || hand.stickCooldown > 0f)
            {
                return;
            }

            if (stick.x > 0.72f)
            {
                manager.ChangeSelectedSpeed(0.5f);
                hand.stickCooldown = 0.35f;
                Pulse(hand.device, 0.18f, 0.025f);
            }
            else if (stick.x < -0.72f)
            {
                manager.ChangeSelectedSpeed(-0.5f);
                hand.stickCooldown = 0.35f;
                Pulse(hand.device, 0.18f, 0.025f);
            }
        }

        private static bool ReadButton(InputDevice device, InputFeatureUsage<bool> usage)
        {
            return device.TryGetFeatureValue(usage, out var value) && value;
        }

        private static void Pulse(InputDevice device, float amplitude, float duration)
        {
            if (device.isValid)
            {
                device.SendHapticImpulse(0u, amplitude, duration);
            }
        }

        private static void RefreshDevice(HandState hand)
        {
            hand.device = InputDevices.GetDeviceAtXRNode(hand.node);
        }

        private void ConfigureFloorTracking()
        {
            inputSubsystems.Clear();
            SubsystemManager.GetSubsystems(inputSubsystems);
            foreach (var subsystem in inputSubsystems)
            {
                if (subsystem.running)
                {
                    subsystem.TrySetTrackingOriginMode(TrackingOriginModeFlags.Floor);
                }
            }
        }

        private static void DisableDesktopOverlayCanvas()
        {
            foreach (var canvas in FindObjectsByType<Canvas>(FindObjectsSortMode.None))
            {
                if (canvas.renderMode == RenderMode.ScreenSpaceOverlay)
                {
                    canvas.gameObject.SetActive(false);
                }
            }
        }

        private void CreateWorldInstructions()
        {
            var board = new GameObject("Quest 2 Controls");
            board.transform.position = new Vector3(0f, 1.75f, -0.25f);
            board.transform.rotation = Quaternion.Euler(0f, 180f, 0f);

            var text = board.AddComponent<TextMesh>();
            text.text = "TIMECRAFT VR  |  QUEST 2\nTrigger: select / drag / scrub timeline\nOne Grip: crumple + hold; release to throw\nBoth Grips + pull apart: tear / trim\nRight stick: speed    B/Y: PLAY / STOP";
            text.anchor = TextAnchor.MiddleCenter;
            text.alignment = TextAlignment.Center;
            text.fontSize = 52;
            text.characterSize = 0.022f;
            text.color = new Color(0.82f, 0.96f, 1f);
        }

        private static void SetLineColor(LineRenderer line, Color color)
        {
            line.startColor = color;
            line.endColor = new Color(color.r, color.g, color.b, 0.25f);
            if (line.sharedMaterial != null)
            {
                line.sharedMaterial.color = color;
            }
        }
    }
}
