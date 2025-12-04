using ElmanGameDevTools.PlayerSystem;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class Swinging : MonoBehaviour
{
    [Header("References")]
    public LineRenderer lr;
    public Transform gunTip, cam;
    public LayerMask whatIsGrappleable;
    public PlayerController pm;
    public Rigidbody rb;
    public Transform orientation;


    [Header("Swinging")]
    public float maxSwingDistance = 25f;
    private Vector3 swingPoint;
    private SpringJoint joint;

    [Header("ODM Gear Movement")]
    public float horizontalThrustForce = 12f;
    public float forwardThrustForce = 18f;
    public float extendCableSpeed = 0.5f;
    public float maxSwingSpeed = 25f;

    [Header("Prediction")]
    public RaycastHit predictionHit;
    public float predictionSphereCastRadius = 2f;
    public Transform predictionPoint;

    [Header("Visual Effects")]
    public Color swingLineColor = Color.yellow;
    [Range(0.01f, 0.5f)]
    public float lineWidth = 0.1f;

    [Header("Input")]
    private PlayerInput playerInput;
    private InputAction swingAction;

    [Header("Debug")]
    public bool debugMode = true;
    public bool showDebugGUI = true;

    private Vector3 currentGrapplePosition;
    private bool isSwinging;

    private IEnumerator VerifySpringJointSettings()
    {
        yield return null; // Wait one frame

        if (joint != null)
        {
            Debug.Log($"[VERIFY] SpringJoint spring is NOW: {joint.spring} (should be 50)");
            Debug.Log($"[VERIFY] SpringJoint damper is NOW: {joint.damper} (should be 15)");

            if (joint.spring < 40f)
            {
                Debug.LogError("WARNING: Spring value too low! Attempting to force set...");
                joint.spring = 50f;
            }
        }
    }

    private void Start()
    {
        pm = GetComponent<PlayerController>();
        rb = GetComponent<Rigidbody>();
        playerInput = GetComponent<PlayerInput>();

        // CRITICAL: Verify Rigidbody exists
        if (rb == null)
        {
            Debug.LogError("SWINGING: No Rigidbody found! Please add a Rigidbody component to Player_Object.");
            return;
        }

        // Ensure Rigidbody starts as kinematic
        rb.isKinematic = true;
        rb.useGravity = false; // Controller handles gravity when not swinging

        if (debugMode) Debug.Log($"Swinging: Rigidbody found. Initial state: isKinematic={rb.isKinematic}");

        // Setup line renderer
        if (lr != null)
        {
            lr.enabled = false;
            lr.startColor = swingLineColor;
            lr.endColor = swingLineColor;
            lr.startWidth = lineWidth;
            lr.endWidth = lineWidth;
            lr.positionCount = 0;
        }

        // Setup input actions
        if (playerInput != null)
        {
            swingAction = playerInput.actions.FindAction("Swing");
            if (swingAction == null)
            {
                swingAction = playerInput.actions.FindAction("Fire");
            }

            if (swingAction == null)
            {
                Debug.LogError("Swing/Fire action not found in Input Action Asset.");
            }
            else
            {
                swingAction.started += ctx => StartSwing();
                swingAction.canceled += ctx => StopSwing();
                if (debugMode) Debug.Log("Swing action successfully bound!");
            }
        }
    }

    private void OnEnable()
    {
        swingAction?.Enable();
    }

    private void OnDisable()
    {
        swingAction?.Disable();
    }

    private void Update()
    {
        // Fallback for testing - use right mouse button
        if (debugMode && Mouse.current != null)
        {
            if (Mouse.current.rightButton.wasPressedThisFrame && !isSwinging)
            {
                Debug.Log("Manual swing trigger (Right Mouse)");
                StartSwing();
            }
            if (Mouse.current.rightButton.wasReleasedThisFrame && isSwinging)
            {
                Debug.Log("Manual swing release (Right Mouse)");
                StopSwing();
            }
        }

        // DIAGNOSTIC: Press T to test if Rigidbody can move at all
        if (debugMode && isSwinging && Keyboard.current != null && Keyboard.current.tKey.wasPressedThisFrame)
        {
            Debug.Log("TEST: Applying massive upward force!");
            rb.AddForce(Vector3.up * 1000f, ForceMode.Impulse);
        }

        CheckForSwingPoints();


    }

    private void FixedUpdate()
    {
        // Use FixedUpdate for physics-based movement
        if (joint != null && isSwinging)
        {
            OdmGearMovement();

            // CONTINUOUS VELOCITY MONITORING
            if (debugMode && Time.frameCount % 30 == 0) // Every 30 frames
            {
                Debug.Log($"[SWING] Current velocity: {rb.linearVelocity}, magnitude: {rb.linearVelocity.magnitude:F2}");
                Debug.Log($"[SWING] Distance from anchor: {Vector3.Distance(transform.position, swingPoint):F2}");
            }
        }
    }

    private void LateUpdate()
    {
        DrawRope();
    }

    private void CheckForSwingPoints()
    {
        if (joint != null) return;

        RaycastHit sphereCastHit;
        Physics.SphereCast(cam.position, predictionSphereCastRadius, cam.forward,
                            out sphereCastHit, maxSwingDistance, whatIsGrappleable);

        RaycastHit raycastHit;
        Physics.Raycast(cam.position, cam.forward,
                            out raycastHit, maxSwingDistance, whatIsGrappleable);

        Vector3 realHitPoint;

        if (raycastHit.point != Vector3.zero)
            realHitPoint = raycastHit.point;
        else if (sphereCastHit.point != Vector3.zero)
            realHitPoint = sphereCastHit.point;
        else
            realHitPoint = Vector3.zero;

        if (realHitPoint != Vector3.zero)
        {
            if (predictionPoint != null)
            {
                predictionPoint.gameObject.SetActive(true);
                predictionPoint.position = realHitPoint;
            }
        }
        else
        {
            if (predictionPoint != null)
            {
                predictionPoint.gameObject.SetActive(false);
            }
        }

        predictionHit = raycastHit.point == Vector3.zero ? sphereCastHit : raycastHit;
    }

    private void StartSwing()
    {
        if (predictionHit.point == Vector3.zero)
        {
            if (debugMode) Debug.Log("No swing target found within range");
            return;
        }

        // Stop any active grapple
        Grappling grappleScript = GetComponent<Grappling>();
        if (grappleScript != null && grappleScript.IsGrappling())
        {
            grappleScript.StopGrapple();
        }

        pm.ResetRestrictions();
        pm.Swinging = true;
        isSwinging = true;
        swingPoint = predictionHit.point;

        if (debugMode)
        {
            Debug.Log($"=== STARTING SWING ===");
            Debug.Log($"Target: {swingPoint}");
            Debug.Log($"Distance: {Vector3.Distance(transform.position, swingPoint):F2}");
        }

        // STEP 1: Disable Character Controller
        if (pm.controller != null)
        {
            pm.controller.enabled = false;
            if (debugMode) Debug.Log("Character Controller disabled");
        }

        // STEP 2: Configure Rigidbody
        if (rb != null)
        {
            // Enable physics - EXPLICITLY SET EVERYTHING
            rb.isKinematic = false;
            rb.useGravity = true;  // CRITICAL: Must be true!

            // Verify gravity is actually enabled
            if (!rb.useGravity)
            {
                Debug.LogError("CRITICAL: useGravity failed to enable! Forcing...");
                rb.useGravity = true;
            }

            // Transfer velocity from Character Controller
            rb.linearVelocity = pm.velocity;
            if (debugMode) Debug.Log($"Initial velocity set to: {rb.linearVelocity}");

            // CRITICAL: Unlock ALL position constraints
            rb.constraints = RigidbodyConstraints.None;

            // Then freeze rotation only - BUT NOT the player body itself
            // The player body rotation should be driven by camera yaw, not physics
            rb.freezeRotation = true;
            rb.angularVelocity = Vector3.zero;

            // Physics settings
            rb.linearDamping = 0.1f;
            rb.angularDamping = 0.05f;
            rb.mass = 70f;
            rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
            rb.interpolation = RigidbodyInterpolation.Interpolate;

            if (debugMode)
            {
                Debug.Log($"Rigidbody configured: isKinematic={rb.isKinematic}, useGravity={rb.useGravity}, mass={rb.mass}");
                Debug.Log($"Rigidbody constraints: {rb.constraints}");
                Debug.Log($"Rigidbody drag: {rb.linearDamping}, angularDrag: {rb.angularDamping}");
            }
        }
        else
        {
            Debug.LogError("SWINGING: Rigidbody is NULL! Cannot swing.");
            StopSwing();
            return;
        }

        // STEP 3: Create SpringJoint
        joint = gameObject.AddComponent<SpringJoint>();
        joint.autoConfigureConnectedAnchor = false;
        joint.connectedAnchor = swingPoint;

        float distanceFromPoint = Vector3.Distance(transform.position, swingPoint);

        // Configure spring
        joint.maxDistance = distanceFromPoint * 0.8f;
        joint.minDistance = distanceFromPoint * 0.25f;
        joint.spring = 10f;
        joint.damper = 3f;
        joint.massScale = 5f;
        joint.tolerance = 0.025f;
        joint.enableCollision = false;
        joint.enablePreprocessing = true;

        if (debugMode)
        {
            Debug.Log($"SpringJoint created: spring={joint.spring}, damper={joint.damper}");
            Debug.Log($"Joint distances: max={joint.maxDistance:F2}, min={joint.minDistance:F2}");
            Debug.Log($"Joint anchor: {joint.connectedAnchor}");
        }

        // STEP 4: Setup line renderer
        if (lr != null)
        {
            lr.positionCount = 2;
            lr.enabled = true;
            currentGrapplePosition = gunTip.position;
        }

        if (debugMode) Debug.Log("=== SWING STARTED SUCCESSFULLY ===");
    }

    public void StopSwing()
    {
        if (!isSwinging) return;

        if (debugMode) Debug.Log("=== STOPPING SWING ===");

        pm.Swinging = false;
        isSwinging = false;

        if (lr != null)
        {
            lr.positionCount = 0;
            lr.enabled = false;
        }

        // Capture velocity BEFORE destroying joint
        Vector3 finalVelocity = Vector3.zero;
        if (rb != null)
        {
            finalVelocity = rb.linearVelocity;

            // REDUCE velocity to prevent ice sliding
            // Keep horizontal momentum but reduce it
            finalVelocity.x *= 0.6f; // 60% horizontal momentum
            finalVelocity.z *= 0.6f;
            finalVelocity.y *= 0.8f; // 80% vertical momentum

            if (debugMode) Debug.Log($"Captured velocity: {rb.linearVelocity} -> Reduced: {finalVelocity}");

            // Reset rigidbody
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.isKinematic = true;
            rb.useGravity = false;
        }

        // Destroy joint
        if (joint != null)
        {
            Destroy(joint);
            joint = null;
            if (debugMode) Debug.Log("SpringJoint destroyed");
        }

        // Re-enable Character Controller
        if (pm.controller != null)
        {
            pm.controller.enabled = true;
            if (debugMode) Debug.Log("Character Controller re-enabled");
        }

        // Transfer REDUCED velocity
        pm.velocity = finalVelocity;
        if (debugMode) Debug.Log($"Velocity transferred to PlayerController: {finalVelocity}");

        if (debugMode) Debug.Log("=== SWING STOPPED ===");
    }

    private void OdmGearMovement()
    {
        if (rb == null || orientation == null || joint == null) return;

        // Get input - using direct keyboard access since it's more reliable for debugging
        Keyboard kb = Keyboard.current;
        if (kb == null) return;

        Vector2 moveInput = Vector2.zero;
        if (kb.wKey.isPressed) moveInput.y = 1f;
        if (kb.sKey.isPressed) moveInput.y = -1f;
        if (kb.aKey.isPressed) moveInput.x = -1f;
        if (kb.dKey.isPressed) moveInput.x = 1f;
        bool pullToward = kb.spaceKey.isPressed;

        // Speed limiting
        float currentSpeed = rb.linearVelocity.magnitude;
        bool canAddForce = currentSpeed < maxSwingSpeed;

        if (debugMode && moveInput.magnitude > 0.1f)
        {
            Debug.Log($"ODM Input: {moveInput}, Speed: {currentSpeed:F2}, CanAddForce: {canAddForce}");
        }

        // === DIRECTIONAL CONTROL ===

        // Left/Right strafe
        if (Mathf.Abs(moveInput.x) > 0.1f && canAddForce)
        {
            Vector3 strafeDirection = orientation.right * moveInput.x;
            rb.AddForce(strafeDirection * horizontalThrustForce, ForceMode.Acceleration);

            if (debugMode)
                Debug.Log($"Applying strafe force: {strafeDirection * horizontalThrustForce}");
        }

        // Forward movement
        if (moveInput.y > 0.1f && canAddForce)
        {
            Vector3 forwardDirection = orientation.forward;
            rb.AddForce(forwardDirection * horizontalThrustForce, ForceMode.Acceleration);

            if (debugMode)
                Debug.Log($"Applying forward force: {forwardDirection * horizontalThrustForce}");
        }

        // === ROPE CONTROL ===

        float distanceFromPoint = Vector3.Distance(transform.position, swingPoint);

        // Pull toward (Space)
        if (pullToward)
        {
            Vector3 directionToPoint = (swingPoint - transform.position).normalized;
            rb.AddForce(directionToPoint * forwardThrustForce, ForceMode.Acceleration);

            joint.maxDistance = Mathf.Max(distanceFromPoint * 0.8f, 2f);
            joint.minDistance = Mathf.Max(distanceFromPoint * 0.25f, 0.5f);

            if (debugMode)
                Debug.Log($"Pulling toward anchor with force: {forwardThrustForce}");
        }
        // Push away (S)
        else if (moveInput.y < -0.1f)
        {
            float extendedDistance = distanceFromPoint + extendCableSpeed;
            joint.maxDistance = extendedDistance * 0.8f;
            joint.minDistance = extendedDistance * 0.25f;
        }
        // Natural tension
        else
        {
            joint.maxDistance = distanceFromPoint * 0.8f;
            joint.minDistance = distanceFromPoint * 0.25f;
        }

        // === SWING ASSIST ===
        if (moveInput.magnitude > 0.1f && canAddForce)
        {
            Vector3 ropeDirection = (swingPoint - transform.position).normalized;
            Vector3 rightOfRope = Vector3.Cross(ropeDirection, Vector3.up).normalized;
            Vector3 forwardOfRope = Vector3.Cross(rightOfRope, ropeDirection).normalized;

            Vector3 assistDirection = (rightOfRope * moveInput.x + forwardOfRope * moveInput.y).normalized;
            rb.AddForce(assistDirection * (horizontalThrustForce * 0.5f), ForceMode.Acceleration);
        }
    }

    private void DrawRope()
    {
        if (!joint || lr == null) return;

        currentGrapplePosition = Vector3.Lerp(currentGrapplePosition, swingPoint, Time.deltaTime * 8f);

        lr.SetPosition(0, gunTip.position);
        lr.SetPosition(1, currentGrapplePosition);
    }

    public bool IsSwinging()
    {
        return isSwinging;
    }

    // Debug GUI
    private void OnGUI()
    {
        if (!showDebugGUI || !isSwinging) return;

        int y = 10;
        int lineHeight = 25;

        GUI.Label(new Rect(10, y, 500, 20), $"=== SWING DEBUG ===");
        y += lineHeight;

        GUI.Label(new Rect(10, y, 500, 20), $"Swinging: {isSwinging}");
        y += lineHeight;

        GUI.Label(new Rect(10, y, 500, 20), $"Joint Exists: {joint != null}");
        y += lineHeight;

        if (rb != null)
        {
            GUI.Label(new Rect(10, y, 500, 20), $"RB isKinematic: {rb.isKinematic}");
            y += lineHeight;

            GUI.Label(new Rect(10, y, 500, 20), $"RB useGravity: {rb.useGravity}");
            y += lineHeight;

            GUI.Label(new Rect(10, y, 500, 20), $"RB Velocity: {rb.linearVelocity} (mag: {rb.linearVelocity.magnitude:F2})");
            y += lineHeight;

            GUI.Label(new Rect(10, y, 500, 20), $"RB Mass: {rb.mass}");
            y += lineHeight;
        }

        if (pm != null && pm.controller != null)
        {
            GUI.Label(new Rect(10, y, 500, 20), $"Controller Enabled: {pm.controller.enabled}");
            y += lineHeight;
        }

        if (joint != null)
        {
            GUI.Label(new Rect(10, y, 500, 20), $"Spring: {joint.spring}, Damper: {joint.damper}");
            y += lineHeight;

            float dist = Vector3.Distance(transform.position, swingPoint);
            GUI.Label(new Rect(10, y, 500, 20), $"Distance to anchor: {dist:F2}");
            y += lineHeight;

            GUI.Label(new Rect(10, y, 500, 20), $"Joint Max/Min: {joint.maxDistance:F2} / {joint.minDistance:F2}");
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (cam != null)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawRay(cam.position, cam.forward * maxSwingDistance);
            Gizmos.DrawWireSphere(cam.position + cam.forward * maxSwingDistance, predictionSphereCastRadius);
        }

        if (isSwinging && joint != null)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawSphere(swingPoint, 0.5f);
            Gizmos.DrawLine(transform.position, swingPoint);

            // Show velocity
            if (rb != null)
            {
                Gizmos.color = Color.red;
                Gizmos.DrawRay(transform.position, rb.linearVelocity.normalized * 3f);
            }
        }
    }
}