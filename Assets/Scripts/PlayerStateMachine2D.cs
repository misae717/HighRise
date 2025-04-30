using UnityEngine;
using UnityEngine.SceneManagement; // Added for scene reloading
using System.Collections; // Added for coroutines

[RequireComponent(typeof(Rigidbody2D), typeof(BoxCollider2D), typeof(AudioSource))]
public class PlayerStateMachine2D : MonoBehaviour {
    public enum State { Idle, Running, Jumping, Falling, Dashing, Death }
    [Header("State")]
    public State currentState = State.Falling;
    private State previousState;

    [Header("Health")]
    public int maxHealth = 100;
    public int CurrentHealth { get; private set; }

    [Header("Movement")]
    public float maxRunSpeed = 12f;
    public float acceleration = 120f;
    public float deceleration = 100f;

    [Header("Jump")]
    public float jumpSpeed = 16f;
    public float gravity = 48f;
    public float maxFallSpeed = 32f;
    public float coyoteTime = 0.12f;
    public float jumpBufferTime = 0.12f;
    public float variableJumpTime = 0.18f;
    public float pogoJumpSpeed = 12f;

    [Header("Dash")]
    public float dashSpeed = 20f;
    public float dashDuration = 0.15f;
    public int maxDashes = 1;
    [SerializeField] private float dashAnimDuration = 0.15f;

    [Header("Attack (Hurtbox & Cooldown)")]
    public GameObject hurtboxPrefab;
    public float attackCooldown = 0.4f;
    public int attackDamage = 10;
    public float attackHurtboxDuration = 0.2f;
    public Vector2 forwardAttackOffset = new Vector2(0.8f, 0f);
    public Vector2 upAttackOffset = new Vector2(0f, 1.0f);
    public Vector2 downAttackOffset = new Vector2(0f, -0.8f);
    public Vector2 forwardAttackSize = new Vector2(1.0f, 0.8f);
    public Vector2 upAttackSize = new Vector2(0.8f, 1.0f);
    public Vector2 downAttackSize = new Vector2(0.8f, 1.0f);
    public float attackInputThreshold = 0.5f;

    [Header("Attack (Visual)")]
    public SlashEffectAnimator slashEffectAnimator;

    [Header("Audio Clips")]
    public AudioClip attackMissSound;
    public AudioClip[] forwardUpAttackHitSounds; // Array for random hits
    public AudioClip downAttackHitSound;
    public AudioClip downAttackPogoSound;
    [Header("Audio Clips - Offsets")]
    [Tooltip("Start offset for the Attack Miss sound.")]
    public float missSoundOffset = 0f;
    [Tooltip("Start offset for Forward/Up Attack Hit sounds.")]
    public float fwdUpHitSoundOffset = 0f;
    [Tooltip("Start offset for the Down Attack Hit sound.")]
    public float downHitSoundOffset = 0f;
    [Tooltip("Start offset for the Down Attack Pogo sound.")]
    public float pogoSoundOffset = 0f;
    [Tooltip("Start offset for Jump sounds.")]
    public float jumpSoundOffset = 0f;
    [Header("Audio Clips - Jump")]
    public AudioClip[] jumpSounds; // Array for random jump sounds
    // Add clips for jump, land, dash, hurt, die etc. later if needed

    [Header("Damage & Invulnerability")]
    public float invulnerabilityDuration = 1.0f;
    public float knockbackForce = 5f;
    public float spriteFlashInterval = 0.1f;

    [Header("Death Reset")]
    public float deathAnimationDuration = 1.0f; // Duration of your death animation
    public float deathGracePeriod = 0.5f; // Extra time before reloading
    public float fallDeathYThreshold = -20f; // Y position below which the player dies

    [Header("Ground Check")]
    public Transform groundCheck;
    public Vector2 groundCheckSize = new Vector2(0.9f, 0.1f);
    public LayerMask groundLayer;

    // Component References (cached in Awake)
    private Rigidbody2D rb;
    private BoxCollider2D boxCollider;
    private Animator animator;
    private SpriteAnimator spriteAnimator;
    private AudioSource audioSource;
    private SpriteRenderer spriteRenderer;

    // Internal State
    private Vector2 velocity;
    private float horizontalInput;
    private float verticalInput;
    private bool jumpHeld;
    private Vector2 attackDirection;
    private bool canAttack = true;
    private bool isInvulnerable = false;
    private bool attackHitReportedThisAttack = false;

    // Timers & counters
    private float coyoteTimer;
    private float jumpBufferTimer;
    private float variableJumpTimer;
    private float attackCooldownTimer;
    private int dashCount;
    private float dashTimer;
    private float invulnerabilityTimer;

    // Animation parameter hashes (for efficiency, if using Unity Animator)
    private readonly int stateHash = Animator.StringToHash("State");

    // Input state variables (set in Update, used in FixedUpdate)
    private bool jumpInputDown;
    private bool dashInputDown;
    private bool attackInputDown;

    void Awake() {
        rb = GetComponent<Rigidbody2D>();
        boxCollider = GetComponent<BoxCollider2D>();
        animator = GetComponent<Animator>();
        spriteAnimator = GetComponent<SpriteAnimator>();
        audioSource = GetComponent<AudioSource>();
        spriteRenderer = GetComponent<SpriteRenderer>();

        if (slashEffectAnimator == null) {
            slashEffectAnimator = GetComponentInChildren<SlashEffectAnimator>();
            if(slashEffectAnimator == null) Debug.LogWarning("SlashEffectAnimator not found or assigned!");
        }

        rb.gravityScale = 0f;
        CurrentHealth = maxHealth;
        dashCount = maxDashes;
        if (dashAnimDuration <= 0) dashAnimDuration = dashDuration;
    }

    // Update is now primarily for Input and non-physics updates
    void Update() {
        // --- Input Polling ---
        // Store input state for FixedUpdate to use
        HandleInput();

        // --- Non-Physics Timers & State ---
        attackCooldownTimer -= Time.deltaTime;
        if (attackCooldownTimer <= 0f) {
            canAttack = true;
        }

        // Invulnerability Timer
        if (isInvulnerable) {
            invulnerabilityTimer -= Time.deltaTime;
            if (invulnerabilityTimer <= 0f) {
                isInvulnerable = false;
                // Ensure sprite is visible at the end
                if (spriteRenderer != null) spriteRenderer.enabled = true;
                // Stop the flashing explicitly if the coroutine is still running (optional safety)
                StopCoroutine("FlashSprite"); // Use string name to stop
            }
        }

        // Death check (can remain here or move to FixedUpdate)
        if (currentState == State.Death) {
           // DeathState() logic might involve physics, consider moving parts
            return;
        }

        // --- Animation & Visual Updates ---
        // Update base player animation ONLY if the state has changed
        // State changes now happen in FixedUpdate
        if (previousState != currentState) {
            UpdateAnimationSystems();
            previousState = currentState; // Update previousState *after* using it
        }
        FlipSprite(); // Flipping based on input/facing direction
    }

    // FixedUpdate handles Physics and State Logic
    void FixedUpdate() {
        // --- State Checks & Early Exits ---
        if (currentState == State.Death) {
            DeathState(); // Contains velocity changes, belongs here
            return;
        }
        // --- Physics Timers ---
        // Update timers that influence physics state transitions
        UpdatePhysicsTimers();

        // --- Uninterruptible States ---
        // Handle states that bypass normal logic first
        if (currentState == State.Dashing) {
            DashingState(); // Contains timer & state transition logic
            ApplyVelocity(); // Apply dash velocity
            return; // Skip standard state logic
        }
        // Attack is no longer a state, but action logic runs below

        // --- Global Action Checks & Execution ---
        // Check and execute actions based on input flags set in Update
        // Order matters: Attack might be allowed during jump/fall, Dash might override jump start
        bool attackedThisFrame = false;
        if (ShouldStartAttack()) {
            StartAttack(); // Does not change state, but sets cooldown
            attackedThisFrame = true;
            // Don't return, allow movement state logic to continue
        }
        if (ShouldStartDash()) {
            StartDash(); // Sets state to Dashing
            dashInputDown = false; // Consume the input flag immediately
            ApplyVelocity(); // Apply immediate dash velocity
            return; // Dash overrides everything else this frame
        }

        // --- Fall Death Check ---
        if (transform.position.y < fallDeathYThreshold && currentState != State.Death)
        {
            Die();
            return; // Skip the rest of FixedUpdate since the player is dead
        }

        // --- Standard State Logic ---
        // Run the state machine for movement states (Idle, Run, Jump, Fall, WallHold)
        RunStateMachine();

        // --- Physics Application ---
        ApplyVelocity();

        // Reset single-frame input flags after they've been processed
        jumpInputDown = false;
        dashInputDown = false;
        attackInputDown = false;
    }

    // Updated HandleInput to set flags
    void HandleInput() {
        horizontalInput = Input.GetAxisRaw("Horizontal");
        verticalInput = Input.GetAxisRaw("Vertical");
        jumpHeld = Input.GetButton("Jump");

        // Set flags for button presses this frame
        if (Input.GetButtonDown("Jump")) jumpInputDown = true;
        if (Input.GetButtonDown("Fire1")) dashInputDown = true;
        if (Input.GetButtonDown("Fire2")) attackInputDown = true;
    }

    // ShouldStart methods now use input flags
    bool ShouldStartDash() {
        return dashInputDown && dashCount > 0;
    }

    bool ShouldStartAttack() {
        return attackInputDown && canAttack;
    }

    // Renamed from UpdateTimers, uses fixedDeltaTime
    void UpdatePhysicsTimers() {
        float dt = Time.fixedDeltaTime;

        // Grounded check related timers
        if (IsGrounded()) {
            coyoteTimer = coyoteTime;
            // Reset dashes only if landing, not during the dash itself - MOVED TO STATE TRANSITIONS
            // if (currentState != State.Dashing) dashCount = maxDashes;
        } else {
            coyoteTimer -= dt;
        }

        // Jump buffer timer
        if (jumpInputDown) { // Check the flag set in Update
            jumpBufferTimer = jumpBufferTime;
        } else {
            jumpBufferTimer -= dt;
        }

        // Variable jump timer (decrements while jumping)
        if (currentState == State.Jumping) {
            variableJumpTimer -= dt;
        }

         // Dash Timer (decrements while dashing - handled in DashingState now)
        // if (currentState == State.Dashing) {
        //     dashTimer -= dt;
        // }
    }

    // ApplyVelocity uses fixedDeltaTime
    void ApplyVelocity() {
        if (currentState != State.Dashing && currentState != State.Death) {
            // Apply gravity
            velocity.y -= gravity * Time.fixedDeltaTime;
            // Clamp fall speed
            velocity.y = Mathf.Max(velocity.y, -maxFallSpeed);
        }
        // Apply calculated velocity to Rigidbody
        rb.velocity = velocity;
    }

    // DashingState uses fixedDeltaTime
    void DashingState() {
        dashTimer -= Time.fixedDeltaTime;
        // Velocity is set in StartDash and remains constant
        // Gravity is not applied (handled in ApplyVelocity check)
        // Revert: Dash ends when timer is up, regardless of ground state
        if (dashTimer <= 0f) {
            // Restore collisions before changing state
            int playerLayer = gameObject.layer;
            for (int i = 0; i < 32; i++) {
                // If layer 'i' is NOT in the groundLayer mask
                if ((groundLayer.value & (1 << i)) == 0) {
                    Physics2D.IgnoreLayerCollision(playerLayer, i, false);
                }
            }

            // Reset invulnerability *if* it wasn't caused by taking damage recently
            // The Update loop handles timer-based invulnerability expiry, so we only
            // need to potentially turn it off if the dash was the *only* source.
            // However, simply letting the Update loop manage isInvulnerable based on
            // invulnerabilityTimer is cleaner and covers all cases.
            // isInvulnerable = false; // Removed for simplicity, handled by timer/TakeDamage logic

            currentState = IsGrounded() ? State.Idle : State.Falling;
            if (currentState == State.Falling) velocity.y = 0f; // Reset vertical velocity if ending mid-air
            velocity.x *= 0.5f; // Keep some horizontal momentum
        }
        // If timer is up but not grounded, effectively keep dashing until landing
        // (velocity remains unchanged, gravity is not applied via ApplyVelocity check) - REMOVED THIS LOGIC
    }

     // HandleHorizontalMovement uses fixedDeltaTime
     void HandleHorizontalMovement(float accel, float decel) {
        if (currentState == State.Death) return;
        float dt = Time.fixedDeltaTime;
        float targetSpeed = horizontalInput * maxRunSpeed;
        float accelerationToUse = (Mathf.Abs(horizontalInput) > 0.01f) ? accel : decel;
        if (Mathf.Abs(horizontalInput) > 0.01f && Mathf.Sign(horizontalInput) != Mathf.Sign(velocity.x) && velocity.x != 0) {
            accelerationToUse *= 2f;
        }
        velocity.x = Mathf.MoveTowards(velocity.x, targetSpeed, accelerationToUse * dt);
    }

    // HandleVariableJump uses fixedDeltaTime
    void HandleVariableJump() {
        // Cut jump short if button released or timer ends
        // variableJumpTimer is now decremented in UpdatePhysicsTimers
        if ((!jumpHeld || variableJumpTimer <= 0f) && velocity.y > 0) {
             velocity.y *= 0.5f;
        }
    }

    // HandleJumpInput uses jumpInputDown flag
    bool HandleJumpInput() {
        // Use buffer timer (decremented in FixedUpdate) and coyote timer
        if (jumpBufferTimer > 0f && coyoteTimer > 0f) {
            // Consume jump buffer immediately
            jumpBufferTimer = 0f;
            DoJump(); // Sets state, resets coyote, applies velocity
            return true;
        }
        return false;
    }

    // DeathState uses fixedDeltaTime
    void DeathState() {
        float dt = Time.fixedDeltaTime;
        velocity.y -= gravity * dt;
        velocity.y = Mathf.Max(velocity.y, -maxFallSpeed);
        velocity.x = Mathf.MoveTowards(velocity.x, 0, deceleration * dt);
        // Setting velocity directly in ApplyVelocity now, no need to set rb.velocity here
    }

    // DoJump initializes variableJumpTimer
    void DoJump() {
        currentState = State.Jumping;
        coyoteTimer = 0f; // Consume coyote time
        velocity.y = jumpSpeed;
        variableJumpTimer = variableJumpTime; // Initialize variable jump timer
        // Buffer is consumed in HandleJumpInput
        // UpdateAnimationSystems(); // Called automatically due to state change before next Update

        // Play a random jump sound
        if (jumpSounds != null && jumpSounds.Length > 0) {
            AudioClip clipToPlay = jumpSounds[Random.Range(0, jumpSounds.Length)];
            PlaySound(clipToPlay, jumpSoundOffset); // Pass jump offset
        }
    }

    void RunStateMachine() {
        switch (currentState) {
            case State.Idle:
            case State.Running:
                GroundedState();
                break;
            case State.Jumping:
                JumpingState();
                break;
            case State.Falling:
                FallingState();
                break;
        }
    }

    void UpdateAnimationSystems() {
        Debug.Log($"UpdateAnimationSystems called. Prev: {previousState}, Current: {currentState}");
        if (animator != null) {
            animator.SetInteger(stateHash, (int)currentState);
        }
        if (spriteAnimator != null) {
            spriteAnimator.ChangeAnimation(currentState);
        }
    }

    void GroundedState() {
        if (HandleJumpInput()) return;
        HandleHorizontalMovement(acceleration, deceleration);
        velocity.y = 0f;

        if (!IsGrounded()) {
            currentState = State.Falling;
            coyoteTimer = 0f;
            return;
        }
        currentState = Mathf.Abs(horizontalInput) > 0.01f ? State.Running : State.Idle;
    }

    void JumpingState() {
        HandleVariableJump();
        HandleHorizontalMovement(acceleration * 0.7f, deceleration * 0.7f);

        // Check for landing BEFORE checking for falling transition
        if (IsGrounded()) {
            dashCount = maxDashes; // Reset dashes on landing
            currentState = Mathf.Abs(velocity.x) > 0.01f ? State.Running : State.Idle;
            return;
        }

        if (velocity.y <= 0f) {
            currentState = State.Falling;
            return;
        }
    }

    void FallingState() {
        HandleHorizontalMovement(acceleration * 0.7f, deceleration * 0.7f);

        if (IsGrounded()) {
            dashCount = maxDashes; // Reset dashes on landing
            currentState = Mathf.Abs(velocity.x) > 0.01f ? State.Running : State.Idle;
            return;
        }
    }

    void StartDash() {
        currentState = State.Dashing;
        dashTimer = dashAnimDuration;
        dashCount--;
        isInvulnerable = true; // Become invulnerable during dash

        // Disable collisions with non-ground layers
        int playerLayer = gameObject.layer;
        for (int i = 0; i < 32; i++) {
            // If layer 'i' is NOT in the groundLayer mask
            if ((groundLayer.value & (1 << i)) == 0) {
                Physics2D.IgnoreLayerCollision(playerLayer, i, true);
            }
        }

        float dashDirectionX = (Mathf.Abs(horizontalInput) > 0.1f) ? Mathf.Sign(horizontalInput) : transform.localScale.x;
        Vector2 dir = new Vector2(dashDirectionX, 0).normalized;

        velocity = dir * dashSpeed;
        velocity.y = 0;

        UpdateAnimationSystems();
    }

    void StartAttack() {
        canAttack = false;
        attackCooldownTimer = attackCooldown;
        attackHitReportedThisAttack = false; // Reset hit flag for this attack

        DetermineAttackDirection();
        SpawnHurtbox();

        if (slashEffectAnimator != null) {
            slashEffectAnimator.PlaySlash(attackDirection);
        }

        // Start coroutine to check for a miss after the hurtbox expires
        StartCoroutine(CheckMissSound(attackDirection, attackHurtboxDuration));
    }

    IEnumerator CheckMissSound(Vector2 directionOfAttack, float delay) {
        yield return new WaitForSeconds(delay);

        // If no hit was reported by the time the hurtbox should have expired
        if (!attackHitReportedThisAttack) {
            // Only play miss sound for forward/up/down, not other directions if added later
            if (directionOfAttack == Vector2.up || directionOfAttack == Vector2.down || directionOfAttack == Vector2.right || directionOfAttack == Vector2.left) {
                 PlaySound(attackMissSound, missSoundOffset);
                 Debug.Log("Attack Missed - Playing Miss Sound"); // DEBUG
            }
        }
    }

    // Called by PlayerHurtbox when it registers a hit against a valid target
    public void ReportHit(Vector2 reportedAttackDirection) {
        attackHitReportedThisAttack = true; // Mark that a hit occurred
        Debug.Log($"[PlayerStateMachine2D] ReportHit called! Direction: {reportedAttackDirection}"); // DEBUG

        if (reportedAttackDirection == Vector2.down) {
            PlaySound(downAttackHitSound, downHitSoundOffset);
            // Pogo sound is handled separately in ReportDownwardHit if applicable
        }
        else if (reportedAttackDirection == Vector2.up || reportedAttackDirection == Vector2.right || reportedAttackDirection == Vector2.left) {
            // Play a random sound from the forward/up hit pool
            if (forwardUpAttackHitSounds != null && forwardUpAttackHitSounds.Length > 0) {
                AudioClip clipToPlay = forwardUpAttackHitSounds[Random.Range(0, forwardUpAttackHitSounds.Length)];
                PlaySound(clipToPlay, fwdUpHitSoundOffset);
            } else {
                Debug.LogWarning("Forward/Up Attack Hit Sounds array is empty or null!");
            }
        }
        // Else: Potentially handle other attack directions if added later
    }

    // Specific method for downward hits that result in a pogo jump
    // Assumes PlayerHurtbox calls BOTH ReportHit(Vector2.down) AND this method for pogo hits.
    public void ReportDownwardHit(float pogoStrengthFromObject) {
        Debug.Log($"[PlayerStateMachine2D] ReportDownwardHit called! Applying pogo strength: {pogoStrengthFromObject}"); // DEBUG
        if (currentState == State.Death) return;

        // --- Pogo Physics --- 
        velocity.y = pogoStrengthFromObject; // Use the strength defined by the hit object
        dashCount = maxDashes; // Reset dashes
        jumpBufferTimer = 0f; // Consume jump buffer if any
        currentState = State.Jumping; // Force into jumping state for upward momentum
        variableJumpTimer = variableJumpTime; // Allow variable jump height from pogo
        coyoteTimer = 0f; // Reset coyote time as we are now airborne from the pogo

        // --- Pogo Audio --- 
        PlaySound(downAttackPogoSound, pogoSoundOffset); // Pass pogo offset

        // Optional: Trigger other effects (particles, screen shake etc.)
    }

    void DetermineAttackDirection() {
        if (verticalInput > attackInputThreshold) {
            attackDirection = Vector2.up;
        }
        else if (verticalInput < -attackInputThreshold) {
            attackDirection = Vector2.down;
        }
        else {
            float facingDirection = Mathf.Sign(transform.localScale.x);
            if (facingDirection == 0) facingDirection = 1;
            attackDirection = (facingDirection > 0) ? Vector2.right : Vector2.left;
        }
    }

    void SpawnHurtbox() {
        if (hurtboxPrefab == null) {
            Debug.LogError("Hurtbox Prefab not assigned in the Inspector!");
            return;
        }
        Vector2 spawnOffset = Vector2.zero;
        Vector2 hurtboxSize = Vector2.one;
        Quaternion rotation = Quaternion.identity;
        float playerFacing = Mathf.Sign(transform.localScale.x);
        if (playerFacing == 0) playerFacing = 1;

        if (attackDirection == Vector2.up) {
            spawnOffset = upAttackOffset;
            hurtboxSize = upAttackSize;
            rotation = Quaternion.Euler(0, 0, 90);
        }
        else if (attackDirection == Vector2.down) {
            spawnOffset = downAttackOffset;
            hurtboxSize = downAttackSize;
            rotation = Quaternion.Euler(0, 0, -90);
        }
        else { // Forward
            spawnOffset = new Vector2(forwardAttackOffset.x * playerFacing, forwardAttackOffset.y);
            hurtboxSize = forwardAttackSize;
            if (attackDirection == Vector2.left) {
                 rotation = Quaternion.Euler(0, 180, 0);
            }
        }
        Vector2 spawnPos = (Vector2)transform.position + spawnOffset;
        // Instantiate the hurtbox as a child of the player transform
        GameObject hurtboxGO = Instantiate(hurtboxPrefab, spawnPos, rotation, transform);

        // --- Initialize the Hurtbox --- 
        PlayerHurtbox hurtbox = hurtboxGO.GetComponent<PlayerHurtbox>();
        if (hurtbox != null) {
            // Initialize with damage, LIFETIME, direction, size, and player reference
             hurtbox.Initialize(this, attackDamage, attackHurtboxDuration, attackDirection, hurtboxSize);
        } else {
             Debug.LogWarning("Spawned Hurtbox Prefab does not have a PlayerHurtbox script! Falling back to timed destroy. Hit reporting and specific sounds will not work.");
             // Fallback: Destroy based on hurtbox duration if script is missing
             Destroy(hurtboxGO, attackHurtboxDuration); 
        }
        // --- End Initialization ---
    }

    // Helper function to play sounds safely, now accepts a specific offset
    void PlaySound(AudioClip clip, float offset) {
        if (audioSource != null && clip != null) {
            // Attempt to play from the offset time.
            // Note: PlayOneShot doesn't always reliably respect audioSource.time set immediately before.
            // If this still fails, the best solution is often to trim silence from the audio file itself.
            if (offset > 0 && offset < clip.length) {
                audioSource.time = offset; // Set the desired start time based on the passed offset
                audioSource.PlayOneShot(clip);
            } else {
                 audioSource.PlayOneShot(clip); // Play normally if offset is zero or invalid
            }
        }
    }

    bool IsGrounded() {
        if (groundCheck == null) return false;
        return Physics2D.OverlapBox(groundCheck.position, groundCheckSize, 0f, groundLayer);
    }

    void FlipSprite() {
        if (currentState == State.Death || currentState == State.Dashing || Mathf.Abs(horizontalInput) < 0.01f) return;

        float direction = Mathf.Sign(horizontalInput);
        transform.localScale = new Vector3(direction, 1, 1);
    }

    void OnDrawGizmosSelected() {
        if (groundCheck != null) {
            Gizmos.color = IsGrounded() ? Color.green : Color.red;
            Gizmos.DrawWireCube(groundCheck.position, groundCheckSize);
        }

        Vector2 currentPos = transform.position;
        float gizmoRadius = 0.2f;
        float playerFacing = Application.isPlaying ? Mathf.Sign(transform.localScale.x) : 1f;
         if (playerFacing == 0) playerFacing = 1;

        Vector2 forwardPos = currentPos + new Vector2(forwardAttackOffset.x * playerFacing, forwardAttackOffset.y);
        Vector2 upPos = currentPos + upAttackOffset;
        Vector2 downPos = currentPos + downAttackOffset;

        Gizmos.color = Color.cyan;
        Gizmos.DrawWireCube(forwardPos, new Vector3(forwardAttackSize.x, forwardAttackSize.y, 1f));

        Gizmos.color = Color.magenta;
        Gizmos.DrawWireCube(upPos, new Vector3(upAttackSize.x, upAttackSize.y, 1f));
        Gizmos.DrawWireCube(downPos, new Vector3(downAttackSize.x, downAttackSize.y, 1f));
    }

    public void TakeDamage(int amount, Vector2? knockbackDirection = null) {
        if (currentState == State.Death || isInvulnerable || amount <= 0) return;

        CurrentHealth -= amount;
        CurrentHealth = Mathf.Max(CurrentHealth, 0);
        Debug.Log($"Player took {amount} damage. Current Health: {CurrentHealth}/{maxHealth}");

        // Start Invulnerability
        isInvulnerable = true;
        invulnerabilityTimer = invulnerabilityDuration;
        StartCoroutine(FlashSprite());
        // PlaySound(hurtSound); // Optional: Add a hurtSound AudioClip field and play it here

        // Apply Knockback
        Vector2 actualKnockback;
        if (knockbackDirection.HasValue) {
            actualKnockback = knockbackDirection.Value.normalized * knockbackForce;
        } else {
            // Default knockback: Horizontal away from facing direction
            float knockbackDirX = -Mathf.Sign(transform.localScale.x);
            if (knockbackDirX == 0) knockbackDirX = -1; // Default if scale is weird
            actualKnockback = new Vector2(knockbackDirX, 1f).normalized * knockbackForce; // Add some upward force
        }
        velocity = actualKnockback; // Override current velocity
        // Ensure rigidbody velocity is also set immediately if needed, though ApplyVelocity handles it later
        rb.velocity = velocity;

        // Check for death AFTER applying knockback/invuln effects
        if (CurrentHealth <= 0) {
            Die();
        } else {
            // Optional: Interrupt actions like dash/attack on taking damage
            if(currentState == State.Dashing) {
                dashTimer = 0; // End dash early
                currentState = State.Falling; // Transition to falling
            }
            // Maybe cancel attack windup? Depends on design.
        }
    }

    // Coroutine for sprite flashing effect
    IEnumerator FlashSprite() {
        if (spriteRenderer == null) yield break; // Safety check

        float flashEndTime = Time.time + invulnerabilityDuration;
        bool spriteVisible = true;

        // Use the timer in Update as the primary control, this coroutine just handles toggling
        while (isInvulnerable) { // Loop while invulnerable flag is true
            spriteRenderer.enabled = spriteVisible;
            spriteVisible = !spriteVisible;
            yield return new WaitForSeconds(spriteFlashInterval);
        }

        // Ensure sprite is enabled when invulnerability ends (handled by Update, but good safety)
        spriteRenderer.enabled = true;
    }

    public void Heal(int amount) {
        if (currentState == State.Death || amount <= 0) return;
        CurrentHealth += amount;
        CurrentHealth = Mathf.Min(CurrentHealth, maxHealth);
        Debug.Log($"Player healed {amount}. Current Health: {CurrentHealth}/{maxHealth}");
    }

    private void Die() {
        if (currentState == State.Death) return; // Prevent multiple triggers

        Debug.Log("Player has died.");
        currentState = State.Death;
        velocity = Vector2.zero; // Stop movement
        rb.velocity = Vector2.zero; // Ensure physics stops
        isInvulnerable = false; // Make sure invulnerability stops on death
        StopAllCoroutines(); // Stop flashing, attack checks, etc.

        UpdateAnimationSystems(); // Trigger death animation
        // PlaySound(deathSound); // Optional: Add and play a death sound

        // Start the reload process after a delay
        StartCoroutine(ReloadLevelAfterDelay());
    }

    private IEnumerator ReloadLevelAfterDelay() {
        // Wait for the duration of the death animation plus a grace period
        yield return new WaitForSeconds(deathAnimationDuration + deathGracePeriod);

        Debug.Log("Reloading level...");
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }
} 