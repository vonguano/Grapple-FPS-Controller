using UnityEngine;

public class ToiletMechSpawner : MonoBehaviour
{
    [Header("Mech References")]
    public GameObject humanoidMech;
    public GameObject genericMech;
    public Animator humanoidAnimator;
    public Animator genericAnimator;

    [Header("Spawn Settings")]
    public AudioClip spawnSound;
    public AudioSource audioSource; // Made public or you can use GetComponent in Start

    [Header("Animation Triggers")]
    public string spawnTriggerName = "Spawn";

    private bool hasSpawned = false;

    void Start()
    {
        // Get or add AudioSource if not assigned
        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
            if (audioSource == null && spawnSound != null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
            }
        }

        // NOTE: Since we removed the code that forces the mech underground,
        // make sure you place the Mech objects in their "Hidden" (underground) 
        // position manually in the Unity Scene View!
    }

    // Update is removed because we don't need to manually move the object anymore

    void OnTriggerEnter(Collider other)
    {
        if (!hasSpawned && other.CompareTag("Player"))
        {
            SpawnMech();
        }
    }

    void SpawnMech()
    {
        hasSpawned = true;
        Debug.Log("Toilet Mech Spawning!");

        if (audioSource != null && spawnSound != null)
        {
            audioSource.PlayOneShot(spawnSound);
        }

        // Trigger the animation to start rising
        if (humanoidAnimator != null)
        {
            humanoidAnimator.SetTrigger(spawnTriggerName);
        }

        if (genericAnimator != null)
        {
            genericAnimator.SetTrigger(spawnTriggerName);
        }
    }

    // Reset logic is simplified to just resetting the boolean
    // To reset the position, we rely on the Animator resetting state
    public void ResetSpawn()
    {
        hasSpawned = false;

        // Optional: Reset Animator to initial state
        if (humanoidAnimator != null) humanoidAnimator.Rebind();
        if (genericAnimator != null) genericAnimator.Rebind();
    }
}