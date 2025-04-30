using UnityEngine;
using System.Linq; // Needed for OrderBy

#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// A simple component to manage sprite animations based on the PlayerStateMachine2D states.
/// Requires sprites to be sliced within the Unity editor and named sequentially (e.g., sheet_0, sheet_1, ...).
/// </summary>
public class SpriteAnimator : MonoBehaviour
{
    [System.Serializable]
    public class AnimationInfo
    {
        public PlayerStateMachine2D.State state;
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
    
    private PlayerStateMachine2D playerStateMachine;
    private int currentFrame;
    private float frameTimer;
    private PlayerStateMachine2D.State currentAnimState;
    private AnimationInfo currentAnimation;
    
    void Start()
    {
        playerStateMachine = GetComponent<PlayerStateMachine2D>();
        
        if (spriteRenderer == null)
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
        }
        
        if (spriteRenderer == null)
        {
            Debug.LogError("No SpriteRenderer assigned or found on the GameObject!");
            enabled = false;
            return;
        }
        
        if (playerStateMachine == null)
        {
            Debug.LogError("No PlayerStateMachine2D found on the GameObject!");
            enabled = false;
            return;
        }
        
        // Start with the current state
        ChangeAnimation(playerStateMachine.currentState);
    }
    
    void Update()
    {
        // Check if state changed
        if (currentAnimState != playerStateMachine.currentState)
        {
            ChangeAnimation(playerStateMachine.currentState);
        }
        
        // Update animation
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
                        // Optionally disable further updates for non-looping anims?
                    }
                }
                
                // Apply sprite if the frame is valid
                if (currentFrame < currentAnimation.sprites.Length)
                {
                    spriteRenderer.sprite = currentAnimation.sprites[currentFrame];
                }
                
                // Reset timer (handle zero frameRate)
                frameTimer = currentAnimation.frameRate > 0 ? (1f / currentAnimation.frameRate) : float.MaxValue;
            }
        }
    }
    
    // Make this method public so PlayerStateMachine2D can call it directly
    public void ChangeAnimation(PlayerStateMachine2D.State newState)
    {
        Debug.Log($"SpriteAnimator.ChangeAnimation called with state: {newState}. Current playing state: {currentAnimState}"); // DEBUG
        // If the new state is the same as the one currently playing, do nothing
        // (unless it's a non-looping animation that has finished - handled by UpdateAnimationFrame)
        if (newState == currentAnimState && currentAnimation != null) { // Check if currentAnimation exists
             // Optionally allow restarting looping animations? 
             // if(currentAnimation.loop) return; 
             return; // Primary change: prevent resetting if state is already correct
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

        currentAnimation = targetAnimation;

        // Reset animation if found and valid
        if (currentAnimation != null && currentAnimation.sprites != null && currentAnimation.sprites.Length > 0)
        {
            currentFrame = 0;
            frameTimer = currentAnimation.frameRate > 0 ? (1f / currentAnimation.frameRate) : float.MaxValue;
            // Ensure the first frame is applied immediately even if framerate is 0
            if(currentFrame < currentAnimation.sprites.Length)
                 spriteRenderer.sprite = currentAnimation.sprites[0]; 
            // UpdateAnimationFrame(); // No longer needed here, Update loop handles it
        }
        else
        {
            // Optionally set a default sprite or hide the renderer if no animation is found
             spriteRenderer.sprite = null; // Clear sprite if no animation found for the state
             Debug.LogWarning($"No animation defined for state: {newState}");
        }
    }
    
    // Helper method to load all sprites from a folder
    public void LoadSpritesFromAseprite(string spritePath, string jsonPath)
    {
        // This would be implemented in a real importer
        Debug.Log($"Would load sprites from {spritePath} with JSON data from {jsonPath}");
    }
}

#if UNITY_EDITOR
[CustomEditor(typeof(SpriteAnimator))]
public class SpriteAnimatorEditor : Editor
{
    public override void OnInspectorGUI()
    {
        // Draw the default inspector fields
        DrawDefaultInspector();

        SpriteAnimator animator = (SpriteAnimator)target;

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Sprite Population Utility", EditorStyles.boldLabel);

        // Provide a way to populate sprites for each AnimationInfo entry
        if (animator.animations != null)
        {
            for (int i = 0; i < animator.animations.Length; i++)
            {
                SpriteAnimator.AnimationInfo animInfo = animator.animations[i]; 
                if (animInfo == null) continue;

                EditorGUILayout.BeginHorizontal();

                // Show the state this entry is for
                EditorGUILayout.LabelField($"State: {animInfo.state}", GUILayout.Width(150));

                // Field to assign the source sprite sheet texture
                animInfo.sourceSheetForEditor = (Texture2D)EditorGUILayout.ObjectField(
                    "Source Sheet",
                    animInfo.sourceSheetForEditor,
                    typeof(Texture2D),
                    false // Allow scene objects? No, only assets.
                );

                // Button to populate sprites
                if (GUILayout.Button("Populate Sprites"))
                {
                    if (animInfo.sourceSheetForEditor != null)
                    {
                        PopulateSprites(animInfo);
                        EditorUtility.SetDirty(animator); // Mark the object as changed
                    }
                    else
                    {
                        Debug.LogWarning($"Please assign a Source Sheet for state {animInfo.state} before populating.");
                    }
                }
                EditorGUILayout.EndHorizontal();

                // --- Animation Duration Utility ---
                float duration = (animInfo.sprites != null && animInfo.frameRate > 0) ? (animInfo.sprites.Length / animInfo.frameRate) : 0f;
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField($"Estimated Duration: {duration:F2} seconds", GUILayout.Width(200));
                if (GUILayout.Button("Copy Duration"))
                {
                    EditorGUIUtility.systemCopyBuffer = duration.ToString("F2");
                    Debug.Log($"Copied animation duration {duration:F2} seconds to clipboard for state {animInfo.state}.");
                }
                EditorGUILayout.EndHorizontal();
            }
        }
    }

    // Ensure the parameter uses the correct custom class
    private void PopulateSprites(SpriteAnimator.AnimationInfo animInfo) 
    {
        string path = AssetDatabase.GetAssetPath(animInfo.sourceSheetForEditor);
        if (string.IsNullOrEmpty(path))
        {
            Debug.LogError("Could not find asset path for the provided texture.");
            return;
        }

        // Load all Sprite assets embedded in the texture file
        Sprite[] loadedSprites = AssetDatabase.LoadAllAssetsAtPath(path)
                                     .OfType<Sprite>()
                                     .OrderBy(s => s.name) // Order them alphabetically/numerically
                                     .ToArray();

        if (loadedSprites.Length == 0)
        {
            Debug.LogWarning($"No sprites found within the texture asset at '{path}'. Did you slice the sprite sheet in the Sprite Editor?");
            return;
        }

        // Assign the loaded sprites to the AnimationInfo
        animInfo.sprites = loadedSprites;

        Debug.Log($"Populated {loadedSprites.Length} sprites for state {animInfo.state} from {System.IO.Path.GetFileName(path)}.");

        // Clear the temporary field after use
        // animInfo.sourceSheetForEditor = null; // Optional: Keep it for reference or re-population
    }
}

[CustomPropertyDrawer(typeof(SpriteAnimator.AnimationInfo))]
public class AnimationInfoDrawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);

        // Calculate rects
        Rect foldoutRect = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
        property.isExpanded = EditorGUI.Foldout(foldoutRect, property.isExpanded, label, true);

        if (property.isExpanded)
        {
            EditorGUI.indentLevel++;
            float y = position.y + EditorGUIUtility.singleLineHeight + 2;
            float lineHeight = EditorGUIUtility.singleLineHeight + 2;

            // State
            SerializedProperty stateProp = property.FindPropertyRelative("state");
            EditorGUI.PropertyField(new Rect(position.x, y, position.width, EditorGUIUtility.singleLineHeight), stateProp);
            y += lineHeight;

            // Sprites
            SerializedProperty spritesProp = property.FindPropertyRelative("sprites");
            EditorGUI.PropertyField(new Rect(position.x, y, position.width, EditorGUI.GetPropertyHeight(spritesProp)), spritesProp, true);
            y += EditorGUI.GetPropertyHeight(spritesProp) + 2;

            // Frame Rate
            SerializedProperty frameRateProp = property.FindPropertyRelative("frameRate");
            EditorGUI.PropertyField(new Rect(position.x, y, position.width, EditorGUIUtility.singleLineHeight), frameRateProp);
            y += lineHeight;

            // Loop
            SerializedProperty loopProp = property.FindPropertyRelative("loop");
            EditorGUI.PropertyField(new Rect(position.x, y, position.width, EditorGUIUtility.singleLineHeight), loopProp);
            y += lineHeight;

            // Estimated Duration
            int frameCount = spritesProp.arraySize;
            float frameRate = frameRateProp.floatValue;
            float duration = (frameRate > 0) ? (frameCount / frameRate) : 0f;
            EditorGUI.LabelField(new Rect(position.x, y, position.width, EditorGUIUtility.singleLineHeight), $"Estimated Duration: {duration:F2} seconds");
            y += lineHeight;

            EditorGUI.indentLevel--;
        }

        EditorGUI.EndProperty();
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        float height = EditorGUIUtility.singleLineHeight;
        if (property.isExpanded)
        {
            height += EditorGUIUtility.singleLineHeight * 4 + 8; // state, frameRate, loop, duration
            SerializedProperty spritesProp = property.FindPropertyRelative("sprites");
            height += EditorGUI.GetPropertyHeight(spritesProp) + 2;
        }
        return height;
    }
}
#endif