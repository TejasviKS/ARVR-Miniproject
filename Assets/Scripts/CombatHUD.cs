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
    float subtitleUntil;
    Color flashColour; float flashAt = -10f, flashStrength;
    string centreText = ""; float centreUntil;
    GUIStyle labelStyle, popStyle, subStyle, bigStyle, smallStyle;
    float shownPlayer = 1f, shownEnemy = 1f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() => Instance = null;

    void Awake() { Instance = this; }
    void OnDestroy() { if (Instance == this) Instance = null; }

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
    public static void Say(string who, string line, float seconds = 3.4f)
    {
        if (Instance == null) return;
        Instance.subtitleWho = who; Instance.subtitle = line; Instance.subtitleUntil = Time.time + seconds;
    }
    public static void Centre(string text, float seconds)
    {
        if (Instance == null) return;
        Instance.centreText = text; Instance.centreUntil = Time.time + seconds;
    }

    void MakeStyles()
    {
        labelStyle = new GUIStyle(GUI.skin.label) { fontSize = 15, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft };
        smallStyle = new GUIStyle(GUI.skin.label) { fontSize = 15, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleRight };
        popStyle = new GUIStyle(GUI.skin.label) { fontSize = 36, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
        subStyle = new GUIStyle(GUI.skin.label) { fontSize = 20, alignment = TextAnchor.MiddleCenter, wordWrap = true };
        bigStyle = new GUIStyle(GUI.skin.label) { fontSize = 48, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
    }

    static void Text(Rect r, string s, GUIStyle st, Color c)
    {
        GUI.color = Color.white;
        st.normal.textColor = new Color(0f, 0f, 0f, c.a * 0.7f);
        GUI.Label(new Rect(r.x + 2f, r.y + 2f, r.width, r.height), s, st);            // shadow
        st.normal.textColor = c;
        GUI.Label(r, s, st);
    }

    static void Bar(Rect r, float fill, Color colour, Color back)
    {
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
        if (player != null)
        {
            shownPlayer = Mathf.MoveTowards(shownPlayer, player.Health.Fraction, dt * 1.5f);
            Rect r = new Rect(24f, Screen.height - 48f, 300f, 16f);
            Text(new Rect(r.x, r.y - 24f, 200f, 22f), "ATTENDANCE", labelStyle, TutorialUI.Accent);
            Text(new Rect(r.x + 100f, r.y - 24f, 200f, 22f), Mathf.CeilToInt(player.Health.Current) + "%", smallStyle, Color.white);
            Bar(r, shownPlayer, Color.Lerp(new Color(0.9f, 0.25f, 0.2f), new Color(0.4f, 0.9f, 0.5f), player.Health.Fraction), new Color(0f, 0f, 0f, 0.55f));
        }

        // the professor
        if (enemy != null && !enemy.Health.Dead)
        {
            shownEnemy = Mathf.MoveTowards(shownEnemy, enemy.Health.Fraction, dt * 1.5f);
            float w = Mathf.Min(enemy.healthBarWidth, Screen.width - 60f);
            Rect r = new Rect(Screen.width / 2f - w / 2f, 40f, w, 14f);
            Text(new Rect(r.x, r.y - 26f, w, 24f), enemy.displayName.ToUpper(), labelStyle, Color.Lerp(enemy.healthBarColour, Color.white, 0.75f));
            TutorialUI.Round(new Rect(r.x - 3f, r.y - 3f, r.width + 6f, r.height + 6f), new Color(0.05f, 0.02f, 0.1f, 0.85f), r.height / 2f + 3f);   // dark frame so it stands out on any room colour
            Bar(r, shownEnemy, enemy.IsStunned ? new Color(1f, 0.9f, 0.3f) : enemy.healthBarColour, new Color(0.12f, 0.06f, 0.2f, 0.95f));
            if (enemy.IsStunned) Text(new Rect(r.x, r.yMax + 2f, w, 22f), "STUNNED: DOUBLE DAMAGE", smallStyle, new Color(1f, 0.9f, 0.3f));
        }

        // what he says
        if (Time.time < subtitleUntil)
        {
            float w = Mathf.Min(760f, Screen.width - 80f);
            float a = Mathf.Clamp01((subtitleUntil - Time.time) / 0.4f);
            Rect r = new Rect(Screen.width / 2f - w / 2f, 92f, w, 40f);
            GUI.color = new Color(0f, 0f, 0f, 0.45f * a);
            GUI.DrawTexture(r, Texture2D.whiteTexture);
            Text(r, subtitleWho + ":  " + subtitle, subStyle, new Color(1f, 1f, 1f, a));
        }

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
