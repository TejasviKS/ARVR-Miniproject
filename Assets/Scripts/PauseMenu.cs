using UnityEngine;
using UnityEngine.InputSystem;

// VERSION 1
// The pause menu, in the style of the "wasted" and "mission passed" screens: the game freezes behind a dark see-through veil, "Game Paused"
// in the big Pricedown font, and three buttons one under the other: Resume, Main Menu, Quit. Esc opens it (and closes it again).
// You do not put this in a scene: it starts by itself with the game and works in every level that has a player.
// (For now Main Menu quits the game, like the button on the "mission passed" screen: there is no main menu yet.)
public class PauseMenu : MonoBehaviour
{
    public static bool IsPaused { get; private set; }

    float savedTimeScale = 1f, openedAt;
    GUIStyle titleStyle, buttonStyle;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Reset() { IsPaused = false; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Start()
    {
        GameObject go = new GameObject("PauseMenu");
        DontDestroyOnLoad(go);
        go.hideFlags = HideFlags.HideInHierarchy;
        go.AddComponent<PauseMenu>();
    }

    // Pausing makes no sense while something else owns the screen
    static bool CanPause()
    {
        if (WastedScreen.IsShowing || MissionPassedScreen.IsShowing || PortalTransition.Busy) return false;
        return FindAnyObjectByType<PlayerController>() != null;
    }

    void Update()
    {
        if (IsPaused && OptionsPanel.IsOpen) { OptionsPanel.Tick(); return; }       // the options screen handles Esc itself (back to this menu)
        Keyboard kb = Keyboard.current;
        if (kb != null && kb.escapeKey.wasPressedThisFrame)
        {
            if (IsPaused) Resume();
            else if (CanPause()) Pause();
        }
        // a level change or a defeat screen while paused (it should not happen) must never leave the game frozen
        if (IsPaused && (WastedScreen.IsShowing || MissionPassedScreen.IsShowing)) Resume();
    }

    void Pause()
    {
        OptionsPanel.Close();
        IsPaused = true;
        openedAt = Time.unscaledTime;
        savedTimeScale = Time.timeScale;
        Time.timeScale = 0f;
        AudioListener.pause = true;
    }

    void Resume()
    {
        if (!IsPaused) return;
        IsPaused = false;
        Time.timeScale = savedTimeScale;
        AudioListener.pause = false;
    }

    void OnDestroy() { if (IsPaused) { IsPaused = false; Time.timeScale = savedTimeScale; AudioListener.pause = false; } }

    static void Quit()
    {
        Debug.Log("[PauseMenu] Quit.");
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    // ------------------------------------------------------------------ drawing
    void OnGUI()
    {
        if (!IsPaused) return;
        GUI.depth = -2000;                                          // on top of every other screen
        float h = Screen.height, w = Screen.width;
        if (titleStyle == null || titleStyle.fontSize == 0)
        {
            Font f = WastedScreen.PickFont();
            titleStyle = new GUIStyle(GUI.skin.label) { fontStyle = f != null ? FontStyle.Normal : FontStyle.Bold, alignment = TextAnchor.MiddleCenter, wordWrap = false };
            buttonStyle = new GUIStyle(GUI.skin.label) { fontStyle = f != null ? FontStyle.Normal : FontStyle.Bold, alignment = TextAnchor.MiddleCenter, richText = true };
            if (f != null) { titleStyle.font = f; buttonStyle.font = f; }
        }

        if (OptionsPanel.IsOpen) { OptionsPanel.Draw(); return; }                // the options screen replaces the pause buttons

        float a = Mathf.Clamp01((Time.unscaledTime - openedAt) / 0.18f);      // a quick fade in (the game clock is stopped, so this uses real time)

        // the dark see-through veil
        GUI.color = new Color(0f, 0f, 0f, 0.68f * a);
        GUI.DrawTexture(new Rect(0, 0, w, h), Texture2D.whiteTexture);
        GUI.color = Color.white;

        // "Game Paused": big, white, with a soft dark glow behind it
        titleStyle.fontSize = Mathf.RoundToInt(h * 0.13f);
        Rect tr = new Rect(0, h * 0.14f, w, h * 0.2f);
        WastedScreen.Pin(titleStyle, new Color(0f, 0f, 0f, 0.5f * a));
        foreach (Vector2 o in new[] { new Vector2(-5f, 0f), new Vector2(5f, 0f), new Vector2(0f, -5f), new Vector2(0f, 7f), new Vector2(5f, 6f), new Vector2(-5f, 6f) })
            GUI.Label(new Rect(tr.x + o.x, tr.y + o.y, tr.width, tr.height), "Game Paused", titleStyle);
        WastedScreen.Pin(titleStyle, new Color(0.97f, 0.97f, 0.98f, a));
        GUI.Label(tr, "Game Paused", titleStyle);

        // the four buttons, stacked and centred: Resume, Options (does nothing yet), Main Menu, Quit
        float bw = Mathf.Max(240f, w * 0.14f), bh = Mathf.Max(52f, h * 0.066f), gap = bh * 0.34f;
        float y = h * 0.37f;
        Rect Slot() { Rect r = new Rect(w / 2f - bw / 2f, y, bw, bh); y += bh + gap; return r; }
        if (GtaMenuUI.Pill(Slot(), "<size={0}>R</size>esume", a, true, buttonStyle)) Resume();
        if (GtaMenuUI.Pill(Slot(), "<size={0}>O</size>ptions", a, true, buttonStyle)) OptionsPanel.Open();
        if (GtaMenuUI.Pill(Slot(), "<size={0}>M</size>ain <size={0}>M</size>enu", a, true, buttonStyle)) MainMenu_();
        if (GtaMenuUI.Pill(Slot(), "<size={0}>Q</size>uit", a, false, buttonStyle)) Quit();
        GUI.color = Color.white;
    }

    // back to the main menu (the game stops where it is: the next Play starts a fresh run)
    static void MainMenu_()
    {
        Time.timeScale = 1f;
        AudioListener.pause = false;
        IsPaused = false;
        UnityEngine.SceneManagement.SceneManager.LoadScene("MainMenu");
    }
}
