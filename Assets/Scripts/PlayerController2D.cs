using UnityEngine;

[RequireComponent(typeof(BoxCollider2D))]
public class PlayerController2D : MonoBehaviour {
    [Header("Movement")]
    public float maxRunSpeed = 90f;
    public float acceleration = 1000f;
    public float deceleration = 400f;
    public float airAccelerationMultiplier = 0.65f;

    [Header("Gravity")]
    public float gravity = 900f;
    public float maxFallSpeed = 160f;

    [Header("Jump")]
    public float jumpSpeed = 105f;
    public float variableJumpTime = 0.2f;
    public float jumpGraceTime = 0.1f;
    public float jumpBufferTime = 0.1f;

    [Header("Dash")]
    public float dashSpeed = 240f;
    public float dashTime = 0.15f;
    public int maxDashes = 1;

    [Header("Wall Slide & Jump")]
    public float wallSlideSpeed = 20f;
    public float wallJumpHSpeed = 130f;
    public Vector2 wallDetectionSize = new Vector2(2f, 14f);

    [Header("Checks")]
    public Transform groundCheck;
    public Vector2 groundCheckSize = new Vector2(8f, 2f);
    public LayerMask groundLayer;

    private BoxCollider2D boxCollider;
    private Vector2 velocity;
    private bool facingRight = true;

    // Timers & state
    private float coyoteTimer;
    private float jumpBufferTimer;
    private float variableJumpTimerCounter;
    private bool isJumping;

    private bool canDash = true;
    private int dashCount;
    private float dashTimer;
    private bool isDashing;

    void Awake() {
        boxCollider = GetComponent<BoxCollider2D>();
        dashCount = maxDashes;
    }

    void Update() {
        float inputX = Input.GetAxisRaw("Horizontal");
        float inputY = Input.GetAxisRaw("Vertical");
        bool jumpPressed = Input.GetButtonDown("Jump");
        bool jumpHeld = Input.GetButton("Jump");
        bool dashPressed = Input.GetButtonDown("Dash"); // map Dash in InputManager

        // Ground & coyote
        if (IsGrounded()) {
            coyoteTimer = jumpGraceTime;
            canDash = true;
            dashCount = maxDashes;
        } else {
            coyoteTimer -= Time.deltaTime;
        }

        // Jump Buffer
        if (jumpPressed) jumpBufferTimer = jumpBufferTime;
        if (jumpBufferTimer > 0f) jumpBufferTimer -= Time.deltaTime;

        // Perform Jump
        if (jumpBufferTimer > 0f && coyoteTimer > 0f) {
            DoJump();
            jumpBufferTimer = 0f;
        }

        // Start Dash
        if (dashPressed && dashCount > 0) {
            StartDash(inputX, inputY);
        }

        // Dash Update
        if (isDashing) {
            dashTimer -= Time.deltaTime;
            if (dashTimer <= 0f) {
                isDashing = false;
                // post-dash: zero vertical momentum
                velocity = Vector2.zero;
            }
        } else {
            // Horizontal Movement
            float targetSpeed = inputX * maxRunSpeed;
            float accelRate = (Mathf.Abs(inputX) > 0.01f ? acceleration : deceleration) * (IsGrounded() ? 1f : airAccelerationMultiplier);
            velocity.x = Mathf.MoveTowards(velocity.x, targetSpeed, accelRate * Time.deltaTime);

            // Gravity
            velocity.y -= gravity * Time.deltaTime;
            if (velocity.y < -maxFallSpeed) velocity.y = -maxFallSpeed;

            // Variable Jump Height
            if (isJumping) {
                variableJumpTimerCounter -= Time.deltaTime;
                if (!jumpHeld || variableJumpTimerCounter <= 0f) {
                    isJumping = false;
                }
                if (!isJumping && velocity.y > 0f) {
                    velocity.y -= gravity * Time.deltaTime * 1.5f;
                }
            }

            // Wall Slide & Jump
            if (!IsGrounded() && inputX != 0 && IsTouchingWall(inputX)) {
                velocity.y = Mathf.Max(velocity.y, -wallSlideSpeed);
                if (jumpPressed) {
                    velocity.y = jumpSpeed;
                    velocity.x = -inputX * wallJumpHSpeed;
                    isJumping = true;
                    variableJumpTimerCounter = variableJumpTime;
                }
            }
        }

        // Apply Movement
        transform.Translate(velocity * Time.deltaTime);

        // Flip
        if (inputX > 0 && !facingRight) Flip();
        else if (inputX < 0 && facingRight) Flip();
    }

    private bool IsGrounded() {
        return Physics2D.OverlapBox(groundCheck.position, groundCheckSize, 0f, groundLayer);
    }

    private bool IsTouchingWall(float dirX) {
        Vector2 origin = (Vector2)transform.position + Vector2.up * (boxCollider.size.y * 0.5f - 1f);
        Vector2 size = wallDetectionSize;
        Vector2 center = origin + Vector2.right * dirX * (boxCollider.size.x * 0.5f + 0.1f);
        return Physics2D.OverlapBox(center, size, 0f, groundLayer);
    }

    private void DoJump() {
        velocity.y = jumpSpeed;
        isJumping = true;
        variableJumpTimerCounter = variableJumpTime;
    }

    private void StartDash(float inputX, float inputY) {
        isDashing = true;
        dashCount--;
        dashTimer = dashTime;
        Vector2 dashDir = new Vector2(inputX, inputY);
        if (dashDir.sqrMagnitude < 0.01f) dashDir = new Vector2(facingRight ? 1f : -1f, 0f);
        dashDir.Normalize();
        velocity = dashDir * dashSpeed;
    }

    private void Flip() {
        facingRight = !facingRight;
        Vector3 s = transform.localScale;
        s.x *= -1f;
        transform.localScale = s;
    }

    void OnDrawGizmosSelected() {
        if (groundCheck) {
            Gizmos.color = Color.green;
            Gizmos.DrawWireCube(groundCheck.position, groundCheckSize);
        }
        if (boxCollider) {
            Gizmos.color = Color.red;
            Vector2 origin = (Vector2)transform.position + Vector2.up * (boxCollider.size.y * 0.5f - 1f);
            Vector2 centerRight = origin + Vector2.right * (boxCollider.size.x * 0.5f + 0.1f);
            Gizmos.DrawWireCube(centerRight, wallDetectionSize);
            Vector2 centerLeft = origin + Vector2.left * (boxCollider.size.x * 0.5f + 0.1f);
            Gizmos.DrawWireCube(centerLeft, wallDetectionSize);
        }
    }
}
