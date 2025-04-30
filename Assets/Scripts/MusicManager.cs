using UnityEngine;
using UnityEngine.SceneManagement; // Added for scene management

public class MusicManager : MonoBehaviour
{
    public AudioClip backgroundMusic; // Assign your music file in the inspector
    
    [Range(0f, 1f)]
    public float musicVolume = 0.5f;
    
    private AudioSource audioSource;
    private static MusicManager instance;
    
    void Awake()
    {
        // Singleton pattern - ensures only one music manager exists
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(gameObject); // Keeps the music playing between scenes
            
            // Set up the audio source
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.clip = backgroundMusic;
            audioSource.volume = musicVolume;
            audioSource.loop = true; // Music will loop continuously
            
            if (backgroundMusic != null)
            {
                audioSource.Play();
                Debug.Log("Started playing background music");
            }
            else
            {
                Debug.LogWarning("No background music assigned to MusicManager!");
            }
            
            // Subscribe to scene change events
            SceneManager.sceneLoaded += OnSceneLoaded;
        }
        else
        {
            // If a MusicManager already exists, destroy this one
            Destroy(gameObject);
        }
    }
    
    // Called when a scene is loaded
    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // Ensure music is still playing after scene changes
        if (audioSource != null && backgroundMusic != null)
        {
            if (!audioSource.isPlaying)
            {
                audioSource.Play();
                Debug.Log("Resumed music after scene change");
            }
        }
    }
    
    void OnDestroy()
    {
        // Unsubscribe from the event when this object is destroyed
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }
    
    // Public method to change the volume
    public void SetVolume(float volume)
    {
        musicVolume = Mathf.Clamp01(volume); // Ensure volume is between 0 and 1
        if (audioSource != null)
        {
            audioSource.volume = musicVolume;
        }
    }
    
    // Method to pause/resume music (can be called from other scripts if needed)
    public void ToggleMusic(bool play)
    {
        if (audioSource != null)
        {
            if (play && !audioSource.isPlaying)
            {
                audioSource.Play();
            }
            else if (!play && audioSource.isPlaying)
            {
                audioSource.Pause();
            }
        }
    }
}