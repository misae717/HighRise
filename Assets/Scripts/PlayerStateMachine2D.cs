using UnityEngine;

[RequireComponent(typeof(Rigidbody2D), typeof(BoxCollider2D))]
public class PlayerStateMachine2D : MonoBehaviour {
    public enum State { Idle, Running, Jumping, Falling, Dashing, WallHold, Death }
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

    [Header("Dash")]
    public float dashSpeed = 20f;
    public float dashDuration = 0.15f;
    public int maxDashes = 1;
    [SerializeField] private float dashAnimDuration = 0.15f;

    [Header("Wall Hold")]
    public float wallSlideSpeed = 2f;
    public LayerMask wallLayer;
    public float wallCheckDistance = 0.6f;

    [Header("Ground Check")]
    public Transform groundCheck;
    public Vector2 groundCheckSize = new Vector2(0.9f, 0.1f);
    public LayerMask groundLayer;

    // Component References (cached in Awake)
    private Rigidbody2D rb;
    private BoxCollider2D boxCollider;
    private Animator animator;
    private SpriteAnimator spriteAnimator;

    // Internal State
    private Vector2 velocity;
    private float horizontalInput;
    private bool jumpHeld;

    // Timers & counters
    private float coyoteTimer;
    private float jumpBufferTimer;
    private float variableJumpTimer;

    private int dashCount;
    private float dashTimer;

    // Animation parameter hashes (for efficiency, if using Unity Animator)
    private readonly int stateHash = Animator.StringToHash("State");

    void Awake() {
        rb = GetComponent<Rigidbody2D>();
        boxCollider = GetComponent<BoxCollider2D>();
        animator = GetComponent<Animator>();
        spriteAnimator = GetComponent<SpriteAnimator>();

        rb.gravityScale = 0f;
        CurrentHealth = maxHealth;
        dashCount = maxDashes;
        dashAnimDuration = dashDuration;
    }

    void Update() {
        // Don't process input or state transitions if dead
        if (currentState == State.Death) {
            DeathState(); // Still need to run death state logic (e.g., gravity)
            return;
        }

        // --- Input Handling ---
        horizontalInput = Input.GetAxisRaw("Horizontal");
        bool jumpPressed = Input.GetButtonDown("Jump");
        jumpHeld = Input.GetButton("Jump");
        bool dashPressed = Input.GetButtonDown("Fire1");

        // --- Global Dash Input Check ---
        if (dashPressed && dashCount > 0 && currentState != State.Dashing) {
            StartDash();
            // After starting dash, skip the rest of the update so dash is uninterruptible
            ApplyVelocity();
            FlipSprite();
            return;
        }

        // If dashing, only process dash logic (uninterruptible except by death)
        if (currentState == State.Dashing) {
            DashingState();
            ApplyVelocity();
            FlipSprite();
            return;
        }

        // --- State Tracking ---
        previousState = currentState;

        // --- Timers ---
        UpdateTimers();

        // --- State Machine Logic ---
        RunStateMachine(jumpPressed, dashPressed);

        // --- Animation Update ---
        if (previousState != currentState) {
            UpdateAnimationSystems();
        }

        // --- Physics Application ---
        ApplyVelocity();
        FlipSprite();
    }

    void UpdateTimers() {
        if (IsGrounded()) {
            coyoteTimer = coyoteTime;
            dashCount = maxDashes;
        } else {
            coyoteTimer -= Time.deltaTime;
        }

        if (Input.GetButtonDown("Jump")) {
            jumpBufferTimer = jumpBufferTime;
        } else {
            jumpBufferTimer -= Time.deltaTime;
        }
    }

    void RunStateMachine(bool jumpPressed, bool dashPressed) {
        if (currentState == State.Dashing) {
            DashingState();
            return;
        }
        switch (currentState) {
            case State.Idle:
            case State.Running:
                GroundedState(dashPressed);
                break;
            case State.Jumping:
                JumpingState();
                break;
            case State.Falling:
                FallingState(dashPressed);
                break;
            case State.WallHold:
                WallHoldState();
                break;
        }
    }

    void UpdateAnimationSystems() {
        if (animator != null) {
            animator.SetInteger(stateHash, (int)currentState);
        }
    }

    void ApplyVelocity() {
        if (currentState != State.Dashing && currentState != State.Death) {
            velocity.y -= gravity * Time.deltaTime;
            velocity.y = Mathf.Max(velocity.y, -maxFallSpeed);
        }

        rb.velocity = velocity;
    }

    void GroundedState(bool dashPressed) {
        HandleDashInput(dashPressed);
        
        if (HandleJumpInput()) {
            return; 
        }

        HandleHorizontalMovement(acceleration, deceleration);

        velocity.y = 0f;

        if (!IsGrounded()) {
            currentState = State.Falling;
            coyoteTimer = 0f;
            return;
        }

        if (Mathf.Abs(horizontalInput) > 0.01f) {
            currentState = State.Running;
        } else {
            currentState = State.Idle;
        }
    }

    void JumpingState() {
        HandleVariableJump();
        HandleHorizontalMovement(acceleration * 0.7f, deceleration * 0.7f);

        if (velocity.y <= 0f) {
            currentState = State.Falling;
            return;
        }
    }

    void FallingState(bool dashPressed) {
        HandleDashInput(dashPressed);
        HandleWallSlide();
        HandleHorizontalMovement(acceleration * 0.7f, deceleration * 0.7f);

        if (IsGrounded()) {
            currentState = Mathf.Abs(velocity.x) > 0.01f ? State.Running : State.Idle;
            return;
        }
    }

    void DashingState() {
        dashTimer -= Time.deltaTime;

        if (dashTimer <= 0f) {
            currentState = State.Falling;
            velocity.y = 0f;
            velocity.x *= 0.5f;
            return;
        }
    }

    void WallHoldState() {
        currentState = State.Falling;
    }

    void DeathState() {
        velocity.x = Mathf.MoveTowards(velocity.x, 0, deceleration * Time.deltaTime);
    }

    void HandleHorizontalMovement(float accel, float decel) {
        if (currentState == State.Death) return;

        float targetSpeed = horizontalInput * maxRunSpeed;
        float accelerationToUse = (Mathf.Abs(horizontalInput) > 0.01f) ? accel : decel;

        if (Mathf.Abs(horizontalInput) > 0.01f && Mathf.Sign(horizontalInput) != Mathf.Sign(velocity.x) && velocity.x != 0) {
            accelerationToUse *= 2f;
        }

        velocity.x = Mathf.MoveTowards(velocity.x, targetSpeed, accelerationToUse * Time.deltaTime);

        // Removed automatic state change when grounded to prevent overriding jump state
        // if (IsGrounded()) {
        //     currentState = Mathf.Abs(velocity.x) > 0.01f ? State.Running : State.Idle;
        // }
    }

    bool HandleJumpInput() {
        if (jumpBufferTimer > 0f && coyoteTimer > 0f) {
            DoJump();
            jumpBufferTimer = 0f;
            coyoteTimer = 0f;
            currentState = State.Jumping;
            return true;
        }
        return false;
    }

    void HandleVariableJump() {
        variableJumpTimer -= Time.deltaTime;
        if (!jumpHeld || variableJumpTimer <= 0f) {
            if (velocity.y > 0) {
                velocity.y *= 0.5f;
            }
        }
    }

    void HandleDashInput(bool dashPressed) {
        if (dashPressed && dashCount > 0) {
            StartDash();
        }
    }

    void HandleWallSlide() {
        if (IsTouchingWall() && !IsGrounded() && horizontalInput != 0 && velocity.y < 0) {
            velocity.y = Mathf.Max(velocity.y, -wallSlideSpeed);
        }
    }

    void StartDash() {
        if (currentState == State.Death) return;

        currentState = State.Dashing;
        dashTimer = dashAnimDuration;
        dashCount--;

        float dashDirectionX = (Mathf.Abs(horizontalInput) > 0.1f) ? Mathf.Sign(horizontalInput) : transform.localScale.x;

        Vector2 dir = new Vector2(dashDirectionX, 0).normalized;

        velocity = dir * dashSpeed;
        velocity.y = 0;

        UpdateAnimationSystems();
    }

    void DoJump() {
        if (currentState == State.Death) return;

        velocity.y = jumpSpeed;
        variableJumpTimer = variableJumpTime;
        UpdateAnimationSystems();
    }

    bool IsGrounded() {
        if (groundCheck == null) return false;
        return Physics2D.OverlapBox(groundCheck.position, groundCheckSize, 0f, groundLayer);
    }

    bool IsTouchingWall() {
        if (boxCollider == null) return false;
        float direction = Mathf.Sign(transform.localScale.x);
        if(direction == 0) direction = 1;

        Vector2 origin = (Vector2)boxCollider.bounds.center + Vector2.right * boxCollider.bounds.extents.x * direction;

        RaycastHit2D hit = Physics2D.Raycast(origin, Vector2.right * direction, wallCheckDistance, wallLayer);
        return hit.collider != null;
    }

    void FlipSprite() {
        if (currentState == State.Death) return;

        if (Mathf.Abs(horizontalInput) > 0.01f) {
            float direction = Mathf.Sign(horizontalInput);
            transform.localScale = new Vector3(direction, 1, 1);
        }
    }

    void OnDrawGizmosSelected() {
        if (groundCheck != null) {
            Gizmos.color = IsGrounded() ? Color.green : Color.red;
            Gizmos.DrawWireCube(groundCheck.position, groundCheckSize);
        }

        if (boxCollider != null) {
            float direction = Mathf.Sign(transform.localScale.x);
            if (direction == 0) direction = 1;
            Vector2 origin = (Vector2)boxCollider.bounds.center + Vector2.right * boxCollider.bounds.extents.x * direction;
            Gizmos.color = IsTouchingWall() ? Color.blue : Color.yellow;
            Gizmos.DrawLine(origin, origin + Vector2.right * direction * wallCheckDistance);
        }
    }

    public void TakeDamage(int amount) {
        if (currentState == State.Death || amount <= 0) {
            return;
        }

        CurrentHealth -= amount;
        CurrentHealth = Mathf.Max(CurrentHealth, 0);

        Debug.Log($"Player took {amount} damage. Current Health: {CurrentHealth}/{maxHealth}");

        if (CurrentHealth <= 0) {
            Die();
        }
        else {
        }
    }

    public void Heal(int amount) {
        if (currentState == State.Death || amount <= 0) {
            return;
        }

        CurrentHealth += amount;
        CurrentHealth = Mathf.Min(CurrentHealth, maxHealth);

        Debug.Log($"Player healed {amount}. Current Health: {CurrentHealth}/{maxHealth}");
    }

    private void Die() {
        if (currentState == State.Death) return;

        Debug.Log("Player has died.");
        currentState = State.Death;
        velocity = Vector2.zero;
        rb.velocity = Vector2.zero;

        UpdateAnimationSystems();
    }
} 