using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;
using TMPro;

public class DialogueManager : MonoBehaviour
{
    [Header("UI Elements")]
    public TextMeshProUGUI dialogueText;           // Text component to display dialogue
    public Image speakerPortrait;       // The SINGLE Image component for the speaker's portrait
    public GameObject dialogueBox;      // The parent GameObject containing the dialogue UI

    [Header("Portrait Config")]
    public RectTransform portraitAnchorLeft; // Assign a RectTransform positioned on the left
    public RectTransform portraitAnchorRight; // Assign a RectTransform positioned on the right
    // Optional: Add rotation if desired
    // public Quaternion portraitRotationLeft = Quaternion.identity;
    // public Quaternion portraitRotationRight = Quaternion.Euler(0, 180, 0); // Example flip

    [Header("Portraits Sprites")]
    public Sprite playerPortraitSprite;     // Assign Player Sprite in Inspector
    public Sprite bossAnonymousSprite;  // Assign Anonymous Boss Sprite in Inspector
    public Sprite bossFullSprite;       // Assign Full Boss Sprite in Inspector

    [Header("Text Streaming")]
    public float typingSpeed = 0.04f; // Time between characters
    [Tooltip("Time to wait after a line finishes typing before automatically advancing.")]
    public float autoAdvanceDelay = 0.5f; // Delay before moving to the next line

    [Header("Audio")]
    public AudioSource typingAudioSource; // Assign an AudioSource component
    public AudioClip typingSound;         // Assign the typing audio clip
    [Tooltip("Base pitch for player voice.")]
    public float playerBasePitch = 1.1f; // Slightly higher
    [Tooltip("Base pitch for boss voice.")]
    public float bossBasePitch = 0.9f;  // Slightly lower
    [Tooltip("How much pitch varies randomly around the base pitch.")]
    public float pitchRandomness = 0.1f;

    private Queue<DialogueLine> lines;
    private Coroutine typingCoroutine;
    private bool isDisplayingLine = false;
    private DialogueLine currentFullLine;
    private float currentBasePitch = 1.0f; // Store the base pitch for the current line

    void Start()
    {
        lines = new Queue<DialogueLine>();
        if (dialogueBox) dialogueBox.SetActive(false);
        if (speakerPortrait) speakerPortrait.enabled = false; // Start with portrait hidden

        // Validate anchors are assigned
        if (portraitAnchorLeft == null || portraitAnchorRight == null)
        {
            Debug.LogError("DialogueManager: Portrait anchor transforms (Left/Right) must be assigned in the Inspector!", this);
        }
    }

    public void StartDialogue(DialogueSequence sequence)
    {
        if (sequence == null || sequence.lines.Count == 0)
        {
            Debug.LogWarning("Tried to start an empty or null dialogue sequence.");
            return;
        }

        // Ensure anchors are assigned before starting
        if (portraitAnchorLeft == null || portraitAnchorRight == null)
        {
             Debug.LogError("Cannot start dialogue, portrait anchors not assigned!", this);
             return;
        }

        if (dialogueBox) dialogueBox.SetActive(true);
        lines.Clear();

        foreach (DialogueLine line in sequence.lines)
        {
            lines.Enqueue(line);
        }

        // Stop any currently running dialogue
        CleanupRunningDialogue();

        DisplayNextLine();
    }

    public void DisplayNextLine()
    {        
        // Ensure previous line's coroutine is stopped before starting next
        if (typingCoroutine != null)
        {
            StopCoroutine(typingCoroutine);
            typingCoroutine = null; // Clear the reference
            isDisplayingLine = false;
            // Also stop sound just in case it was cut off mid-way
             if (typingAudioSource && typingAudioSource.isPlaying) typingAudioSource.Stop();
        }

        if (lines.Count == 0)
        {
            EndDialogue();
            return;
        }

        currentFullLine = lines.Dequeue(); 

        // Ensure portrait exists before trying to use it
        if (speakerPortrait == null)
        {
            Debug.LogError("DialogueManager: Speaker Portrait Image is not assigned!", this);
            EndDialogue(); // Can't show portrait, end dialogue
            return;
        }

        speakerPortrait.enabled = false; // Hide portrait initially for this line
        RectTransform targetAnchor = null;
        Sprite targetSprite = null;
        // Quaternion targetRotation = Quaternion.identity; // Use if implementing rotation

        // Determine base pitch for the current speaker
        switch (currentFullLine.speaker)
        {
            case SpeakerPortrait.Player:
                currentBasePitch = playerBasePitch;
                targetSprite = playerPortraitSprite;
                targetAnchor = portraitAnchorLeft;
                // targetRotation = portraitRotationLeft;
                break;
            case SpeakerPortrait.Boss_Anonymous:
                currentBasePitch = bossBasePitch;
                targetSprite = bossAnonymousSprite;
                targetAnchor = portraitAnchorRight;
                // targetRotation = portraitRotationRight;
                break;
            case SpeakerPortrait.Boss_Full:
                currentBasePitch = bossBasePitch;
                targetSprite = bossFullSprite;
                targetAnchor = portraitAnchorRight;
                // targetRotation = portraitRotationRight;
                break;
            default:
                currentBasePitch = 1.0f; // Default pitch if no specific speaker
                break;
        }

        // Apply position and sprite if a speaker was matched
        if (targetAnchor != null && targetSprite != null)
        {
            speakerPortrait.rectTransform.position = targetAnchor.position;
            // speakerPortrait.rectTransform.rotation = targetRotation; // Uncomment if using rotation
            speakerPortrait.sprite = targetSprite;
            speakerPortrait.enabled = true;
        }
        else
        {
             speakerPortrait.enabled = false; // Ensure it's hidden if no speaker/sprite defined
        }

        // Start typing the line text
        typingCoroutine = StartCoroutine(StreamText(currentFullLine));
    }

    IEnumerator StreamText(DialogueLine line)
    {
        isDisplayingLine = true;
        dialogueText.text = "";
        foreach (char letter in line.text.ToCharArray())
        {
            dialogueText.text += letter;
            // Play typing sound ONLY if the source isn't already playing
            if (typingAudioSource && typingSound && !char.IsWhiteSpace(letter) && !typingAudioSource.isPlaying)
            {
                // Apply pitch variation around the current line's base pitch
                typingAudioSource.pitch = Random.Range(currentBasePitch - pitchRandomness, currentBasePitch + pitchRandomness);
                typingAudioSource.PlayOneShot(typingSound);
            }
            yield return new WaitForSeconds(typingSpeed);
        }
        
        // Finished typing the line
        isDisplayingLine = false;
        typingCoroutine = null; // Coroutine instance is finished

        // Wait for the specified delay
        yield return new WaitForSeconds(autoAdvanceDelay);

        // Automatically advance to the next line if the dialogue hasn't been ended externally
        if (dialogueBox != null && dialogueBox.activeSelf) // Check if dialogue is still active
        {
             DisplayNextLine(); 
        }
    }
    
    private void CleanupRunningDialogue()
    {
        if (typingCoroutine != null)
        {
            StopCoroutine(typingCoroutine);
            typingCoroutine = null;
        }
        if (typingAudioSource && typingAudioSource.isPlaying)
        {
            typingAudioSource.Stop();
        }
        isDisplayingLine = false;
        currentFullLine = null; 
    }

    void EndDialogue()
    {
        if (dialogueBox) dialogueBox.SetActive(false);
        if (speakerPortrait) speakerPortrait.enabled = false;

        CleanupRunningDialogue(); // Stop coroutines, audio, clear state

        if (typingAudioSource) typingAudioSource.pitch = 1f; // Reset pitch to default
        Debug.Log("Dialogue ended.");
    }
} 