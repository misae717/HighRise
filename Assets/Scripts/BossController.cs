using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Require components the boss will need
[RequireComponent(typeof(Rigidbody2D), typeof(Collider2D), typeof(BossSpriteAnimator))] // Split into two attributes
[RequireComponent(typeof(AudioSource))] // Added AudioSource requirement separately
public class BossController : MonoBehaviour
{
    // --- Restored Enum Definition ---
    public enum BossState { Idle, Hovering, Dialogue, Vulnerable, Shielding, Attacking, Death }

    [Header("State")]
    public BossState currentState = BossState.Idle; // Start state
    private BossState previousState;

    // --- Restored Variables ---
    [Header("Health")]
    public int maxHealth = 300;
    public int currentHealth;
    private int hitsTakenThisCycle = 0;
    public int hitsPerCycle = 3; // Hits needed to trigger shield

    [Header("Movement")]
    public float hoverSpeed = 1.0f;
    public float hoverAmplitude = 0.5f;
    private Vector3 startPosition;

    [Header("Combat")]
    public float shieldDuration = 10.0f; // How long the shield phase lasts
    public GameObject tentacleAttackPrefab; // Assign your Tentacle Prefab here
    public float tentacleSpawnRate = 0.5f; // How often tentacles spawn (seconds)
    public float tentacleSpawnRadius = 5.0f; // How far from the player tentacles can spawn
    public LayerMask groundLayer; // LayerMask for ground detection
    public float tentacleSpawnMaxRaycastDistance = 20f; // How far down to check for ground
    [Tooltip("Prefab for the shield visual effect.")]
    public GameObject shieldPrefab; // Renamed from shieldVisual, expecting a prefab
    private float shieldTimer;
    private float tentacleSpawnTimer;
    private bool isInvulnerable = false;

    [Header("Interaction")]
    // public Collider2D playerDetectionTrigger; // REMOVED - Using distance check now
    public Transform playerTransform; // Reference to the player
    public float detectionRadius = 15f; // Distance to trigger the fight
    // Dialogue identifiers (adjust as needed for your DialogueManager)
    public string initialDialogueID = "BossIntro";
    public string postShieldDialogueID = "BossPhaseChange";

    [Header("Dialogue Integration")] // Added header
    public DialogueManager dialogueManager; // Assign Dialogue Manager GameObject in Inspector
    public DialogueDatabase dialogueDatabase; // Assign your Dialogue Database asset

    [Header("Death")]
    public float deathAnimationDuration = 2.0f; // Match your death animation length
    [Tooltip("Prefab containing explosion animation (e.g., using SpriteAnimator)")]
    public GameObject explosionPrefab; // Assign Explosion Prefab (Sprite/Particle)
    public AudioClip explosionSound;
    [Range(0f, 1f)] // Add slider for volume
    public float explosionVolume = 1.0f; // Volume control
    public int explosionCount = 5;
    public float explosionInterval = 0.2f;

    // Component References
    private Rigidbody2D rb;
    private BossSpriteAnimator bossSpriteAnimator;
    private Collider2D mainCollider; // The boss's main physical collider
    private AudioSource audioSource; // Added AudioSource reference
    private GameObject currentShieldInstance; // To store the instantiated shield

    // --- End Restored Variables ---

    private bool isDialogueActive = false;

    void Awake()
    {
        // --- Restored Awake Content ---
        rb = GetComponent<Rigidbody2D>();
        bossSpriteAnimator = GetComponent<BossSpriteAnimator>();
        audioSource = GetComponent<AudioSource>(); // Get AudioSource
        // Try to find the main collider automatically if not assigned (e.g., if trigger is a child)
        Collider2D[] colliders = GetComponents<Collider2D>();
        foreach(Collider2D col in colliders) {
            if (!col.isTrigger) {
                mainCollider = col;
                break;
            }
        }
        if (mainCollider == null && colliders.Length > 0) mainCollider = colliders[0]; // Fallback

        // Ensure player detection trigger is set as trigger - REMOVED
        // if (playerDetectionTrigger != null) playerDetectionTrigger.isTrigger = true;
        // else Debug.LogWarning("BossController: Player Detection Trigger not assigned!");

        // Find player if not assigned
        if (playerTransform == null)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                playerTransform = player.transform;
            }
            else
            {
                Debug.LogError("BossController cannot find Player Transform! Tag your player object with 'Player'.");
            }
        }

        // Setup Rigidbody for hovering
        rb.isKinematic = true;
        rb.gravityScale = 0;
        rb.constraints = RigidbodyConstraints2D.FreezeRotation;

        currentHealth = maxHealth;
        startPosition = transform.position;
        previousState = currentState; // Initialize previous state
         // --- End Restored Awake Content ---

        // --- Dialogue Manager Null Checks (kept from previous edit) ---
        if (dialogueManager == null)
        {
            Debug.LogError("BossController: Dialogue Manager not assigned in Inspector!", this);
            dialogueManager = FindObjectOfType<DialogueManager>();
            if (dialogueManager == null) {
                 Debug.LogError("BossController: Could not find Dialogue Manager in scene! Dialogue will not work.");
            }
        }
        if (dialogueDatabase == null)
        {
            Debug.LogError("BossController: Dialogue Database not assigned in Inspector!", this);
        }
        // --- End Null Checks ---

        // Disable Shield Visual initially - REMOVED (Now instantiated on demand)
        // if (shieldVisual != null) { ... }
        if (shieldPrefab == null) {
            Debug.LogWarning("BossController: Shield Prefab not assigned.");
        }

        UpdateAnimation(); // Set initial animation
    }

    // --- Restored Update ---
    void Update()
    {
        if (playerTransform == null && currentState != BossState.Idle && currentState != BossState.Death) {
             Debug.LogWarning("BossController: Player transform missing, returning to Idle.");
             currentState = BossState.Idle; // Can't operate without player ref usually
        }

        // Only run state logic if dialogue isn't active (unless in Dialogue or Death state)
        if (!isDialogueActive || currentState == BossState.Dialogue || currentState == BossState.Death)
        {
             RunStateMachine();
        }

        // Update animations if state changed
        if (previousState != currentState)
        {
            UpdateAnimation();
            previousState = currentState;
        }
    }
    // --- End Restored Update ---

    // --- Restored RunStateMachine ---
    void RunStateMachine()
    {
        // Main State Machine Logic
        switch (currentState)
        {
            case BossState.Idle:
                IdleState();
                break;
            case BossState.Hovering:
                HoveringState();
                break;
            case BossState.Dialogue:
                // Logic handled by StartDialogue/FinishDialogue methods
                break;
            case BossState.Vulnerable:
                VulnerableState();
                break;
            case BossState.Shielding:
                ShieldingState(); // Setup, then transitions to Attacking
                break;
            case BossState.Attacking:
                AttackingState(); // Tentacle spawning
                break;
            case BossState.Death:
                // Death logic runs once, maybe triggered from TakeDamage
                break;
        }
    }
    // --- End Restored RunStateMachine ---

    // --- Restored State Implementations ---
    void IdleState()
    {
        // Check distance to player to start the fight
        CheckPlayerDistanceAndStartFight();
    }

    void HoveringState()
    {
        // Simple up/down hover movement
        float yOffset = Mathf.Sin(Time.time * hoverSpeed) * hoverAmplitude;
        rb.MovePosition(startPosition + new Vector3(0, yOffset, 0));

        // Check distance to player to start the fight
        CheckPlayerDistanceAndStartFight();
    }

    // ADDED Helper method for distance check
    void CheckPlayerDistanceAndStartFight()
    {
        // Only check if in Idle or Hovering state
        if (currentState != BossState.Idle && currentState != BossState.Hovering) return;
        
        if (playerTransform != null) // Ensure player exists
        {
            float distanceToPlayer = Vector2.Distance(transform.position, playerTransform.position);
            if (distanceToPlayer <= detectionRadius)
            {
                 Debug.LogWarning($"DISTANCE SUCCESS: Player within {detectionRadius} units. Starting dialogue...");
                 StartDialogue(initialDialogueID, BossState.Vulnerable);
                 // No trigger to disable, state change prevents re-triggering immediately
            }
        }
    }

    void VulnerableState()
    {
        Debug.LogWarning("STATE CHECK: Entered VulnerableState."); // ADDED log
        isInvulnerable = false;
        HoveringState();
    }

    void ShieldingState()
    {
        Debug.LogWarning("Boss Shielding!"); // Changed to Warning
        isInvulnerable = true;
        shieldTimer = shieldDuration;
        tentacleSpawnTimer = 0; // Reset spawn timer for the first spawn
        
        // --- Instantiate Shield Prefab --- 
        if(shieldPrefab != null && currentShieldInstance == null) // Only instantiate if prefab exists and not already instantiated
        {
            currentShieldInstance = Instantiate(shieldPrefab, transform.position, Quaternion.identity, transform); // Instantiate as child
            currentShieldInstance.transform.localPosition = Vector3.zero; // Ensure local position is 0,0,0
             Debug.Log("Shield Instantiated.");
        }
        // --- End Instantiate Shield ---
        
        currentState = BossState.Attacking;
    }

    void AttackingState()
    {
        isInvulnerable = true;
        HoveringState(); // Keep hovering

        shieldTimer -= Time.deltaTime;
        tentacleSpawnTimer -= Time.deltaTime;

        if (shieldTimer <= 0)
        {
            Debug.LogWarning("Shield Down!"); // Changed to Warning
            isInvulnerable = false;
            
            // --- Destroy Shield Instance --- 
            if(currentShieldInstance != null)
            {
                Destroy(currentShieldInstance);
                currentShieldInstance = null; 
            }
            // --- End Destroy Shield ---
            
            StartDialogue(postShieldDialogueID, BossState.Vulnerable);
        }
        else
        {
            if (tentacleSpawnTimer <= 0)
            {
                SpawnTentacle();
                tentacleSpawnTimer = tentacleSpawnRate;
            }
        }
    }

     void StartDeathSequence() {
        if (currentState == BossState.Death) return; // Already dying

        Debug.LogWarning("Boss Defeated! Starting Death Sequence."); // Changed to Warning
        currentState = BossState.Death;
        isInvulnerable = true;
        rb.velocity = Vector2.zero;
        rb.isKinematic = true;
        StopAllCoroutines(); // Stop any active timers/spawners like dialogue waits

        // Disable visuals/colliders
        if(mainCollider) mainCollider.enabled = false;
        // if(playerDetectionTrigger) playerDetectionTrigger.enabled = false;
        if(shieldPrefab != null) {
            Destroy(currentShieldInstance);
            currentShieldInstance = null;
        }

        // Start Explosion Coroutine
        StartCoroutine(DeathExplosionEffect());

        // UpdateAnimation() called by state change in Update() for death anim

        // Destroy the main boss object after animations/effects
        Destroy(gameObject, deathAnimationDuration);
    }
    // --- End Restored State Implementations ---

    // --- Restored Helper Methods ---
     void SpawnTentacle()
    {
        if (tentacleAttackPrefab == null || playerTransform == null) {
             Debug.LogWarning("Tentacle Prefab or Player Transform missing, cannot spawn tentacle.");
             return;
        }
        if (groundLayer == 0) {
            Debug.LogWarning("Ground LayerMask not set in BossController Inspector. Cannot guarantee ground spawning.");
             // Optionally fallback to old spawning method or just return
             return; 
        }

        // 1. Calculate random X position near the player
        float randomX = playerTransform.position.x + Random.Range(-tentacleSpawnRadius, tentacleSpawnRadius);
        // 2. Start raycast from high above that X position
        Vector2 raycastStart = new Vector2(randomX, transform.position.y + tentacleSpawnMaxRaycastDistance * 0.5f); // Start above boss's height
        
        // 3. Raycast downwards to find the ground
        RaycastHit2D hit = Physics2D.Raycast(raycastStart, Vector2.down, tentacleSpawnMaxRaycastDistance, groundLayer);

        // 4. If ground is hit within range, spawn the tentacle there
        if (hit.collider != null)
        {
            Vector2 spawnPosition = hit.point;
            Debug.Log($"Spawning Tentacle at ground position: {spawnPosition}");
            Instantiate(tentacleAttackPrefab, spawnPosition, Quaternion.identity);
        }
        else {
            Debug.LogWarning($"Tentacle spawn failed: No ground found below ({raycastStart}) within {tentacleSpawnMaxRaycastDistance}m on layer {LayerMask.LayerToName(groundLayer)}.");
            // Optional: Try spawning at player's feet or another fallback?
        }
    }

     public void TakeDamage(int amount)
    {
        if (isInvulnerable || currentState == BossState.Death) return;

        // ADDED Log to check state BEFORE checking if vulnerable
        Debug.LogWarning($"DAMAGE CHECK: TakeDamage called. Current state is: {currentState}"); 

        currentHealth -= amount;
        Debug.Log($"Boss took {amount} damage. Health: {currentHealth}/{maxHealth}"); 

        if (currentHealth <= 0)
        {
            StartDeathSequence();
        }
        else
        {
            // Check if vulnerable AFTER confirming damage can be taken
            if (currentState == BossState.Vulnerable)
            {
                hitsTakenThisCycle++;
                Debug.Log($"DAMAGE CHECK: Hit counted in Vulnerable state. Hits this cycle: {hitsTakenThisCycle}/{hitsPerCycle}"); // Modified log
                if (hitsTakenThisCycle >= hitsPerCycle)
                {
                    hitsTakenThisCycle = 0; 
                    currentState = BossState.Shielding;
                    Debug.LogWarning("DAMAGE CHECK: Hit threshold reached. Transitioning to Shielding."); // ADDED log
                }
            }
            else
            {
                Debug.LogWarning($"DAMAGE CHECK: Damage taken, but boss state is {currentState}, not Vulnerable. Hit not counted for cycle."); // ADDED log
            }
        }
    }

    void UpdateAnimation()
    {
         if (bossSpriteAnimator != null)
         {
              bossSpriteAnimator.ChangeAnimation(currentState);
         }
         else {
            Debug.LogWarning("BossController cannot find BossSpriteAnimator component!");
         }
    }
    // --- End Restored Helper Methods ---

    // --- Restored Trigger Detection ---
    // void OnTriggerEnter2D(Collider2D other)
    // { ... }
    // --- End Restored Trigger Detection ---

    // --- Dialogue Integration Methods (Kept from previous edit) ---
    void StartDialogue(string dialogueId, BossState stateAfterDialogue)
    {
        if (isDialogueActive || dialogueManager == null || dialogueDatabase == null)
        {
            Debug.LogWarning("Cannot start dialogue. Already active, manager/database missing, or ID invalid.");
            if(!isDialogueActive) FinishDialogue(stateAfterDialogue);
            return;
        }

        Debug.LogWarning($"START DIALOGUE: Attempting sequence '{dialogueId}'. Current state: {currentState}"); // ADDED log

        DialogueSequence sequence = dialogueDatabase.GetSequence(dialogueId);
        if (sequence == null)
        {
            Debug.LogError($"START DIALOGUE FAIL: Sequence '{dialogueId}' not found! Skipping dialogue.");
             FinishDialogue(stateAfterDialogue); 
            return;
        }

        Debug.LogWarning($"START DIALOGUE: Sequence '{dialogueId}' found. Setting state to Dialogue."); // ADDED log
        isDialogueActive = true;
        currentState = BossState.Dialogue;
        UpdateAnimation(); 

        dialogueManager.StartDialogue(sequence);

        float estimatedDuration = CalculateSequenceDuration(sequence);
        Debug.LogWarning($"START DIALOGUE: Estimated duration {estimatedDuration:F2}s. Starting wait coroutine."); // ADDED log
        StartCoroutine(WaitForDialogueToEnd(estimatedDuration, stateAfterDialogue));
    }

    private IEnumerator WaitForDialogueToEnd(float duration, BossState nextState)
    {
        Debug.LogWarning($"WAIT COROUTINE: Waiting for {duration}s before transitioning to {nextState}."); // ADDED log
        yield return new WaitForSeconds(duration);
        Debug.LogWarning($"WAIT COROUTINE: Wait finished. Checking state..."); // ADDED log
        if (isDialogueActive && currentState == BossState.Dialogue)
        {
             Debug.LogWarning("WAIT COROUTINE: Conditions met, calling FinishDialogue."); // ADDED log
             FinishDialogue(nextState);
        }
        else {
            Debug.LogWarning("WaitForDialogueToEnd finished, but boss was no longer in Dialogue state or dialogue wasn't active.");
        }
    }

    private float CalculateSequenceDuration(DialogueSequence sequence)
    {
        if (sequence == null || sequence.lines.Count == 0) return 0f;
        float totalDuration = 0f;
        float typingSpeed = dialogueManager.typingSpeed > 0 ? dialogueManager.typingSpeed : 0.05f;
        float autoAdvanceDelay = dialogueManager.autoAdvanceDelay;
        foreach (DialogueLine line in sequence.lines)
        {
            totalDuration += line.text.Length * typingSpeed;
            totalDuration += autoAdvanceDelay;
        }
        totalDuration += 0.2f;
        return totalDuration;
    }

    void FinishDialogue(BossState nextState)
    {
        Debug.LogWarning($"FINISH DIALOGUE: Setting state to {nextState}."); // ADDED log
        isDialogueActive = false;
        currentState = nextState;
    }
    // --- End Dialogue Integration Methods ---

    // --- Added Death Explosion Coroutine ---
    private IEnumerator DeathExplosionEffect()
    {
        Debug.Log("Starting death explosions...");
        if (explosionPrefab == null) {
            Debug.LogWarning("Explosion Prefab not assigned.");
            yield break; // Exit if no prefab
        }

        for (int i = 0; i < explosionCount; i++)
        {
            // Spawn explosion at boss position (or slight random offset?)
            Vector2 spawnPos = (Vector2)transform.position + Random.insideUnitCircle * 0.5f; // Small offset
            // Ensure the Explosion Prefab has its own script/animator to handle its lifecycle
            Instantiate(explosionPrefab, spawnPos, Quaternion.identity);

            // Play sound with volume control
            if (audioSource != null && explosionSound != null) {
                // Play slightly overlapping sounds
                audioSource.PlayOneShot(explosionSound, explosionVolume); // Use volume variable
            }

            // Wait for interval
            yield return new WaitForSeconds(explosionInterval);
        }
        Debug.Log("Finished death explosions.");
    }
    // --- End Death Explosion Coroutine ---

    // --- Restored Gizmos (Modified) ---
     void OnDrawGizmosSelected()
    {
        // Visualize distance detection range
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectionRadius);

         // Visualize hover range
        Vector3 currentStartPosition = Application.isPlaying ? startPosition : transform.position;
        Gizmos.color = Color.cyan;
        Vector3 top = currentStartPosition + Vector3.up * hoverAmplitude;
        Vector3 bottom = currentStartPosition + Vector3.down * hoverAmplitude;
        Gizmos.DrawLine(top, bottom);
        Gizmos.DrawWireSphere(top, 0.1f);
        Gizmos.DrawWireSphere(bottom, 0.1f);

        // Visualize tentacle spawn radius around player
        if(playerTransform != null)
        {
             Gizmos.color = Color.red;
             Gizmos.DrawWireSphere(playerTransform.position, tentacleSpawnRadius);
        }
    }
    // --- End Restored Gizmos ---
}