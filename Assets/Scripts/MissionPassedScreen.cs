using UnityEngine;
using UnityEngine.InputSystem;

// VERSION 1
// The "mission passed" ending screen, in the style of the classic open-world one:
//   - the picture dims and the corners go dark (the game keeps running behind it, in colour),
//   - a big gold "Mission Passed!" in the Pricedown font fades in and settles, with a line under it,
//   - the "mission passed" music plays (Assets/Resources/Audio/mission_passed.mp3, a ripped game sound),
//   - after a moment a rounded "Main Menu" button fades in (click it, or press Enter). For now it quits the game.
// You do NOT put this in a scene. Call MissionPassedScreen.Show(). The computer puzzle does it when the last computer is fixed.
public class MissionPassedScreen : MonoBehaviour
{
    static MissionPassedScreen current;

    string title, subtitle;
    float startedAt;
    bool finished;
    AudioSource audioSrc;
    GUIStyle titleStyle, subStyle, buttonStyle, hintStyle;
    Camera cam;
    RenderTexture picture;
    Material dimMat;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { current = null; }

    public static bool IsShowing => current != null;

    public static void Show(string text = "Mission Passed!", string sub = "Attendance 75%")
    {
        if (current != null) return;
        var go = new GameObject("MissionPassedScreen");
        current = go.AddComponent<MissionPassedScreen>();
        current.title = text;
        current.subtitle = sub;
    }

    void Start()
    {
        startedAt = Time.unscaledTime;
        Cursor.lockState = CursorLockMode.None;           // the button needs the mouse
        Cursor.visible = true;

        // the player stops: no walking, no looking, no "Press E"
        PlayerController pc = FindAnyObjectByType<PlayerController>();
        if (pc != null) pc.enabled = false;

        // the picture: the player's camera is drawn into a texture every frame (so the world keeps moving behind the text)
        cam = pc != null ? pc.GetComponentInChildren<Camera>() : Camera.main;
        Shader sh = Shader.Find("Custom/WastedGrey");     // (the same dimming shader, with the black-and-white turned off)
        if (sh != null) dimMat = new Material(sh);
        if (cam != null && dimMat != null) picture = new RenderTexture(Screen.width, Screen.height, 24) { name = "MissionPassedPicture" };

        audioSrc = gameObject.AddComponent<AudioSource>();
        audioSrc.spatialBlend = 0f;
        audioSrc.ignoreListenerPause = true;
        audioSrc.volume = 1f;
        audioSrc.clip = Resources.Load<AudioClip>("Audio/mission_passed");
        if (audioSrc.clip != null) audioSrc.Play();
    }

    void OnDestroy()
    {
        if (picture != null) { picture.Release(); Destroy(picture); }
        if (dimMat != null) Destroy(dimMat);
        if (current == this) current = null;
    }

    float T => Time.unscaledTime - startedAt;
    const float ButtonAt = 2.6f;

    void Update()
    {
        var kb = Keyboard.current;
        if (!finished && T > ButtonAt && kb != null && (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame)) MainMenu();
    }

    void LateUpdate()
    {
        if (picture == null || cam == null) return;
        RenderTexture old = cam.targetTexture;
        cam.targetTexture = picture;
        cam.Render();
        cam.targetTexture = old;
    }

    // for now the "main menu" does not exist yet: it ends the game
    void MainMenu()
    {
        if (finished) return;
        finished = true;
        Debug.Log("[MissionPassedScreen] Main Menu pressed: quitting (the main menu is not made yet).");
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    void OnGUI()
    {
        GUI.depth = -1000;                                  // on top of every other HUD
        float h = Screen.height, w = Screen.width;
        if (titleStyle == null || titleStyle.fontSize == 0)
        {
            Font f = WastedScreen.PickFont();
            titleStyle = new GUIStyle(GUI.skin.label) { fontStyle = f != null ? FontStyle.Normal : FontStyle.Bold, alignment = TextAnchor.MiddleCenter, wordWrap = false };
            subStyle = new GUIStyle(titleStyle);
            buttonStyle = new GUIStyle(GUI.skin.label) { fontStyle = f != null ? FontStyle.Normal : FontStyle.Bold, alignment = TextAnchor.MiddleCenter, richText = true };
            hintStyle = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            if (f != null) { titleStyle.font = f; subStyle.font = f; buttonStyle.font = f; }
        }

        float t = T;
        float fade = Mathf.Clamp01(t / 0.6f);

        // dim the picture (still in colour), dark corners
        if (Event.current.type == EventType.Repaint)
        {
            if (picture != null && dimMat != null)
            {
                dimMat.SetFloat("_Grey", 0f);
                dimMat.SetFloat("_Dark", 0.45f * fade);
                dimMat.SetFloat("_Vignette", 0.9f * fade);
                Graphics.DrawTexture(new Rect(0, 0, w, h), picture, dimMat);
            }
            else
            {
                GUI.color = new Color(0f, 0f, 0f, 0.6f * fade);
                GUI.DrawTexture(new Rect(0, 0, w, h), Texture2D.whiteTexture);
            }
        }

        // the gold title fades in and settles
        Color gold = new Color(0.96f, 0.76f, 0.18f);
        float tt = Mathf.Clamp01((t - 0.4f) / 0.6f);
        float ease = 1f - Mathf.Pow(1f - tt, 3f);
        if (tt > 0f)
        {
            titleStyle.fontSize = Mathf.RoundToInt(h * 0.17f * Mathf.Lerp(1.15f, 1f, ease));
            Rect r = new Rect(0, h * 0.28f, w, h * 0.26f);
            WastedScreen.Pin(titleStyle, new Color(0f, 0f, 0f, 0.5f * ease));
            foreach (Vector2 o in new[] { new Vector2(-5f, 0f), new Vector2(5f, 0f), new Vector2(0f, -5f), new Vector2(0f, 8f), new Vector2(6f, 7f), new Vector2(-6f, 7f) })
                GUI.Label(new Rect(r.x + o.x, r.y + o.y, r.width, r.height), title, titleStyle);
            WastedScreen.Pin(titleStyle, new Color(gold.r, gold.g, gold.b, ease));
            GUI.Label(r, title, titleStyle);

            // a thin gold line that draws out from the middle under the title
            float lineW = w * 0.46f * ease;
            GUI.color = new Color(gold.r, gold.g, gold.b, 0.9f * ease);
            GUI.DrawTexture(new Rect(w / 2f - lineW / 2f, h * 0.54f + h * 0.01f, lineW, Mathf.Max(3f, h * 0.004f)), Texture2D.whiteTexture);
            GUI.color = Color.white;
        }

        // the line under it (a reward line, in white)
        float st = Mathf.Clamp01((t - 1.3f) / 0.5f);
        if (st > 0f)
        {
            subStyle.fontSize = Mathf.RoundToInt(h * 0.075f);
            Rect r = new Rect(0, h * 0.56f, w, h * 0.12f);
            WastedScreen.Pin(subStyle, new Color(0f, 0f, 0f, 0.5f * st));
            GUI.Label(new Rect(r.x + 3f, r.y + 4f, r.width, r.height), subtitle, subStyle);
            WastedScreen.Pin(subStyle, new Color(1f, 1f, 1f, st));
            GUI.Label(r, subtitle, subStyle);
        }

        // the Main Menu button
        float ba = Mathf.Clamp01((t - ButtonAt) / 0.4f);
        if (ba > 0f && !finished)
        {
            float bw = Mathf.Max(210f, w * 0.115f), bh = Mathf.Max(46f, h * 0.062f);
            Rect b = new Rect(w / 2f - bw / 2f, h * 0.72f, bw, bh);
            bool hover = b.Contains(Event.current.mousePosition);
            float rad = bh * 0.32f;
            TutorialUI.Round(new Rect(b.x - 2f, b.y - 2f, b.width + 4f, b.height + 4f), hover ? new Color(1f, 0.85f, 0.35f, ba) : new Color(0.62f, 0.62f, 0.68f, 0.7f * ba), rad + 2f);
            TutorialUI.Round(b, hover ? new Color(0.86f, 0.64f, 0.08f, 0.95f * ba) : new Color(0.07f, 0.07f, 0.09f, 0.82f * ba), rad);
            buttonStyle.fontSize = Mathf.RoundToInt(bh * 0.52f);
            WastedScreen.Pin(buttonStyle, hover ? new Color(0.05f, 0.04f, 0.02f, ba) : new Color(0.95f, 0.95f, 0.97f, ba));
            GUI.color = Color.white;
            int big = Mathf.RoundToInt(buttonStyle.fontSize * 1.3f);
            GUI.Label(b, "<size=" + big + ">M</size>ain <size=" + big + ">M</size>enu", buttonStyle);          // capital M and M stand taller, like the other button
            hintStyle.fontSize = Mathf.RoundToInt(bh * 0.30f);
            WastedScreen.Pin(hintStyle, new Color(0.85f, 0.85f, 0.9f, 0.75f * ba));
            GUI.Label(new Rect(b.x - 60f, b.yMax + 8f, b.width + 120f, bh * 0.5f), "or press Enter", hintStyle);
            if (t > ButtonAt + 0.15f && GUI.Button(b, GUIContent.none, GUIStyle.none)) MainMenu();
        }
        GUI.color = Color.white;
    }
}
