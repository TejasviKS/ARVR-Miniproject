using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

// VERSION 2
// The credits, in the style of the rest of the game. Started by the Credits button of the main menu (it makes its own object).
// The menu's poster (sky, sun and the five cards) stays; only the menu's writing (title, buttons, rating box) fades away, and the poster is
// dimmed a little so the credits read on top of it.
//
//   0.0 - 1.5 s   the menu's writing fades away and its music fades out
//   1.5 - 3.0 s   silence
//   3.0 s         the credits music starts: its opening snare is the BANG (a fireball, smoke, sparks, a shockwave, a screen shake) and
//                 the first credit, "Director: Tanmay T A", slams out of it
//   +3 s          a brief pause, then the credits roll up the screen (role in gold, name in white), ending on "Made by Kengeri Studios"
//   then          a pause, the credits fade out, "AVR MINI-PROJECT" is shown for 5 s, and the credits fade away: the menu's writing returns
//
// The credits music (Music/credits.ogg) is NOT tied to the screen: it plays to its end (see MusicDirector), and only Play cuts it.
// Esc, Backspace, Space or Enter skips the credits and goes back to the menu.
public class CreditsScreen : MonoBehaviour
{
    public static bool Active { get; private set; }

    // how visible the main menu's writing is (title, buttons, rating box): 1 normally, 0 while the credits are on. The menu multiplies by this.
    public static float MenuUiAlpha { get; private set; } = 1f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { Active = false; MenuUiAlpha = 1f; }

    public static void Begin()
    {
        if (Active) return;
        Active = true;
        new GameObject("CreditsScreen").AddComponent<CreditsScreen>();
    }

    // ---------------------------------------------------------------- the words
    // (role, person) in the order they roll
    static readonly string[][] People =
    {
        new[] { "Director", "Tanmay T A" },
        new[] { "Game Concept", "Shashank Arya" },
        new[] { "Game Design", "Tanmay T A" },
        new[] { "Puzzle Design", "Pramiti Udupa" },
        new[] { "Combat Design", "Shashank Arya" },
        new[] { "Audio Puzzle Design", "Tejasvi K S" },
        new[] { "Level Design", "Tejasvi K S" },
        new[] { "Quiz Writing", "Pramiti Udupa" },
        new[] { "World Design", "Shashank Arya" },
        new[] { "Lead Programming", "Tanmay T A" },
        new[] { "Combat Playtesting", "Shashank Arya" },
        new[] { "Art Direction", "Tanmay T A" },
        new[] { "UI Design", "Tanmay T A" },
        new[] { "Creative Consultant", "Pramiti Udupa" },
        new[] { "Audio Director", "Tanmay T A" },
        new[] { "Puzzle Playtesting", "Pramiti Udupa" },
        new[] { "Music Composed by", "Tanmay T A" },
        new[] { "Sound Design", "Tanmay T A" },
    };
    static readonly string[][] Thanks =
    {
        new[] { "3D Building Models", "Cave Labs @ PES University" },
        new[] { "AVR Course Faculty", "Dr. Adithya Balasubramanyam" },
    };
    static readonly string[][] Songs =
    {
        new[] { "Aria Math (Drum Cover)", "Drummy Walnut" },
        new[] { "Halo 3 Warthog Run (Complete Version)", "Dirghadarzana" },
        new[] { "Hardpoint Shipment", "Speedbird-One" },
        new[] { "Main Menu Theme", "Speedbird-One" },
    };

    // ---------------------------------------------------------------- timing (seconds since the button was pressed)
    const float FadeOut = 1.5f, Silence = 1.5f;
    const float Bang = FadeOut + Silence;               // the credits music starts, the bang
    const float TitlePause = 3.8f;                     // the first credit sits alone this long after the bang, then everything rolls
    const float RollStart = Bang + TitlePause;
    const float EndHold = 2.5f;                         // the last card (Kengeri Studios) stays a moment
    const float ContentFade = 0.8f;
    const float TitleIn = 0.8f, TitleShown = 5.0f, TitleOut = 1.0f;     // "AVR MINI-PROJECT": about 5 s on screen
    const float VeilFade = 1.2f;                        // then the credits fade away and the menu's writing comes back
    const float ScrollSpeed = 0.085f;                   // screen heights per second
    const float Dim = 0.58f;                            // how dark the poster is behind the credits

    float start;
    bool musicStarted, skipping;
    float skipAt;
    Texture2D glow, ring, kengeri;
    GUIStyle big, small;

    void Start()
    {
        start = Time.unscaledTime;
        MusicDirector.StopMenu(FadeOut);                 // the menu music fades out
        MusicDirector.StopCredits(FadeOut);              // (and the credits music, if it is still going from an earlier time)
        glow = MakeGlow(); ring = MakeRing();
        kengeri = SplashScreen.Circle(Resources.Load<Texture2D>("Logos/kengeri_studios"), 0.995f);
    }

    void OnDestroy() { Active = false; MenuUiAlpha = 1f; }

    // ---------------------------------------------------------------- the layout of the rolling part (in pixels, from the top of the screen)
    struct Item { public int kind; public string a, b; public float y, height; }     // kind 1 entry, 2 section header, 3 Kengeri
    List<Item> layout;
    float layoutH, endScrollMax;

    void Build(float h)
    {
        layout = new List<Item>(); layoutH = h;
        float y = h * 0.43f;                                                              // the first credit sits in the middle of the screen
        foreach (var p in People) { layout.Add(new Item { kind = 1, a = p[0], b = p[1], y = y, height = h * 0.15f }); y += h * 0.17f; }
        y += h * 0.12f;
        layout.Add(new Item { kind = 2, a = "Special Thanks", y = y, height = h * 0.1f }); y += h * 0.13f;
        foreach (var p in Thanks) { layout.Add(new Item { kind = 1, a = p[0], b = p[1], y = y, height = h * 0.15f }); y += h * 0.17f; }
        y += h * 0.12f;
        layout.Add(new Item { kind = 2, a = "Music", y = y, height = h * 0.1f }); y += h * 0.13f;
        foreach (var p in Songs) { layout.Add(new Item { kind = 1, a = p[0], b = p[1], y = y, height = h * 0.15f }); y += h * 0.17f; }
        y += h * 0.25f;
        float kh = h * 0.56f;
        layout.Add(new Item { kind = 3, a = "Made by", b = "Kengeri Studios", y = y, height = kh });
        endScrollMax = y + kh / 2f - h * 0.5f;                                            // scroll until the last card is in the middle
    }

    float RollSeconds(float h) => endScrollMax / (ScrollSpeed * h);

    float TitleStart(float h) => RollStart + RollSeconds(h) + EndHold + ContentFade + 0.3f;
    float VeilStart(float h) => TitleStart(h) + TitleShown - 0.4f;
    float EndTime(float h) => VeilStart(h) + VeilFade;

    // ---------------------------------------------------------------- input
    void Update()
    {
        float now = Time.unscaledTime, t = now - start;
        if (!musicStarted && t >= Bang && !skipping) { musicStarted = true; MusicDirector.PlayCredits(); }
        Keyboard kb = Keyboard.current;
        if (!skipping && kb != null && t > 0.2f && (kb.escapeKey.wasPressedThisFrame || kb.backspaceKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame
                                                    || kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame))
        {
            skipping = true; skipAt = now;
            if (!musicStarted && !MusicDirector.CreditsPlaying) MusicDirector.PlayMenu(1f);          // left before the music began: the menu music comes back
        }
        if (skipping && now - skipAt > 0.8f) { Destroy(gameObject); return; }
        float h = Screen.height;
        if (layout == null || Mathf.Abs(layoutH - h) > 0.5f) Build(h);
        if (t >= EndTime(h)) { Destroy(gameObject); return; }

        // the menu's writing: fades out at the start, comes back as the credits fade away
        float ui = t < VeilStart(h) ? 1f - Mathf.Clamp01(t / FadeOut) : Mathf.Clamp01((t - VeilStart(h)) / VeilFade);
        if (skipping) ui = Mathf.Max(ui, Mathf.Clamp01((now - skipAt) / 0.8f));
        MenuUiAlpha = ui;
    }

    // ---------------------------------------------------------------- drawing
    void Fill(Rect r, Color c) { GUI.color = c; GUI.DrawTexture(r, Texture2D.whiteTexture); GUI.color = Color.white; }

    void OnGUI()
    {
        GUI.depth = -500;                                    // on top of the menu
        float w = Screen.width, h = Screen.height;
        if (layout == null || Mathf.Abs(layoutH - h) > 0.5f) Build(h);
        if (big == null || big.fontSize == 0) { big = GtaMenuUI.TitleStyle(); small = GtaMenuUI.TitleStyle(); }
        float now = Time.unscaledTime, t = now - start;
        if (t < Bang) return;                                // (the menu's poster is all there is until the bang)
        float skipK = skipping ? Mathf.Clamp01((now - skipAt) / 0.8f) : 0f;
        float age = t - Bang;                                // seconds since the bang

        // ---- when does each part end? ----
        float contentFadeStart = RollStart + RollSeconds(h) + EndHold;
        float titleStart = TitleStart(h);
        float veil = 1f - Mathf.Clamp01((t - VeilStart(h)) / VeilFade);               // the credits fading away
        if (skipping) veil = Mathf.Min(veil, 1f - skipK);
        if (veil <= 0f) return;

        // ---- the poster is dimmed behind the credits ----
        Fill(new Rect(0, 0, w, h), new Color(0.03f, 0.01f, 0.06f, Dim * Mathf.Clamp01(age / 0.5f) * veil));

        // ---- the screen shake of the bang ----
        Matrix4x4 saved = GUI.matrix;
        float shake = Mathf.Exp(-age * 5.5f) * h * 0.02f;
        if (age < 1.2f) GUI.matrix = Matrix4x4.TRS(new Vector3(Mathf.Sin(age * 91f) * shake, Mathf.Cos(age * 77f) * shake, 0f), Quaternion.identity, Vector3.one);

        // ---- the rolling text ----
        float scroll = Mathf.Min(endScrollMax, Mathf.Max(0f, (t - RollStart) * ScrollSpeed * h));
        float contentA = veil * (1f - Mathf.Clamp01((t - contentFadeStart) / ContentFade));
        float laterIn = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((t - (RollStart - 0.3f)) / 0.9f));      // everything after the first credit appears as the roll starts
        if (contentA > 0f)
        {
            for (int i = 0; i < layout.Count; i++)
            {
                Item it = layout[i];
                float y = it.y - scroll;
                if (y > h * 1.1f || y + it.height < -h * 0.1f) continue;
                // everything fades a little as it leaves the top and the bottom edge
                float edge = Mathf.Clamp01(Mathf.Min((y + it.height * 0.5f) / (h * 0.12f), (h - (y + it.height * 0.5f)) / (h * 0.06f)));
                float a = contentA * Mathf.Max(edge, 0f) * (i == 0 ? 1f : laterIn);
                if (i == 0) DrawDirector(w, h, it, age, contentA);                      // the first credit has its own big card, it does not roll
                else if (it.kind == 1) DrawEntry(w, h, y, it, a, -1f);
                else if (it.kind == 2) DrawHeader(w, h, y, it, a);
                else DrawKengeri(w, h, y, it, a);
            }
        }

        // ---- "AVR MINI-PROJECT", like the title card of a game ----
        float tt = t - titleStart;
        if (tt > 0f)
        {
            float ta = Mathf.Clamp01(tt / TitleIn) * (1f - Mathf.Clamp01((tt - (TitleShown - TitleOut)) / TitleOut));
            ta = Mathf.SmoothStep(0f, 1f, ta) * veil;
            big.alignment = TextAnchor.MiddleCenter;
            big.fontSize = Mathf.RoundToInt(h * 0.17f);
            float zoom = 1f + 0.04f * (1f - Mathf.Clamp01(tt / 4f));
            Matrix4x4 m = GUI.matrix;
            GUIUtility.ScaleAroundPivot(new Vector2(zoom, zoom), new Vector2(w / 2f, h / 2f));
            GtaMenuUI.Outlined(new Rect(0, h * 0.5f - h * 0.12f, w, h * 0.24f), "AVR Mini-Project", big, new Color(0.97f, 0.97f, 0.98f), Mathf.Max(3f, h * 0.008f), ta);
            GUI.matrix = m;
        }
        GUI.matrix = saved;

        // ---- the bang itself ----
        if (age < 2.4f && !skipping) DrawBang(w, h, age, veil);
        GUI.color = Color.white;
    }

    static readonly Color Gold = new Color(0.96f, 0.76f, 0.16f);

    // The first credit: big, in the middle of the screen. It slams out of the explosion (bigger for an instant, then it settles), stays a
    // moment and fades out before the rolling credits come in.
    const float DirectorHold = 2.5f, DirectorFade = 0.6f;       // (the smoke is gone by about 1.5 s; it then hovers for a second before fading)
    void DrawDirector(float w, float h, Item it, float age, float contentA)
    {
        float a = contentA * Mathf.Clamp01((age - 0.06f) / 0.08f) * (1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((age - DirectorHold) / DirectorFade)));
        if (a <= 0f) return;
        small.alignment = TextAnchor.MiddleCenter; big.alignment = TextAnchor.MiddleCenter;
        small.fontSize = Mathf.RoundToInt(h * 0.04f);
        big.fontSize = Mathf.RoundToInt(h * 0.1f);
        float k = Mathf.Clamp01((age - 0.08f) / 0.3f), scale = Mathf.Lerp(1.5f, 1f, Ease(k));
        float top = h * 0.5f - h * 0.09f;
        Matrix4x4 m = GUI.matrix;
        GUIUtility.ScaleAroundPivot(new Vector2(scale, scale), new Vector2(w / 2f, h * 0.5f));
        GtaMenuUI.Outlined(new Rect(0, top, w, h * 0.05f), it.a.ToUpperInvariant(), small, Gold, Mathf.Max(2f, h * 0.0035f), a);
        GtaMenuUI.Outlined(new Rect(0, top + h * 0.048f, w, h * 0.13f), it.b, big, new Color(0.97f, 0.97f, 0.98f), Mathf.Max(3f, h * 0.0055f), a);
        GUI.matrix = m;
    }

    // slamAge >= 0: (no longer used by the first credit; kept for a plain entry)
    void DrawEntry(float w, float h, float y, Item it, float a, float slamAge)
    {
        small.alignment = TextAnchor.MiddleCenter; big.alignment = TextAnchor.MiddleCenter;
        small.fontSize = Mathf.RoundToInt(h * 0.034f);
        big.fontSize = Mathf.RoundToInt(h * 0.062f);
        Matrix4x4 m = GUI.matrix;
        if (slamAge >= 0f && slamAge < 0.5f)
        {
            float k = Mathf.Clamp01((slamAge - 0.08f) / 0.3f), scale = Mathf.Lerp(1.9f, 1f, 1f - (1f - k) * (1f - k));
            a *= Mathf.Clamp01((slamAge - 0.06f) / 0.08f);
            GUIUtility.ScaleAroundPivot(new Vector2(scale, scale), new Vector2(w / 2f, y + h * 0.07f));
        }
        else if (slamAge >= 0f && slamAge < 0.06f) a = 0f;
        GtaMenuUI.Outlined(new Rect(0, y, w, h * 0.05f), it.a.ToUpperInvariant(), small, Gold, Mathf.Max(2f, h * 0.003f), a);
        GtaMenuUI.Outlined(new Rect(0, y + h * 0.048f, w, h * 0.09f), it.b, big, new Color(0.97f, 0.97f, 0.98f), Mathf.Max(2f, h * 0.0045f), a);
        GUI.matrix = m;
    }

    void DrawHeader(float w, float h, float y, Item it, float a)
    {
        big.alignment = TextAnchor.MiddleCenter;
        big.fontSize = Mathf.RoundToInt(h * 0.085f);
        GtaMenuUI.Outlined(new Rect(0, y, w, h * 0.11f), it.a, big, Gold, Mathf.Max(3f, h * 0.0055f), a);
        Fill(new Rect(w * 0.5f - h * 0.28f, y + h * 0.115f, h * 0.56f, Mathf.Max(2f, h * 0.004f)), new Color(Gold.r, Gold.g, Gold.b, 0.8f * a));
    }

    void DrawKengeri(float w, float h, float y, Item it, float a)
    {
        small.alignment = TextAnchor.MiddleCenter; big.alignment = TextAnchor.MiddleCenter;
        float d = h * 0.34f;
        if (kengeri != null) { GUI.color = new Color(1f, 1f, 1f, a); GUI.DrawTexture(new Rect(w / 2f - d / 2f, y, d, d), kengeri); GUI.color = Color.white; }
        small.fontSize = Mathf.RoundToInt(h * 0.034f);
        big.fontSize = Mathf.RoundToInt(h * 0.075f);
        GtaMenuUI.Outlined(new Rect(0, y + d + h * 0.025f, w, h * 0.05f), it.a.ToUpperInvariant(), small, Gold, Mathf.Max(2f, h * 0.003f), a);
        GtaMenuUI.Outlined(new Rect(0, y + d + h * 0.07f, w, h * 0.1f), it.b, big, new Color(0.97f, 0.97f, 0.98f), Mathf.Max(3f, h * 0.0055f), a);
    }

    // ---------------------------------------------------------------- the bang
    // A short hot flash, a lumpy fireball that swells and cools from white-yellow to orange to dull red, dark smoke that rolls out and rises,
    // sparks that fly out as streaks and fall, and a thin shockwave. Everything is worked out from the age, so there is nothing to keep.
    void Blob(Vector2 p, float size, Color c)
    {
        if (c.a <= 0.002f || size <= 1f) return;
        GUI.color = c;
        GUI.DrawTexture(new Rect(p.x - size / 2f, p.y - size / 2f, size, size), glow);
    }

    void DrawBang(float w, float h, float age, float veil)
    {
        Vector2 c = new Vector2(w / 2f, h * 0.46f);
        float R = h * 0.42f;

        // --- smoke: dark, slow, it starts a little after the flame and rises ---
        for (int i = 0; i < 11; i++)
        {
            float st = 0.10f + 0.08f * Hash(i * 2.1f), life = 0.95f + 0.35f * Hash(i * 3.7f);        // (the smoke clears quickly)
            float k = (age - st) / life;
            if (k <= 0f || k >= 1f) continue;
            float ang = Hash(i * 1.3f) * Mathf.PI * 2f, dist = R * (0.15f + 0.55f * Hash(i * 5.1f));
            Vector2 p = c + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * dist * (0.4f + 0.6f * Ease(Mathf.Clamp01(age / 0.7f))) + new Vector2(0f, -h * 0.07f * age);
            float size = R * Mathf.Lerp(0.9f, 1.7f, Hash(i * 4.3f)) * Mathf.Lerp(0.5f, 1f, Ease(Mathf.Clamp01(k * 2f)));
            float a = 0.5f * Mathf.Sin(Mathf.Clamp01(k) * Mathf.PI) * veil;
            Blob(p, size, new Color(0.10f, 0.085f, 0.085f, a));
        }

        // --- the fireball: many overlapping soft blobs, so its outline is lumpy ---
        float grow = Ease(Mathf.Clamp01(age / 0.42f));
        for (int i = 0; i < 16; i++)
        {
            float ang = Hash(i * 1.7f + 3f) * Mathf.PI * 2f, dist = R * 0.42f * Mathf.Sqrt(Hash(i * 6.1f + 1f));
            Vector2 p = c + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * dist * grow + new Vector2(0f, -h * 0.03f * Mathf.Max(0f, age - 0.2f));
            float size = R * Mathf.Lerp(0.55f, 1.05f, Hash(i * 2.9f)) * Mathf.Lerp(0.15f, 1f, grow);
            float life = Mathf.Lerp(0.9f, 1.5f, Hash(i * 8.3f));
            float k = Mathf.Clamp01(age / life);                                           // 0 young and hot .. 1 gone
            Color hot = Color.Lerp(new Color(1f, 0.97f, 0.75f), new Color(1f, 0.55f, 0.12f), Mathf.Clamp01(k * 2.2f));
            Color col = Color.Lerp(hot, new Color(0.38f, 0.1f, 0.05f), Mathf.Clamp01((k - 0.35f) / 0.65f));
            col.a = Mathf.Clamp01(age / 0.05f) * (1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((k - 0.3f) / 0.7f))) * 0.95f * veil;
            Blob(p, size, col);
        }
        // the white-hot core, brief
        float core = (1f - Mathf.Clamp01(age / 0.28f)); core *= core;
        Blob(c, R * Mathf.Lerp(0.5f, 1.3f, grow), new Color(1f, 0.98f, 0.9f, core * veil));
        // and a smaller, yellower heart that outlasts it, so the fireball is bright in the middle and orange at the edge
        float heart = 1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((age - 0.1f) / 0.6f));
        Blob(c + new Vector2(0f, -h * 0.01f), R * Mathf.Lerp(0.35f, 0.8f, grow), Color.Lerp(new Color(1f, 0.9f, 0.45f), new Color(1f, 0.7f, 0.2f), Mathf.Clamp01(age / 0.6f)) * new Color(1f, 1f, 1f, heart * 0.9f * veil));

        // --- sparks: thin streaks along their path, they fall under gravity and cool ---
        for (int i = 0; i < 70; i++)
        {
            float ang = Hash(i * 3.1f + 9f) * Mathf.PI * 2f, spd = h * Mathf.Lerp(0.5f, 2.0f, Hash(i * 7.7f + 2f)), life = Mathf.Lerp(0.6f, 1.6f, Hash(i * 5.3f + 4f));
            if (age > life) continue;
            Vector2 v = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * spd;
            float drag = Mathf.Exp(-age * 1.4f);
            Vector2 p = c + v * (1f - drag) / 1.4f + new Vector2(0f, h * 0.9f * age * age);
            Vector2 vel = v * drag + new Vector2(0f, h * 1.8f * age);
            float len = Mathf.Clamp(vel.magnitude * 0.035f, h * 0.004f, h * 0.06f), thick = Mathf.Max(1.5f, h * Mathf.Lerp(0.0025f, 0.005f, Hash(i * 1.1f)));
            float k = age / life;
            Color col = Color.Lerp(new Color(1f, 0.95f, 0.7f), new Color(1f, 0.4f, 0.08f), Mathf.Clamp01(k * 1.6f));
            col.a = (1f - k * k) * veil;
            Matrix4x4 m = GUI.matrix;
            GUIUtility.RotateAroundPivot(Mathf.Atan2(vel.y, vel.x) * Mathf.Rad2Deg, p);
            Fill(new Rect(p.x - len, p.y - thick / 2f, len, thick), col);
            GUI.matrix = m;
        }

        // --- a thin, fast shockwave ---
        float rk = Mathf.Clamp01(age / 0.55f);
        float ra = (1f - rk) * 0.55f * veil;
        if (ra > 0f)
        {
            float rs = h * Mathf.Lerp(0.15f, 2.6f, Ease(rk));
            GUI.color = new Color(1f, 0.93f, 0.8f, ra);
            GUI.DrawTexture(new Rect(c.x - rs / 2f, c.y - rs / 2f, rs, rs), ring);
        }

        // --- the flash: warm white, over in a blink (not shaken with the rest) ---
        float fl = 1f - Mathf.Clamp01(age / 0.3f); fl *= fl;
        if (fl > 0f) Fill(new Rect(-w, -h, w * 3f, h * 3f), new Color(1f, 0.96f, 0.85f, 0.9f * fl * veil));
    }

    static float Ease(float k) { return 1f - (1f - k) * (1f - k) * (1f - k); }

    static float Hash(float x) { float s = Mathf.Sin(x * 12.9898f) * 43758.5453f; return s - Mathf.Floor(s); }

    // ---------------------------------------------------------------- textures made in code
    // a soft round blob: full in the middle, fading smoothly to nothing at the edge
    static Texture2D MakeGlow()
    {
        const int n = 128;
        var t = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float dx = (x + 0.5f) / n * 2f - 1f, dy = (y + 0.5f) / n * 2f - 1f;
                float d = Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy));
                float a = d * d * (3f - 2f * d);
                t.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        t.Apply();
        return t;
    }

    static Texture2D MakeRing()
    {
        const int n = 256;
        var t = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float dx = (x + 0.5f) / n * 2f - 1f, dy = (y + 0.5f) / n * 2f - 1f;
                float r = Mathf.Sqrt(dx * dx + dy * dy);
                t.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(1f - Mathf.Abs(r - 0.94f) / 0.035f)));
            }
        t.Apply();
        return t;
    }
}
