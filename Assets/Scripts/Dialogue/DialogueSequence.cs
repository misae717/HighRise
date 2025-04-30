using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "New Dialogue Sequence", menuName = "Dialogue/Dialogue Sequence")]
public class DialogueSequence : ScriptableObject
{
    [Tooltip("Unique identifier for this sequence (e.g., BossIntro, PlayerResponse1)")]
    public string sequenceID;

    public List<DialogueLine> lines = new List<DialogueLine>();
}
