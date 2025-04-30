using UnityEngine;
using System.Collections; // Needed for Coroutines

#if UNITY_EDITOR
using UnityEditor;
#endif

// Basic structure for the Slash Effect Animator
[RequireComponent(typeof(SpriteRenderer))]
public class SlashEffectAnimator : MonoBehaviour
{
    // Enum to explicitly define the animation type
    public enum AttackDirectionType { ForwardRight, ForwardLeft, Up, Down }

    [System.Serializable]
    public class DirectionalAnimation
    {
        public AttackDirectionType animationType = AttackDirectionType.ForwardRight;
        public Sprite[] sprites;
        public float frameRate = 15f;
        public Vector2 offset = Vector2.zero; // Offset relative to this GameObject's position
        public float rotation = 0f;      // Rotation to apply
        public Vector2 scale = Vector2.one;     // Scale to apply
        // Add flip options? Maybe handled by rotation/scale.

#if UNITY_EDITOR
        // Store original state for preview revert
        [System.NonSerialized] public bool isPreviewing = false;
        [System.NonSerialized] public Sprite originalSprite;
        [System.NonSerialized] public Vector3 originalLocalPos;
        [System.NonSerialized] public Quaternion originalLocalRot;
        [System.NonSerialized] public Vector3 originalLocalScale;
        [System.NonSerialized] public bool originalEnabledState;
#endif
    }

    public DirectionalAnimation[] directionalAnimations;
    private SpriteRenderer spriteRenderer;
    private Coroutine activeAnimationCoroutine;

    void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        // Start disabled
        spriteRenderer.enabled = false;
    }

    public void PlaySlash(Vector2 attackDirection)
    {
        // Ensure preview state is reverted if playing starts
        #if UNITY_EDITOR
          RevertAllPreviews();
        #endif

        // Determine the required animation type from the attack direction vector
        AttackDirectionType requiredType = GetAnimationTypeFromVector(attackDirection);

        // Find the animation explicitly marked with the required type
        DirectionalAnimation animToPlay = FindAnimationByType(requiredType);

        // If ForwardLeft wasn't found, try using ForwardRight (we might flip it via scale later)
        if (animToPlay == null && requiredType == AttackDirectionType.ForwardLeft)
        {
            // Debug.Log("ForwardLeft anim not found, trying ForwardRight...");
            animToPlay = FindAnimationByType(AttackDirectionType.ForwardRight);
        }

        if (animToPlay != null && animToPlay.sprites != null && animToPlay.sprites.Length > 0)
        {
            // Stop any previous slash animation
            if (activeAnimationCoroutine != null)
            {
                StopCoroutine(activeAnimationCoroutine);
            }
            // Start the new one
            activeAnimationCoroutine = StartCoroutine(AnimateSlash(animToPlay, requiredType));
        }
        else
        {
            Debug.LogWarning($"No slash animation found for direction: {attackDirection} (Required Type: {requiredType})");
            spriteRenderer.enabled = false; // Ensure it's hidden if no anim found
        }
    }

    private AttackDirectionType GetAnimationTypeFromVector(Vector2 direction)
    {
        // Normalize just in case
        Vector2 dir = direction.normalized;

        if (Vector2.Dot(dir, Vector2.up) > 0.8f) return AttackDirectionType.Up;
        if (Vector2.Dot(dir, Vector2.down) > 0.8f) return AttackDirectionType.Down;
        if (Vector2.Dot(dir, Vector2.left) > 0.8f) return AttackDirectionType.ForwardLeft;
        // Default to ForwardRight if none of the above match strongly
        return AttackDirectionType.ForwardRight;
    }

    // Change to public so the Editor script can call it
    public DirectionalAnimation FindAnimationByType(AttackDirectionType typeToFind)
    {
        foreach (var anim in directionalAnimations)
        {
            if (anim.animationType == typeToFind) return anim;
        }
        return null; // Not found
    }

    private IEnumerator AnimateSlash(DirectionalAnimation anim, AttackDirectionType actualDirectionType)
    {
        // Apply offset, rotation, scale
        transform.localPosition = anim.offset;
        transform.localRotation = Quaternion.Euler(0, 0, anim.rotation);

        // Handle potential mirroring for ForwardLeft if using ForwardRight animation
        Vector2 finalScale = anim.scale;
        if (actualDirectionType == AttackDirectionType.ForwardLeft && anim.animationType == AttackDirectionType.ForwardRight)
        {
            finalScale.x *= -1; // Flip the scale horizontally
        }
        transform.localScale = new Vector3(finalScale.x, finalScale.y, 1f);

        spriteRenderer.enabled = true;
        float delay = (anim.frameRate > 0) ? (1f / anim.frameRate) : float.MaxValue; // Prevent division by zero
        int frame = 0;

        while (frame < anim.sprites.Length)
        {
            if (anim.sprites[frame] == null) {
                Debug.LogWarning($"Missing sprite at frame {frame} for {anim.animationType}");
                frame++; // Skip null sprite
                continue;
            }
            spriteRenderer.sprite = anim.sprites[frame];
            frame++;
            // If frameRate is <= 0, play instantly and break
            if (delay == float.MaxValue) break;
            yield return new WaitForSeconds(delay);
        }

        // Animation finished
        spriteRenderer.enabled = false;
        activeAnimationCoroutine = null;
    }

#if UNITY_EDITOR
    // Called by the custom editor to revert any active previews
    public void RevertAllPreviews() {
        if (directionalAnimations == null) return;
        // Ensure renderer is assigned for editor calls
        if (spriteRenderer == null) spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer == null) {
            Debug.LogError("SpriteRenderer missing on SlashEffectAnimator GameObject!");
            return; // Can't revert if no renderer
        }

        foreach (var anim in directionalAnimations) {
            if (anim.isPreviewing) {
                RevertPreview(anim);
            }
        }
    }

    private void RevertPreview(DirectionalAnimation anim) {
        if (!anim.isPreviewing) return;

        // Ensure renderer is assigned for editor calls
        if (spriteRenderer == null) spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer == null) {
             Debug.LogError("SpriteRenderer missing on SlashEffectAnimator GameObject!");
             anim.isPreviewing = false; // Stop trying to preview if no renderer
             return;
        }

         spriteRenderer.sprite = anim.originalSprite;
         transform.localPosition = anim.originalLocalPos;
         transform.localRotation = anim.originalLocalRot;
         transform.localScale = anim.originalLocalScale;
         spriteRenderer.enabled = anim.originalEnabledState;
         anim.isPreviewing = false;
         // Mark scene as dirty maybe?
         // EditorUtility.SetDirty(this);
         // SceneView.RepaintAll();
    }
#endif
}

#if UNITY_EDITOR
[CustomEditor(typeof(SlashEffectAnimator))]
public class SlashEffectAnimatorEditor : Editor
{
    private SlashEffectAnimator targetAnimator;

    private void OnEnable() {
        targetAnimator = (SlashEffectAnimator)target;
        // Ensure previews are reverted when deselected or editor closes
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
    }

    private void OnDisable() {
         if (targetAnimator != null) targetAnimator.RevertAllPreviews();
         EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
    }

    // Revert previews when entering/exiting play mode
    private void OnPlayModeStateChanged(PlayModeStateChange state) {
        if (targetAnimator != null) targetAnimator.RevertAllPreviews();
    }

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Animation Preview", EditorStyles.boldLabel);

        if (targetAnimator.directionalAnimations == null) return;

        // Ensure SpriteRenderer reference is available for preview
        SpriteRenderer previewRenderer = targetAnimator.GetComponent<SpriteRenderer>();
        if (previewRenderer == null) {
             EditorGUILayout.HelpBox("SpriteRenderer component required for preview.", MessageType.Warning);
             return;
        }

        for (int i = 0; i < targetAnimator.directionalAnimations.Length; i++)
        {
            SlashEffectAnimator.DirectionalAnimation anim = targetAnimator.directionalAnimations[i];
            if (anim == null) continue;

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"Preview: {anim.animationType}", GUILayout.Width(150));

            if (anim.isPreviewing) {
                if (GUILayout.Button("Revert Preview")) {
                    targetAnimator.SendMessage("RevertPreview", anim, SendMessageOptions.RequireReceiver);
                    SceneView.RepaintAll(); // Update scene view
                }
            } else {
                if (GUILayout.Button("Show Static Preview")) {
                    // Revert any other previews first
                    targetAnimator.RevertAllPreviews();

                    // Store original state
                    anim.originalSprite = previewRenderer.sprite;
                    anim.originalLocalPos = targetAnimator.transform.localPosition;
                    anim.originalLocalRot = targetAnimator.transform.localRotation;
                    anim.originalLocalScale = targetAnimator.transform.localScale;
                    anim.originalEnabledState = previewRenderer.enabled;
                    anim.isPreviewing = true;

                    // Apply preview state (first frame)
                    previewRenderer.sprite = (anim.sprites != null && anim.sprites.Length > 0) ? anim.sprites[0] : null;
                    targetAnimator.transform.localPosition = anim.offset;
                    targetAnimator.transform.localRotation = Quaternion.Euler(0, 0, anim.rotation);

                    // Handle potential mirroring for left preview
                    Vector2 finalScale = anim.scale;
                    if (anim.animationType == SlashEffectAnimator.AttackDirectionType.ForwardLeft) {
                        // Check if there's an actual ForwardLeft anim defined, or if we should mirror Right
                        bool hasExplicitLeft = false;
                        foreach(var a in targetAnimator.directionalAnimations) {
                            if (a.animationType == SlashEffectAnimator.AttackDirectionType.ForwardLeft) {
                                hasExplicitLeft = true;
                                break;
                            }
                        }
                        // If no explicit Left animation, mirror the scale based on the Right anim settings
                        if (!hasExplicitLeft) {
                             SlashEffectAnimator.DirectionalAnimation rightAnim = targetAnimator.FindAnimationByType(SlashEffectAnimator.AttackDirectionType.ForwardRight);
                             if(rightAnim != null) finalScale = rightAnim.scale; // Use right scale
                             finalScale.x *= -1; // Flip it
                        }
                    }
                    targetAnimator.transform.localScale = new Vector3(finalScale.x, finalScale.y, 1f);
                    previewRenderer.enabled = true;

                    EditorUtility.SetDirty(targetAnimator); // Mark for scene save
                    SceneView.RepaintAll(); // Update scene view
                }
            }
            EditorGUILayout.EndHorizontal();
        }
         // Button to revert all previews at once
         if (GUILayout.Button("Revert All Previews"))
         {
             targetAnimator.RevertAllPreviews();
             SceneView.RepaintAll();
         }
    }

    // Draw Gizmos in Scene View
    void OnSceneGUI() {
        if (targetAnimator == null || targetAnimator.directionalAnimations == null) return;

        Transform effectTransform = targetAnimator.transform;

        foreach (var anim in targetAnimator.directionalAnimations) {
            if (anim == null || anim.sprites == null || anim.sprites.Length == 0 || anim.sprites[0] == null) continue;

            // Get sprite size for accurate gizmo bounds
            Sprite firstSprite = anim.sprites[0];
            Vector2 spriteSize = firstSprite.rect.size / firstSprite.pixelsPerUnit;

            // Calculate final transform for the gizmo
            Vector3 position = effectTransform.TransformPoint(anim.offset);
            Quaternion rotation = effectTransform.rotation * Quaternion.Euler(0, 0, anim.rotation);
            Vector3 scale = Vector3.Scale(effectTransform.lossyScale, new Vector3(anim.scale.x, anim.scale.y, 1f));

            // Handle potential mirroring for ForwardLeft gizmo
             Vector2 actualScale = anim.scale;
             if (anim.animationType == SlashEffectAnimator.AttackDirectionType.ForwardLeft)
             {
                 bool hasExplicitLeft = false;
                 foreach(var a in targetAnimator.directionalAnimations) {
                     if (a.animationType == SlashEffectAnimator.AttackDirectionType.ForwardLeft) {
                         hasExplicitLeft = true;
                         break;
                     }
                 }
                 if (!hasExplicitLeft) {
                      SlashEffectAnimator.DirectionalAnimation rightAnim = targetAnimator.FindAnimationByType(SlashEffectAnimator.AttackDirectionType.ForwardRight);
                      if(rightAnim != null) actualScale = rightAnim.scale; // Base on right scale if mirroring
                      actualScale.x *= -1;
                      scale = Vector3.Scale(effectTransform.lossyScale, new Vector3(actualScale.x, actualScale.y, 1f));
                 }
             }

            // Set Gizmo color based on type
            switch (anim.animationType) {
                case SlashEffectAnimator.AttackDirectionType.ForwardRight:
                case SlashEffectAnimator.AttackDirectionType.ForwardLeft:
                    Handles.color = Color.cyan;
                    break;
                case SlashEffectAnimator.AttackDirectionType.Up:
                case SlashEffectAnimator.AttackDirectionType.Down:
                    Handles.color = Color.magenta;
                    break;
                default:
                    Handles.color = Color.white;
                    break;
            }

            // Draw the wire cube gizmo using Handles for rotation
            Matrix4x4 matrixBackup = Handles.matrix;
            Handles.matrix = Matrix4x4.TRS(position, rotation, scale);
            // Use sprite size for the cube dimensions
            Handles.DrawWireCube(Vector3.zero, spriteSize);
            Handles.matrix = matrixBackup;
        }
    }
}
#endif 