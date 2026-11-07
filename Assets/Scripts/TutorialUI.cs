using UnityEngine;

// VERSION 1
// The drawing tools for the "teaching box" in the corner of the screen (keys, mouse icon, arrows, tick, rounded panel).
// Used by the combat tutorial; the cabin tutorial (TutorialDirector) has its own copy of the same look.
// Everything is drawn with the built-in UI in a 440 x 200 box that is scaled as a whole.
public static class TutorialUI
{
    public const float BoxW = 440f, BoxH = 200f;

    public static readonly Color Accent = new Color(1f, 0.78f, 0.3f);
    public static readonly Color Dim = new Color(0.45f, 0.48f, 0.55f);
    public static readonly Color Good = new Color(0.4f, 0.9f, 0.5f);

    static Texture2D triangle;
    public static GUIStyle TitleStyle, BodyStyle, KeyStyle, SmallStyle, KeySmallStyle;

    // (if Unity's GUI skin was not ready the first time, styles come back with font size 0: build them again)
    public static void Ensure()
    {
        if (triangle == null) triangle = MakeTriangle();
        if (TitleStyle != null && TitleStyle.fontSize != 0) return;
        TitleStyle = new GUIStyle(GUI.skin.label) { fontSize = 17, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft };
        BodyStyle = new GUIStyle(GUI.skin.label) { fontSize = 19, alignment = TextAnchor.UpperLeft, wordWrap = true };
        KeyStyle = new GUIStyle(GUI.skin.label) { fontSize = 24, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
        KeySmallStyle = new GUIStyle(GUI.skin.label) { fontSize = 17, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
        SmallStyle = new GUIStyle(GUI.skin.label) { fontSize = 15, alignment = TextAnchor.UpperLeft, wordWrap = true };
    }

    static Texture2D MakeTriangle()
    {
        const int n = 64;
        var t = new Texture2D(n, n, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
        var px = new Color[n * n];
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float halfWidth = (1f - (float)y / (n - 1)) * 0.5f * (n - 1);          // wide at the bottom, a point at the top
                px[y * n + x] = Mathf.Abs(x - (n - 1) / 2f) <= halfWidth ? Color.white : new Color(1f, 1f, 1f, 0f);
            }
        t.SetPixels(px);
        t.Apply();
        return t;
    }

    // ---------------------------------------------------------------- the box itself
    // Starts drawing in box coordinates (0..440 x 0..200), bottom right of the screen, scaled. Call End with what Begin gave back.
    public static Rect Begin(float scale, out Matrix4x4 saved)
    {
        saved = GUI.matrix;
        Vector3 origin = new Vector3(Screen.width - BoxW * scale - 24f, Screen.height - BoxH * scale - 24f, 0f);
        GUI.matrix = saved * Matrix4x4.TRS(origin, Quaternion.identity, new Vector3(scale, scale, 1f));
        return new Rect(0f, 0f, BoxW, BoxH);
    }

    public static void End(Matrix4x4 saved)
    {
        GUI.matrix = saved;
        GUI.color = Color.white;
    }

    public static void Panel(Rect box, float a)
    {
        Round(box, new Color(0.03f, 0.05f, 0.08f, 0.9f * a), 12f);
        GUI.color = new Color(Accent.r, Accent.g, Accent.b, a);
        GUI.DrawTexture(new Rect(box.x + 14f, box.y, box.width - 28f, 3f), Texture2D.whiteTexture);
    }

    // title, body text, small text (right of the icon) and an optional progress bar
    public static void Texts(Rect box, string title, string body, string small, float progress, float a)
    {
        Text(new Rect(box.x + 20f, box.y + 12f, 300f, 26f), title, TitleStyle, new Color(Accent.r, Accent.g, Accent.b, a));
        float textX = box.x + 238f, textW = box.width - 238f - 18f;
        Text(new Rect(textX, box.y + 46f, textW, 90f), body, BodyStyle, new Color(1f, 1f, 1f, a));
        Text(new Rect(textX, box.y + 126f, textW, 60f), small, SmallStyle, new Color(0.62f, 0.66f, 0.74f, a));
        if (progress >= 0f)
        {
            Rect bar = new Rect(box.x + 20f, box.y + box.height - 18f, box.width - 40f, 5f);
            Round(bar, new Color(1f, 1f, 1f, 0.12f * a), 3f);
            if (progress > 0.01f) Round(new Rect(bar.x, bar.y, bar.width * Mathf.Clamp01(progress), bar.height), new Color(Accent.r, Accent.g, Accent.b, a), 3f);
        }
    }

    public static void DoneTick(Rect box, float a)
    {
        Round(new Rect(box.xMax - 56f, box.y + 8f, 44f, 36f), new Color(0.08f, 0.2f, 0.1f, 0.95f * a), 10f);
        Tick(new Vector2(box.xMax - 34f, box.y + 27f), 22f, new Color(Good.r, Good.g, Good.b, a));
    }

    // ---------------------------------------------------------------- the objective strip (top left)
    // "▶  Find the light switch": the same strip the cabin tutorial uses, for every level's objective.
    static GUIStyle objectiveStyle;

    // An opponent's health bar tells the strip where it is (screen pixels, top left origin). The strip stays in the top left
    // corner as long as it ends before the bar starts. If the bar is wide (a boss bar nearly as wide as the screen) the strip
    // simply drops DOWN below the bar instead, so the two never overlap, whatever width the bar has.
    // CombatHUD sets this every frame; it is empty when there is no bar.
    public static Rect ObjectiveAvoid = Rect.zero;

    // Where the strip was last drawn, so other things (the subtitles) can keep out of its way. Empty if it is not showing.
    static Rect objectiveRect; static float objectiveRectTime = -10f;
    public static Rect ObjectiveRect => Time.unscaledTime - objectiveRectTime < 0.25f ? objectiveRect : Rect.zero;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetLimit() => ObjectiveAvoid = Rect.zero;

    public static void ObjectiveStrip(string text)
    {
        if (string.IsNullOrEmpty(text)) return;
        if (objectiveStyle == null || objectiveStyle.fontSize == 0)
            objectiveStyle = new GUIStyle(GUI.skin.label) { fontSize = 20, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft, wordWrap = true };
        string line = "▶  " + text;
        const float left = 18f, padX = 12f;
        float top = 18f;
        float maxW = Screen.width - 2f * left;
        float w = Mathf.Min(objectiveStyle.CalcSize(new GUIContent(line)).x + 2f * padX + 6f, maxW);          // as wide as its text
        float h = Mathf.Max(40f, objectiveStyle.CalcHeight(new GUIContent(line), w - 2f * padX) + 8f);
        Rect bar = ObjectiveAvoid;
        if (bar.width > 0f && left + w + 14f > bar.x && top < bar.yMax && top + h > bar.y)                  // would touch the bar?
            top = bar.yMax + 10f;                                                                            // then sit just below it
        objectiveRect = new Rect(left, top, w, h); objectiveRectTime = Time.unscaledTime;
        GUI.color = new Color(0f, 0f, 0f, 0.45f);
        GUI.DrawTexture(new Rect(left, top, w, h), Texture2D.whiteTexture);
        Text(new Rect(left + padX, top, w - 2f * padX, h), line, objectiveStyle, Accent);
        GUI.color = Color.white;
    }

    // ---------------------------------------------------------------- shapes
    public static void Round(Rect r, Color c, float radius)
    {
        GUI.DrawTexture(r, Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0f, c, Vector4.zero, new Vector4(radius, radius, radius, radius));
    }

    public static void Text(Rect r, string text, GUIStyle style, Color colour)
    {
        GUI.color = Color.white;
        style.normal.textColor = colour;
        GUI.Label(r, text, style);
    }

    public static void Key(Rect r, string label, bool lit, float a, GUIStyle style = null)
    {
        Round(r, new Color(0f, 0f, 0f, 0.5f * a), 9f);
        Rect face = new Rect(r.x + 2f, r.y + 2f, r.width - 4f, r.height - 7f);
        Round(face, lit ? new Color(Accent.r, Accent.g, Accent.b, a) : new Color(0.20f, 0.23f, 0.29f, a), 8f);
        Text(face, label, style ?? KeyStyle, lit ? new Color(0.1f, 0.08f, 0.02f, a) : new Color(0.85f, 0.88f, 0.95f, a));
    }

    // Rotate what is drawn next around a point given in the CURRENT drawing coordinates
    public static void RotateAround(float angle, Vector2 pivot)
    {
        GUI.matrix = GUI.matrix * Matrix4x4.TRS(pivot, Quaternion.Euler(0f, 0f, angle), Vector3.one) * Matrix4x4.TRS(-pivot, Quaternion.identity, Vector3.one);
    }

    // a triangle pointing in a direction: 0 = up, 90 = right, 180 = down, 270 = left
    public static void Arrow(Vector2 centre, float size, float angle, Color c)
    {
        Matrix4x4 saved = GUI.matrix;
        RotateAround(angle, centre);
        GUI.color = c;
        GUI.DrawTexture(new Rect(centre.x - size / 2f, centre.y - size / 2f, size, size), triangle);
        GUI.matrix = saved;
        GUI.color = Color.white;
    }

    static void Stroke(Vector2 a, Vector2 b, float w, Color c)
    {
        Vector2 d = b - a;
        Matrix4x4 saved = GUI.matrix;
        RotateAround(Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg, a);
        GUI.color = c;
        GUI.DrawTexture(new Rect(a.x, a.y - w / 2f, d.magnitude, w), Texture2D.whiteTexture);
        GUI.matrix = saved;
        GUI.color = Color.white;
    }

    public static void Tick(Vector2 c, float s, Color col)
    {
        Stroke(c + new Vector2(-0.45f, 0.0f) * s, c + new Vector2(-0.12f, 0.32f) * s, s * 0.14f, col);
        Stroke(c + new Vector2(-0.12f, 0.32f) * s, c + new Vector2(0.48f, -0.38f) * s, s * 0.14f, col);
    }

    // a mouse; the left / right button can be lit (pulse = 0..1 makes a lit button breathe)
    public static void Mouse(Vector2 centre, bool leftLit, bool rightLit, float pulse, float a)
    {
        Rect body = new Rect(centre.x - 30f, centre.y - 46f, 60f, 92f);
        Color rim = new Color(0.75f, 0.78f, 0.85f, a);
        Round(body, rim, 30f);
        Round(new Rect(body.x + 3f, body.y + 3f, body.width - 6f, body.height - 6f), new Color(0.14f, 0.16f, 0.2f, a), 27f);
        Color lit = Color.Lerp(new Color(Accent.r * 0.7f, Accent.g * 0.7f, Accent.b * 0.7f, a), new Color(Accent.r, Accent.g, Accent.b, a), pulse);
        if (leftLit) Round(new Rect(body.x + 6f, body.y + 6f, body.width / 2f - 7f, 42f), lit, 20f);
        if (rightLit) Round(new Rect(centre.x + 1f, body.y + 6f, body.width / 2f - 7f, 42f), lit, 20f);
        GUI.color = rim;
        GUI.DrawTexture(new Rect(centre.x - 1f, body.y + 5f, 2f, 44f), Texture2D.whiteTexture);                 // split between the buttons
        GUI.DrawTexture(new Rect(body.x + 5f, body.y + 48f, body.width - 10f, 2f), Texture2D.whiteTexture);     // under the buttons
        GUI.color = Color.white;
        Round(new Rect(centre.x - 3f, body.y + 12f, 6f, 14f), new Color(0.3f, 0.3f, 0.35f, a), 3f);              // wheel
    }
}
