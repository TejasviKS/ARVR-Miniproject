using UnityEngine;
using UnityEngine.SceneManagement;

// VERSION 1
// The "You escaped!" screen at the end of the game. Put this on ONE empty object in Cad_lab.
// Connect it: Beeping Computer Puzzle > On Solved > + > drag this object > EndScreen.Show
// It fades the screen to dark, shows the score and time, and has Play Again / Quit buttons.
public class EndScreen : MonoBehaviour
{
    public string title = "You escaped the multiverse!";
    [Tooltip("Seconds to wait before fading (lets the last power-down sound play)")]
    public float delay = 1.5f;
    public float fadeTime = 1.5f;
    [Tooltip("Scene that Play Again loads")]
    public string firstScene = "Classroom";

    bool showing;
    float shownAt;
    float timeTaken;
    GUIStyle titleStyle, textStyle, buttonStyle;

    void Start() => Debug.Log("[Scripts] EndScreen v1 running", this);

    // Called by BeepingComputerPuzzle > On Solved
    public void Show()
    {
        if (showing) return;
        showing = true;
        shownAt = Time.time + delay;
        timeTaken = Time.time - GameProgress.startTime;

        // Freeze the player: no walking, no "Press E" prompts, mouse free for the buttons
        PlayerController pc = FindAnyObjectByType<PlayerController>();
        if (pc != null) pc.enabled = false;
        PlayerInteractor pi = FindAnyObjectByType<PlayerInteractor>();
        if (pi != null) pi.enabled = false;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    void OnGUI()
    {
        if (!showing || Time.time < shownAt) return;
        GUI.depth = -10;    // draw on top of everything else

        if (titleStyle == null)
        {
            titleStyle = new GUIStyle(GUI.skin.label) { fontSize = 44, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            titleStyle.normal.textColor = new Color(0.3f, 0.9f, 1f);
            textStyle = new GUIStyle(GUI.skin.label) { fontSize = 24, alignment = TextAnchor.MiddleCenter };
            textStyle.normal.textColor = Color.white;
            buttonStyle = new GUIStyle(GUI.skin.button) { fontSize = 24 };
        }

        // Fade from clear to dark over fadeTime seconds
        float a = Mathf.Clamp01((Time.time - shownAt) / fadeTime);
        GUI.color = new Color(0f, 0f, 0.03f, 0.92f * a);
        GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
        GUI.color = new Color(1f, 1f, 1f, a);      // text and buttons fade in too

        float cx = Screen.width / 2f, cy = Screen.height / 2f;
        GUI.Label(new Rect(0, cy - 170, Screen.width, 60), title, titleStyle);

        string riddles = GameProgress.riddlesDone
            ? "Riddles: " + GameProgress.riddlesCorrect + "/" + GameProgress.riddlesTotal
            : "Riddles: skipped";
        int minutes = (int)(timeTaken / 60f), seconds = (int)(timeTaken % 60f);
        GUI.Label(new Rect(0, cy - 90, Screen.width, 36), riddles, textStyle);
        GUI.Label(new Rect(0, cy - 54, Screen.width, 36), "Computers switched off: " + GameProgress.computersSwitchedOff, textStyle);
        GUI.Label(new Rect(0, cy - 18, Screen.width, 36), "Time: " + minutes + ":" + seconds.ToString("00"), textStyle);

        if (a < 1f) { GUI.color = Color.white; return; }   // buttons work once the fade is done
        if (GUI.Button(new Rect(cx - 210, cy + 50, 200, 56), "Play Again", buttonStyle)) PlayAgain();
        if (GUI.Button(new Rect(cx + 10, cy + 50, 200, 56), "Quit", buttonStyle)) Quit();
        GUI.color = Color.white;
    }

    void PlayAgain()
    {
        GameProgress.Reset();
        SceneManager.LoadScene(firstScene);
    }

    void Quit()
    {
        Debug.Log("[EndScreen] Quit");
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;   // in the editor: stop Play mode
#else
        Application.Quit();                                // in a real build: close the game
#endif
    }
}
