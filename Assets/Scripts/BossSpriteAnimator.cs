using UnityEngine;
using System.Linq; // Needed for OrderBy

#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Manages sprite animations for the Boss based on the BossController states.
/// Requires sprites to be sliced and named sequentially.
/// </summary>
[RequireComponent(typeof(SpriteRenderer))] // Require SpriteRenderer directly
public class BossSpriteAnimator : MonoBehaviour
{
    [System.Serializable]
    public class AnimationInfo
    {
        public BossController.BossState state; // Use BossController's state enum
        public Sprite[] sprites;
        public float frameRate = 10f;
        public bool loop = true;

#if UNITY_EDITOR
        // Temporary field used by the editor script
        [HideInInspector] public Texture2D sourceSheetForEditor;
#endif
    }

    public AnimationInfo[] animations;
    public SpriteRenderer spriteRenderer;

    // No need for direct reference to BossController here, it will call ChangeAnimation

    private int currentFrame;
    private float frameTimer;
    private BossController.BossState currentAnimState = BossController.BossState.Idle; // Track current anim state
    private AnimationInfo currentAnimation;

    void Awake() // Changed from Start to Awake to ensure renderer is ready early
    {
        if (spriteRenderer == null)
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
        }

        if (spriteRenderer == null)
        {
            Debug.LogError("BossSpriteAnimator: No SpriteRenderer assigned or found!");
            enabled = false;
            return;
        }

        // Initialize with a default state if possible (e.g., Idle)
        // The BossController will call ChangeAnimation soon after anyway
        ChangeAnimation(currentAnimState); // Attempt to set initial animation
    }

    void Update()
    {
        // Animation frame update is handled internally based on currentAnimation
        UpdateAnimationFrame();
    }

    void UpdateAnimationFrame()
    {
        if (currentAnimation != null && currentAnimation.sprites != null && currentAnimation.sprites.Length > 0)
        {
            frameTimer -= Time.deltaTime;

            if (frameTimer <= 0f)
            {
                // Advance frame
                currentFrame++;

                // Loop or clamp frame
                if (currentFrame >= currentAnimation.sprites.Length)
                {
                    if (currentAnimation.loop)
                    {
                        currentFrame = 0;
                    }
                    else
                    {
                        // Stay on the last frame if not looping
                        currentFrame = currentAnimation.sprites.Length - 1;
                        // Stop updating timer for non-looping finished animations
                        frameTimer = float.MaxValue;
                    }
                }

                // Apply sprite if the frame is valid and within bounds
                if (currentFrame < currentAnimation.sprites.Length)
                {
                    spriteRenderer.sprite = currentAnimation.sprites[currentFrame];
                }

                // Reset timer only if the animation is looping or hasn't reached the end
                if (frameTimer != float.MaxValue) // Avoid resetting if clamped on last frame
                {
                     frameTimer = currentAnimation.frameRate > 0 ? (1f / currentAnimation.frameRate) : float.MaxValue;
                }
            }
        }
    }

    // Public method for BossController to change the animation
    public void ChangeAnimation(BossController.BossState newState)
    {
        // Debug.Log($"BossSpriteAnimator changing to: {newState}. Currently: {currentAnimState}");

        // Prevent unnecessary resets if the state is already the target state,
        // UNLESS the current animation is non-looping and has finished.
        bool isNonLoopingFinished = (currentAnimation != null && !currentAnimation.loop && currentFrame >= currentAnimation.sprites.Length -1);
        if (newState == currentAnimState && currentAnimation != null && !isNonLoopingFinished)
        {
             // Debug.Log("State already set, skipping reset.");
             return;
        }

        currentAnimState = newState;

        // Find animation for this state
        AnimationInfo targetAnimation = null;
        foreach (AnimationInfo anim in animations)
        {
            if (anim.state == newState)
            {
                targetAnimation = anim;
                break;
            }
        }

        // If no specific animation found for the new state, try to default to Idle
        if (targetAnimation == null && newState != BossController.BossState.Idle) 
        {
            Debug.LogWarning($"No specific animation for state {newState}, attempting to use Idle.");
            foreach (AnimationInfo anim in animations)
            {
                if (anim.state == BossController.BossState.Idle)
                {
                    targetAnimation = anim; 
                    // Don't change currentAnimState here, BossController still thinks it's Shielding/Attacking
                    break;
                }
            }
        }

        currentAnimation = targetAnimation;

        // Reset animation if found and valid
        if (currentAnimation != null && currentAnimation.sprites != null && currentAnimation.sprites.Length > 0)
        {
            currentFrame = 0;
            frameTimer = currentAnimation.frameRate > 0 ? (1f / currentAnimation.frameRate) : float.MaxValue;
            // Ensure the first frame is applied immediately
            if(currentFrame < currentAnimation.sprites.Length)
                 spriteRenderer.sprite = currentAnimation.sprites[0];
            // Debug.Log($"Animation set to {newState}, first frame applied.");
        }
        else
        {
            // Optionally set a default sprite or hide the renderer if no animation is found
             spriteRenderer.sprite = null; // Clear sprite if no animation found for the state
             Debug.LogWarning($"BossSpriteAnimator: No animation defined for state: {newState}");
        }
    }
}


// --- Editor Script for BossSpriteAnimator ---
#if UNITY_EDITOR
[CustomEditor(typeof(BossSpriteAnimator))]
public class BossSpriteAnimatorEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        BossSpriteAnimator animator = (BossSpriteAnimator)target;

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Sprite Population Utility", EditorStyles.boldLabel);

        if (animator.animations != null)
        {
            for (int i = 0; i < animator.animations.Length; i++)
            {
                BossSpriteAnimator.AnimationInfo animInfo = animator.animations[i];
                if (animInfo == null) continue;

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField($"State: {animInfo.state}", GUILayout.Width(150));

                animInfo.sourceSheetForEditor = (Texture2D)EditorGUILayout.ObjectField(
                    "Source Sheet",
                    animInfo.sourceSheetForEditor,
                    typeof(Texture2D),
                    false
                );

                if (GUILayout.Button("Populate Sprites"))
                {
                    if (animInfo.sourceSheetForEditor != null)
                    {
                        PopulateSprites(animInfo);
                        EditorUtility.SetDirty(animator);
                    }
                    else
                    {
                        Debug.LogWarning($"Please assign a Source Sheet for state {animInfo.state}.");
                    }
                }
                EditorGUILayout.EndHorizontal();
                
                // Animation Duration Utility
                float duration = (animInfo.sprites != null && animInfo.frameRate > 0 && animInfo.sprites.Length > 0) ? (animInfo.sprites.Length / animInfo.frameRate) : 0f;
                 EditorGUILayout.BeginHorizontal();
                 EditorGUILayout.LabelField($"Est. Duration: {duration:F2}s ({animInfo.sprites?.Length ?? 0} frames)", GUILayout.Width(200));
                 if (GUILayout.Button("Copy Duration"))
                 {
                     EditorGUIUtility.systemCopyBuffer = duration.ToString("F2");
                     Debug.Log($"Copied duration {duration:F2}s for state {animInfo.state}.");
                 }
                 EditorGUILayout.EndHorizontal();
                 EditorGUILayout.Space(); // Add space between entries
            }
        }
    }

    private void PopulateSprites(BossSpriteAnimator.AnimationInfo animInfo)
    {
        string path = AssetDatabase.GetAssetPath(animInfo.sourceSheetForEditor);
        if (string.IsNullOrEmpty(path))
        {
            Debug.LogError("Could not find asset path for the texture.");
            return;
        }

        Sprite[] loadedSprites = AssetDatabase.LoadAllAssetsAtPath(path)
                                     .OfType<Sprite>()
                                     .OrderBy(s => s.name) // Ensure correct order
                                     .ToArray();

        if (loadedSprites.Length == 0)
        {
            Debug.LogWarning($"No sprites found in '{path}'. Ensure the texture type is Sprite (Multiple) and sprites are sliced and named correctly.");
            animInfo.sprites = new Sprite[0]; // Clear existing sprites if none found
        }
        else
        {
             animInfo.sprites = loadedSprites;
             Debug.Log($"Populated {loadedSprites.Length} sprites for state {animInfo.state} from {System.IO.Path.GetFileName(path)}.");
        }
    }
}

// Custom drawer for AnimationInfo to make it look nicer in the inspector array
[CustomPropertyDrawer(typeof(BossSpriteAnimator.AnimationInfo))]
public class BossAnimationInfoDrawer : PropertyDrawer
{
     // Adjust line height as needed
    private const float lineHeight = 18f; // Approximately default line height
    private const float spacing = 2f;

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        // Using BeginProperty / EndProperty ensures prefab override logic works correctly.
        EditorGUI.BeginProperty(position, label, property);

        // Don't make child fields be indented
        var indent = EditorGUI.indentLevel;
        EditorGUI.indentLevel = 0;

        // Calculate rects
        var stateRect = new Rect(position.x, position.y, position.width, lineHeight);
        var spritesRect = new Rect(position.x, position.y + lineHeight + spacing, position.width, lineHeight);
        var frameRateRect = new Rect(position.x, position.y + 2 * (lineHeight + spacing), position.width * 0.7f, lineHeight); // Give less width to frame rate
        var loopRect = new Rect(position.x + position.width * 0.75f, position.y + 2 * (lineHeight + spacing), position.width * 0.25f, lineHeight); // Place loop toggle next to frame rate

        // Draw fields - passs GUIContent.none to each so they don't draw labels
        EditorGUI.PropertyField(stateRect, property.FindPropertyRelative("state"), new GUIContent("Anim State"));
        EditorGUI.PropertyField(spritesRect, property.FindPropertyRelative("sprites"), true); // True shows children for array
        EditorGUI.PropertyField(frameRateRect, property.FindPropertyRelative("frameRate"), new GUIContent("Frame Rate"));
        EditorGUI.PropertyField(loopRect, property.FindPropertyRelative("loop"), GUIContent.none); // No label for the bool toggle

        // Set indent back to what it was
        EditorGUI.indentLevel = indent;

        EditorGUI.EndProperty();
    }

     public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
         // Calculate the height needed for the 'sprites' array field
         SerializedProperty spritesProp = property.FindPropertyRelative("sprites");
         float spritesHeight = EditorGUI.GetPropertyHeight(spritesProp, true);

         // Height is base height for other fields + sprites array height + spacing
         return (lineHeight + spacing) * 2 + spritesHeight + spacing; 
    }
}
#endif 