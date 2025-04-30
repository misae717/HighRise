using UnityEngine;
using System.Collections.Generic; // Needed for List
using System.Collections; // Needed for Coroutine

[RequireComponent(typeof(Collider2D))]
public class PlayerHurtbox : MonoBehaviour
{
    private PlayerStateMachine2D playerOwner;
    private int damageAmount;
    private Vector2 attackDirection;
    // Size is now primarily for visualizer/initial setup, collider might not resize perfectly mid-frame
    private Vector2 intendedHurtboxSize;

    private Collider2D hurtboxCollider;
    private float activeLifetime;
    private List<Collider2D> alreadyHitObjects;
    private Coroutine activeCoroutine;

    public void Initialize(PlayerStateMachine2D owner, int damage, float lifetime, Vector2 direction, Vector2 size)
    {
        playerOwner = owner;
        damageAmount = damage;
        attackDirection = direction;
        intendedHurtboxSize = size; // Store intended size
        activeLifetime = lifetime;

        alreadyHitObjects = new List<Collider2D>(); // Initialize hit list

        hurtboxCollider = GetComponent<Collider2D>();
        if (hurtboxCollider == null) {
            Debug.LogError("Hurtbox prefab is missing a Collider2D component!");
            Destroy(gameObject);
            return;
        }

        hurtboxCollider.isTrigger = true;
        hurtboxCollider.enabled = false; // Start disabled, activate below

        // Attempt to dynamically resize the collider (best effort)
        ResizeCollider();

        // Activate the hurtbox and start deactivation timer
        Activate();
    }

    void Activate() {
        // Debug.Log("Hurtbox Activated");
        hurtboxCollider.enabled = true;
        alreadyHitObjects.Clear(); // Clear hit list on activation

        // Stop previous deactivation if any
        if (activeCoroutine != null) StopCoroutine(activeCoroutine);
        // Start new deactivation coroutine
        activeCoroutine = StartCoroutine(DeactivateAfterTime(activeLifetime));
    }

    IEnumerator DeactivateAfterTime(float delay)
    {
        yield return new WaitForSeconds(delay);
        // Debug.Log("Hurtbox Deactivating");
        hurtboxCollider.enabled = false;
        // Optional: Disable GameObject slightly later or let it be destroyed by another script
         Destroy(gameObject, 0.1f); // Destroy shortly after deactivation
        activeCoroutine = null;
    }

    void ResizeCollider() {
         if (hurtboxCollider is BoxCollider2D boxCollider) {
            boxCollider.size = intendedHurtboxSize;
        } else if (hurtboxCollider is CircleCollider2D circleCollider) {
             circleCollider.radius = Mathf.Max(intendedHurtboxSize.x, intendedHurtboxSize.y) / 2f;
        }
        // Add cases for other collider types if needed
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        // Hurtbox must be enabled to register hits
        if (!hurtboxCollider.enabled) return;

        // Ignore hitting the player owner
        if (playerOwner != null && other.gameObject == playerOwner.gameObject) {
            return;
        }

        // Check if this object has already been hit by this activation
        if (alreadyHitObjects.Contains(other)) {
            return;
        }

        // Try to get the Hittable component
        Hittable hittableObject = other.GetComponent<Hittable>();
        if (hittableObject != null) {
            // Debug.Log($"[PlayerHurtbox] Hitting Hittable: {other.gameObject.name}");
            hittableObject.TakeHit(damageAmount);
            alreadyHitObjects.Add(other); // Add to hit list

            // --- Report the successful hit back to the Player State Machine --- 
            if (playerOwner != null) {
                playerOwner.ReportHit(attackDirection); // Notify state machine of the hit
            }
            // --- End Reporting Hit ---

            // Check for pogo condition
            if (playerOwner != null && attackDirection == Vector2.down) {
                playerOwner.ReportDownwardHit(hittableObject.pogoStrength);
            }
        }
    }

    // Optional: Consider OnTriggerStay2D if fast-moving objects might pass through in one frame
    // void OnTriggerStay2D(Collider2D other) {
    //      if (!hurtboxCollider.enabled || (playerOwner != null && other.gameObject == playerOwner.gameObject) || alreadyHitObjects.Contains(other)) return;
    //      Hittable hittableObject = other.GetComponent<Hittable>();
    //      if (hittableObject != null) {
    //          Debug.Log($"[PlayerHurtbox - Stay] Hitting Hittable: {other.gameObject.name}");
    //          hittableObject.TakeHit(damageAmount);
    //          alreadyHitObjects.Add(other);
    //          if (playerOwner != null && attackDirection == Vector2.down) {
    //              playerOwner.ReportDownwardHit(hittableObject.pogoStrength);
    //          }
    //      }
    // }

} 