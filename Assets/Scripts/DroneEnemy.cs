using UnityEngine;
using System.Collections;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
[RequireComponent(typeof(Hittable))]
[RequireComponent(typeof(AudioSource))] // Added AudioSource (Corrected syntax)
public class DroneEnemy : MonoBehaviour
{
    [Header("Movement")]
    public float moveSpeed = 3f;
    public float patrolDistance = 5f; // How far to move left/right from start

    [Header("Combat")]
    public int damageOnCollision = 10; // Damage dealt to player on touch
    public float respawnDelay = 5f; // Time in seconds before respawning

    [Header("Animation")]
    public SpriteRenderer spriteRenderer; // Assign the drone's SpriteRenderer
    public Sprite[] idleFrames; // Assign the idle/moving frames (e.g., drone-1)
    public Sprite[] turnFrames; // Assign drone-1, drone-2, drone-3, drone-4
    public Sprite[] explosionFrames; // Assign the explosion sprites
    // public float frameRate = 10f; // Frames per second for standard animations (handled per-animation now)
    public float turnAnimationDuration = 0.4f; // Duration matches 4 frames at 10fps
    public float explosionDuration = 0.6f; // Duration for the explosion animation

    [Header("Audio")]
    public AudioClip hoverSound;
    [Range(0f, 1f)] public float hoverVolume = 0.5f;
    public AudioClip explosionSound;
    [Range(0f, 1f)] public float explosionVolume = 0.8f;

    // Component References
    private Rigidbody2D rb;
    private Hittable hittable;
    private Collider2D mainCollider; // Cache the main collider
    private AudioSource audioSource;
    // Add if using SpriteAnimator: private SpriteAnimator spriteAnimator;
    // Add if using Unity Animator: private Animator animator;

    // State
    private Vector2 startPosition;
    private Vector2 targetPosition;
    private bool movingRight = true;
    private bool isTurning = false;
    private Coroutine activeDeathSequence = null; // Track if death sequence is running
    private bool isTemporarilyDead = false; // Use this instead of isDead for respawn logic

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        hittable = GetComponent<Hittable>();
        mainCollider = GetComponent<Collider2D>();
        audioSource = GetComponent<AudioSource>();

        if (spriteRenderer == null)
        {
            spriteRenderer = GetComponentInChildren<SpriteRenderer>();
            if (spriteRenderer == null) Debug.LogError("DroneEnemy needs a SpriteRenderer assigned or as a child.", this);
        }

        startPosition = transform.position;
        // CalculateInitialTarget(); // Moved to Start after potentially enabling audio
        rb.gravityScale = 0;
        rb.freezeRotation = true;

        if (hittable != null)
        {
            hittable.OnDeath.AddListener(HandleDeath);
        }
         else {
            Debug.LogError("DroneEnemy is missing the Hittable component!", this);
        }

        if (mainCollider == null) {
             Debug.LogError("DroneEnemy is missing a Collider2D component!", this);
        }
        if (audioSource == null) {
             Debug.LogError("DroneEnemy is missing an AudioSource component!", this);
        }
        else {
            // Initial audio setup
            audioSource.playOnAwake = false; // We control playback manually
            audioSource.loop = true;
            audioSource.clip = hoverSound; // Pre-assign hover sound
        }
    }

    void Start()
    {
        // Initial state setup - ensures clean state on first start or after potential scene reload
        ResetAndActivate();
    }

    // Centralized function to reset state and activate the drone
    void ResetAndActivate() {
        // Debug.Log($"Drone {gameObject.name}: ResetAndActivate called."); // DEBUG
        transform.position = startPosition; // Ensure position is correct
        isTemporarilyDead = false; // Mark as active
        isTurning = false;          // Ensure not stuck in turning state
        activeDeathSequence = null; // Clear any potential lingering death coroutine reference
        ResetPhysicsAndCollisions(); // Enable physics, collider, hittable, reset health
        CalculateInitialTarget();    // Determine first move target
        InitializeSprite();         // Set initial sprite and facing direction
        StartHoverSound();          // Start the hover sound
    }


    void ResetPhysicsAndCollisions() {
        rb.isKinematic = false;
        rb.velocity = Vector2.zero; // Explicitly zero velocity
        if(mainCollider != null) mainCollider.enabled = true;
        if(hittable != null) {
            hittable.enabled = true;
            hittable.ResetHealth();
        }
        // Debug.Log($"Drone {gameObject.name}: Physics and Collisions Reset."); // DEBUG
    }

    void CalculateInitialTarget() {
         // Always start moving right from the start position after respawn/init
         movingRight = true;
         targetPosition = startPosition + Vector2.right * patrolDistance;
         // Debug.Log($"Drone {gameObject.name}: New Target Calculated: {targetPosition} | Moving Right: {movingRight}"); // DEBUG
    }

    void InitializeSprite() {
        if (spriteRenderer != null) {
             spriteRenderer.enabled = true; // Ensure visible
             if (idleFrames != null && idleFrames.Length > 0) {
                 spriteRenderer.sprite = idleFrames[0];
             }
        }
        UpdateFacingDirection();
        // Debug.Log($"Drone {gameObject.name}: Sprite Initialized. Facing Right: {!spriteRenderer.flipX}"); // DEBUG
    }


    void FixedUpdate()
    {
        if (isTurning || isTemporarilyDead)
        {
            // Debug.Log($"Drone {gameObject.name}: FixedUpdate skipped (Turning: {isTurning}, TempDead: {isTemporarilyDead})"); // DEBUG
            // Keep velocity zeroed if stopped
            if (!rb.isKinematic && rb.velocity != Vector2.zero) rb.velocity = Vector2.zero;
            return;
        }

        // Debug.Log($"Drone {gameObject.name}: FixedUpdate proceeding to Move (Velocity: {rb.velocity}, Target: {targetPosition}, Pos: {transform.position})"); // DEBUG
        Move();
    }

    void Move()
    {
        float step = moveSpeed * Time.fixedDeltaTime;
        Vector2 currentPosition = transform.position;
        float distanceToTarget = Vector2.Distance(currentPosition, targetPosition);

        // If very close to the target, initiate the turn
        // Increased threshold slightly to prevent potential floating point issues
        if (distanceToTarget < 0.15f) {
            // Debug.Log($"Drone {gameObject.name}: Reached target {targetPosition}, distance {distanceToTarget}. Starting turn."); // DEBUG
            rb.velocity = Vector2.zero; // Stop precisely
            transform.position = targetPosition; // Snap to ensure alignment
            StartTurnSequence();
            return;
        }

        // Move towards target
        Vector2 directionToTarget = (targetPosition - currentPosition).normalized;
        rb.velocity = directionToTarget * moveSpeed;
    }

    void StartTurnSequence()
    {
        if (isTurning || isTemporarilyDead) return;

        // Debug.Log($"Drone {gameObject.name}: Starting Turn Sequence. Current Target: {targetPosition}"); // DEBUG
        isTurning = true;
        movingRight = !movingRight; // Decide NEW direction first
        // Then calculate the next target based on the new direction
        targetPosition = movingRight ? startPosition + Vector2.right * patrolDistance : startPosition - Vector2.right * patrolDistance;
        // Debug.Log($"Drone {gameObject.name}: New Target set for after turn: {targetPosition}"); // DEBUG
        StartCoroutine(TurnAnimation());
    }

    IEnumerator TurnAnimation()
    {
        rb.velocity = Vector2.zero; // Ensure stopped during turn animation

        if (turnFrames != null && turnFrames.Length > 0 && spriteRenderer != null)
        {
            float timePerFrame = turnAnimationDuration / turnFrames.Length;
            for (int i = 0; i < turnFrames.Length; i++)
            {
                spriteRenderer.sprite = turnFrames[i];
                yield return new WaitForSeconds(timePerFrame);
            }
        }
        else
        {
            yield return new WaitForSeconds(turnAnimationDuration); // Fallback wait
        }

        // After turn animation, set back to idle frame
        if (idleFrames != null && idleFrames.Length > 0 && spriteRenderer != null)
        {
             spriteRenderer.sprite = idleFrames[0];
        }
        // Ensure facing direction matches the *new* direction of travel
        UpdateFacingDirection();
        isTurning = false; // Allow movement again
        // Debug.Log($"Drone {gameObject.name}: Turn Sequence Complete. Ready to move towards {targetPosition}"); // DEBUG
    }

    void UpdateFacingDirection()
    {
        if (spriteRenderer != null)
        {
            // If base sprite faces LEFT: Flip means facing right = movingRight is true.
            // If base sprite faces RIGHT: Flip means facing left = movingRight is false (!movingRight is true).
            // Change this line based on your base sprite's orientation.
            spriteRenderer.flipX = movingRight; // Assumes base sprite faces LEFT
            // spriteRenderer.flipX = !movingRight; // Assumes base sprite faces RIGHT (Previous default)
        }
    }

    void HandleDeath()
    {
        // Only start if not already dead or in the death sequence
        if (activeDeathSequence == null && !isTemporarilyDead)
        {
             // Debug.Log($"Drone {gameObject.name}: Death handled, starting explosion sequence."); // DEBUG
             activeDeathSequence = StartCoroutine(ExplosionSequence());
        }
        else {
            // Debug.Log($"Drone {gameObject.name}: Death handled, but already dead/dying (TempDead: {isTemporarilyDead}, ActiveSeq: {activeDeathSequence != null}). Ignoring."); // DEBUG
        }
    }

    IEnumerator ExplosionSequence()
    {
        isTemporarilyDead = true; // Mark as inactive FIRST
        StopHoverSound();
        PlayExplosionSound();

        // Halt physics and disable interactions
        rb.velocity = Vector2.zero;
        rb.isKinematic = true;
        if(mainCollider != null) mainCollider.enabled = false;
        if(hittable != null) hittable.enabled = false;

        // Explosion Animation
        if (explosionFrames != null && explosionFrames.Length > 0 && spriteRenderer != null)
        {
            float timePerFrame = explosionDuration / explosionFrames.Length;
             spriteRenderer.flipX = false; // Ensure explosion isn't flipped
            for (int i = 0; i < explosionFrames.Length; i++)
            {
                spriteRenderer.sprite = explosionFrames[i];
                yield return new WaitForSeconds(timePerFrame);
            }
        }
        else
        { 
            Debug.LogWarning("No explosion frames assigned, waiting duration.", this);
             yield return new WaitForSeconds(explosionDuration);
        }

        // Hide sprite after explosion is done
        if(spriteRenderer != null) spriteRenderer.enabled = false;

        // Wait for respawn delay
        yield return new WaitForSeconds(respawnDelay);

        // --- Respawn --- //
        // Debug.Log($"Drone {gameObject.name}: Respawning..."); // DEBUG
        ResetAndActivate(); // Use the centralized reset function
        // Debug.Log($"Drone {gameObject.name}: Respawn complete via ResetAndActivate."); // DEBUG
    }

    void StartHoverSound() {
         if (audioSource != null && hoverSound != null && !audioSource.isPlaying) {
            audioSource.clip = hoverSound;
            audioSource.volume = hoverVolume;
            audioSource.loop = true; // Ensure loop is set
            audioSource.Play();
            // Debug.Log($"Drone {gameObject.name}: Started Hover Sound (Volume: {hoverVolume})"); // DEBUG
         }
    }

    void StopHoverSound() {
         if (audioSource != null && audioSource.isPlaying) {
            audioSource.Stop();
            // Debug.Log($"Drone {gameObject.name}: Stopped Hover Sound"); // DEBUG
         }
    }

    void PlayExplosionSound() {
         if (audioSource != null && explosionSound != null) {
             // Use PlayOneShot for non-looping sounds that might overlap or play rapidly
            audioSource.PlayOneShot(explosionSound, explosionVolume);
            // Debug.Log($"Drone {gameObject.name}: Played Explosion Sound (Volume: {explosionVolume})"); // DEBUG
         }
    }

    void OnCollisionEnter2D(Collision2D collision)
    {
        // Only damage player if active
        if (isTemporarilyDead) return;

        if (collision.gameObject.CompareTag("Player"))
        {
            PlayerStateMachine2D player = collision.gameObject.GetComponent<PlayerStateMachine2D>();
            if (player != null)
            {
                player.TakeDamage(damageOnCollision);
            }
        }
    }

    // Optional: Disable sound if the object is disabled unexpectedly
    void OnDisable() {
        // Only stop sound if the component is actually being disabled, 
        // not just during the managed temporary death state.
        if (isTemporarilyDead && activeDeathSequence != null) {
             // Likely being disabled as part of the death sequence, sound is already handled.
        } else {
             // Disabled externally, ensure sound stops.
             StopHoverSound();
        }
    }

    // Optional: Re-enable sound if the object is re-enabled externally
     void OnEnable() {
         // Only play sound if the drone is not marked as dead (might be enabled during respawn sequence otherwise)
         // Also check if Awake has run (audioSource would be assigned)
         if (audioSource != null && !isTemporarilyDead && activeDeathSequence == null) {
             StartHoverSound();
         }
     }


    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        // Use startPosition consistently for gizmo anchor point
        Vector3 gizmoStart = Application.isPlaying ? (Vector3)startPosition : transform.position;
        Vector3 leftEnd = gizmoStart + Vector3.left * patrolDistance;
        Vector3 rightEnd = gizmoStart + Vector3.right * patrolDistance;
        Gizmos.DrawLine(leftEnd + Vector3.up * 0.2f, rightEnd + Vector3.up * 0.2f);
        Gizmos.DrawWireSphere(leftEnd, 0.2f);
        Gizmos.DrawWireSphere(rightEnd, 0.2f);

        // Draw target only if playing and active
        if (Application.isPlaying && !isTemporarilyDead) {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(targetPosition, 0.3f);
        }
    }
} 