using System.Collections.Generic;
using UnityEngine;

// VERSION 1
// Everything you see on screen during a fight (built-in UI, no Canvas):
//  - your ATTENDANCE bar (your health) at the bottom left
//  - the professor's name and health bar at the top
//  - what he says, as subtitles under his bar
//  - short popups in the middle ("PERFECT BLOCK!", "DODGED"), a red flash when you are hit
//  - a bar under the crosshair while you charge a heavy swing
// Put it on one object in the fight room and fill in the two slots.
public class CombatHUD : MonoBehaviour
{
    public static CombatHUD Instance { get; private set; }

    public PlayerCombat player;
    public ProfessorAI enemy;

    struct Pop { public string text; public Color colour; public float at; }
    readonly List<Pop> pops = new List<Pop>();
    string subtitle = "", subtitleWho = "";
    float subtitleUntil, subtitleStart;
    Color flashColour; float flashAt = -10f, flashStrength;
    string centreText = ""; float centreUntil;
    GUIStyle labelStyle, popStyle, subStyle, bigStyle, smallStyle;
    float shownPlayer = 1f, shownEnemy = 1f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { Instance = null; playerBarHidden = false; playerBarAlpha = 1f; drawAlpha = 1f; }

    // Your attendance bar can be hidden (the boss fight hides it during the boss's intro speech) and fades back in quickly when shown again.
    static bool playerBarHidden; static float playerBarAlpha = 1f;
    static float drawAlpha = 1f;                              // multiplies everything Text() and Bar() draw (used for that fade)
    public static void SetPlayerBarHidden(bool hidden, bool instant = false)
    {
        playerBarHidden = hidden;
        if (instant) playerBarAlpha = hidden ? 0f : 1f;
    }

    void Awake() { Instance = this; }
    void OnDestroy() { if (Instance == this) Instance = null; TutorialUI.ObjectiveAvoid = Rect.zero; }

    void Start()
    {
        if (player == null) player = FindAnyObjectByType<PlayerCombat>();
        if (enemy == null) enemy = FindAnyObjectByType<ProfessorAI>();
        if (enemy != null) enemy.Barked += line => Say(enemy.displayName, line);
    }

    // ---- what other scripts call
    public static void Popup(string text, Color colour)
    {
        if (Instance == null) return;
        Instance.pops.Add(new Pop { text = text, colour = colour, at = Time.time });
    }
    public static void Flash(Color colour, float strength)
    {
        if (Instance == null) return;
        Instance.flashColour = colour; Instance.flashStrength = strength; Instance.flashAt = Time.time;
    }
    // seconds = how long it stays AFTER it has been typed out. Left out, it depends on the length of the line (ProfessorAI.ReadSeconds).
    public static void Say(string who, string line, float seconds = -1f)
    {
        if (Instance == null) return;
        if (seconds < 0f) seconds = ProfessorAI.ReadSeconds(line);
        Instance.subtitleWho = who; Instance.subtitle = line; Instance.subtitleStart = Time.time;
        Instance.subtitleUntil = Time.time + line.Length / ProfessorAI.LettersPerSecond + seconds;       // (the typing time is not part of the reading time)
    }
    public static void Centre(string text, float seconds)
    {
        if (Instance == null) return;
        Instance.centreText = text; Instance.centreUntil = Time.time + seconds;
    }

    void MakeStyles()
    {
        labelStyle = new GUIStyle(GUI.skin.label) { fontSize = 18, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft };
        smallStyle = new GUIStyle(GUI.skin.label) { fontSize = 15, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleRight };
        popStyle = new GUIStyle(GUI.skin.label) { fontSize = 36, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
        subStyle = new GUIStyle(GUI.skin.label) { fontSize = 24, alignment = TextAnchor.MiddleCenter, wordWrap = true, richText = true };
        bigStyle = new GUIStyle(GUI.skin.label) { fontSize = 48, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
    }

    static void Text(Rect r, string s, GUIStyle st, Color c)
    {
        GUI.color = Color.white;
        c.a *= drawAlpha;
        st.normal.textColor = new Color(0f, 0f, 0f, c.a * 0.7f);
        GUI.Label(new Rect(r.x + 2f, r.y + 2f, r.width, r.height), s, st);            // shadow
        st.normal.textColor = c;
        GUI.Label(r, s, st);
    }

    static void Bar(Rect r, float fill, Color colour, Color back)
    {
        back.a *= drawAlpha; colour.a *= drawAlpha;
        TutorialUI.Round(r, back, r.height / 2f);
        if (fill > 0.005f) TutorialUI.Round(new Rect(r.x, r.y, Mathf.Max(r.height, r.width * Mathf.Clamp01(fill)), r.height), colour, r.height / 2f);
    }

    void OnGUI()
    {
        if (labelStyle == null || labelStyle.fontSize == 0) MakeStyles();
        float dt = Time.unscaledDeltaTime;

        // red / white flash when something happens to you
        float fl = flashStrength * Mathf.Clamp01(1f - (Time.time - flashAt) / 0.45f);
        if (fl > 0.01f)
        {
            GUI.color = new Color(flashColour.r, flashColour.g, flashColour.b, fl * 0.45f);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = Color.white;
        }

        // your attendance
        playerBarAlpha = Mathf.MoveTowards(playerBarAlpha, playerBarHidden ? 0f : 1f, dt * 3f);          // (fades in over about a third of a second)
        if (player != null && playerBarAlpha > 0.01f)
        {
            drawAlpha = playerBarAlpha;
            shownPlayer = Mathf.MoveTowards(shownPlayer, player.Health.Fraction, dt * 1.5f);
            Rect r = new Rect(24f, Screen.height - 48f, 300f, 16f);
            Text(new Rect(r.x, r.y - 24f, 200f, 22f), "ATTENDANCE", labelStyle, TutorialUI.Accent);
            Text(new Rect(r.x + 100f, r.y - 24f, 200f, 22f), Mathf.CeilToInt(player.Health.Current) + "%", smallStyle, Color.white);
            Bar(r, shownPlayer, Color.Lerp(new Color(0.9f, 0.25f, 0.2f), new Color(0.4f, 0.9f, 0.5f), player.Health.Fraction), new Color(0f, 0f, 0f, 0.55f));
            drawAlpha = 1f;
        }

        // the opponent's bar + name and what he says are drawn in a 'virtual' screen that is scaled up with the real one (S),
        // so they are not tiny in full screen
        float S = TutorialUI.SizeFactor * 1.05f;
        float sw = Screen.width / S;
        Matrix4x4 savedMatrix = GUI.matrix;
        GUI.matrix = Matrix4x4.Scale(new Vector3(S, S, 1f));

        // the professor
        TutorialUI.ObjectiveAvoid = Rect.zero;                  // (nothing in the way, unless his bar is drawn below)
        if (enemy != null && !enemy.Health.Dead)
        {
            shownEnemy = Mathf.MoveTowards(shownEnemy, enemy.Health.Fraction, dt * 1.5f);
            float w = enemy.barScreenFraction > 0f ? sw * enemy.barScreenFraction : Mathf.Min(enemy.healthBarWidth, sw - 60f);
            Rect r = new Rect(sw / 2f - w / 2f, 40f, w, 14f);
            TutorialUI.ObjectiveAvoid = new Rect((r.x - 3f) * S, (r.y - 33f) * S, (w + 6f) * S, (33f + r.height + 24f) * S);      // (screen pixels) the objective strip keeps out of this (it drops below a wide bar)
            Text(new Rect(r.x, r.y - 31f, w, 28f), enemy.displayName.ToUpper(), labelStyle, Color.Lerp(enemy.healthBarColour, Color.white, 0.75f));
            TutorialUI.Round(new Rect(r.x - 3f, r.y - 3f, r.width + 6f, r.height + 6f), new Color(0.05f, 0.02f, 0.1f, 0.85f), r.height / 2f + 3f);   // dark frame so it stands out on any room colour
            Bar(r, shownEnemy, enemy.IsStunned ? new Color(1f, 0.9f, 0.3f) : enemy.healthBarColour, new Color(0.12f, 0.06f, 0.2f, 0.95f));
            // boss: thin notches where the next phase starts, and the phase name under the bar's left end
            if (enemy.barNotches != null)
                foreach (float n in enemy.barNotches)
                {
                    float nx = r.x + r.height / 2f + (r.width - r.height) * Mathf.Clamp01(n);
                    GUI.color = new Color(1f, 1f, 1f, 0.8f);
                    GUI.DrawTexture(new Rect(nx - 1f, r.y - 2f, 2f, r.height + 4f), Texture2D.whiteTexture);
                    GUI.color = Color.white;
                }
            if (!string.IsNullOrEmpty(enemy.barTag))
            {
                GUIStyle left = new GUIStyle(smallStyle) { alignment = TextAnchor.MiddleLeft };
                Text(new Rect(r.x, r.yMax + 2f, w * 0.5f, 22f), enemy.barTag.ToUpper(), left, Color.Lerp(enemy.healthBarColour, Color.white, 0.5f));
            }
            if (enemy.IsStunned) Text(new Rect(r.x, r.yMax + 2f, w, 22f), "STUNNED: DOUBLE DAMAGE", smallStyle, new Color(1f, 0.9f, 0.3f));
        }

        // what he says: at the BOTTOM of the screen, just above your attendance bar. It is centred, at most 820 wide, and long lines wrap onto
        // more lines (the box grows upwards). While a tutorial box is showing at the bottom right, the dialogue stays to the left of it.
        if (Time.time < subtitleUntil)
        {
            float a = Mathf.Clamp01((subtitleUntil - Time.time) / 0.4f);
            // always centred on the SCREEN. When the tutorial box is showing at the bottom right, the dialogue only gets narrower
            // (equally on both sides, so it stays centred) until it ends 20 px before that box
            float cx = sw / 2f;
            float halfWidth = Mathf.Min(820f, sw - 80f) / 2f;
            Rect panel = TutorialUI.BoxRect;                                          // (screen pixels)
            if (panel.width > 0f) halfWidth = Mathf.Min(halfWidth, panel.x / S - 20f / S - cx);
            halfWidth = Mathf.Max(halfWidth, 120f);
            float maxWidth = halfWidth * 2f;

            // the full line, so the box is as big as the finished text needs (the part not typed yet is invisible but already takes its place)
            string plainFull = "<b>" + subtitleWho + ":</b>  " + subtitle;
            // as wide as the text (plus a little room at both ends); only a line too long for the space is wrapped, and then it uses all of it
            float w = Mathf.Min(maxWidth, subStyle.CalcSize(new GUIContent(plainFull)).x + 40f);
            float h = Mathf.Max(48f, subStyle.CalcHeight(new GUIContent(plainFull), w) + 16f);       // (one line = the usual 48 high box)
            float bottom = (Screen.height - 90f) / S;                                   // above the attendance bar (which is at the bottom left)
            Rect r = new Rect(cx - w / 2f, bottom - h, w, h);
            GUI.color = new Color(0f, 0f, 0f, 0.45f * a);
            GUI.DrawTexture(r, Texture2D.whiteTexture);
            // the speaker's name is bold and coloured like their health bar, so it stands apart from what they say
            Color nameColour = enemy != null ? Color.Lerp(enemy.healthBarColour, Color.white, 0.45f) : new Color(0.8f, 0.7f, 1f);
            string hex = ColorUtility.ToHtmlStringRGB(nameColour);
            int count = Mathf.Min(subtitle.Length, Mathf.FloorToInt((Time.time - subtitleStart) * ProfessorAI.LettersPerSecond));
            // the letters not typed yet are still there but invisible, so the centred line does not shift sideways as it types
            string typed = subtitle.Substring(0, count) + "<color=#00000000>" + subtitle.Substring(count) + "</color>";      // typed out like Gemini's speech
            string rich = "<b><color=#" + hex + ">" + subtitleWho + ":</color></b>  " + typed;
            string plain = "<b>" + subtitleWho + ":</b>  " + typed;
            GUI.color = Color.white;
            subStyle.normal.textColor = new Color(0f, 0f, 0f, 0.7f * a);
            GUI.Label(new Rect(r.x + 2f, r.y + 2f, r.width, r.height), plain, subStyle);               // shadow (plain colour)
            subStyle.normal.textColor = new Color(1f, 1f, 1f, a);
            GUI.Label(r, rich, subStyle);
        }

        GUI.matrix = savedMatrix;

        // popups rise and fade
        for (int i = pops.Count - 1; i >= 0; i--)
        {
            float age = Time.time - pops[i].at;
            if (age > 0.9f) { pops.RemoveAt(i); continue; }
            Color c = pops[i].colour; c.a = 1f - Mathf.Clamp01((age - 0.5f) / 0.4f);
            Text(new Rect(0f, Screen.height * 0.3f - age * 40f, Screen.width, 60f), pops[i].text, popStyle, c);
        }

        // charging a heavy swing
        if (player != null && player.Charging)
        {
            Rect r = new Rect(Screen.width / 2f - 44f, Screen.height / 2f + 26f, 88f, 8f);
            Bar(r, player.Charge01, player.Charge01 >= 1f ? new Color(1f, 0.85f, 0.3f) : new Color(1f, 1f, 1f, 0.85f), new Color(0f, 0f, 0f, 0.5f));
        }

        // a big message in the middle (e.g. when you are beaten)
        if (Time.time < centreUntil)
        {
            GUI.color = new Color(0f, 0f, 0f, 0.6f);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
            Text(new Rect(0f, Screen.height / 2f - 40f, Screen.width, 80f), centreText, bigStyle, Color.white);
        }
        GUI.color = Color.white;
    }
}
