using UnityEngine;
using System.Collections; // Only if using a delay before destroy

[RequireComponent(typeof(SpriteRenderer))] // Needs a SpriteRenderer
public class ExplosionAnimator : MonoBehaviour
{
    [Header("Animation")]
    public Sprite[] explosionFrames; // Assign explosion spritesheet frames here
    public float frameRate = 15f;   // Frames per second
    public bool destroyOnCompletion = true; // Destroy GameObject when animation finishes

    private SpriteRenderer spriteRenderer;
    private int currentFrame = 0;
    private float frameTimer;

    void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();

        if (explosionFrames == null || explosionFrames.Length == 0)
        {
            Debug.LogError("ExplosionAnimator: Frames not assigned or empty!", this);
            enabled = false;
            return;
        }

        // Initialize
        spriteRenderer.sprite = explosionFrames[0];
        frameTimer = 1f / frameRate;
        currentFrame = 0;
    }

    void Update()
    {
        frameTimer -= Time.deltaTime;

        if (frameTimer <= 0f)
        {
            frameTimer += 1f / frameRate; // Add interval
            if (frameTimer < 0) frameTimer = 1f / frameRate; // Prevent negative timer

            currentFrame++;

            // Check if animation finished
            if (currentFrame >= explosionFrames.Length)
            {
                if (destroyOnCompletion)
                {
                    Destroy(gameObject);
                }
                else
                {
                    // Optionally stop animation on last frame if not destroying
                    currentFrame = explosionFrames.Length - 1;
                    enabled = false; // Disable Update loop
                }
                return; // Exit update if done or destroying
            }

            // Update sprite
            spriteRenderer.sprite = explosionFrames[currentFrame];
        }
    }
} 