# Dialogue System Usage

This folder contains the scripts for a simple dialogue system with dual portraits.

## Setup

1.  **Create Dialogue UI:**
    *   Create a Canvas (`GameObject -> UI -> Canvas`), e.g., "DialogueCanvas".
    *   Add a Panel (`GameObject -> UI -> Panel`) as a child (e.g., "DialoguePanel"). This is the background.
    *   Add a UI Text (`GameObject -> UI -> Text` or `Text - TextMeshPro`) as a child of the Panel (e.g., "DialogueText").
    *   Add a UI Image (`GameObject -> UI -> Image`) for the **left** portrait (Player) as a child of the Panel (e.g., "SpeakerPortraitImageLeft").
    *   Add a **second** UI Image (`GameObject -> UI -> Image`) for the **right** portrait (Boss) as a child of the Panel (e.g., "SpeakerPortraitImageRight").
    *   **Important:** Position/anchor "SpeakerPortraitImageLeft" on the left side of your dialogue panel and "SpeakerPortraitImageRight" on the right side using the Rect Tool.
    *   Adjust size, appearance, etc., as needed.

2.  **Create Dialogue Manager GameObject:**
    *   Create an empty GameObject (e.g., "DialogueManager").
    *   Attach the `DialogueManager.cs` script.

3.  **Configure Dialogue Manager:**
    *   Select the DialogueManager GameObject.
    *   In the Inspector:
        *   Drag "DialogueText" to `Dialogue Text`.
        *   Drag "SpeakerPortraitImageLeft" to `Speaker Portrait Left`.
        *   Drag "SpeakerPortraitImageRight" to `Speaker Portrait Right`.
        *   Drag "DialoguePanel" to `Dialogue Box`.
        *   Assign `Sprite` assets for `Player Portrait`, `Boss Anonymous Portrait`, `Boss Full Portrait`.
        *   Adjust `Typing Speed`.

4.  **Setup Audio:**
    *   Add an `AudioSource` component to the DialogueManager GameObject.
    *   Assign this AudioSource to the `Typing Audio Source` field.
    *   Assign a typing `AudioClip` asset to `Typing Sound`.
    *   Ensure the AudioSource has `Play On Awake` **disabled**.

5.  **Create Dialogue Sequences:**
    *   In Project window (e.g., `Assets/Scripts/Dialogue`), right-click -> `Create -> Dialogue -> Dialogue Sequence`.
    *   Name the asset (e.g., "Checkpoint1Dialogue").
    *   Select the asset, add lines to `Lines` list, set `Speaker` and `Text` for each.

## Triggering Dialogue

There are several ways to start a dialogue sequence:

**Method 1: Using `DialogueTriggerZone` (Recommended for testing/simple triggers)**

1.  Create an empty GameObject in your scene where you want the dialogue to trigger (e.g., "Checkpoint1Trigger").
2.  Add a `Collider2D` component (e.g., `BoxCollider2D`, `CircleCollider2D`).
3.  **Important:** Check the `Is Trigger` box on the Collider2D component.
4.  Attach the `DialogueTriggerZone.cs` script to this GameObject.
5.  In the Inspector for `DialogueTriggerZone`:
    *   Drag your `DialogueManager` GameObject to the `Dialogue Manager` field (or leave empty if you want it to find it automatically, though assigning is safer).
    *   Drag the specific `DialogueSequence` asset you want to play (e.g., "Checkpoint1Dialogue") to the `Sequence To Play` field.
    *   Check `Trigger Once` if the dialogue should only happen the first time.
6.  **Important:** Ensure your Player GameObject has the **Tag** set to "Player". The trigger zone checks for this tag.
7.  When the Player enters the trigger collider, the dialogue will start.

**Method 2: Calling from another script**

1.  In your custom script (e.g., `PlayerStateMachine2D`, `InteractionController`), get a reference to the `DialogueManager`.
    *   **Option A (Assign in Inspector - Recommended):**
        ```csharp
        public DialogueManager dialogueManager;
        // In the Unity Editor, drag the DialogueManager GameObject onto this field.
        ```
    *   **Option B (Find Automatically - Use sparingly):**
        ```csharp
        private DialogueManager dialogueManager;

        void Start()
        {
            dialogueManager = FindObjectOfType<DialogueManager>();
            if (dialogueManager == null) 
            {
                Debug.LogError("Could not find DialogueManager!"); 
            }
        }
        ```

2.  Have a reference to the `DialogueSequence` asset you want to play.
    ```csharp
    public DialogueSequence mySpecificDialogue;
    // Assign this in the Inspector by dragging the DialogueSequence asset.
    ```

3.  At the desired point in your code (e.g., inside an interaction function, on a state change, when a button is pressed), call `StartDialogue`:
    ```csharp
    if (dialogueManager != null && mySpecificDialogue != null)
    {
        dialogueManager.StartDialogue(mySpecificDialogue);
        // You might want to pause player movement or other actions here
    }
    ```

## How it Works & UI

*   `DialogueLine.cs`: Data for one line (speaker enum, text string).
*   `SpeakerPortrait` enum (`Player`, `Boss_Anonymous`, `Boss_Full`) determines speaker.
*   `DialogueSequence.cs`: `ScriptableObject` asset holding a `List<DialogueLine>`.
*   `DialogueManager.cs`: Handles UI, timing, audio, and sequence flow.
    *   Uses two `Image` components: `speakerPortraitLeft` and `speakerPortraitRight`.
    *   When displaying a line:
        *   If `Speaker` is `Player`, activates `speakerPortraitLeft` with `playerPortrait` sprite.
        *   If `Speaker` is `Boss_Anonymous` or `Boss_Full`, activates `speakerPortraitRight` with the corresponding boss sprite.
        *   Disables the inactive portrait.
*   `DialogueTriggerZone.cs`: Simple component using `OnTriggerEnter2D` to start a sequence via the `DialogueManager`.
*   Input (e.g., mouse click in `DialogueManager`'s `Update`) calls `DisplayNextLine` to advance text or move to the next line. 