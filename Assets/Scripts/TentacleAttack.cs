using UnityEngine;
using System.Collections; // For coroutines

[RequireComponent(typeof(Collider2D), typeof(SpriteRenderer))] // Basic components needed
public class TentacleAttack : MonoBehaviour
{
    private enum TentacleState { WarmingUp, Active, Retracting, Done }

    [Header("Attack Settings")]
    public int damage = 15;         // Damage dealt to the player
    public float knockbackForce = 4f; // Knockback applied to player

    [Header("Animation & Timing")] // Renamed header
    public Sprite[] animationFrames; // Assign the full tentacle animation spritesheet frames here
    public float frameRate = 12f;   // Frames per second
    [Tooltip("Frame index (0-based) when the hitbox activates (e.g., 10 for the 11th frame)")]
    public int hitboxActivationFrame = 10; // 11th frame
    [Tooltip("Frame index (0-based) when the hitbox deactivates (e.g., 11 for the 12th frame, hitbox active for one frame)")]
    public int hitboxDeactivationFrame = 11; // 12th frame
    [Tooltip("How long the hitboxDeactivationFrame stays visible after the hitbox deactivates")]
    public float lingerDuration = 0.5f; // How long the 12th frame stays

    private Collider2D attackCollider;
    private SpriteRenderer spriteRenderer;
    private bool hasHitPlayer = false; // Prevent multiple hits from one tentacle
    private float originalYScale; // Store the initial Y scale

    // State machine variables
    private TentacleState currentState = TentacleState.WarmingUp;
    private int currentFrame = 0;
    private float frameTimer;
    private float lingerTimer;

    void Awake()
    {
        attackCollider = GetComponent<Collider2D>();
        spriteRenderer = GetComponent<SpriteRenderer>();
        originalYScale = transform.localScale.y; // Store original scale

        attackCollider.isTrigger = true; // Ensure it's a trigger
        attackCollider.enabled = false; // Start with hitbox inactive

        if (animationFrames == null || animationFrames.Length == 0)
        {
            Debug.LogError("TentacleAttack: Animation Frames not assigned or empty!", this);
            enabled = false; // Disable script if no animation
            return;
        }

        // Initialize timers and set first frame
        frameTimer = 1f / frameRate;
        spriteRenderer.sprite = animationFrames[0];
        currentFrame = 0;
    }

    // We don't need Start() anymore, Update handles the logic
    // void Start() {}

    void Update()
    {
        if (currentState == TentacleState.Done) return; // Nothing more to do

        // Timer for frame advancement
        frameTimer -= Time.deltaTime;

        bool advanceFrame = false;
        if (frameTimer <= 0f)
        {
            frameTimer += 1f / frameRate; // Add interval, don't just reset to prevent drift
            // Ensure timer doesn't get stuck negative if frameRate is very high or deltaTime is large
            if (frameTimer < 0) frameTimer = 1f / frameRate;
            advanceFrame = true;
        }

        // --- State Machine Logic ---
        switch (currentState)
        {
            case TentacleState.WarmingUp:
                if (advanceFrame)
                {
                    currentFrame++;
                    if (currentFrame >= hitboxActivationFrame)
                    {
                        Debug.Log($"Tentacle Frame {currentFrame}: Activating Hitbox");
                        attackCollider.enabled = true;
                        hasHitPlayer = false;
                        currentState = TentacleState.Active;
                        // --- Set initial active scale --- 
                        transform.localScale = new Vector3(transform.localScale.x, originalYScale * 0.5f, transform.localScale.z);
                        advanceFrame = false; // Consume frame advancement this update as scale was just set
                    }
                    // Check bounds before applying frame
                    if (currentFrame >= animationFrames.Length)
                    {
                         Debug.LogWarning("Hitbox activation frame is out of bounds!");
                         currentState = TentacleState.Done;
                         Destroy(gameObject);
                         return;
                    }
                }
                break;

            case TentacleState.Active:
                 if (advanceFrame)
                 {
                     currentFrame++;

                     // --- Adjust Scale based on frame --- 
                     if (currentFrame == hitboxActivationFrame)
                     {
                          // This case is handled when transitioning from WarmingUp
                          // transform.localScale = new Vector3(transform.localScale.x, originalYScale * 0.5f, transform.localScale.z);
                     }
                     else if (currentFrame > hitboxActivationFrame && currentFrame <= hitboxDeactivationFrame)
                     {
                         // Go to full scale on the next frame(s)
                         transform.localScale = new Vector3(transform.localScale.x, originalYScale, transform.localScale.z);
                     }
                     // --- End Scale Adjustment ---

                     if (currentFrame > hitboxDeactivationFrame)
                     {
                         Debug.Log($"Tentacle Frame {currentFrame}: Deactivating Hitbox");
                         attackCollider.enabled = false;
                         lingerTimer = lingerDuration;
                         currentState = TentacleState.Retracting;
                         // --- Reset scale before linger/retract --- 
                         transform.localScale = new Vector3(transform.localScale.x, originalYScale, transform.localScale.z);
                         currentFrame = hitboxDeactivationFrame;
                         if(currentFrame >= animationFrames.Length) currentFrame = animationFrames.Length -1;
                         advanceFrame = false;
                     }
                     // Check bounds before applying frame
                     if (currentFrame >= animationFrames.Length)
                     {
                          Debug.LogWarning("Hitbox deactivation frame is out of bounds!");
                          currentState = TentacleState.Done;
                          Destroy(gameObject);
                          return;
                     }
                 }
                break;

            case TentacleState.Retracting:
                // Handle linger duration first
                if (lingerTimer > 0)
                {
                    lingerTimer -= Time.deltaTime;
                    advanceFrame = false; // Don't advance frame while lingering
                }
                else if (advanceFrame) // Only advance frame after linger
                {
                     currentFrame++;
                     // Check if animation finished
                     if (currentFrame >= animationFrames.Length)
                     {
                         currentState = TentacleState.Done;
                         Destroy(gameObject);
                         return; // Exit immediately on destroy
                     }
                }
                break;
        }

        // --- Update Sprite --- 
        // Only update sprite if frame advanced and within bounds
        if (advanceFrame && currentFrame < animationFrames.Length)
        {
             spriteRenderer.sprite = animationFrames[currentFrame];
        }
        // Special case for Retracting state to ensure the correct linger frame is displayed
        else if (currentState == TentacleState.Retracting && currentFrame < animationFrames.Length) 
        {
             spriteRenderer.sprite = animationFrames[currentFrame];
        }
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        // Hitbox is enabled/disabled by the state machine now
        // We only need to check if the player was hit *during this activation*
        if (hasHitPlayer) return; // Already hit someone this activation

        if (other.CompareTag("Player"))
        {            
            PlayerStateMachine2D player = other.GetComponent<PlayerStateMachine2D>();
            if (player != null)
            {
                Debug.Log("Tentacle hit Player!");
                hasHitPlayer = true; // Mark as hit for this activation window

                // Calculate knockback direction - ALWAYS push left and slightly up
                Vector2 knockbackDirection = new Vector2(-1f, 0.5f).normalized;

                player.TakeDamage(damage, knockbackDirection * knockbackForce);

                 // Disable collider immediately after successful hit? Optional.
                 // attackCollider.enabled = false; 
            }
        }
    }
} 