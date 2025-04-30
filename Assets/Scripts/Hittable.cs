using UnityEngine;
using UnityEngine.Events; // For UnityEvents if desired

public class Hittable : MonoBehaviour
{
    [Header("Pogo Interaction")]
    [Tooltip("The upward speed the player gains when hitting this object with a downward attack.")]
    public float pogoStrength = 12f; // Default pogo strength, configurable per object

    [Header("Health (Optional)")]
    [Tooltip("Set to 0 or less if this object should not be destroyable by health.")]
    public int maxHealth = 0;
    private int currentHealth;

    [Header("Events (Optional)")]
    public UnityEvent OnHit;       // Event triggered when hit
    public UnityEvent OnDeath;     // Event triggered when health reaches zero

    void Start()
    {
        if (maxHealth > 0) {
            currentHealth = maxHealth;
        }
    }

    // Called by PlayerHurtbox when this object is hit
    public void TakeHit(int damage)
    {
        // Trigger the OnHit event (e.g., for sound effects, particle effects)
        OnHit?.Invoke();

        // Only process health if the object has health configured
        if (maxHealth > 0)
        {
            currentHealth -= damage;
            Debug.Log($"{gameObject.name} took {damage} damage. Health: {currentHealth}/{maxHealth}");

            if (currentHealth <= 0)
            {
                Die();
            }
        }
        else
        {
            // Object doesn't have health, but might still react to hits
            // Debug.Log($"{gameObject.name} was hit!");
        }
    }

    private void Die()
    {
        // Debug.Log($"{gameObject.name} has triggered OnDeath."); // Modified log
        // Trigger the OnDeath event (e.g., for spawning loot, special effects, notifying game manager)
        OnDeath?.Invoke();

        // Default death behaviour removed: destroy the GameObject
        // The object subscribing to OnDeath (like DroneEnemy) will now handle destruction/disabling/respawn.
        // Destroy(gameObject);
    }

    // Public method to allow external scripts (like DroneEnemy) to reset health
    public void ResetHealth() {
        if (maxHealth > 0) {
            currentHealth = maxHealth;
            // Debug.Log($"{gameObject.name} health reset to {currentHealth}");
        }
    }
} 