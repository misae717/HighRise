using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(BoxCollider2D))]
public class PlayerController2D : MonoBehaviour
{
    [Header("Movement")]
    public float moveSpeed = 90f; // pixels/sec, Celeste: MaxRun
    public float runAccel = 1000f; // Celeste: RunAccel
    public float runReduce = 400f; // Celeste: RunReduce
    public float airMult = 0.65f; // Celeste: AirMult
    public float maxFall = 160f; // Celeste: MaxFall
    public float gravity = 900f; // Celeste: Gravity

    [Header("Jump")]
    public float jumpSpeed = 105f; // Celeste: JumpSpeed
    public float jumpHBoost = 40f; // Celeste: JumpHBoost
    public float varJumpTime = 0.2f; // Celeste: VarJumpTime
    public float coyoteTime = 0.1f; // Celeste: JumpGraceTime
    public float jumpBufferTime = 0.1f;

    [Header("Wall")]
    public float wallJumpH = 130f; // Celeste: WallJumpHSpeed
    public float wallJumpV = 105f; // Celeste: JumpSpeed
    public float wallSlideMax = 20f; // Celeste: WallSlideStartMax

    [Header("Dash")]
    public float dashSpeed = 240f; // Celeste: DashSpeed
    public float dashTime = 0.15f; // Celeste: DashTime
    public float dashCooldown = 0.2f;
    public int maxDashes = 1;

    [Header("Layers")]
    public LayerMask groundLayer;
    public LayerMask wallLayer;

    private Rigidbody2D rb;
    private BoxCollider2D box;
    private Vector2 input;
    private Vector2 dashDir;
    private bool grounded;
    private bool onWall;
    private int wallDir;
    private float coyoteCounter;
    private float jumpBufferCounter;
    private float varJumpCounter;
    private bool isJumping;
    private bool isWallSliding;
    private bool isDashing;
    private float dashTimer;
    private int dashes;
    private int facing = 1;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        box = GetComponent<BoxCollider2D>();
        dashes = maxDashes;
    }

    void Update()
    {
        // Input
        input = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
        if (input.x != 0) facing = (int)Mathf.Sign(input.x);

        // Jump buffer
        if (Input.GetButtonDown("Jump"))
            jumpBufferCounter = jumpBufferTime;
        else
            jumpBufferCounter -= Time.deltaTime;

        // Dash input (8 directions)
        if (Input.GetButtonDown("Fire3") && dashes > 0 && !isDashing)
        {
            Vector2 dashInput = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
            if (dashInput == Vector2.zero)
                dashInput = new Vector2(facing, 0); // default to facing
            dashDir = dashInput.normalized;
            StartDash();
        }
    }

    void FixedUpdate()
    {
        grounded = IsGrounded();
        onWall = IsOnWall(out wallDir);

        // Coyote time
        if (grounded)
            coyoteCounter = coyoteTime;
        else
            coyoteCounter -= Time.fixedDeltaTime;

        // Wall slide
        isWallSliding = false;
        if (!grounded && onWall && input.x == wallDir)
        {
            isWallSliding = true;
            if (rb.velocity.y < -wallSlideMax)
                rb.velocity = new Vector2(rb.velocity.x, -wallSlideMax);
        }

        // Horizontal movement (null-cancelling, acceleration, reduction)
        if (!isDashing)
        {
            float targetSpeed = input.x * moveSpeed;
            float accel = grounded ? (Mathf.Abs(targetSpeed) > 0.01f ? runAccel : runReduce) : (Mathf.Abs(targetSpeed) > 0.01f ? runAccel * airMult : runReduce * airMult);
            float speedDiff = targetSpeed - rb.velocity.x;
            float movement = speedDiff * accel * Time.fixedDeltaTime / moveSpeed;
            rb.velocity = new Vector2(rb.velocity.x + movement, Mathf.Clamp(rb.velocity.y, -maxFall, 100));
        }

        // Jump
        if (jumpBufferCounter > 0 && (coyoteCounter > 0 || isWallSliding) && !isDashing)
        {
            if (isWallSliding && !grounded)
            {
                // Wall jump
                rb.velocity = new Vector2(-wallDir * wallJumpH, wallJumpV);
            }
            else
            {
                // Normal jump
                rb.velocity = new Vector2(rb.velocity.x + jumpHBoost * input.x, jumpSpeed);
            }
            isJumping = true;
            varJumpCounter = varJumpTime;
            jumpBufferCounter = 0;
            coyoteCounter = 0;
        }

        // Variable jump height
        if (isJumping)
        {
            if (!Input.GetButton("Jump") || varJumpCounter <= 0)
            {
                if (rb.velocity.y > 0)
                    rb.velocity = new Vector2(rb.velocity.x, rb.velocity.y * 0.5f);
                isJumping = false;
            }
            else
            {
                varJumpCounter -= Time.fixedDeltaTime;
            }
        }

        // Dash logic
        if (isDashing)
        {
            dashTimer -= Time.fixedDeltaTime;
            rb.velocity = dashDir * dashSpeed;
            if (dashTimer <= 0)
            {
                isDashing = false;
                rb.gravityScale = gravity / 9.81f; // restore gravity
            }
        }
        else
        {
            rb.gravityScale = gravity / 9.81f;
            if (grounded)
                dashes = maxDashes;
        }
    }

    void StartDash()
    {
        isDashing = true;
        dashTimer = dashTime;
        dashes--;
        rb.gravityScale = 0;
        rb.velocity = dashDir * dashSpeed;
    }

    bool IsGrounded()
    {
        Vector2 origin = (Vector2)transform.position + box.offset;
        Vector2 size = box.size;
        float extra = 0.05f;
        RaycastHit2D hit = Physics2D.BoxCast(origin, size, 0f, Vector2.down, extra, groundLayer);
        return hit.collider != null;
    }

    bool IsOnWall(out int dir)
    {
        Vector2 origin = (Vector2)transform.position + box.offset;
        Vector2 size = box.size;
        float extra = 0.05f;
        RaycastHit2D hitLeft = Physics2D.BoxCast(origin, size, 0f, Vector2.left, extra, wallLayer);
        RaycastHit2D hitRight = Physics2D.BoxCast(origin, size, 0f, Vector2.right, extra, wallLayer);
        if (hitLeft.collider != null)
        {
            dir = -1;
            return true;
        }
        else if (hitRight.collider != null)
        {
            dir = 1;
            return true;
        }
        dir = 0;
        return false;
    }
}