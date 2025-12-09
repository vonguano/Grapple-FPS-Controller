using UnityEngine;

namespace ElmanGameDevTools.PlayerSystem
{
    public class ShieldCollectible : MonoBehaviour
    {
        public float rotationSpeed = 90f;
        public float bobHeight = 0.2f;
        public float bobSpeed = 2f;

        private Vector3 startPosition;
        private PlayerHealth owner;

        public void Initialize(PlayerHealth healthOwner)
        {
            owner = healthOwner;
            startPosition = transform.position;
        }

        private void Awake()
        {
            startPosition = transform.position;
            var collider = GetComponent<Collider>();
            if (collider != null)
            {
                collider.isTrigger = true;
            }
        }

        private void Update()
        {
            transform.Rotate(Vector3.up, rotationSpeed * Time.deltaTime, Space.World);
            var offset = Mathf.Sin(Time.time * bobSpeed) * bobHeight;
            transform.position = startPosition + Vector3.up * offset;
        }

        private void OnTriggerEnter(Collider other)
        {
            var playerHealth = other.GetComponent<PlayerHealth>();
            if (playerHealth == null) return;

            playerHealth.GrantShield();

            if (owner != null)
            {
                owner.NotifyCollectibleConsumed(this);
            }

            Destroy(gameObject);
        }
    }
}
