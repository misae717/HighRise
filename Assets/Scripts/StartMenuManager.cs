using UnityEngine;
using UnityEngine.SceneManagement; // Required for scene management

public class StartMenuManager : MonoBehaviour
{
    // Public function to be called by the button's OnClick event
    public void LoadLevelScene()
    {
        // Load the scene named "Level"
        // Ensure "Level" is added to your Build Settings (File > Build Settings...)
        SceneManager.LoadScene("Level"); 
        Debug.Log("Attempting to load scene: Level"); // Added for confirmation
    }
}
