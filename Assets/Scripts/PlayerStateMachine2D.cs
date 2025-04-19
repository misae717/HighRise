using UnityEngine;

public class PlayerStateMachine2D : MonoBehaviour {
    public enum State { Idle, Running, Jumping, Falling, Dashing, WallHold }
    [Header("State")]
    public State currentState = State.Falling;

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

    [Header("Wall Hold")]
    public float wallSlideSpeed = 2f;
    public LayerMask wallLayer;
    public float wallCheckDistance = 0.6f;

    [Header("Ground Check")]
    public Transform groundCheck;
    public Vector2 groundCheckSize = new Vector2(0.9f, 0.1f);
    public LayerMask groundLayer;

    private Rigidbody2D rb;
    private BoxCollider2D boxCollider;
    private Vector2 velocity;
    private float horizontalInput;
    private bool jumpHeld;

    // Timers & counters
    private float coyoteTimer;
    private float jumpBufferTimer;
    private float variableJumpTimer;

    private int dashCount;
    private float dashTimer;

    void Awake() {
        rb = GetComponent<Rigidbody2D>();
        boxCollider = GetComponent<BoxCollider2D>();
        rb.gravityScale = 0f;
        dashCount = maxDashes;
    }

    void Update() {
        horizontalInput = Input.GetAxisRaw("Horizontal");
        bool jumpPressed = Input.GetButtonDown("Jump");
        jumpHeld = Input.GetButton("Jump");
        bool dashPressed = Input.GetButtonDown("Fire1");

        // Coyote
        if (IsGrounded()) {
            coyoteTimer = coyoteTime;
            dashCount = maxDashes; // Reset dashes on ground
        } else {
            coyoteTimer -= Time.deltaTime;
        }
        // Jump buffer
        if (jumpPressed) jumpBufferTimer = jumpBufferTime;
        else jumpBufferTimer -= Time.deltaTime;

        // State machine
        switch (currentState) {
            case State.Idle:
            case State.Running:
                GroundedState(jumpPressed, dashPressed);
                break;
            case State.Jumping:
                JumpingState();
                break;
            case State.Falling:
                FallingState(jumpPressed, dashPressed);
                break;
            case State.Dashing:
                DashingState();
                break;
            case State.WallHold:
                WallHoldState(jumpPressed);
                break;
        }

        // Apply
        rb.velocity = velocity;
        FlipSprite();
    }

    void GroundedState(bool jumpPressed, bool dashPressed) {
        // Dash
        if (dashPressed && dashCount > 0) { StartDash(); return; }
        // Jump (coyote and buffer)
        if (jumpBufferTimer > 0f && coyoteTimer > 0f) {
            DoJump();
            jumpBufferTimer = 0f;
            currentState = State.Jumping;
            return;
        }
        // Horizontal
        float target = horizontalInput * maxRunSpeed;
        float accel = (Mathf.Abs(horizontalInput) > 0.01f ? acceleration : deceleration);
        // Snappy direction change
        if (Mathf.Sign(horizontalInput) != Mathf.Sign(velocity.x) && Mathf.Abs(horizontalInput) > 0.01f) {
            velocity.x = 0f;
        }
        velocity.x = Mathf.MoveTowards(velocity.x, target, accel * Time.deltaTime);
        // Reset vertical
        velocity.y = 0f;
        // Transition
        currentState = Mathf.Abs(velocity.x) > 0.01f ? State.Running : State.Idle;
    }

    void JumpingState() {
        // Variable jump
        variableJumpTimer -= Time.deltaTime;
        if (!jumpHeld || variableJumpTimer <= 0f) currentState = State.Falling;
        // Gravity
        velocity.y -= gravity * Time.deltaTime;
        velocity.y = Mathf.Max(velocity.y, -maxFallSpeed);
        // Air control (snappy, but less than ground)
        float target = horizontalInput * maxRunSpeed;
        float airAccel = acceleration * 0.7f;
        // Snappy direction change in air
        if (Mathf.Sign(horizontalInput) != Mathf.Sign(velocity.x) && Mathf.Abs(horizontalInput) > 0.01f) {
            velocity.x = 0f;
        }
        velocity.x = Mathf.MoveTowards(velocity.x, target, airAccel * Time.deltaTime);
    }

    void FallingState(bool jumpPressed, bool dashPressed) {
        // Wall hold (only slow descent, not stick)
        if (horizontalInput != 0 && IsTouchingWall()) {
            // Only slow descent, don't stick
            velocity.y = Mathf.Max(velocity.y, -wallSlideSpeed);
        }
        // Dash
        if (dashPressed && dashCount > 0) { StartDash(); return; }
        // Land
        if (IsGrounded()) { currentState = Mathf.Abs(horizontalInput) > 0.01f ? State.Running : State.Idle; return; }
        // Gravity
        velocity.y -= gravity * Time.deltaTime;
        velocity.y = Mathf.Max(velocity.y, -maxFallSpeed);
        // Air control (snappy)
        float target = horizontalInput * maxRunSpeed;
        float airAccel = acceleration * 0.7f;
        if (Mathf.Sign(horizontalInput) != Mathf.Sign(velocity.x) && Mathf.Abs(horizontalInput) > 0.01f) {
            velocity.x = 0f;
        }
        velocity.x = Mathf.MoveTowards(velocity.x, target, airAccel * Time.deltaTime);
    }

    void DashingState() {
        dashTimer -= Time.deltaTime;
        if (dashTimer <= 0f) { currentState = State.Falling; return; }
        // Constant velocity
    }

    void WallHoldState(bool jumpPressed) {
        // This state is deprecated, but kept for animation hooks if needed
        // Just slow descent in FallingState now
        currentState = State.Falling;
    }

    void StartDash() {
        currentState = State.Dashing;
        dashTimer = dashDuration;
        dashCount--;
        Vector2 dir = new Vector2(horizontalInput != 0 ? horizontalInput : transform.localScale.x, 0).normalized;
        velocity = dir * dashSpeed;
    }

    void DoJump() {
        velocity.y = jumpSpeed;
        variableJumpTimer = variableJumpTime;
    }

    bool IsGrounded() {
        if (groundCheck == null) return false;
        return Physics2D.OverlapBox(groundCheck.position, groundCheckSize, 0f, groundLayer);
    }

    bool IsTouchingWall() {
        Vector2 dir = Vector2.right * Mathf.Sign(horizontalInput);
        Vector2 origin = (Vector2)boxCollider.bounds.center;
        return Physics2D.Raycast(origin, dir, wallCheckDistance, wallLayer);
    }

    void FlipSprite() {
        if (horizontalInput > 0) transform.localScale = Vector3.one;
        else if (horizontalInput < 0) transform.localScale = new Vector3(-1,1,1);
    }

    void OnDrawGizmosSelected() {
        // Draw ground check
        if (groundCheck != null) {
            Gizmos.color = Color.green;
            Gizmos.DrawWireCube(groundCheck.position, groundCheckSize);
        }
        // Draw wall check
        if (boxCollider != null) {
            Gizmos.color = Color.red;
            Vector2 origin = boxCollider.bounds.center;
            Vector2 dir = Vector2.right * Mathf.Sign(horizontalInput != 0 ? horizontalInput : 1);
            Gizmos.DrawLine(origin, origin + dir * wallCheckDistance);
        }
    }
} 