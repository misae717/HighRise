
using UnityEngine;

public enum SpeakerPortrait {
    Player,
    Boss_Anonymous,
    Boss_Full
}

[System.Serializable]
public class DialogueLine
{
    public SpeakerPortrait speaker;
    [TextArea(3, 10)]
    public string text;
}
