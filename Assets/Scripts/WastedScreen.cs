using System;
using UnityEngine;
using UnityEngine.InputSystem;

// VERSION 2
// The "you were beaten" screen, in the style of the classic open-world "wasted" screen:
//   - the game drops into slow motion and the picture turns BLACK AND WHITE with dark corners,
//     slowly pushing in,
//   - a big red line in the classic "Pricedown" font fades in on the middle of the screen ("Attendance 0%"),
//   - the "wasted" sound plays (Assets/Resources/Audio/wasted.mp3; a made-up sting is used if that file is missing),
//   - after a moment a TRY AGAIN button appears (click it, or press Enter).
// You do NOT put this in a scene. Call WastedScreen.Show(whatToDoOnTryAgain). The caller restarts the fight.
public class WastedScreen : MonoBehaviour
{
    static WastedScreen current;

    Action onTryAgain;
    string title;
    float startedAt;
    bool finished;
    AudioSource audioSrc;
    GUIStyle titleStyle, buttonStyle, subStyle;
    Camera cam;
    RenderTexture picture;
    Material greyMat;
    static Font font;

    // "Enter Play Mode Options" is on (no domain reload): never keep a stuck slow motion from an earlier Play
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { current = null; Time.timeScale = 1f; font = null; }

    public static bool IsShowing => current != null;

    public static void Show(Action tryAgain, string text = "Attendance 0%")
    {
        if (current != null) return;
        var go = new GameObject("WastedScreen");
        current = go.AddComponent<WastedScreen>();
        current.onTryAgain = tryAgain;
        current.title = text;
        MusicDirector.Defeat();                           // the music drops away
    }

    void Start()
    {
        startedAt = Time.unscaledTime;
        GameCursor.Request(this);                         // the button needs the mouse

        // the picture: the player's camera is drawn into a texture every frame, which we show in black and white
        PlayerController pc = FindAnyObjectByType<PlayerController>();
        cam = pc != null ? pc.GetComponentInChildren<Camera>() : Camera.main;
        Shader sh = Shader.Find("Custom/WastedGrey");
        if (sh != null) greyMat = new Material(sh);
        if (cam != null && greyMat != null) picture = new RenderTexture(Screen.width, Screen.height, 24) { name = "WastedPicture" };

        audioSrc = gameObject.AddComponent<AudioSource>();
        audioSrc.spatialBlend = 0f;
        audioSrc.ignoreListenerPause = true;
        audioSrc.volume = 1f;
        audioSrc.clip = Resources.Load<AudioClip>("Audio/wasted");
        if (audioSrc.clip == null) audioSrc.clip = MakeSting();
        audioSrc.Play();
    }

    void OnDestroy()
    {
        GameCursor.Release(this);
        Time.timeScale = 1f;
        if (picture != null) { picture.Release(); Destroy(picture); }
        if (greyMat != null) Destroy(greyMat);
        if (current == this) current = null;
    }

    float T => Time.unscaledTime - startedAt;
    const float ButtonAt = 2.2f;

    void Update()
    {
        // slow motion, easing in over the first half second
        Time.timeScale = finished ? 1f : Mathf.Lerp(1f, 0.2f, Mathf.Clamp01(T / 0.6f));
        var kb = Keyboard.current;
        if (!finished && T > ButtonAt && kb != null && (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame)) TryAgain();
    }

    void LateUpdate()
    {
        if (finished || picture == null || cam == null) return;
        RenderTexture old = cam.targetTexture;
        cam.targetTexture = picture;                      // one extra render of the world into the texture
        cam.Render();
        cam.targetTexture = old;
    }

    void TryAgain()
    {
        if (finished) return;
        finished = true;
        Time.timeScale = 1f;
        GameCursor.Release(this);                         // the mouse is captured again
        if (audioSrc != null) audioSrc.Stop();
        MusicDirector.ResumeAfterCut(1.5f);               // the music comes back (a fight can choose its own track right after, in onTryAgain)
        onTryAgain?.Invoke();
        Destroy(gameObject, 0.05f);
    }

    // ------------------------------------------------------------------ drawing
    public static Font PickFont()
    {
        if (font != null) return font;
        font = Resources.Load<Font>("Fonts/PricedownBl");          // the classic font (Assets/Resources/Fonts), if it is in the project
        if (font != null) return font;
        string[] installed = Font.GetOSInstalledFontNames();
        // chunky, rounded, heavy fonts that look closest to the classic one; the first that is installed wins
        foreach (string n in new[] { "Pricedown Bl", "Arial Rounded MT Bold", "Berlin Sans FB Demi", "Gill Sans Ultra Bold", "Impact", "Arial Black" })
            if (Array.IndexOf(installed, n) >= 0) { font = Font.CreateDynamicFontFromOSFont(n, 64); break; }
        return font;
    }

    // IMGUI also gives text a "hover" colour (white) when the mouse is over it: make every state the same colour
    public static void Pin(GUIStyle s, Color c)
    {
        s.normal.textColor = c; s.hover.textColor = c; s.active.textColor = c; s.focused.textColor = c;
        s.onNormal.textColor = c; s.onHover.textColor = c; s.onActive.textColor = c; s.onFocused.textColor = c;
    }

    void OnGUI()
    {
        GUI.depth = -1000;                                  // on top of every other HUD
        float h = Screen.height, w = Screen.width;
        if (titleStyle == null || titleStyle.fontSize == 0)
        {
            Font f = PickFont();
            titleStyle = new GUIStyle(GUI.skin.label) { fontStyle = f != null ? FontStyle.Normal : FontStyle.Bold, alignment = TextAnchor.MiddleCenter, wordWrap = false };
            if (f != null) titleStyle.font = f;
            buttonStyle = new GUIStyle(GUI.skin.label) { fontStyle = f != null ? FontStyle.Normal : FontStyle.Bold, alignment = TextAnchor.MiddleCenter, richText = true };
            if (f != null) buttonStyle.font = f;
            subStyle = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
        }

        float t = T;
        float fade = Mathf.Clamp01(t / 0.5f);

        // the picture: black and white, darker at the corners, slowly pushing in
        if (Event.current.type == EventType.Repaint)
        {
            float zoom = 1f + 0.05f * Mathf.Clamp01(t / 6f);
            Rect full = new Rect(-(zoom - 1f) * w / 2f, -(zoom - 1f) * h / 2f, w * zoom, h * zoom);
            if (picture != null && greyMat != null)
            {
                greyMat.SetFloat("_Grey", fade);
                greyMat.SetFloat("_Dark", 0.08f * fade);
                greyMat.SetFloat("_Vignette", 0.8f * fade);
                Graphics.DrawTexture(full, picture, greyMat);
            }
            else
            {
                GUI.color = new Color(0.1f, 0.1f, 0.12f, 0.8f * fade);                       // (no camera: just a dark veil)
                GUI.DrawTexture(new Rect(0, 0, w, h), Texture2D.whiteTexture);
            }
        }

        // the big red line fades in and settles
        float tt = Mathf.Clamp01((t - 0.35f) / 0.5f);
        float ease = 1f - Mathf.Pow(1f - tt, 3f);
        if (tt > 0f)
        {
            titleStyle.fontSize = Mathf.RoundToInt(h * 0.17f * Mathf.Lerp(1.12f, 1f, ease));
            Rect r = new Rect(0, h * 0.30f, w, h * 0.30f);
            float a = 0.95f * ease;
            Pin(titleStyle, new Color(0f, 0f, 0f, 0.35f * ease));                              // soft dark glow behind
            foreach (Vector2 o in new[] { new Vector2(-6f, 0f), new Vector2(6f, 0f), new Vector2(0f, -6f), new Vector2(0f, 8f), new Vector2(5f, 6f), new Vector2(-5f, 6f) })
                GUI.Label(new Rect(r.x + o.x, r.y + o.y, r.width, r.height), title, titleStyle);
            Pin(titleStyle, new Color(0.72f, 0.04f, 0.07f, a));                                // deep red
            GUI.Label(r, title, titleStyle);
        }

        // the TRY AGAIN button
        float ba = Mathf.Clamp01((t - ButtonAt) / 0.4f);
        if (ba > 0f && !finished)
        {
            float bw = Mathf.Max(210f, w * 0.115f), bh = Mathf.Max(46f, h * 0.062f);
            Rect b = new Rect(w / 2f - bw / 2f, h * 0.30f + h * 0.30f + h * 0.03f, bw, bh);
            bool hover = b.Contains(Event.current.mousePosition);
            float rad = bh * 0.32f;
            // a dark, slightly see-through pill with a thin grey rim; it turns the same deep red as the title when hovered
            TutorialUI.Round(new Rect(b.x - 2f, b.y - 2f, b.width + 4f, b.height + 4f), hover ? new Color(0.9f, 0.25f, 0.27f, ba) : new Color(0.62f, 0.62f, 0.68f, 0.7f * ba), rad + 2f);
            TutorialUI.Round(b, hover ? new Color(0.72f, 0.04f, 0.07f, 0.95f * ba) : new Color(0.07f, 0.07f, 0.09f, 0.82f * ba), rad);
            buttonStyle.fontSize = Mathf.RoundToInt(bh * 0.52f);
            Pin(buttonStyle, new Color(0.95f, 0.95f, 0.97f, ba));
            GUI.color = Color.white;
            int big = Mathf.RoundToInt(buttonStyle.fontSize * 1.3f);
            GUI.Label(b, "<size=" + big + ">T</size>ry <size=" + big + ">A</size>gain", buttonStyle);      // capital T and A stand taller, like the title
            subStyle.fontSize = Mathf.RoundToInt(bh * 0.30f);
            Pin(subStyle, new Color(0.8f, 0.8f, 0.85f, 0.7f * ba));
            GUI.Label(new Rect(b.x - 60f, b.yMax + 8f, b.width + 120f, bh * 0.5f), "or press Enter", subStyle);
            if (t > ButtonAt + 0.15f && GUI.Button(b, GUIContent.none, GUIStyle.none)) TryAgain();
        }
        GUI.color = Color.white;
    }

    // ------------------------------------------------------------------ fallback sound
    // Only used if Assets/Resources/Audio/wasted.mp3 is missing: a dark sting made of simple tones.
    static AudioClip MakeSting()
    {
        const int rate = 44100;
        const float seconds = 4f;
        int n = Mathf.CeilToInt(rate * seconds);
        float[] d = new float[n];
        float boomPhase = 0f, glidePhase = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = (float)i / rate;
            boomPhase += 2f * Mathf.PI * (70f * Mathf.Exp(-t * 0.9f) + 28f) / rate;
            glidePhase += 2f * Mathf.PI * Mathf.Lerp(520f, 70f, Mathf.Clamp01(t / 1.6f)) / rate;
            d[i] = (Mathf.Sin(boomPhase) * 0.9f * Mathf.Exp(-t * 1.4f) + Mathf.Sin(glidePhase) * 0.3f * Mathf.Exp(-t * 1.3f)) * Mathf.Clamp01((seconds - t) / 0.5f);
        }
        AudioClip clip = AudioClip.Create("wasted_sting", n, 1, rate, false);
        clip.SetData(d, 0);
        return clip;
    }
}
