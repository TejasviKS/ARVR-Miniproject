using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

// VERSION 2
// The opening screens, shown once when the game starts, before the main menu:
//   (a long black pause first: in a build, Unity's own "Made with Unity" screen plays at the very start and uses up part of it)
//   1. the Cave Labs logo (cropped to a circle) with a slow, majestic publisher sting
//   2. the Kengeri Studios logo with "PRESENTS" under it and a warm retro sting
//   then the main menu fades in.
// (There is no "Made with Unity" screen of our own: a build adds Unity's automatically.)
// Space / Enter / Esc / a click skips to the next screen. Put this on one object in the Splash scene (a Camera and nothing else is needed).
//
// Files: Assets/Resources/Logos/cave_labs.jpeg and kengeri_studios.png, and Assets/Resources/Sting/sting_cave.ogg and sting_kengeri.ogg.
public class SplashScreen : MonoBehaviour
{
    public string nextScene = "MainMenu";

    // the main menu reads this to fade in slowly after the splash
    public static bool Played;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() => Played = false;

    class Card
    {
        public float length, fadeIn, fadeOutAt;
        public string sting;
        public float stingVolume;
    }

    // how long each screen lasts, and when its logo fades in / out (seconds)
    readonly Card[] cards =
    {
        new Card { length = 7.2f, fadeIn = 2.8f, fadeOutAt = 5.6f, sting = "Sting/sting_cave", stingVolume = 0.9f },
        new Card { length = 6.8f, fadeIn = 2.0f, fadeOutAt = 5.4f, sting = "Sting/sting_kengeri", stingVolume = 0.9f },
    };
    const float StartBlank = 5f;      // black before the first logo (Unity's own splash screen, added by a build, uses up some of this)
    const float Gap = 0.9f;           // black between two screens
    const float MenuDelay = 1f;       // the menu music starts when the last logo has faded out; the main menu starts 1 s later
    const float SkipFade = 0.25f;

    int index = -1;
    float cardStart, skipAt = -1f;
    bool leaving;
    float leaveAt;
    AudioSource audioSrc;
    Texture2D caveCircle, kengeriCircle;
    GUIStyle presents;
    readonly List<AudioSource> playing = new List<AudioSource>();

    void Start()
    {
        Time.timeScale = 1f;
        AudioListener.pause = false;
        // (no cursor request: the mouse arrow stays hidden during the opening screens)
        caveCircle = Circle(Resources.Load<Texture2D>("Logos/cave_labs"), 0.985f);
        kengeriCircle = Circle(Resources.Load<Texture2D>("Logos/kengeri_studios"), 0.995f);
        Next(Time.unscaledTime + StartBlank);
    }

    // ---------------------------------------------------------------- the sequence
    void Next(float startAt)
    {
        index++;
        skipAt = -1f;
        cardStart = startAt;
        if (index >= cards.Length) { leaving = true; leaveAt = Time.unscaledTime; MusicDirector.PlayMenu(); return; }   // the menu music starts now
    }

    bool soundStarted;

    void Update()
    {
        if (leaving)
        {
            if (Time.unscaledTime - leaveAt >= MenuDelay) { Played = true; SceneManager.LoadScene(nextScene); enabled = false; }
            return;
        }
        if (index < 0 || index >= cards.Length) return;
        float now = Time.unscaledTime;
        if (now < cardStart) return;

        if (!soundStarted)
        {
            soundStarted = true;
            AudioClip clip = Resources.Load<AudioClip>(cards[index].sting);
            if (clip != null)
            {
                var go = new GameObject("Sting");
                go.transform.SetParent(transform);
                var src = go.AddComponent<AudioSource>();
                src.spatialBlend = 0f; src.volume = cards[index].stingVolume; src.clip = clip; src.Play();
                playing.Add(src);
            }
        }

        Keyboard kb = Keyboard.current;
        Mouse mouse = Mouse.current;
        bool skip = (kb != null && (kb.spaceKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame || kb.escapeKey.wasPressedThisFrame))
                    || (mouse != null && mouse.leftButton.wasPressedThisFrame);
        if (skip && skipAt < 0f && now - cardStart > 0.3f) skipAt = now;

        // the sting of a skipped screen fades out quickly
        if (skipAt >= 0f)
            foreach (var s in playing) if (s != null) s.volume = Mathf.MoveTowards(s.volume, 0f, Time.unscaledDeltaTime / SkipFade * 0.9f);

        bool over = skipAt >= 0f ? now - skipAt > SkipFade : now - cardStart >= cards[index].length;
        if (over)
        {
            soundStarted = false;
            if (skipAt >= 0f) { foreach (var s in playing) if (s != null) s.Stop(); }
            Next(now + (skipAt >= 0f ? 0.1f : Gap));
        }
    }

    // how visible the current screen is (0..1): fades in, holds, fades out
    float Alpha()
    {
        if (index < 0 || index >= cards.Length) return 0f;
        float now = Time.unscaledTime;
        if (now < cardStart) return 0f;
        Card c = cards[index];
        float t = now - cardStart;
        float a = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / c.fadeIn));
        a *= 1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((t - c.fadeOutAt) / (c.length - c.fadeOutAt)));
        if (skipAt >= 0f) a *= 1f - Mathf.Clamp01((now - skipAt) / SkipFade);
        return a;
    }

    // ---------------------------------------------------------------- drawing
    void OnGUI()
    {
        float w = Screen.width, h = Screen.height;
        GUI.color = Color.black;
        GUI.DrawTexture(new Rect(0, 0, w, h), Texture2D.whiteTexture);
        if (index < 0 || index >= cards.Length) return;
        float a = Alpha();
        if (a <= 0f) return;
        float t = Mathf.Max(0f, Time.unscaledTime - cardStart);
        float grow = 1f + 0.04f * (1f - Mathf.Clamp01(t / cards[index].length)) ;   // a very slow push-in

        if (index == 0)
        {
            float d = h * 0.56f * grow;
            Rect r = new Rect(w / 2f - d / 2f, h / 2f - d / 2f, d, d);
            GUI.color = new Color(1f, 1f, 1f, a);
            if (caveCircle != null) GUI.DrawTexture(r, caveCircle);
        }
        else
        {
            float d = h * 0.6f * grow;
            Rect r = new Rect(w / 2f - d / 2f, h * 0.47f - d / 2f, d, d);
            GUI.color = new Color(1f, 1f, 1f, a);
            if (kengeriCircle != null) GUI.DrawTexture(r, kengeriCircle);
            float pa = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((t - 2.6f) / 1.3f)) * a;
            if (presents == null) presents = MakeStyle(0.042f, FontStyle.Bold);
            presents.fontSize = Mathf.RoundToInt(h * 0.042f);
            GUI.color = new Color(1f, 1f, 1f, pa);
            presents.alignment = TextAnchor.MiddleCenter;
            WastedScreen.Pin(presents, Color.white);
            GUI.Label(new Rect(0, h * 0.47f + d / 2f + h * 0.02f, w, h * 0.08f), "P R E S E N T S", presents);
        }
        GUI.color = Color.white;
    }

    GUIStyle MakeStyle(float size, FontStyle fs)
    {
        var s = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontStyle = fs, richText = false };
        s.wordWrap = false;
        s.normal.textColor = Color.white;
        return s;
    }

    // ---------------------------------------------------------------- textures made in code
    // A copy of the picture cropped to a circle (the biggest circle that fits in the middle, with a soft edge)
    public static Texture2D Circle(Texture2D src, float radius)
    {
        if (src == null) return null;
        int n = Mathf.Min(src.width, src.height);
        int x0 = (src.width - n) / 2, y0 = (src.height - n) / 2;
        Color[] px;
        try { px = src.GetPixels(x0, y0, n, n); }
        catch { return src; }                                         // (not readable: show it square rather than not at all)
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float dx = (x + 0.5f) / n * 2f - 1f, dy = (y + 0.5f) / n * 2f - 1f;
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                px[y * n + x].a *= Mathf.Clamp01((radius - d) * n * 0.5f);
            }
        var t = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
        t.SetPixels(px); t.Apply();
        return t;
    }
}
