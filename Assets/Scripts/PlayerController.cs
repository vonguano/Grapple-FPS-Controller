using System.Collections;
using System.Collections.Generic;
using TMPro;
using ElmanGameDevTools.PlayerSystem;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ElmanGameDevTools.PlayerSystem
{
    [AddComponentMenu("Elman Game Dev Tools/Player System/Player Controller")]
    public class PlayerController : MonoBehaviour
    {
        [Header("REFERENCES")]
        public CharacterController controller;
        public Transform playerCamera;
        public Transform gunTip, cam, player;
        public LineRenderer lr;
        public LayerMask whatIsGrappleable;

        [Header("MOVEMENT SETTINGS")]
        public float speed = 6f;
        public float runSpeed = 14f;
        public float jumpHeight = 2f;
        public float gravity = -9.81f;
        public float sensitivity = 10f;

        [Header("CROUCH SETTINGS")]
        private float crouchHeight = 1.3f;
        public float crouchSmoothTime = 0.2f;

        [Header("CAMERA SETTINGS")]
        public float maxLookUpAngle = 90f;
        public float maxLookDownAngle = -90f;
        public bool enableHeadBob = true;
        public float walkBobSpeed = 14f;
        public float walkBobAmount = 0.05f;
        public float runBobSpeed = 18f;
        public float runBobAmount = 0.03f;

        [Header("CAMERA EFFECTS")]
        public bool enableCameraTilt = true;
        [Range(0f, 10f)] public float tiltAmount = 2f;
        [Range(1f, 20f)] public float tiltSmoothness = 8f;

        [Header("FOV SETTINGS")]
        public bool enableRunFov = true;
        public float normalFov = 60f;
        public float runFov = 70f;
        public float grappleFov = 95f;
        [Range(1f, 20f)] public float fovChangeSpeed = 8f;

        [Header("GRAPPLING SETTINGS")]
        public float grappleSpeedMultiplier = 1.5f;
        public float grappleAirControl = 0.3f;

        [Header("STANDING DETECTION")]
        public GameObject standingHeightMarker;
        public float standingCheckRadius = 0.2f;
        public LayerMask obstacleLayerMask = ~0;
        public float minStandingClearance = 0.01f;
        public float standCheckCooldown = 0.1f;

        // Private variables
        private Vector3 grappleVelocity;
        private Vector3 velocityToSet;
        private float xRotation;
        private float currentTilt;
        private float currentFov;
        private float targetFov;
        private float timer;
        private float originalHeight;
        private float targetHeight;
        private float currentHeightVelocity;
        private float cameraHeightVelocity;
        private float markerHeightOffset;
        private float lastStandCheckTime;
        private float currentMovementSpeed;
        private float defaultYPos;
        private float cameraBaseHeight;
        private float lastGroundedTime;
        private bool isGrounded;
        private bool isCrouching;
        private bool wantsToStand;
        private bool markerInitialized;
        private bool isCrouchKeyHeld;
        private bool wasRunningWhenJumped;
        private bool enableMovementOnNextTouch;
        private MovementState currentMovementState = MovementState.Walking;

        private const float ungroundedDuration = 0.2f;

        public enum MovementState { Freeze, Grappling, Swinging, Walking, Running, Crouching, Jumping, Air }

        // Public properties
        public bool IsGrounded => isGrounded;
        public bool freeze;
        public bool activeGrapple;
        public bool Swinging;
        public Vector3 velocity;
        public float CurrentSpeed => currentMovementSpeed;
        public float groundDrag;
        public float swingSpeed;

        void Start()
        {
            Cursor.lockState = CursorLockMode.Locked;
            originalHeight = controller.height;
            targetHeight = originalHeight;
            defaultYPos = playerCamera.localPosition.y;
            cameraBaseHeight = defaultYPos;

            if (playerCamera.GetComponent<Camera>() != null)
            {
                currentFov = targetFov = normalFov;
                playerCamera.GetComponent<Camera>().fieldOfView = currentFov;
            }

            if (standingHeightMarker != null)
            {
                markerHeightOffset = standingHeightMarker.transform.position.y - transform.position.y;
                markerInitialized = true;
            }

            currentMovementSpeed = speed;
        }

        void Update()
        {
            // Ground check
            bool wasGrounded = isGrounded;
            isGrounded = controller.isGrounded;
            if (isGrounded) lastGroundedTime = Time.time;
            if (!wasGrounded && isGrounded && !activeGrapple)
            {
                currentMovementState = isCrouching ? MovementState.Crouching : MovementState.Walking;
                wasRunningWhenJumped = false;
            }

            // Reset vertical velocity when grounded
            if (isGrounded && velocity.y < 0 && !activeGrapple) velocity.y = -2f;

            // Input and state updates
            HandleCrouching();
            UpdateMovementState();
            HandleCameraControl(); // Always update camera
            HandleCameraTilt();
            HandleFovChange();
            if (enableHeadBob) HandleHeadBob();
        }

        void FixedUpdate()
        {
            // Movement at fixed timestep
            HandleMovement();
            HandleControllerHeightAdjustment();
        }

        private bool IsEffectivelyGrounded()
        {
            return isGrounded || (Time.time - lastGroundedTime <= ungroundedDuration && velocity.y <= 0);
        }

        private void UpdateMovementState()
        {
            if (freeze)
            {
                currentMovementState = MovementState.Freeze;
                currentMovementSpeed = 0f;
                velocity = Vector3.zero;
                return;
            }

            if (activeGrapple)
            {
                currentMovementState = MovementState.Grappling;
                currentMovementSpeed = swingSpeed;
                return;
            }

            if (Swinging)
            {
                currentMovementState = MovementState.Swinging;
                currentMovementSpeed = swingSpeed;
                return;
            }

            if (!IsEffectivelyGrounded())
            {
                currentMovementState = MovementState.Air;
                return;
            }

            if (isCrouching)
            {
                currentMovementState = MovementState.Crouching;
                currentMovementSpeed = speed * 0.5f;
            }
            else
            {
                bool wantsToRun = Keyboard.current.shiftKey.isPressed && Keyboard.current.wKey.isPressed;
                currentMovementState = wantsToRun ? MovementState.Running : MovementState.Walking;
                currentMovementSpeed = wantsToRun ? runSpeed : speed;
            }
        }

        private void HandleMovement()
        {
            if (freeze) return;

            // Grappling
            if (activeGrapple)
            {
                Keyboard kb = Keyboard.current;
                float moveX = 0f, moveZ = 0f;
                if (kb.wKey.isPressed) moveZ += 1f;
                if (kb.aKey.isPressed) moveX -= 1f;
                if (kb.sKey.isPressed) moveZ -= 1f;
                if (kb.dKey.isPressed) moveX += 1f;

                Vector3 move = (transform.right * moveX + transform.forward * moveZ).normalized;
                controller.Move(move * (currentMovementSpeed * grappleAirControl) * Time.fixedDeltaTime);
                controller.Move(grappleVelocity * Time.fixedDeltaTime);
                grappleVelocity.y += gravity * Time.fixedDeltaTime;
                return;
            }

            // Swinging - Rigidbody handles it
            if (Swinging) return;

            // Normal movement
            Keyboard kb2 = Keyboard.current;
            float moveX2 = 0f, moveZ2 = 0f;
            if (kb2.wKey.isPressed) moveZ2 += 1f;
            if (kb2.aKey.isPressed) moveX2 -= 1f;
            if (kb2.sKey.isPressed) moveZ2 -= 1f;
            if (kb2.dKey.isPressed) moveX2 += 1f;

            Vector3 move2 = (transform.right * moveX2 + transform.forward * moveZ2).normalized;

            // Jumping
            if (Keyboard.current.spaceKey.wasPressedThisFrame && IsEffectivelyGrounded() && !isCrouching)
            {
                wasRunningWhenJumped = currentMovementState == MovementState.Running;
                velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
            }

            // Air or ground
            if (!IsEffectivelyGrounded())
            {
                controller.Move(move2 * (currentMovementSpeed * 0.5f) * Time.fixedDeltaTime);
                velocity.x *= 0.99f;
                velocity.z *= 0.99f;
            }
            else
            {
                float horizontalSpeed = Mathf.Sqrt(velocity.x * velocity.x + velocity.z * velocity.z);
                Vector3 horizontalMovement = Vector3.zero;

                if (horizontalSpeed > 0.5f)
                {
                    horizontalMovement += new Vector3(velocity.x, 0, velocity.z);
                    float frictionFactor = Mathf.Pow(0.3f, Time.fixedDeltaTime);
                    velocity.x *= frictionFactor;
                    velocity.z *= frictionFactor;
                }
                else
                {
                    velocity.x = 0;
                    velocity.z = 0;
                }

                if (move2.magnitude > 0.1f)
                {
                    horizontalMovement += move2 * currentMovementSpeed;
                    if (horizontalSpeed > currentMovementSpeed * 0.5f)
                    {
                        velocity.x *= 0.5f;
                        velocity.z *= 0.5f;
                    }
                }

                controller.Move(horizontalMovement * Time.fixedDeltaTime);
            }

            UpdateMarkerPosition();
            velocity.y += gravity * Time.fixedDeltaTime;
            controller.Move(new Vector3(0, velocity.y, 0) * Time.fixedDeltaTime);
        }

        private void HandleCameraControl()
        {
            // Ensure we have valid input
            if (Mouse.current == null)
                return;

            Vector2 mouseDelta = Mouse.current.delta.ReadValue();
            
            // Only apply rotation if there's actual mouse movement
            if (mouseDelta.sqrMagnitude < 0.0001f)
                return;

            // Calculate rotation amounts
            float yawDelta = mouseDelta.x * sensitivity * 0.08f;
            float pitchDelta = mouseDelta.y * sensitivity * 0.08f;

            // Apply yaw to player body (horizontal look)
            transform.Rotate(Vector3.up * yawDelta, Space.Self);

            // Apply pitch to camera (vertical look)
            xRotation -= pitchDelta;
            xRotation = Mathf.Clamp(xRotation, maxLookDownAngle, maxLookUpAngle);

            // Set camera rotation (pitch only, no yaw since player body handles it)
            playerCamera.localRotation = Quaternion.Euler(xRotation, 0f, 0f);
        }

        private void HandleCameraTilt()
        {
            // Don't tilt during certain states
            if (!enableCameraTilt || (!IsEffectivelyGrounded() && !activeGrapple && !Swinging))
            {
                currentTilt = Mathf.Lerp(currentTilt, 0f, tiltSmoothness * Time.deltaTime);
                ApplyTilt();
                return;
            }

            // Get movement input
            Keyboard kb = Keyboard.current;
            if (kb == null)
            {
                currentTilt = Mathf.Lerp(currentTilt, 0f, tiltSmoothness * Time.deltaTime);
                ApplyTilt();
                return;
            }

            float moveX = 0f;
            if (kb.dKey.isPressed) moveX += 1f;
            if (kb.aKey.isPressed) moveX -= 1f;

            // Calculate target tilt
            float targetTilt = 0f;
            if (Mathf.Abs(moveX) > 0.1f)
            {
                targetTilt = -moveX * tiltAmount;
            }

            // Smooth tilt transition
            currentTilt = Mathf.Lerp(currentTilt, targetTilt, tiltSmoothness * Time.deltaTime);
            
            ApplyTilt();
        }

        private void ApplyTilt()
        {
            // Apply tilt as roll to the camera
            // Get current pitch from local rotation
            Quaternion currentRot = playerCamera.localRotation;
            Vector3 eulerAngles = currentRot.eulerAngles;
            
            // Reapply with tilt on Z-axis
            playerCamera.localRotation = Quaternion.Euler(eulerAngles.x, eulerAngles.y, currentTilt);
        }

        private void HandleFovChange()
        {
            if (!enableRunFov || playerCamera.GetComponent<Camera>() == null) return;

            if (activeGrapple)
                targetFov = grappleFov;
            else
                targetFov = (IsEffectivelyGrounded() && currentMovementState == MovementState.Running) ? runFov : normalFov;

            currentFov = Mathf.Lerp(currentFov, targetFov, fovChangeSpeed * Time.deltaTime);
            playerCamera.GetComponent<Camera>().fieldOfView = currentFov;
        }

        private void HandleHeadBob()
        {
            float headBobHeight = cameraBaseHeight * (controller.height / originalHeight);

            if (!IsEffectivelyGrounded() || activeGrapple)
            {
                playerCamera.localPosition = new Vector3(
                    playerCamera.localPosition.x,
                    Mathf.Lerp(playerCamera.localPosition.y, headBobHeight, Time.deltaTime * 12f),
                    playerCamera.localPosition.z
                );
                timer = 0;
                return;
            }

            Keyboard kb = Keyboard.current;
            float moveX = 0f, moveZ = 0f;
            if (kb.dKey.isPressed) moveX += 1f;
            if (kb.aKey.isPressed) moveX -= 1f;
            if (kb.wKey.isPressed) moveZ += 1f;
            if (kb.sKey.isPressed) moveZ -= 1f;

            if (Mathf.Abs(moveX) > 0.15f || Mathf.Abs(moveZ) > 0.15f)
            {
                bool isRunning = Keyboard.current.shiftKey.isPressed && !isCrouching && moveZ > 0.1f;
                float bobSpeed = (isRunning ? runBobSpeed : walkBobSpeed) * (isCrouching ? 0.6f : 1f);
                float bobAmount = (isRunning ? runBobAmount : walkBobAmount) * (isCrouching ? 0.4f : 1f);

                timer += Time.deltaTime * bobSpeed;
                playerCamera.localPosition = new Vector3(
                    playerCamera.localPosition.x,
                    headBobHeight + Mathf.Sin(timer) * bobAmount,
                    playerCamera.localPosition.z
                );
            }
            else
            {
                timer = 0;
                playerCamera.localPosition = new Vector3(
                    playerCamera.localPosition.x,
                    Mathf.Lerp(playerCamera.localPosition.y, headBobHeight, Time.deltaTime * 8f),
                    playerCamera.localPosition.z
                );
            }
        }

        // Grappling methods
        private void SetVelocity()
        {
            enableMovementOnNextTouch = true;
            grappleVelocity = velocityToSet;
            if (playerCamera.GetComponent<Camera>() != null)
                targetFov = grappleFov;
        }

        public void ResetRestrictions()
        {
            activeGrapple = false;
            enableMovementOnNextTouch = false;
            if (playerCamera.GetComponent<Camera>() != null)
                targetFov = normalFov;
        }

        private void OnControllerColliderHit(ControllerColliderHit hit)
        {
            if (enableMovementOnNextTouch)
            {
                enableMovementOnNextTouch = false;
                ResetRestrictions();
                GetComponent<Grappling>()?.StopGrapple();
            }
        }

        public Vector3 CalculateJumpVelocity(Vector3 startPoint, Vector3 endPoint, float trajectoryHeight)
        {
            float gravityForce = Physics.gravity.y;
            float displacementY = endPoint.y - startPoint.y;
            Vector3 displacementXZ = new Vector3(endPoint.x - startPoint.x, 0f, endPoint.z - startPoint.z);

            Vector3 velocityY = Vector3.up * Mathf.Sqrt(-2 * gravityForce * trajectoryHeight);
            Vector3 velocityXZ = displacementXZ / (Mathf.Sqrt(-2 * trajectoryHeight / gravityForce)
                + Mathf.Sqrt(2 * (displacementY - trajectoryHeight) / gravityForce));

            return (velocityXZ + velocityY) * grappleSpeedMultiplier;
        }

        public void JumpToPosition(Vector3 targetPosition, float trajectoryHeight)
        {
            activeGrapple = true;
            velocityToSet = CalculateJumpVelocity(transform.position, targetPosition, trajectoryHeight);
            Invoke(nameof(SetVelocity), 0.02f);
            Invoke(nameof(ResetRestrictions), 3f);
        }

        // Crouch methods
        private void HandleCrouching()
        {
            if (activeGrapple || freeze) return;

            if (Keyboard.current.cKey.wasPressedThisFrame)
            {
                isCrouchKeyHeld = true;
                if (!isCrouching)
                {
                    isCrouching = true;
                    wantsToStand = false;
                    targetHeight = crouchHeight;
                }
            }

            if (Keyboard.current.cKey.wasReleasedThisFrame)
            {
                isCrouchKeyHeld = false;
                if (isCrouching) wantsToStand = true;
            }

            if (wantsToStand && !isCrouchKeyHeld && Time.time - lastStandCheckTime > standCheckCooldown)
            {
                lastStandCheckTime = Time.time;
                if (CanStandUp())
                {
                    isCrouching = false;
                    targetHeight = originalHeight;
                    wantsToStand = false;
                }
            }
        }

        private void HandleControllerHeightAdjustment()
        {
            float prevHeight = controller.height;
            float newHeight = Mathf.SmoothDamp(controller.height, targetHeight, ref currentHeightVelocity, crouchSmoothTime);
            float heightDiff = newHeight - prevHeight;
            controller.height = newHeight;

            if (heightDiff > 0) controller.Move(Vector3.up * heightDiff * 0.5f);

            float camHeight = cameraBaseHeight * (controller.height / originalHeight);
            float newCamHeight = Mathf.SmoothDamp(playerCamera.localPosition.y, camHeight, ref cameraHeightVelocity, crouchSmoothTime);
            playerCamera.localPosition = new Vector3(playerCamera.localPosition.x, newCamHeight, playerCamera.localPosition.z);
        }

        private void UpdateMarkerPosition()
        {
            if (standingHeightMarker != null && markerInitialized)
            {
                standingHeightMarker.transform.position = new Vector3(
                    transform.position.x,
                    transform.position.y + markerHeightOffset,
                    transform.position.z
                );
            }
        }

        private bool CanStandUp()
        {
            if (standingHeightMarker == null || !markerInitialized) return true;

            Collider[] hits = Physics.OverlapSphere(standingHeightMarker.transform.position, standingCheckRadius, obstacleLayerMask);
            foreach (Collider col in hits)
            {
                if (col.transform == transform || col.transform.IsChildOf(transform))
                    continue;

                if (col.bounds.min.y < standingHeightMarker.transform.position.y + minStandingClearance)
                    return false;
            }
            return true;
        }

        /// <summary>
        /// Returns whether the player is currently crouching
        /// </summary>
        /// <returns>True if crouching</returns>
        public bool IsCrouching()
        {
            return isCrouching;
        }
    }
}