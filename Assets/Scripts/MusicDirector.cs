using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

// VERSION 1
// The game's music: it starts by itself with the game (you do not put it in a scene) and lives across scene loads.
//
// THE TRACKS (Assets/Resources/Music/, any audio format; the NAME is what counts, so swap in your own files with the same names):
//   calm     the relaxed-but-a-little-uneasy loop for the non-combat levels (Cabin_Tutorial, Classroom, Cad_lab)
//   combat   the fighting music (GPT's and Claude's levels, from the first second, scolding included)
//   fable    the intense section that bursts in when Claude transforms into Fable
//
// HOW IT SOUNDS
//   - Every track plays on its own looping source with its own fade, so a crossfade is simply "one goes to 0 while the other goes to 1".
//   - The music is NOT interrupted by a scene change: if the next level wants the track that is already playing it just carries on
//     (calm level to calm level), otherwise the new track crossfades in (see StartForScene).
//   - A portal makes a short whoosh and the music dips a little for a moment (PortalTransition calls Whoosh).
//   - Dialogue ducks the music (DuckFor / HoldDuck): it dips to 40% while someone speaks and comes back slowly afterwards.
//   - The pause menu turns the music down to 30% (full volume while the sound options are open, so you can hear the slider).
//   - Loudness is evened out when a track is first used: its average level is measured and a base volume is worked out so all the music is
//     equally loud (combat and fable are a little louder on purpose). The "Game Music Volume" option then scales all of it.
public enum MusicTrack { None, Calm, Combat, Fable, CombatClaude }   // Combat = GPT's level (combat_gpt.ogg), CombatClaude = Claude's level (combat_claude.ogg)

public class MusicDirector : MonoBehaviour
{
    // how loud each track should be compared with the others (1 = the reference level)
    static readonly float[] Weight = { 0f, 1.5f, 1.25f, 1.25f, 1.25f };
    static readonly string[] FileName = { "", "calm", "combat_gpt", "fable", "combat_claude" };
    const float ReferenceRms = 0.07f;          // the average level a weight-1 track is brought to (before the volume option)
    const float DuckLevel = 0.4f, PausedLevel = 0.3f, WhooshDip = 0.55f, ListenLevel = 0.35f;

    class Layer
    {
        public AudioSource src;
        public float baseGain = 0.5f;
        public float level, target, rate = 1f;       // level and target are 0..1; rate = how much per second
        public bool measured, missing;
    }

    static MusicDirector inst;
    readonly Layer[] layers = new Layer[5];
    MusicTrack current = MusicTrack.None;          // what is meant to be playing right now
    MusicTrack resumeTrack = MusicTrack.None;      // what comes back after a defeat
    float duck = 1f, pauseFactor = 1f, duckUntil, dip = 1f, dipUntil, listen = 1f, listenUntil;
    AudioSource sfx;
    AudioClip whooshClip;
    int holdDucks;
    string handledScene;

    public static MusicTrack Current => inst != null ? inst.current : MusicTrack.None;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { inst = null; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Boot()
    {
        if (inst != null) return;
        var go = new GameObject("MusicDirector");
        DontDestroyOnLoad(go);
        go.hideFlags = HideFlags.HideInHierarchy;
        inst = go.AddComponent<MusicDirector>();
    }

    void Awake()
    {
        for (int i = 1; i < layers.Length; i++)
        {
            var l = new Layer();
            l.src = gameObject.AddComponent<AudioSource>();
            l.src.loop = true; l.src.playOnAwake = false; l.src.spatialBlend = 0f;
            l.src.ignoreListenerPause = true;                 // the pause menu turns the music down itself (see Update)
            l.src.priority = 0;
            l.src.volume = 0f;
            layers[i] = l;
        }
        sfx = gameObject.AddComponent<AudioSource>();
        sfx.playOnAwake = false; sfx.spatialBlend = 0f; sfx.ignoreListenerPause = true;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void Start()
    {
        // the very first scene (the one Play was pressed in) may have been loaded before we listened
        if (handledScene == null) StartForScene(SceneManager.GetActiveScene().name);
    }

    void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        if (inst == this) inst = null;
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode) { StartForScene(scene.name); }

    // ---------------------------------------------------------------- what every level starts with
    // The music is not cut by a scene change. If the level wants the track that is already playing, it simply carries on (the same place in
    // the loop). Otherwise the new track fades in while the old one fades out; from silence it fades in slowly.
    void StartForScene(string scene)
    {
        handledScene = scene;
        holdDucks = 0; duckUntil = 0f; listenUntil = 0f;
        resumeTrack = MusicTrack.None;
        bool silent = current == MusicTrack.None;
        if (scene != "MainMenu" && scene != "Splash") { EndMenu(0.6f); EndCredits(0.6f); }      // the menu and credits music belong to the menu (and the opening screens) only
        switch (scene)
        {
            case "Cabin_Tutorial":
            case "Classroom":
            case "Cad_lab":
                PlayInternal(MusicTrack.Calm, 1.5f, silent ? 0.05f : 2.5f); break;               // (the calm file has its own fade-in built in)
            case "Cabin_Combat":
                PlayInternal(MusicTrack.Combat, 2.5f, silent ? 0.05f : 2.5f); break;             // GPT: combat_gpt.ogg, from the first second, scolding included
            case "Classroom_boss":
                PlayInternal(MusicTrack.CombatClaude, 2.5f, silent ? 0.05f : 2.5f); break;       // Claude: combat_claude.ogg
            default:
                if (current != MusicTrack.None) Silence(1f);                      // the main menu and anything else: the music fades away
                break;
        }
    }

    // ---------------------------------------------------------------- what other scripts call
    // Fades this track in over fadeIn seconds and the others out over fadeOut (default: the same time = a crossfade).
    public static void Play(MusicTrack track, float fadeIn = 2f, float fadeOut = -1f)
    {
        if (inst != null) inst.PlayInternal(track, fadeIn, fadeOut < 0f ? fadeIn : fadeOut);
    }

    // Everything fades out over the given seconds
    public static void Silence(float seconds = 1.5f)
    {
        if (inst == null) return;
        inst.current = MusicTrack.None;
        inst.FadeAll(seconds);
    }

    // Stepping through a portal: a short whoosh, and the music dips a little for about a second and swells back.
    public static void Whoosh()
    {
        if (inst == null) return;
        inst.dipUntil = Time.unscaledTime + 0.9f;
        if (inst.whooshClip == null) inst.whooshClip = MakeWhoosh();
        inst.sfx.PlayOneShot(inst.whooshClip, 0.18f);
    }

    // A rushing sound made in code: noise through a filter that opens and closes, loudest in the middle (0.7 s)
    static AudioClip MakeWhoosh()
    {
        const int rate = 44100;
        int n = (int)(0.7f * rate);
        var data = new float[n];
        var rng = new System.Random(5);
        float lp = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)n;
            float open = Mathf.Sin(t * Mathf.PI);                                   // 0 -> 1 -> 0
            float cutoff = 0.02f + 0.55f * open * open;                             // the filter sweeps open then shut
            float noise = (float)(rng.NextDouble() * 2.0 - 1.0);
            lp += (noise - lp) * cutoff;
            data[i] = lp * open * open * 1.6f;
        }
        var clip = AudioClip.Create("whoosh", n, 1, rate, false);
        clip.SetData(data, 0);
        return clip;
    }

    // Brings back what a defeat interrupted, slowly. Does nothing if nothing was playing.
    public static void ResumeAfterCut(float fadeIn = 3.5f)
    {
        if (inst == null || inst.resumeTrack == MusicTrack.None || inst.current != MusicTrack.None) return;
        MusicTrack t = inst.resumeTrack;
        inst.resumeTrack = MusicTrack.None;
        inst.PlayInternal(t, fadeIn, 0.1f);
    }

    // The defeat screen: the music drops away fast, and comes back (ResumeAfterCut) when you try again
    public static void Defeat()
    {
        if (inst == null) return;
        if (inst.current != MusicTrack.None) inst.resumeTrack = inst.current;
        inst.current = MusicTrack.None;
        inst.FadeAll(0.6f);
    }

    // The music dips for this many seconds (a line of dialogue). Calling it again for the next line just extends it.
    public static void DuckFor(float seconds)
    {
        if (inst == null) return;
        inst.duckUntil = Mathf.Max(inst.duckUntil, Time.unscaledTime + seconds);
    }

    // The music drops to about a third for the next few seconds, so something quiet can be heard (the beeping computer in Lab 1).
    // Call it every frame while it matters: it stays low until 3 seconds after the last call (so the short silences between beeps stay quiet).
    public static void ListenFor(float seconds = 3f)
    {
        if (inst == null) return;
        inst.listenUntil = Mathf.Max(inst.listenUntil, Time.unscaledTime + seconds);
    }

    // The music stays dipped until you call HoldDuck(false) (a whole conversation)
    public static void HoldDuck(bool on)
    {
        if (inst == null) return;
        inst.holdDucks = Mathf.Max(0, inst.holdDucks + (on ? 1 : -1));
    }

    // for tests: what is every layer doing?
    public static string Status()
    {
        if (inst == null) return "no MusicDirector";
        var parts = new List<string>();
        for (int i = 1; i < inst.layers.Length; i++)
        {
            Layer l = inst.layers[i];
            parts.Add(FileName[i] + (inst.current == (MusicTrack)i ? "*" : "") + ": level " + l.level.ToString("0.00") + "->" + l.target.ToString("0") + " vol " + l.src.volume.ToString("0.000")
                + (l.src.isPlaying ? " playing" : " stopped") + " base " + l.baseGain.ToString("0.00") + (l.missing ? " MISSING" : ""));
        }
        parts.Add("duck " + inst.duck.ToString("0.00") + " pause " + inst.pauseFactor.ToString("0.00") + " master " + OptionsPanel.GameMusicVolume.ToString("0.00"));
        return string.Join("\n", parts);
    }

    // ---------------------------------------------------------------- the main menu music (Music/menu.ogg)
    // Separate from the game tracks: it plays from the start, and when it ends it waits MenuLoopPause seconds and plays again. Its volume is
    // the "Menu Music Volume" option. The opening screens start it; the main menu makes sure it is on; any other scene fades it out.
    const float MenuLoopPause = 3f;
    AudioSource menuSrc;
    float menuBase = 0.5f, menuLevel, menuTarget, menuRate = 1f, menuRestartAt = -1f;
    bool menuWanted, menuMeasured, menuMissing;

    public static void PlayMenu(float fadeIn = 0.05f) { if (inst != null) inst.StartMenu(fadeIn); }
    public static void StopMenu(float fadeOut = 0.6f) { if (inst != null) inst.EndMenu(fadeOut); }
    public static bool MenuPlaying => inst != null && inst.menuWanted;

    void StartMenu(float fadeIn)
    {
        if (menuWanted || menuMissing || creditsActive) return;                  // (not while the credits music is playing)
        if (menuSrc == null)
        {
            menuSrc = gameObject.AddComponent<AudioSource>();
            menuSrc.loop = false; menuSrc.playOnAwake = false; menuSrc.spatialBlend = 0f; menuSrc.ignoreListenerPause = true; menuSrc.priority = 0; menuSrc.volume = 0f;
        }
        if (menuSrc.clip == null)
        {
            AudioClip clip = Resources.Load<AudioClip>("Music/menu");
            if (clip == null) { menuMissing = true; Debug.LogWarning("[MusicDirector] No music file named 'menu' in Assets/Resources/Music."); return; }
            menuSrc.clip = clip;
        }
        if (!menuMeasured) { menuMeasured = true; menuBase = BaseGainFor(menuSrc.clip, 1.25f); }
        menuWanted = true;
        menuTarget = 1f; menuRate = 1f / Mathf.Max(0.02f, fadeIn);
        menuRestartAt = -1f;
        menuSrc.time = 0f; menuSrc.Play();
    }

    void EndMenu(float fadeOut)
    {
        menuWanted = false; menuRestartAt = -1f;
        menuTarget = 0f; menuRate = 1f / Mathf.Max(0.02f, fadeOut);
    }

    void UpdateMenuMusic(float dt)
    {
        if (menuSrc == null) return;
        if (menuLevel != menuTarget) menuLevel = Mathf.MoveTowards(menuLevel, menuTarget, dt * menuRate);
        menuSrc.volume = menuBase * menuLevel * menuLevel * OptionsPanel.MenuMusicVolume;
        if (menuWanted)
        {
            if (!menuSrc.isPlaying && menuRestartAt < 0f) menuRestartAt = Time.unscaledTime + MenuLoopPause;      // it ended: a pause, then again
            if (menuRestartAt >= 0f && Time.unscaledTime >= menuRestartAt) { menuRestartAt = -1f; menuSrc.time = 0f; menuSrc.Play(); }
        }
        else if (menuLevel <= 0f && menuSrc.isPlaying) menuSrc.Stop();
    }

    // ---------------------------------------------------------------- the credits music (Music/credits.ogg)
    // Started by the credits screen (from its first second, so the opening snare lands on the bang). It plays right to its end, whatever
    // the screen is doing: it is only cut by Play (StopCredits) or by leaving the menu. When it ends, the menu music comes back after the
    // usual 3 s pause. Its volume is the "Menu Music Volume" option.
    AudioSource creditsSrc;
    float creditsBase = 0.5f, creditsLevel, creditsTarget, creditsRate = 1f;
    bool creditsActive, creditsMeasured, creditsMissing;

    public static void PlayCredits() { if (inst != null) inst.StartCredits(); }
    public static void StopCredits(float fadeOut = 0.6f) { if (inst != null) inst.EndCredits(fadeOut); }
    public static bool CreditsPlaying => inst != null && inst.creditsActive;

    void StartCredits()
    {
        if (creditsMissing) return;
        if (creditsSrc == null)
        {
            creditsSrc = gameObject.AddComponent<AudioSource>();
            creditsSrc.loop = false; creditsSrc.playOnAwake = false; creditsSrc.spatialBlend = 0f; creditsSrc.ignoreListenerPause = true; creditsSrc.priority = 0; creditsSrc.volume = 0f;
        }
        if (creditsSrc.clip == null)
        {
            AudioClip clip = Resources.Load<AudioClip>("Music/credits");
            if (clip == null) { creditsMissing = true; Debug.LogWarning("[MusicDirector] No music file named 'credits' in Assets/Resources/Music."); return; }
            creditsSrc.clip = clip;
        }
        if (!creditsMeasured) { creditsMeasured = true; creditsBase = BaseGainFor(creditsSrc.clip, 1.25f); }
        EndMenu(0.05f);                                            // (the menu music is off while the credits music plays)
        creditsActive = true;
        creditsTarget = 1f; creditsLevel = 1f;                     // it starts at full volume, no fade: the snare is the first thing you hear
        creditsSrc.Stop(); creditsSrc.time = 0f; creditsSrc.Play();
    }

    void EndCredits(float fadeOut)
    {
        creditsActive = false;
        creditsTarget = 0f; creditsRate = 1f / Mathf.Max(0.02f, fadeOut);
    }

    void UpdateCreditsMusic(float dt)
    {
        if (creditsSrc == null) return;
        if (creditsLevel != creditsTarget) creditsLevel = Mathf.MoveTowards(creditsLevel, creditsTarget, dt * creditsRate);
        creditsSrc.volume = creditsBase * creditsLevel * creditsLevel * OptionsPanel.MenuMusicVolume;
        if (creditsActive && !creditsSrc.isPlaying)
        {
            creditsActive = false;                                  // the song ended by itself: after the usual pause, the menu music starts again
            creditsTarget = 0f;
            ResumeMenuAfterPause();
        }
        else if (!creditsActive && creditsLevel <= 0f && creditsSrc.isPlaying) creditsSrc.Stop();
    }

    // The menu music is "wanted" again but not playing yet: UpdateMenuMusic then waits its loop pause (3 s) and starts it
    void ResumeMenuAfterPause()
    {
        if (menuMissing) return;
        if (menuSrc == null) { StartMenu(0.05f); if (menuSrc != null) { menuSrc.Stop(); } }
        if (menuSrc == null || menuSrc.clip == null) return;
        menuWanted = true; menuTarget = 1f; menuLevel = 1f; menuRate = 20f;
        menuRestartAt = -1f;
    }

    // ---------------------------------------------------------------- inside
    void PlayInternal(MusicTrack track, float fadeIn, float fadeOut)
    {
        if (track == MusicTrack.None) { FadeAll(fadeOut); return; }
        if (current == track && layers[(int)track].target > 0f) return;           // already on
        Layer l = layers[(int)track];
        if (!Prepare(track)) return;
        current = track;
        l.target = 1f; l.rate = 1f / Mathf.Max(0.02f, fadeIn);
        if (!l.src.isPlaying) { l.src.time = 0f; l.src.Play(); }
        for (int i = 1; i < layers.Length; i++)
            if (i != (int)track && layers[i].target > 0f) { layers[i].target = 0f; layers[i].rate = 1f / Mathf.Max(0.02f, fadeOut); }
    }

    void FadeAll(float seconds)
    {
        for (int i = 1; i < layers.Length; i++)
            if (layers[i].level > 0f || layers[i].target > 0f) { layers[i].target = 0f; layers[i].rate = 1f / Mathf.Max(0.02f, seconds); }
    }

    // Loads the clip and works out its base volume the first time the track is used
    bool Prepare(MusicTrack track)
    {
        Layer l = layers[(int)track];
        if (l.missing) return false;
        if (l.src.clip == null)
        {
            AudioClip clip = Resources.Load<AudioClip>("Music/" + FileName[(int)track]);
            if (clip == null) { l.missing = true; Debug.LogWarning("[MusicDirector] No music file named '" + FileName[(int)track] + "' in Assets/Resources/Music."); return false; }
            l.src.clip = clip;
        }
        if (!l.measured)
        {
            l.measured = true;
            l.baseGain = BaseGainFor(l.src.clip, Weight[(int)track]);
        }
        return true;
    }

    // The average loudness (RMS) of the whole clip, then the volume that brings it to the reference level (times the track's weight).
    // A louder file gets a lower base volume, a quieter one a higher one (never above 1), so any file you drop in sounds equally loud.
    static float BaseGainFor(AudioClip clip, float weight)
    {
        const float fallback = 0.5f;
        if (clip.loadState != AudioDataLoadState.Loaded) clip.LoadAudioData();
        int ch = clip.channels, window = 32768;
        if (clip.samples < window) return fallback;
        var buf = new float[window * ch];
        double sum = 0; long count = 0;
        for (int off = 0; off + window <= clip.samples; off += window)
        {
            if (!clip.GetData(buf, off)) return fallback;                          // (the file must be "Decompress On Load": the import script sets that)
            for (int i = 0; i < buf.Length; i += 3) { sum += buf[i] * buf[i]; count++; }
        }
        if (count == 0) return fallback;
        float rms = Mathf.Sqrt((float)(sum / count));
        if (rms < 1e-4f) return fallback;
        return Mathf.Min(1f, ReferenceRms * weight / rms);
    }

    void Update()
    {
        float dt = Time.unscaledDeltaTime;

        bool ducking = holdDucks > 0 || Time.unscaledTime < duckUntil;
        duck = Mathf.MoveTowards(duck, ducking ? DuckLevel : 1f, dt * (ducking ? 3f : 1f));       // down in 0.2 s, back up in 0.6 s

        bool paused = PauseMenu.IsPaused && !OptionsPanel.IsOpen;
        pauseFactor = Mathf.MoveTowards(pauseFactor, paused ? PausedLevel : 1f, dt * 3f);
        bool listening = Time.unscaledTime < listenUntil;
        listen = Mathf.MoveTowards(listen, listening ? ListenLevel : 1f, dt * (listening ? 1.5f : 0.4f));       // down in ~0.4 s, back up slowly
        bool dipping = Time.unscaledTime < dipUntil;
        dip = Mathf.MoveTowards(dip, dipping ? WhooshDip : 1f, dt * (dipping ? 6f : 0.7f));         // quick down, a slower swell back up

        UpdateMenuMusic(dt);
        UpdateCreditsMusic(dt);

        float master = OptionsPanel.GameMusicVolume;
        for (int i = 1; i < layers.Length; i++)
        {
            Layer l = layers[i];
            if (l.level != l.target) l.level = Mathf.MoveTowards(l.level, l.target, dt * l.rate);
            l.src.volume = l.baseGain * l.level * l.level * master * duck * dip * listen * pauseFactor;          // (level squared: fades sound smoother to the ear)
            if (l.target <= 0f && l.level <= 0f && l.src.isPlaying) l.src.Stop();
        }
    }
}
