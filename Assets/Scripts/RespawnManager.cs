using UnityEngine;
using ElmanGameDevTools.PlayerSystem;

public class RespawnManager : MonoBehaviour
{
    [Header("Respawn Settings")]
    public Transform respawnPoint;
    public float fallThresholdY = -50f; // Y position that triggers respawn

    [Header("References")]
    private PlayerController player;
    private Grappling grappling;
    private Swinging swinging;
    private CharacterController controller;

    [Header("Debug")]
    public bool debugMode = true;

    void Start()
    {
        player = FindObjectOfType<PlayerController>();

        if (player != null)
        {
            controller = player.controller;
            grappling = player.GetComponent<Grappling>();
            swinging = player.GetComponent<Swinging>();
        }
        else
        {
            Debug.LogError("RespawnManager: PlayerController not found!");
        }

        if (respawnPoint == null)
        {
            Debug.LogWarning("RespawnManager: No respawn point set! Creating one at player start position.");
            GameObject spawnObj = new GameObject("RespawnPoint");
            respawnPoint = spawnObj.transform;
            respawnPoint.position = player.transform.position;
        }
    }

    void Update()
    {
        // Check if player fell off map
        if (player != null && player.transform.position.y < fallThresholdY)
        {
            if (debugMode) Debug.Log("Player fell off map - respawning");
            RespawnPlayer();
        }
    }

    public void RespawnPlayer()
    {
        if (player == null || respawnPoint == null) return;

        // Stop any active movement abilities
        if (grappling != null && grappling.IsGrappling())
        {
            grappling.StopGrapple();
        }

        if (swinging != null && swinging.IsSwinging())
        {
            swinging.StopSwing();
        }

        // Reset player state
        player.freeze = false;
        player.activeGrapple = false;
        player.Swinging = false;
        player.velocity = Vector3.zero;
        player.ResetRestrictions();

        // Teleport player
        controller.enabled = false;
        player.transform.position = respawnPoint.position;
        player.transform.rotation = respawnPoint.rotation;
        controller.enabled = true;

        if (debugMode) Debug.Log($"Player respawned at {respawnPoint.position}");
    }

    // Optional: Manual respawn trigger for testing
    void OnGUI()
    {
        if (debugMode)
        {
            if (GUI.Button(new Rect(10, 200, 150, 30), "Respawn (R)"))
            {
                RespawnPlayer();
            }
        }
    }

    private void OnDrawGizmos()
    {
        if (respawnPoint != null)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(respawnPoint.position, 1f);
            Gizmos.DrawLine(respawnPoint.position, respawnPoint.position + respawnPoint.forward * 2f);
        }
    }
}