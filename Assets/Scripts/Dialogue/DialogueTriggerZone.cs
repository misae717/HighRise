
using UnityEngine;

[RequireComponent(typeof(Collider2D))] // Ensure a Collider2D is attached
public class DialogueTriggerZone : MonoBehaviour
{
    [Tooltip("The Dialogue Manager instance in the scene.")]
    public DialogueManager dialogueManager;

    [Tooltip("The Dialogue Sequence asset to play when triggered.")]
    public DialogueSequence sequenceToPlay;

    [Tooltip("If true, the dialogue will only trigger the first time the player enters.")]
    public bool triggerOnce = true;

    private bool hasTriggered = false;

    private void Awake()
    {
        // Disable the Collider component visually in the scene view if it's just a trigger
        Collider2D col = GetComponent<Collider2D>();
        if (col && col.isTrigger)
        {
            // Optionally hide the collider gizmo if desired, though this needs editor code.
            // For runtime, ensure it *is* a trigger.
            if (!col.isTrigger)
            {
                Debug.LogWarning($"Collider on {gameObject.name} is not set to 'Is Trigger'. DialogueTriggerZone requires a trigger collider.", this);
            }
        }
        else if (!col)
        {
             Debug.LogError($"No Collider2D found on {gameObject.name}. DialogueTriggerZone requires a Collider2D.", this);
        }

        // Attempt to find DialogueManager if not assigned
        if (dialogueManager == null)
        {
            dialogueManager = FindObjectOfType<DialogueManager>();
            if (dialogueManager == null)
            {
                Debug.LogError("DialogueTriggerZone could not find a DialogueManager in the scene!", this);
            }
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        // Check if the entering object is tagged as "Player"
        if (other.CompareTag("Player"))
        {
            // Check if we should trigger (either first time or always)
            if (!triggerOnce || !hasTriggered)
            {
                // Check if manager and sequence are assigned
                if (dialogueManager != null && sequenceToPlay != null)
                {
                    dialogueManager.StartDialogue(sequenceToPlay);
                    hasTriggered = true; // Mark as triggered
                }
                else
                {
                    if(dialogueManager == null) Debug.LogWarning($"DialogueTriggerZone on {gameObject.name} is missing Dialogue Manager reference.", this);
                    if(sequenceToPlay == null) Debug.LogWarning($"DialogueTriggerZone on {gameObject.name} is missing Dialogue Sequence reference.", this);
                }
            }
        }
    }
}
