using ElmanGameDevTools.PlayerSystem;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class Grappling : MonoBehaviour
{
    [Header("References")]
    private PlayerController pm;
    public Transform cam;
    public Transform gunTip;
    public LayerMask grappleableLayer;
    public LineRenderer lr;

    [Header("Grappling")]
    public float maxGrappleDistance = 50f;
    public float grappleDelayTime = 0.15f;
    public float overshootYAxis = 3f;
    private Vector3 grapplePoint;

    [Header("Cooldown")]
    public float grappleCooldown = 1f;
    private float grappleCooldownTimer;

    [Header("Visual Effects")]
    public float lineDrawSpeed = 20f;
    public Color grappleLineColor = Color.cyan;
    [Range(0.01f, 0.5f)]
    public float lineWidth = 0.1f;

    [Header("Input")]
    private PlayerInput playerInput;
    private InputAction grappleAction;

    [Header("Debug")]
    public bool debugMode = true;

    private bool isGrappling;
    private float lineDrawProgress;
    private bool isDrawingLine;

    private void Start()
    {
        pm = GetComponent<PlayerController>();
        playerInput = GetComponent<PlayerInput>();

        // Setup line renderer
        if (lr != null)
        {
            lr.enabled = false;
            lr.startColor = grappleLineColor;
            lr.endColor = grappleLineColor;
            lr.startWidth = lineWidth;
            lr.endWidth = lineWidth;
        }

        if (playerInput != null)
        {
            grappleAction = playerInput.actions.FindAction("Grapple");
            if (grappleAction == null)
            {
                Debug.LogError("Grapple action not found in Input Action Asset. Make sure you have a 'Grapple' action defined.");
                Debug.LogError("Available actions: " + string.Join(", ", GetAvailableActions()));
            }
            else
            {
                grappleAction.performed += ctx => StartGrapple();
                if (debugMode) Debug.Log("Grapple action successfully bound!");
            }
        }
        else
        {
            Debug.LogError("PlayerInput component not found on the GameObject. Add a PlayerInput component or use legacy input.");
        }
    }

    private void OnEnable()
    {
        grappleAction?.Enable();
    }

    private void OnDisable()
    {
        grappleAction?.Disable();
    }

    private void Update()
    {
        // Handle cooldown
        if (grappleCooldownTimer > 0)
            grappleCooldownTimer -= Time.deltaTime;

        // Debug input - TEMPORARY: Remove this once input is working
        if (debugMode && Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
        {
            Debug.Log("Left mouse clicked - but grapple action not triggered. Check your Input Action Asset bindings.");
        }

        // Fallback for testing - use 'G' key if Input System isn't working
        if (debugMode && Keyboard.current != null && Keyboard.current.gKey.wasPressedThisFrame)
        {
            Debug.Log("Manual grapple trigger (G key)");
            StartGrapple();
        }

        // Animate line drawing
        if (isDrawingLine)
        {
            lineDrawProgress += Time.deltaTime * lineDrawSpeed;
            if (lineDrawProgress >= 1f)
            {
                lineDrawProgress = 1f;
                isDrawingLine = false;
            }
        }
    }

    private void LateUpdate()
    {
        // Keep the line renderer updated if grappling
        if (isGrappling && lr != null && lr.enabled)
        {
            // Always update start position to follow gun
            lr.SetPosition(0, gunTip.position);

            // Animate the line extending to target
            if (isDrawingLine)
            {
                Vector3 currentEndPoint = Vector3.Lerp(gunTip.position, grapplePoint, lineDrawProgress);
                lr.SetPosition(1, currentEndPoint);
            }
            else
            {
                lr.SetPosition(1, grapplePoint);
            }
        }
    }

    private void StartGrapple()
    {
        if (grappleCooldownTimer > 0)
        {
            if (debugMode) Debug.Log($"Grapple on cooldown: {grappleCooldownTimer:F2}s remaining");
            return;
        }

        RaycastHit hit;
        if (Physics.Raycast(cam.position, cam.forward, out hit, maxGrappleDistance, grappleableLayer))
        {
            grapplePoint = hit.point;
            isGrappling = true;
            pm.freeze = true;

            // Setup animated line renderer
            if (lr != null)
            {
                lr.enabled = true;
                lr.SetPosition(0, gunTip.position);
                lr.SetPosition(1, gunTip.position); // Start at gun tip
                lineDrawProgress = 0f;
                isDrawingLine = true;
            }

            if (debugMode) Debug.Log($"Grapple started to point: {hit.point} on {hit.collider.name}");

            Invoke(nameof(ExecuteGrapple), grappleDelayTime);
        }
        else
        {
            // No valid grapple target
            grappleCooldownTimer = grappleCooldown * 0.5f;
            if (debugMode) Debug.Log("No grapple target found within range");
        }
    }

    private void ExecuteGrapple()
    {
        pm.freeze = false;

        // Calculate the highest point on the arc
        float grapplePointRelativeYPos = grapplePoint.y - transform.position.y;
        float highestPointOnArc;

        if (grapplePointRelativeYPos < 0)
        {
            // Grappling downward - arc should be at current height + overshoot
            highestPointOnArc = transform.position.y + overshootYAxis;
        }
        else
        {
            // Grappling upward - arc should be at grapple point + overshoot
            highestPointOnArc = grapplePoint.y + overshootYAxis;
        }

        pm.JumpToPosition(grapplePoint, highestPointOnArc);

        if (debugMode) Debug.Log($"Executing grapple with arc height: {highestPointOnArc}");

        Invoke(nameof(StopGrapple), 1f);
    }

    public void StopGrapple()
    {
        pm.freeze = false;
        isGrappling = false;
        isDrawingLine = false;
        grappleCooldownTimer = grappleCooldown;

        if (lr != null)
            lr.enabled = false;

        if (debugMode) Debug.Log("Grapple stopped");
    }

    public bool IsGrappling()
    {
        return isGrappling;
    }

    public Vector3 GetGrapplePoint()
    {
        return grapplePoint;
    }

    // Helper to debug available actions
    private string[] GetAvailableActions()
    {
        if (playerInput == null || playerInput.actions == null) return new string[0];

        var actions = new System.Collections.Generic.List<string>();
        foreach (var action in playerInput.actions)
        {
            actions.Add(action.name);
        }
        return actions.ToArray();
    }

    // Visualize grapple range in editor
    private void OnDrawGizmosSelected()
    {
        if (cam != null)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawRay(cam.position, cam.forward * maxGrappleDistance);
            Gizmos.DrawWireSphere(cam.position + cam.forward * maxGrappleDistance, 0.5f);
        }

        if (isGrappling)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawSphere(grapplePoint, 0.3f);
            Gizmos.DrawLine(transform.position, grapplePoint);
        }
    }
}