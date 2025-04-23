# Player System Documentation

This document outlines the functionality of the core player systems: the Health System and the Sprite Animator.

## Health System (`PlayerStateMachine2D.cs`)

The player has a health system managed within the `PlayerStateMachine2D` script.

### Properties:

*   `maxHealth` (public int): The maximum health the player can have. Configurable in the Inspector.
*   `CurrentHealth` (public int, read-only): The player's current health points. Cannot be set directly from the Inspector, but can be accessed by other scripts.

### Methods:

*   `TakeDamage(int amount)`: Call this method to make the player take damage. 
    *   Subtracts `amount` from `CurrentHealth`.
    *   Clamps health so it cannot go below 0.
    *   If health reaches 0 or less, calls the internal `Die()` method.
    *   Logs the damage taken and current health to the console.
    *   Does nothing if the player is already in the `Death` state or if `amount` is zero or negative.
*   `Heal(int amount)`: Call this method to heal the player.
    *   Adds `amount` to `CurrentHealth`.
    *   Clamps health so it cannot exceed `maxHealth`.
    *   Logs the healing amount and current health to the console.
    *   Does nothing if the player is in the `Death` state or if `amount` is zero or negative.

### Death State:

*   When `CurrentHealth` reaches 0, the player's state (`currentState`) is set to `State.Death`.
*   In the `Death` state:
    *   Input processing and most state transitions stop.
    *   Horizontal velocity is gradually reduced to zero.
    *   Gravity is still applied (via the `ApplyVelocity` method).
    *   The `Death` animation is triggered (via `UpdateAnimationSystems`).
    *   Currently, the player remains in the `Death` state indefinitely. Further logic (like triggering a game over screen, respawning, or destroying the GameObject) should be added either in the `DeathState()` method or managed by a separate game manager script.

## Sprite Animator System (`SpriteAnimator.cs`)

This component handles playing sprite animations based on the player's current state in `PlayerStateMachine2D`.

### Setup:

1.  Add the `SpriteAnimator` component to the same GameObject as the `PlayerStateMachine2D`.
2.  Ensure the GameObject also has a `SpriteRenderer` component. The `SpriteAnimator` will attempt to find it automatically, but you can assign it manually to the `spriteRenderer` field in the Inspector.
3.  Populate the `Animations` array in the Inspector.
    *   Set the `Size` of the array to match the number of player states you want to animate (e.g., Idle, Running, Jumping, Falling, Dashing, Death).
    *   For each element (Animation Info):
        *   Select the corresponding `State` from the dropdown (e.g., `Idle`, `Running`).
        *   Set the desired `Frame Rate` for the animation.
        *   Check the `Loop` box if the animation should repeat.
        *   **Leave the `Sprites` array empty for now.**

### Populating Sprites Automatically:

1.  Make sure you have imported your sprite sheet textures (e.g., `MC IDLE-Sheet.png`) into your Unity project.
2.  Select the imported texture asset in the Project window.
3.  In the Inspector, set `Texture Type` to `Sprite (2D and UI)` and `Sprite Mode` to `Multiple`.
4.  Click the `Sprite Editor` button.
5.  Use the `Slice` menu (e.g., `Automatic`, `Grid By Cell Size`) to divide your sheet into individual sprites. **Make sure the individual sprite names are sequential or alphabetically ordered** (e.g., `MC IDLE-Sheet_0`, `MC IDLE-Sheet_1`, etc.) for correct animation order.
6.  Click `Apply` in the Sprite Editor.
7.  Select your Player GameObject in the Hierarchy.
8.  In the `Sprite Animator` component's Inspector, find the `Sprite Population Utility` section.
9.  For each `Animation Info` entry:
    *   Drag the corresponding **main sprite sheet texture asset** (e.g., `MC IDLE-Sheet.png` from the Project window) onto the `Source Sheet` field for that state.
    *   Click the `Populate Sprites` button next to it.
10. The `Sprites` array for that `Animation Info` will be automatically filled with the sliced sprites from the sheet, ordered by their names.

### How it Works:

*   The `SpriteAnimator` gets the `PlayerStateMachine2D` component on `Start`.
*   In its `Update` loop, it checks if the `playerStateMachine.currentState` has changed.
*   If the state changed, it finds the corresponding `AnimationInfo` in its `animations` array.
*   It then resets the animation timer and frame counter for the new animation.
*   It continuously updates the `spriteRenderer.sprite` based on the `frameRate` and `sprites` defined in the current `AnimationInfo`.
*   It handles looping or holding the last frame for non-looping animations. 