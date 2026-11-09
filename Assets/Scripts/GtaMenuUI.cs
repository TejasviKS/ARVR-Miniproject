using UnityEngine;

// VERSION 1
// The drawing tools shared by the main menu and the pause menu, in the style of the "wasted" and "mission passed" screens:
// the big Pricedown title with a dark edge, and the rounded pill button (dark with a thin grey rim; it turns gold or red when you point at it,
// and the first letters are a little taller).
public static class GtaMenuUI
{
    // The text with a thick dark edge around the letters (drawn a few pixels away in 8 directions, then the real text on top)
    public static void Outlined(Rect r, string text, GUIStyle style, Color colour, float thickness, float alpha = 1f)
    {
        Color edge = new Color(0.03f, 0f, 0.01f, alpha);
        for (int i = 0; i < 8; i++)
        {
            float a = i * Mathf.PI / 4f;
            WastedScreen.Pin(style, edge);
            GUI.Label(new Rect(r.x + Mathf.Cos(a) * thickness, r.y + Mathf.Sin(a) * thickness, r.width, r.height), text, style);
        }
        colour.a *= alpha;
        WastedScreen.Pin(style, colour);
        GUI.Label(r, text, style);
    }

    // Makes the style the buttons use (the Pricedown font if it is in the project)
    public static GUIStyle ButtonStyle()
    {
        Font f = WastedScreen.PickFont();
        var s = new GUIStyle(GUI.skin.label) { fontStyle = f != null ? FontStyle.Normal : FontStyle.Bold, alignment = TextAnchor.MiddleCenter, richText = true };
        if (f != null) s.font = f;
        return s;
    }

    public static GUIStyle TitleStyle()
    {
        Font f = WastedScreen.PickFont();
        var s = new GUIStyle(GUI.skin.label) { fontStyle = f != null ? FontStyle.Normal : FontStyle.Bold, alignment = TextAnchor.MiddleCenter, wordWrap = false };
        if (f != null) s.font = f;
        return s;
    }

    // The same rounded button as "Try Again" (red when you point at it) and "Main Menu" (gold when you point at it).
    // label uses {0} for the taller first letters: "<size={0}>P</size>lay". Returns true when it is clicked.
    public static bool Pill(Rect b, string label, float a, bool gold, GUIStyle style)
    {
        bool hover = b.Contains(Event.current.mousePosition);
        float rad = b.height * 0.32f;
        Color rimHover = gold ? new Color(1f, 0.85f, 0.35f, a) : new Color(0.9f, 0.25f, 0.27f, a);
        Color fillHover = gold ? new Color(0.86f, 0.64f, 0.08f, 0.95f * a) : new Color(0.72f, 0.04f, 0.07f, 0.95f * a);
        TutorialUI.Round(new Rect(b.x - 2f, b.y - 2f, b.width + 4f, b.height + 4f), hover ? rimHover : new Color(0.62f, 0.62f, 0.68f, 0.7f * a), rad + 2f);
        TutorialUI.Round(b, hover ? fillHover : new Color(0.07f, 0.07f, 0.09f, 0.82f * a), rad);
        style.fontSize = Mathf.RoundToInt(b.height * 0.52f);
        WastedScreen.Pin(style, gold && hover ? new Color(0.05f, 0.04f, 0.02f, a) : new Color(0.95f, 0.95f, 0.97f, a));
        GUI.color = Color.white;
        int big = Mathf.RoundToInt(style.fontSize * 1.3f);
        GUI.Label(b, string.Format(label, big), style);
        return GUI.Button(b, GUIContent.none, GUIStyle.none) && a >= 0.99f;
    }
}
