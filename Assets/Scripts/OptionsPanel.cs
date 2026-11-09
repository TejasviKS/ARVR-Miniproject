using UnityEngine;
using UnityEngine.InputSystem;

// VERSION 1
// The options screen, shared by the main menu and the pause menu. It looks like the "Sound" sub-menu of a real game:
// a big "Sound" title, two slider rows (Menu Music Volume, Game Music Volume) and a "Back" button at the bottom left.
// The sliders store their values (PlayerPrefs "MenuMusicVolume" / "GameMusicVolume", 0 to 1). Game Music Volume scales all the level music
// (MusicDirector reads it every frame, so it changes live); nothing listens to Menu Music Volume yet.
// How to use it (the menus already do this):
//   OptionsPanel.Open();                     // when the Options button is pressed
//   if (OptionsPanel.IsOpen) { OptionsPanel.Tick(); return; }   // in Update (it handles Esc, W/S, A/D, Enter)
//   if (OptionsPanel.IsOpen) { OptionsPanel.Draw(); return; }   // in OnGUI, instead of drawing the menu
// Esc, the Back button, or Enter on Back closes it and the menu underneath shows again.
public static class OptionsPanel
{
    public static bool IsOpen { get; private set; }

    public static float MenuMusicVolume { get; private set; } = 0.8f;
    public static float GameMusicVolume { get; private set; } = 0.8f;

    static readonly string[] Labels = { "Menu Music Volume", "Game Music Volume" };
    static readonly Color Gold = new Color(0.96f, 0.76f, 0.18f);

    static int selected;                 // 0, 1 = the sliders, 2 = Back
    static int dragging = -1;
    static float openedAt, holdStart, nextRepeat;
    static Vector2 lastMouse;
    static GUIStyle titleStyle, labelStyle, valueStyle, backStyle;
    static Texture2D circle;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        IsOpen = false; dragging = -1; titleStyle = null; circle = null; arrow = null;
        MenuMusicVolume = PlayerPrefs.GetFloat("MenuMusicVolume", 0.8f);            // the saved volumes are loaded when the game starts
        GameMusicVolume = PlayerPrefs.GetFloat("GameMusicVolume", 0.8f);
    }

    static float Get(int i) { return i == 0 ? MenuMusicVolume : GameMusicVolume; }

    static void Set(int i, float v)
    {
        v = Mathf.Clamp01(v);
        if (i == 0) MenuMusicVolume = v; else GameMusicVolume = v;
    }

    static void Save()
    {
        PlayerPrefs.SetFloat("MenuMusicVolume", MenuMusicVolume);
        PlayerPrefs.SetFloat("GameMusicVolume", GameMusicVolume);
        PlayerPrefs.Save();
    }

    public static void Open()
    {
        MenuMusicVolume = PlayerPrefs.GetFloat("MenuMusicVolume", 0.8f);
        GameMusicVolume = PlayerPrefs.GetFloat("GameMusicVolume", 0.8f);
        IsOpen = true;
        selected = 0;
        dragging = -1;
        openedAt = Time.unscaledTime;
    }

    public static void Close()
    {
        if (!IsOpen) return;
        IsOpen = false;
        dragging = -1;
        Save();
    }

    // keyboard: Esc = back, W/S = move between the rows, A/D (or the arrows) = change a slider, Enter on Back = back
    public static void Tick()
    {
        Keyboard kb = Keyboard.current;
        if (kb == null || Time.unscaledTime - openedAt < 0.15f) return;
        if (kb.escapeKey.wasPressedThisFrame) { Close(); return; }
        if (kb.sKey.wasPressedThisFrame || kb.downArrowKey.wasPressedThisFrame) selected = (selected + 1) % 3;
        if (kb.wKey.wasPressedThisFrame || kb.upArrowKey.wasPressedThisFrame) selected = (selected + 2) % 3;
        if (selected == 2)
        {
            if (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame) Close();
            return;
        }
        int dir = 0;
        bool left = kb.aKey.isPressed || kb.leftArrowKey.isPressed, right = kb.dKey.isPressed || kb.rightArrowKey.isPressed;
        if (left != right) dir = right ? 1 : -1;
        bool pressedNow = kb.aKey.wasPressedThisFrame || kb.leftArrowKey.wasPressedThisFrame || kb.dKey.wasPressedThisFrame || kb.rightArrowKey.wasPressedThisFrame;
        if (dir != 0)
        {
            if (pressedNow) { holdStart = Time.unscaledTime; nextRepeat = holdStart + 0.35f; Set(selected, Get(selected) + dir * 0.05f); }
            else if (Time.unscaledTime >= nextRepeat) { nextRepeat = Time.unscaledTime + 0.05f; Set(selected, Get(selected) + dir * 0.05f); }
        }
    }

    static Texture2D Circle()
    {
        if (circle != null) return circle;
        const int n = 64;
        circle = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float dx = (x + 0.5f) / n * 2f - 1f, dy = (y + 0.5f) / n * 2f - 1f;
                circle.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01((1f - Mathf.Sqrt(dx * dx + dy * dy)) * n * 0.5f)));
            }
        circle.Apply();
        return circle;
    }

    static Texture2D arrow;
    static Texture2D Arrow()
    {
        if (arrow != null) return arrow;
        const int n = 64;
        arrow = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float fx = (x + 0.5f) / n, fy = (y + 0.5f) / n;
                arrow.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01((1f - fx - Mathf.Abs(2f * fy - 1f) * 0.5f) * n * 0.5f)));
            }
        arrow.Apply();
        return arrow;
    }

    static void Fill(Rect r, Color c) { GUI.color = c; GUI.DrawTexture(r, Texture2D.whiteTexture); GUI.color = Color.white; }

    public static void Draw()
    {
        if (!IsOpen) return;
        float h = Screen.height, w = Screen.width;
        if (titleStyle == null || titleStyle.fontSize == 0)
        {
            titleStyle = GtaMenuUI.TitleStyle(); titleStyle.alignment = TextAnchor.MiddleLeft;
            labelStyle = GtaMenuUI.TitleStyle(); labelStyle.alignment = TextAnchor.MiddleLeft;
            backStyle = GtaMenuUI.TitleStyle(); backStyle.alignment = TextAnchor.MiddleLeft;
            valueStyle = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleRight };
        }
        float a = Mathf.Clamp01((Time.unscaledTime - openedAt) / 0.2f);
        Event e = Event.current;
        Vector2 mouse = e.mousePosition;
        bool moved = (mouse - lastMouse).sqrMagnitude > 0.5f; lastMouse = mouse;

        // a dark veil over whatever is underneath
        Fill(new Rect(0, 0, w, h), new Color(0.04f, 0.02f, 0.07f, 0.96f * a));

        // the title and a gold line under it
        float left = w * 0.08f;
        titleStyle.fontSize = Mathf.RoundToInt(h * 0.12f);
        GtaMenuUI.Outlined(new Rect(left, h * 0.07f, w * 0.6f, h * 0.15f), "Sound", titleStyle, new Color(0.97f, 0.97f, 0.98f), Mathf.Max(3f, h * 0.0065f), a);
        Fill(new Rect(left + 4f, h * 0.235f, w * 0.84f, Mathf.Max(2f, h * 0.004f)), new Color(Gold.r, Gold.g, Gold.b, 0.85f * a));

        // the two slider rows
        float rowH = h * 0.1f, top = h * 0.30f, rowW = w * 0.84f;
        labelStyle.fontSize = Mathf.RoundToInt(h * 0.05f);
        valueStyle.fontSize = Mathf.RoundToInt(h * 0.032f);
        for (int i = 0; i < 2; i++)
        {
            Rect row = new Rect(left, top + i * (rowH + h * 0.02f), rowW, rowH);
            bool sel = selected == i;
            if (moved && row.Contains(mouse) && dragging < 0) selected = i;
            if (sel)
            {
                Fill(row, new Color(1f, 1f, 1f, 0.07f * a));
                Fill(new Rect(row.x, row.y, Mathf.Max(4f, h * 0.007f), row.height), new Color(Gold.r, Gold.g, Gold.b, a));
            }
            GtaMenuUI.Outlined(new Rect(row.x + h * 0.03f, row.y, row.width * 0.4f, row.height), Labels[i], labelStyle, sel ? Gold : new Color(0.95f, 0.95f, 0.97f), Mathf.Max(2f, h * 0.0035f), a);

            // the slider: a dark track, a gold fill, a round knob, and the number on the right
            Rect track = new Rect(row.x + row.width * 0.45f, row.center.y - h * 0.006f, row.width * 0.40f, h * 0.012f);
            float v = Get(i);
            Fill(new Rect(track.x - 2f, track.y - 2f, track.width + 4f, track.height + 4f), new Color(0f, 0f, 0f, 0.6f * a));
            Fill(track, new Color(0.35f, 0.33f, 0.4f, a));
            Fill(new Rect(track.x, track.y, track.width * v, track.height), new Color(Gold.r, Gold.g, Gold.b, a));
            float kd = h * 0.04f;
            GUI.color = new Color(0f, 0f, 0f, 0.55f * a);
            GUI.DrawTexture(new Rect(track.x + track.width * v - kd / 2f - 2f, track.center.y - kd / 2f - 2f, kd + 4f, kd + 4f), Circle());
            GUI.color = sel || dragging == i ? new Color(1f, 0.9f, 0.5f, a) : new Color(0.95f, 0.95f, 0.97f, a);
            GUI.DrawTexture(new Rect(track.x + track.width * v - kd / 2f, track.center.y - kd / 2f, kd, kd), Circle());
            GUI.color = Color.white;
            valueStyle.normal.textColor = new Color(1f, 1f, 1f, 0.9f * a);
            GUI.Label(new Rect(track.xMax + h * 0.02f, row.y, row.xMax - track.xMax - h * 0.04f, row.height), Mathf.RoundToInt(v * 100f) + "%", valueStyle);

            // mouse: press on the slider (or its knob) and drag
            Rect hit = new Rect(track.x - kd / 2f, row.y, track.width + kd, row.height);
            if (e.type == EventType.MouseDown && e.button == 0 && hit.Contains(mouse)) { dragging = i; selected = i; }
            if (dragging == i && (e.type == EventType.MouseDown || e.type == EventType.MouseDrag))
            {
                Set(i, (mouse.x - track.x) / track.width);
                e.Use();
            }
        }
        if (e.type == EventType.MouseUp && dragging >= 0) { dragging = -1; Save(); }

        // Back, at the bottom left like the main menu list
        float bh = h * 0.082f;
        Rect back = new Rect(left, h - h * 0.075f - bh, w * 0.2f, bh);
        if (moved && back.Contains(mouse) && dragging < 0) selected = 2;
        bool backSel = selected == 2;
        backStyle.fontSize = Mathf.RoundToInt(h * 0.062f);
        if (backSel)
        {
            GUI.color = new Color(Gold.r, Gold.g, Gold.b, a);
            GUI.DrawTexture(new Rect(back.x - h * 0.004f, back.y + bh * 0.27f, h * 0.034f, bh * 0.46f), Arrow());     // a gold arrow pointing right
            GUI.color = Color.white;
            Fill(new Rect(left + h * 0.05f, back.yMax - bh * 0.14f, back.width * 0.55f, Mathf.Max(2f, h * 0.0035f)), new Color(Gold.r, Gold.g, Gold.b, 0.85f * a));
        }
        GtaMenuUI.Outlined(new Rect(left + h * 0.05f + (backSel ? h * 0.012f : 0f), back.y, back.width, bh), "Back", backStyle, backSel ? Gold : new Color(0.97f, 0.97f, 0.98f), Mathf.Max(2f, h * 0.0045f), a);
        if (a > 0.9f && GUI.Button(back, GUIContent.none, GUIStyle.none)) Close();
        GUI.color = Color.white;
    }
}
