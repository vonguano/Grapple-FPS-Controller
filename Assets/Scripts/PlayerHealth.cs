using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace ElmanGameDevTools.PlayerSystem
{
    [RequireComponent(typeof(CharacterController))]
    public class PlayerHealth : MonoBehaviour
    {
        [Header("Health Settings")]
        public float maxHealth = 100f;
        public float fallRespawnHeight = -20f;
        public float collectibleRespawnDelay = 8f;

        [Header("UI Settings")]
        public Vector2 healthBarSize = new Vector2(320f, 26f);
        public float healthBarBottomPadding = 30f;
        public float shieldIndicatorOffset = 62f;

        [Header("Collectible Settings")]
        public Vector3 collectibleOffset = new Vector3(0f, 1f, 4f);
        public Vector3 collectibleScale = Vector3.one * 0.6f;

        private float currentHealth;
        private bool hasShield;
        private Vector3 respawnPoint;
        private Vector3 collectibleSpawnPoint;
        private Image healthFillImage;
        private GameObject shieldIndicator;
        private Text shieldLabel;
        private ShieldCollectible activeCollectible;
        private CharacterController characterController;
        private PlayerController playerController;

        private void Awake()
        {
            characterController = GetComponent<CharacterController>();
            playerController = GetComponent<PlayerController>();
            respawnPoint = transform.position;
            collectibleSpawnPoint = respawnPoint + transform.TransformVector(collectibleOffset);
            currentHealth = maxHealth;
        }

        private void Start()
        {
            BuildHud();
            UpdateHealthUi();
            UpdateShieldUi();
            SpawnCollectible();
        }

        private void Update()
        {
            if (transform.position.y < fallRespawnHeight)
            {
                Respawn();
            }
        }

        public void TakeDamage(float amount)
        {
            if (amount <= 0f) return;

            if (hasShield)
            {
                hasShield = false;
                UpdateShieldUi();
                return;
            }

            currentHealth = Mathf.Max(0f, currentHealth - amount);
            UpdateHealthUi();

            if (currentHealth <= 0f)
            {
                Respawn();
            }
        }

        public void Heal(float amount)
        {
            if (amount <= 0f) return;

            currentHealth = Mathf.Clamp(currentHealth + amount, 0f, maxHealth);
            UpdateHealthUi();
        }

        public void GrantShield()
        {
            hasShield = true;
            UpdateShieldUi();
        }

        internal void NotifyCollectibleConsumed(ShieldCollectible collectible)
        {
            if (collectible == activeCollectible)
            {
                activeCollectible = null;
            }

            StartCoroutine(RespawnCollectibleAfterDelay());
        }

        private void Respawn()
        {
            currentHealth = maxHealth;
            hasShield = false;
            UpdateHealthUi();
            UpdateShieldUi();

            if (characterController != null)
            {
                characterController.enabled = false;
                transform.position = respawnPoint;
                characterController.enabled = true;
            }
            else
            {
                transform.position = respawnPoint;
            }

            if (playerController != null)
            {
                playerController.velocity = Vector3.zero;
            }

            if (activeCollectible == null)
            {
                SpawnCollectible();
            }
        }

        private IEnumerator RespawnCollectibleAfterDelay()
        {
            yield return new WaitForSeconds(collectibleRespawnDelay);
            SpawnCollectible();
        }

        private void BuildHud()
        {
            var canvasGo = new GameObject("PlayerHUD", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 1f;

            DontDestroyOnLoad(canvasGo);

            var healthContainer = new GameObject("HealthBar", typeof(RectTransform));
            healthContainer.transform.SetParent(canvas.transform, false);
            var healthRect = healthContainer.GetComponent<RectTransform>();
            healthRect.sizeDelta = healthBarSize;
            healthRect.anchorMin = new Vector2(0.5f, 0f);
            healthRect.anchorMax = new Vector2(0.5f, 0f);
            healthRect.pivot = new Vector2(0.5f, 0.5f);
            healthRect.anchoredPosition = new Vector2(0f, healthBarBottomPadding);

            var background = new GameObject("Background", typeof(Image));
            background.transform.SetParent(healthContainer.transform, false);
            var bgImage = background.GetComponent<Image>();
            bgImage.color = new Color(0.05f, 0.05f, 0.05f, 0.85f);
            var bgRect = background.GetComponent<RectTransform>();
            bgRect.anchorMin = Vector2.zero;
            bgRect.anchorMax = Vector2.one;
            bgRect.offsetMin = Vector2.zero;
            bgRect.offsetMax = Vector2.zero;

            var fill = new GameObject("Fill", typeof(Image));
            fill.transform.SetParent(healthContainer.transform, false);
            healthFillImage = fill.GetComponent<Image>();
            healthFillImage.color = new Color(0.9f, 0.2f, 0.2f, 0.95f);
            healthFillImage.type = Image.Type.Filled;
            healthFillImage.fillMethod = Image.FillMethod.Horizontal;
            healthFillImage.fillOrigin = (int)Image.OriginHorizontal.Left;
            var fillRect = fill.GetComponent<RectTransform>();
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = Vector2.one;
            fillRect.offsetMin = new Vector2(4f, 4f);
            fillRect.offsetMax = new Vector2(-4f, -4f);

            var shieldRoot = new GameObject("ShieldIndicator", typeof(Image));
            shieldRoot.transform.SetParent(canvas.transform, false);
            var shieldRect = shieldRoot.GetComponent<RectTransform>();
            shieldRect.sizeDelta = new Vector2(200f, 34f);
            shieldRect.anchorMin = new Vector2(0.5f, 0f);
            shieldRect.anchorMax = new Vector2(0.5f, 0f);
            shieldRect.pivot = new Vector2(0.5f, 0.5f);
            shieldRect.anchoredPosition = new Vector2(0f, shieldIndicatorOffset);

            var shieldBg = shieldRoot.GetComponent<Image>();
            shieldBg.color = new Color(0.15f, 0.3f, 0.75f, 0.9f);

            var textObj = new GameObject("Label", typeof(Text));
            textObj.transform.SetParent(shieldRoot.transform, false);
            shieldLabel = textObj.GetComponent<Text>();
            shieldLabel.alignment = TextAnchor.MiddleCenter;
            shieldLabel.color = Color.white;
            shieldLabel.fontSize = 18;
            shieldLabel.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            var textRect = textObj.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(6f, 4f);
            textRect.offsetMax = new Vector2(-6f, -4f);

            shieldIndicator = shieldRoot;
        }

        private void SpawnCollectible()
        {
            if (activeCollectible != null) return;

            var collectible = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            collectible.name = "Shield Collectible";
            collectible.transform.localScale = collectibleScale;
            collectible.transform.position = collectibleSpawnPoint;

            var capsuleCollider = collectible.GetComponent<Collider>();
            if (capsuleCollider != null) capsuleCollider.isTrigger = true;

            var renderer = collectible.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.material.color = new Color(0.25f, 0.6f, 1f, 1f);
            }

            activeCollectible = collectible.AddComponent<ShieldCollectible>();
            activeCollectible.Initialize(this);
        }

        private void UpdateHealthUi()
        {
            if (healthFillImage != null)
            {
                healthFillImage.fillAmount = Mathf.InverseLerp(0f, maxHealth, currentHealth);
            }
        }

        private void UpdateShieldUi()
        {
            if (shieldIndicator != null)
            {
                shieldIndicator.SetActive(hasShield);
            }

            if (shieldLabel != null)
            {
                shieldLabel.text = hasShield ? "Shield Ready" : string.Empty;
            }
        }
    }
}
