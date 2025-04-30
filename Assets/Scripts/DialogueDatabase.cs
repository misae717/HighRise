using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// ScriptableObject to store and retrieve dialogue sequences.
/// Assumes DialogueLine and DialogueSequence are defined elsewhere.
/// Create instances via Assets -> Create -> Dialogue -> Dialogue Database.
/// </summary>
[CreateAssetMenu(fileName = "NewDialogueDatabase", menuName = "Dialogue/Dialogue Database")]
public class DialogueDatabase : ScriptableObject
{
    // Reference the existing DialogueSequence class
    public List<DialogueSequence> sequences = new List<DialogueSequence>();

    /// <summary>
    /// Finds and returns a dialogue sequence by its ID.
    /// Assumes DialogueSequence has a string property named 'sequenceID'.
    /// </summary>
    /// <param name="id">The unique identifier for the sequence.</param>
    /// <returns>The found DialogueSequence, or null if not found.</returns>
    public DialogueSequence GetSequence(string id)
    {
        // Ensure the DialogueSequence class actually has a 'sequenceID' field or property to compare against.
        // If the field name is different in the original definition, update the comparison below.
        foreach (DialogueSequence sequence in sequences)
        {
            // If DialogueSequence doesn't have sequenceID, this check needs to be adapted.
            // For example, maybe you compare sequence.name or another unique identifier.
            if (sequence.sequenceID == id) 
            {
                return sequence;
            }
        }
        Debug.LogWarning($"Dialogue sequence with ID '{id}' not found in database {this.name}.");
        return null;
    }
}

// Note: Ensure the original definitions of DialogueLine and DialogueSequence are accessible
// (e.g., not nested privately within another class) and that they have the necessary fields
// (like 'sequenceID' on DialogueSequence and 'text'/'speaker' on DialogueLine) 
// that BossController and DialogueManager rely on.

// You might also need the SpeakerPortrait enum if it's not globally accessible
// If DialogueManager doesn't define it publicly, you might need to define it here or elsewhere:
/*
public enum SpeakerPortrait
{
    Player,
    Boss_Anonymous,
    Boss_Full
    // Add other speakers as needed
}
*/
// Check DialogueManager.cs first. If it's defined publicly there, you don't need to redefine it. 