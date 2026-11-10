using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

// VERSION 2
// The main menu of "Grand Theft Attendance", drawn like a poster of the game (everything is made in code, no art files):
//   - a sunset sky with a big sun, and a collage of tall panels on the right, one for each of the game's characters
//     (you with your textbook, Prof. GPT, Lab Asst. Gemini, Prof. Claude, and a gold panel with the 74%),
//   - the title top left, with "PES University presents" above it and an "attendance required" rating box in the corner, like the ones on game covers,
//   - the menu as a plain list of words at the bottom left: Play, Options, Credits, Quit. Point at one (or use W/S or the arrow keys) and it turns gold
//     with an arrow; click (or Enter) to choose it.
// Options and Credits do nothing yet. Put this on one object in the MainMenu scene (a Camera and nothing else is needed).
public class MainMenu : MonoBehaviour
{
    [Tooltip("The first level of the game")]
    public string firstScene = "Cabin_Tutorial";

    static readonly Color Gold = new Color(0.96f, 0.76f, 0.18f);
    static readonly Color Ink = new Color(0.07f, 0.04f, 0.10f);

    GUIStyle titleStyle, itemStyle, smallStyle, boldSmall;
    Texture2D sky, circle, triDown, triUp, vignette, band;
    const float IntroSlow = 4f;          // the fade in from black takes 0.5 x this = 2 s, and everything else in the opening is stretched the same amount
    float startedAt, leaving = -1f;
    bool loading, cursorShown;
    int selected;
    Vector2 lastMouse;

    void Start()
    {
        startedAt = Time.unscaledTime;
        OptionsPanel.Close();
        Time.timeScale = 1f;
        AudioListener.pause = false;
        MusicDirector.PlayMenu();                  // (already playing if the opening screens started it)
    }

    void OnDestroy() { GameCursor.Release(this); }

    // ---------------------------------------------------------------- textures made in code
    static Texture2D MakeSky()
    {
        const int n = 128;
        var t = new Texture2D(1, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
        Color top = new Color(0.99f, 0.80f, 0.32f), mid = new Color(0.96f, 0.47f, 0.33f), low = new Color(0.62f, 0.20f, 0.38f), bottom = new Color(0.20f, 0.09f, 0.26f);
        for (int y = 0; y < n; y++)
        {
            float k = 1f - y / (n - 1f);                       // 0 at the top of the screen, 1 at the bottom
            Color c = k < 0.4f ? Color.Lerp(top, mid, k / 0.4f) : k < 0.75f ? Color.Lerp(mid, low, (k - 0.4f) / 0.35f) : Color.Lerp(low, bottom, (k - 0.75f) / 0.25f);
            t.SetPixel(0, y, c);
        }
        t.Apply();
        return t;
    }

    static Texture2D MakeBand()
    {
        const int n = 128;
        var t = new Texture2D(n, 1, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
        for (int x = 0; x < n; x++) { float k = 1f - x / (n - 1f); t.SetPixel(x, 0, new Color(1f, 1f, 1f, 0.5f * k * k)); }
        t.Apply();
        return t;
    }

    static Texture2D MakeCircle()
    {
        const int n = 128;
        var t = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float dx = (x + 0.5f) / n * 2f - 1f, dy = (y + 0.5f) / n * 2f - 1f;
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                t.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01((1f - d) * n * 0.5f)));
            }
        t.Apply();
        return t;
    }

    // a triangle with its point at the bottom (down = true) or at the top
    static Texture2D MakeTriangle(bool down)
    {
        const int n = 128;
        var t = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float fy = (y + 0.5f) / n;                    // 0 = bottom row of the texture
                float width = down ? fy : 1f - fy;              // wide at the top for a downward point
                float dx = Mathf.Abs((x + 0.5f) / n - 0.5f) * 2f;
                t.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01((width - dx) * n * 0.5f)));
            }
        t.Apply();
        return t;
    }

    static Texture2D MakeVignette()
    {
        const int n = 128;
        var t = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float dx = (x - n / 2f) / (n / 2f), dy = (y - n / 2f) / (n / 2f);
                t.SetPixel(x, y, new Color(0f, 0f, 0f, Mathf.Clamp01((Mathf.Sqrt(dx * dx + dy * dy) - 0.55f) / 0.8f) * 0.55f));
            }
        t.Apply();
        return t;
    }

    // ---------------------------------------------------------------- input
    void Update()
    {
        // the mouse arrow stays hidden through the opening screens and until the menu has fully appeared (the rating box is the last thing in)
        bool wantCursor = Time.unscaledTime - startedAt >= 1.9f * IntroSlow && !CreditsScreen.Active;      // (hidden again while the credits are on)
        if (wantCursor && !cursorShown) { cursorShown = true; GameCursor.Request(this); }
        else if (!wantCursor && cursorShown) { cursorShown = false; GameCursor.Release(this); }
        if (CreditsScreen.Active) return;                                            // the credits are on top: the menu keeps still
        if (leaving >= 0f)
        {
            if (!loading && Time.unscaledTime - leaving > 0.5f)
            {
                loading = true;                                                        // (leaving keeps its time, so the screen stays black while the scene loads)
                GameProgress.Reset();                                                  // a new run
                SceneManager.LoadScene(firstScene);
            }
            return;
        }
        if (OptionsPanel.IsOpen) { OptionsPanel.Tick(); return; }                    // the options screen is on top: it handles the keys
        Keyboard kb = Keyboard.current;
        if (kb == null || Time.unscaledTime - startedAt < 1.2f * IntroSlow) return;       // (not before the menu has appeared)
        if (kb.sKey.wasPressedThisFrame || kb.downArrowKey.wasPressedThisFrame) selected = (selected + 1) % 4;
        if (kb.wKey.wasPressedThisFrame || kb.upArrowKey.wasPressedThisFrame) selected = (selected + 3) % 4;
        if (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame) Choose(selected);
    }

    void Choose(int i)
    {
        switch (i)
        {
            case 0: leaving = Time.unscaledTime; MusicDirector.StopMenu(0.45f); break;                  // Play: the screen fades out, then the first level loads
            case 1: OptionsPanel.Open(); break;                             // Options: the sound screen
            case 2: CreditsScreen.Begin(); break;                           // Credits: the menu fades out, the credits roll
            case 3:
#if UNITY_EDITOR
                UnityEditor.EditorApplication.isPlaying = false;
#else
                Application.Quit();
#endif
                break;
        }
    }

    // ---------------------------------------------------------------- drawing
    void Fill(Rect r, Color c) { GUI.color = c; GUI.DrawTexture(r, Texture2D.whiteTexture); GUI.color = Color.white; }
    void Disc(Rect r, Color c) { GUI.color = c; GUI.DrawTexture(r, circle); GUI.color = Color.white; }
    void TriDown(Rect r, Color c) { GUI.color = c; GUI.DrawTexture(r, triDown); GUI.color = Color.white; }
    void TriUp(Rect r, Color c) { GUI.color = c; GUI.DrawTexture(r, triUp); GUI.color = Color.white; }
    void Rounded(Rect r, Color c, float radius) { TutorialUI.Round(r, c, radius); }

    void OnGUI()
    {
        float h = Screen.height, w = Screen.width;
        if (titleStyle == null || titleStyle.fontSize == 0)
        {
            titleStyle = GtaMenuUI.TitleStyle();
            itemStyle = GtaMenuUI.TitleStyle(); itemStyle.alignment = TextAnchor.MiddleLeft;
            smallStyle = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft };
            boldSmall = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            sky = MakeSky(); circle = MakeCircle(); triDown = MakeTriangle(true); triUp = MakeTriangle(false); vignette = MakeVignette(); band = MakeBand();
            boldSmall.wordWrap = false;
        }
        float t = (Time.unscaledTime - startedAt) / IntroSlow;       // the opening choreography (fade-in, title, cards sliding up) runs IntroSlow times slower

        // ---- the sky and the sun ----
        GUI.DrawTexture(new Rect(0, 0, w, h), sky);
        float sunR = h * 0.46f;
        Disc(new Rect(w * 0.66f - sunR, h * 0.40f - sunR, sunR * 2f, sunR * 2f), new Color(1f, 0.93f, 0.62f, 0.55f));
        Disc(new Rect(w * 0.66f - sunR * 0.72f, h * 0.40f - sunR * 0.72f, sunR * 1.44f, sunR * 1.44f), new Color(1f, 0.97f, 0.80f, 0.55f));

        // ---- the collage of characters ----
        DrawCollage(w, h, t);

        // ---- dark corners ----
        GUI.DrawTexture(new Rect(0, 0, w, h), vignette);
        // the options screen replaces the title and the menu (the poster stays dimmed behind it)
        if (OptionsPanel.IsOpen) { OptionsPanel.Draw(); return; }
        // a dark band on the left so the title and the menu read on top of the sky
        float ui = CreditsScreen.MenuUiAlpha;                                // (the credits fade the menu's writing away and back)
        GUI.color = new Color(0.05f, 0.02f, 0.08f, ui);
        GUI.DrawTexture(new Rect(0, 0, w * 0.42f, h), band);               // fades out towards the right
        GUI.color = Color.white;

        // ---- the title ----
        float fade = Mathf.Clamp01((t - 0.2f) / 0.8f), ease = 1f - Mathf.Pow(1f - fade, 3f);
        float drift = (1f - ease) * h * 0.04f;
        ease *= ui;
        float left = w * 0.045f;
        titleStyle.alignment = TextAnchor.MiddleLeft;
        titleStyle.fontSize = Mathf.RoundToInt(h * 0.115f);
        GtaMenuUI.Outlined(new Rect(left, h * 0.095f - drift, w * 0.5f, h * 0.13f), "Grand Theft", titleStyle, new Color(0.97f, 0.97f, 0.98f), Mathf.Max(3f, h * 0.0065f), ease);
        titleStyle.fontSize = Mathf.RoundToInt(h * 0.13f);
        GtaMenuUI.Outlined(new Rect(left, h * 0.205f - drift, w * 0.5f, h * 0.15f), "Attendance", titleStyle, Gold, Mathf.Max(3f, h * 0.0075f), ease);
        titleStyle.alignment = TextAnchor.MiddleCenter;
        smallStyle.fontSize = Mathf.RoundToInt(h * 0.026f);
        smallStyle.normal.textColor = new Color(1f, 0.97f, 0.9f, Mathf.Clamp01((t - 0.9f) / 0.6f) * ui);
        GUI.Label(new Rect(left + 4f, h * 0.345f, w * 0.3f, h * 0.04f), "Reach class.  Hit 75%.", smallStyle);

        // ---- the menu: a plain list at the bottom left ----
        string[] items = { "Play", "Options", "Credits", "Quit" };
        float itemH = h * 0.082f, listTop = h - h * 0.075f - itemH * items.Length;
        itemStyle.fontSize = Mathf.RoundToInt(h * 0.062f);
        Vector2 mouse = Event.current.mousePosition;
        bool moved = (mouse - lastMouse).sqrMagnitude > 0.5f; lastMouse = mouse;
        for (int i = 0; i < items.Length; i++)
        {
            float a = Mathf.Clamp01((t - 1.0f - 0.12f * i) / 0.4f) * ui;
            if (leaving >= 0f) a *= 1f - Mathf.Clamp01((Time.unscaledTime - leaving) / 0.35f);
            Rect row = new Rect(left, listTop + i * itemH, w * 0.24f, itemH);
            if (leaving < 0f && a > 0.9f && moved && row.Contains(mouse)) selected = i;                // the mouse picks the row (only when it moves)
            bool sel = selected == i && leaving < 0f;
            float shift = sel ? h * 0.012f : 0f;
            if (sel)
            {
                // a gold arrow and a thin gold line under the word
                TriUpRight(new Rect(left - h * 0.004f, row.y + itemH * 0.27f, h * 0.034f, itemH * 0.46f), new Color(Gold.r, Gold.g, Gold.b, a));
                Fill(new Rect(left + h * 0.05f, row.yMax - itemH * 0.14f, row.width * 0.7f, Mathf.Max(2f, h * 0.0035f)), new Color(Gold.r, Gold.g, Gold.b, 0.85f * a));
            }
            GtaMenuUI.Outlined(new Rect(left + h * 0.05f + shift, row.y, row.width, itemH), items[i], itemStyle, sel ? Gold : new Color(0.97f, 0.97f, 0.98f), Mathf.Max(2f, h * 0.0045f), a);
            if (leaving < 0f && a > 0.9f && !CreditsScreen.Active && GUI.Button(row, GUIContent.none, GUIStyle.none)) Choose(i);
        }

        // ---- the rating box, like the one on a game cover ----
        DrawRating(w, h, Mathf.Clamp01((t - 1.2f) / 0.6f) * ui);

        // ---- fades: in from black when the menu opens, out to black when Play is pressed ----
        if (t < 0.5f) Fill(new Rect(0, 0, w, h), new Color(0f, 0f, 0f, 1f - t / 0.5f));
        if (leaving >= 0f) Fill(new Rect(0, 0, w, h), new Color(0f, 0f, 0f, Mathf.Clamp01((Time.unscaledTime - leaving) / 0.45f)));
        GUI.color = Color.white;
    }

    // an arrow pointing right (the "up" triangle turned on its side)
    void TriUpRight(Rect r, Color c)
    {
        Matrix4x4 saved = GUI.matrix;
        GUIUtility.RotateAroundPivot(90f, r.center);
        GUI.color = c; GUI.DrawTexture(new Rect(r.center.x - r.height / 2f, r.center.y - r.width / 2f, r.height, r.width), triUp); GUI.color = Color.white;
        GUI.matrix = saved;
    }

    void DrawRating(float w, float h, float a)
    {
        float bw = w * 0.115f, bh = h * 0.17f, x = w - bw - w * 0.03f, y = h - bh - h * 0.05f;
        Fill(new Rect(x - 3f, y - 3f, bw + 6f, bh + 6f), new Color(1f, 1f, 1f, 0.95f * a));
        Fill(new Rect(x, y, bw, bh), new Color(0.05f, 0.03f, 0.08f, 0.95f * a));
        boldSmall.fontSize = Mathf.RoundToInt(bh * 0.30f);
        boldSmall.normal.textColor = new Color(1f, 1f, 1f, a);
        GUI.Label(new Rect(x, y + bh * 0.03f, bw, bh * 0.38f), "75%", boldSmall);
        boldSmall.fontSize = Mathf.RoundToInt(bh * 0.115f);
        boldSmall.normal.textColor = new Color(1f, 1f, 1f, 0.95f * a);
        GUI.Label(new Rect(x, y + bh * 0.47f, bw, bh * 0.28f), "ATTENDANCE\nREQUIRED", boldSmall);
        boldSmall.fontSize = Mathf.RoundToInt(bh * 0.085f);
        boldSmall.normal.textColor = new Color(0.75f, 0.75f, 0.8f, a);
        GUI.Label(new Rect(x, y + bh * 0.8f, bw, bh * 0.16f), "PESU  ·  AVR", boldSmall);
    }

    // ---------------------------------------------------------------- the collage
    // five tall panels, each a different colour with a dark silhouette of one character, sliding up one after the other.
    void DrawCollage(float w, float h, float t)
    {
        float x0 = w * 0.40f, pw = w * 0.118f, step = pw * 0.98f;
        // (colour of the panel, how tall it is as a share of the screen, its top as a share of the screen, who is in it)
        Color[] cols = { new Color(0.18f, 0.77f, 0.71f), new Color(0.949f, 0.541f, 0.188f), new Color(0.66f, 0.06f, 0.12f), new Color(0.49f, 0.38f, 0.72f), new Color(0.96f, 0.72f, 0.20f) };
        float[] tops = { 0.16f, 0.09f, 0.04f, 0.12f, 0.19f };
        float[] hts = { 0.60f, 0.65f, 0.72f, 0.63f, 0.55f };
        int[] order = { 0, 1, 3, 4, 2 };                                   // draw order: the boss last, in front
        for (int o = 0; o < 5; o++)
        {
            int i = order[o];
            float slide = Mathf.Clamp01((t - 0.3f - 0.14f * i) / 0.7f);
            float ease = 1f - Mathf.Pow(1f - slide, 3f);
            float bob = Mathf.Sin(Time.unscaledTime * 0.8f + i * 1.3f) * h * 0.004f;
            float px = x0 + i * step + (i == 2 ? -pw * 0.08f : 0f), pwi = pw * (i == 2 ? 1.28f : 1f);
            float ph = h * hts[i], py = h * tops[i] + (1f - ease) * h * 0.9f + bob;
            Rect panel = new Rect(px, py, pwi, ph);
            // a cream frame, the colour, a soft light from the top
            Fill(new Rect(panel.x - 5f, panel.y - 5f, panel.width + 10f, panel.height + 10f), new Color(1f, 0.97f, 0.88f));
            Fill(panel, cols[i]);
            Fill(new Rect(panel.x, panel.y, panel.width, panel.height * 0.45f), new Color(1f, 1f, 1f, 0.10f));
            Fill(new Rect(panel.x, panel.yMax - panel.height * 0.2f, panel.width, panel.height * 0.2f), new Color(0f, 0f, 0f, 0.18f));   // (under the character, so the gold bits keep one colour)
            GUI.BeginGroup(panel);
            DrawCharacter(i, panel.width, panel.height, cols[i], Time.unscaledTime);
            GUI.EndGroup();
        }
    }

    // draws one silhouette in a panel's own coordinates (0..pw, 0..ph)
    void DrawCharacter(int who, float pw, float ph, Color panelColour, float time)
    {
        Color dark = Ink;
        // GPT and Gemini are centred on the part of their card that Claude's card does not cover
        float cx = pw * (0.5f + (who == 1 ? -0.05f : who == 3 ? 0.10f : 0f));
        switch (who)
        {
            case 0:                                    // YOU: a student with a textbook held up
            {
                float head = pw * 0.30f;
                Rounded(new Rect(cx - pw * 0.27f, ph * 0.20f + head * 0.95f, pw * 0.54f, ph * 0.55f), dark, pw * 0.2f);     // body
                // the textbook: navy with cream pages, held up in front
                Rect book = new Rect(cx - pw * 0.05f, ph * 0.43f, pw * 0.42f, ph * 0.20f);
                Fill(new Rect(book.x - 4f, book.y - 4f, book.width + 8f, book.height + 8f), dark);
                Fill(book, new Color(0.14f, 0.22f, 0.55f));
                Fill(new Rect(book.x + book.width * 0.12f, book.y + book.height * 0.18f, book.width * 0.76f, book.height * 0.12f), new Color(0.96f, 0.93f, 0.82f));
                Fill(new Rect(book.x + book.width * 0.12f, book.y + book.height * 0.5f, book.width * 0.5f, book.height * 0.1f), new Color(0.96f, 0.93f, 0.82f));
                // a mortarboard on the head (the academic touch)
                Rect cap = new Rect(cx - pw * 0.27f, ph * 0.115f, pw * 0.54f, ph * 0.05f);
                Fill(cap, dark);
                Fill(new Rect(cx - head / 2f, ph * 0.165f, head, ph * 0.085f), dark);                      // the band of the hat, down over the head
                Fill(new Rect(cx + pw * 0.22f, ph * 0.12f, pw * 0.022f, ph * 0.09f), Gold);
                // a thin see-through ring around the top half of the head, cutting the hat from the head (not the body)
                float gap = pw * 0.02f, mid = ph * 0.20f + head * 0.5f;
                Color seen = Color.Lerp(panelColour, Color.white, 0.10f);
                GUI.BeginGroup(new Rect(0, 0, pw, mid));
                Disc(new Rect(cx - head / 2f - gap, ph * 0.20f - gap, head + gap * 2f, head + gap * 2f), seen);
                GUI.EndGroup();
                Disc(new Rect(cx - head / 2f, ph * 0.20f, head, head), dark);                               // the head                // the tassel
                break;
            }
            case 1:                                    // PROF. GPT: all rectangles
            {
                Fill(new Rect(cx - pw * 0.26f, ph * 0.43f, pw * 0.52f, ph * 0.42f), dark);                  // body
                Fill(new Rect(cx - pw * 0.17f, ph * 0.23f, pw * 0.34f, ph * 0.17f), dark);                  // head
                Fill(new Rect(cx - pw * 0.12f, ph * 0.285f, pw * 0.24f, ph * 0.03f), new Color(1f, 0.72f, 0.15f));          // the amber visor
                Fill(new Rect(cx - pw * 0.40f, ph * 0.45f, pw * 0.10f, ph * 0.30f), dark);                   // arms
                Fill(new Rect(cx + pw * 0.30f, ph * 0.45f, pw * 0.10f, ph * 0.30f), dark);
                Fill(new Rect(cx + pw * 0.325f, ph * 0.70f, pw * 0.05f, ph * 0.18f), new Color(1f, 0.72f, 0.15f));          // the pointer
                break;
            }
            case 2:                                    // PROF. CLAUDE: all triangles, the big one in the middle
            {
                TriDown(new Rect(cx - pw * 0.40f, ph * 0.32f, pw * 0.80f, ph * 0.50f), dark);               // torso
                TriDown(new Rect(cx - pw * 0.15f, ph * 0.20f, pw * 0.30f, ph * 0.14f), dark);               // head
                Fill(new Rect(cx - pw * 0.05f, ph * 0.245f, pw * 0.10f, ph * 0.014f), new Color(1f, 0.32f, 0.1f));          // the eye slit
                for (int k = 0; k < 5; k++)                                                                   // the crown
                {
                    float kx = cx + (k - 2) * pw * 0.12f, kh = (k == 2 ? 0.12f : k % 2 == 0 ? 0.085f : 0.1f) * ph;
                    TriUp(new Rect(kx - pw * 0.05f, ph * 0.20f - kh, pw * 0.10f, kh), dark);
                }
                TriUp(new Rect(cx - pw * 0.50f, ph * 0.30f, pw * 0.26f, ph * 0.12f), dark);                  // pauldrons
                TriUp(new Rect(cx + pw * 0.24f, ph * 0.30f, pw * 0.26f, ph * 0.12f), dark);
                TriDown(new Rect(cx - pw * 0.07f, ph * 0.40f, pw * 0.14f, ph * 0.09f), new Color(1f, 0.32f, 0.1f));         // the glowing core
                break;
            }
            case 3:                                    // LAB ASST. GEMINI: round, with a cyan glow
            {
                Color cyan = new Color(0.45f, 0.95f, 1f);
                float head = pw * 0.30f;
                Disc(new Rect(cx - head / 2f - 5f, ph * 0.26f - 5f, head + 10f, head + 10f), cyan);
                Disc(new Rect(cx - head / 2f, ph * 0.26f, head, head), dark);
                Rounded(new Rect(cx - pw * 0.25f - 5f, ph * 0.26f + head * 1.0f - 5f, pw * 0.50f + 10f, ph * 0.46f + 10f), cyan, pw * 0.25f);
                Rounded(new Rect(cx - pw * 0.25f, ph * 0.26f + head * 1.0f, pw * 0.50f, ph * 0.46f), dark, pw * 0.25f);
                Fill(new Rect(cx - pw * 0.06f, ph * 0.33f, pw * 0.12f, ph * 0.014f), cyan);                 // the visor
                Disc(new Rect(cx - pw * 0.42f, ph * 0.88f, pw * 0.84f, ph * 0.05f), new Color(cyan.r, cyan.g, cyan.b, 0.8f));  // the projector disc
                break;
            }
            default:                                   // the gold panel: the score, and a rubber stamp
            {
                boldSmall.fontSize = Mathf.RoundToInt(pw * 0.34f);
                boldSmall.normal.textColor = dark;
                GUI.Label(new Rect(0, ph * 0.12f, pw, ph * 0.3f), "74%", boldSmall);
                boldSmall.fontSize = Mathf.RoundToInt(pw * 0.12f);
                boldSmall.normal.textColor = new Color(0.78f, 0.06f, 0.1f);
                Matrix4x4 saved = GUI.matrix;
                GUIUtility.RotateAroundPivot(-14f, new Vector2(cx, ph * 0.62f));
                Fill(new Rect(cx - pw * 0.42f, ph * 0.55f, pw * 0.84f, ph * 0.15f), new Color(0.78f, 0.06f, 0.1f, 0.18f));
                GUI.Label(new Rect(cx - pw * 0.42f, ph * 0.55f, pw * 0.84f, ph * 0.15f), "SHORTAGE", boldSmall);
                GUI.matrix = saved;
                boldSmall.font = null;
                break;
            }
        }
    }
}
