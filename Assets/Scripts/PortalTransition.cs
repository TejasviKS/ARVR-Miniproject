using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// VERSION 1
// The "stepping through a portal" sequence. Portal.cs starts it; you don't put this in a scene yourself
// (it makes its own object that survives scene loads, so it can finish the effect in the next scene).
//
//   1. Fly-in   (~1 s): walking is taken over, the player drifts through the portal while the field of view
//                       punches wide and a round iris closes in the portal's colour. A whoosh plays.
//   2. Swap             : with the screen completely covered, the portal does its job (load the next scene,
//                         or teleport + "On Used" events), so you never see the cut.
//   3. Reveal   (~1 s): the iris opens on the new place and the field of view settles back. Control returns.
// The cover colour is the portal's colour, so if the next realm is that colour too (the flat orange look)
// the screen simply "opens" onto it.
public class PortalTransition : MonoBehaviour
{
    const float FlyInTime = 0.95f;
    const float RevealTime = 1.1f;
    const float FovPunch = 45f;        // extra degrees of field of view at the peak of the rush
    const float DriftSpeed = 3.5f;     // m/s the player is carried through the portal at the end

    static PortalTransition instance;

    // "Enter Play Mode Options" is on (no domain reload): forget the old object from the previous Play
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() => instance = null;

    public static bool Busy => instance != null && instance.busy;

    bool busy;
    RawImage wipe;
    Material wipeMat;
    AudioSource sfx;
    AudioClip whooshIn, whooshOut;

    // atCover runs while the screen is fully covered. If sceneToLoad is set, that scene is loaded right after it.
    public static void Play(Portal portal, PlayerController player, Action atCover, string sceneToLoad)
    {
        if (instance == null)
        {
            GameObject go = new GameObject("PortalTransition");
            DontDestroyOnLoad(go);
            instance = go.AddComponent<PortalTransition>();
            instance.Build();
        }
        if (instance.busy) return;
        instance.StartCoroutine(instance.Run(portal, player, atCover, sceneToLoad));
    }

    // ---------------------------------------------------------------- the sequence
    IEnumerator Run(Portal portal, PlayerController player, Action atCover, string sceneToLoad)
    {
        busy = true;
        Color fill = portal.portalColor, accent = portal.accentColor;
        Vector3 dir = portal.TravelDirection(player.transform.position);
        CharacterController cc = player.GetComponent<CharacterController>();
        Camera cam = player.GetComponentInChildren<Camera>(true);
        float baseFov = cam != null ? cam.fieldOfView : 60f;

        Hold(player, true);
        sfx.pitch = 1f;
        sfx.PlayOneShot(whooshIn, 0.9f);

        // 1. Fly in
        for (float t = 0f; t < FlyInTime; t += Time.deltaTime)
        {
            float k = t / FlyInTime;
            if (cam != null) cam.fieldOfView = baseFov + FovPunch * k * k;
            if (cc != null && cc.enabled) cc.Move(dir * Mathf.Lerp(1f, DriftSpeed, k) * Time.deltaTime);
            SetCover(Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.3f, 1f, k)), fill, accent);
            yield return null;
        }
        SetCover(1f, fill, accent);
        if (cam != null) cam.fieldOfView = baseFov;

        // 2. Swap, hidden behind the cover
        if (!string.IsNullOrEmpty(sceneToLoad))
        {
            atCover?.Invoke();
            UnityEngine.Events.UnityAction<Scene, LoadSceneMode> holdNewPlayer = (s, m) => Hold(FindAnyObjectByType<PlayerController>(), true);
            SceneManager.sceneLoaded += holdNewPlayer;    // freeze the new player before its first frame
            AsyncOperation op = SceneManager.LoadSceneAsync(sceneToLoad);
            while (!op.isDone) yield return null;
            SceneManager.sceneLoaded -= holdNewPlayer;
            player = FindAnyObjectByType<PlayerController>();
        }
        else
        {
            atCover?.Invoke();                            // e.g. teleport, which switches walking back on
            Hold(player, true);
        }
        yield return null;                                // one covered frame so the new place settles

        // 3. Reveal
        cam = player != null ? player.GetComponentInChildren<Camera>(true) : null;
        baseFov = cam != null ? cam.fieldOfView : 60f;
        sfx.pitch = 1f;
        sfx.PlayOneShot(whooshOut, 0.8f);
        bool released = false;
        for (float t = 0f; t < RevealTime; t += Time.deltaTime)
        {
            float k = t / RevealTime;
            float ease = 1f - Mathf.Pow(1f - k, 3f);      // fast at first, gentle landing
            if (cam != null) cam.fieldOfView = Mathf.Lerp(baseFov + FovPunch * 0.8f, baseFov, ease);
            SetCover(1f - ease, fill, accent);
            if (!released && k > 0.7f) { Hold(player, false); released = true; }   // you can move before it fully opens
            yield return null;
        }
        if (cam != null) cam.fieldOfView = baseFov;
        SetCover(0f, fill, accent);
        if (!released) Hold(player, false);
        busy = false;
    }

    // Stops/starts walking and "Press E" (the interactor would otherwise switch walking back on by itself)
    static void Hold(PlayerController pc, bool hold)
    {
        if (pc == null) return;
        PlayerInteractor pi = pc.GetComponent<PlayerInteractor>();
        if (pi != null) pi.enabled = !hold;
        pc.enabled = !hold;
    }

    void SetCover(float cover, Color fill, Color accent)
    {
        if (wipe == null) return;
        wipe.enabled = cover > 0.001f;
        wipeMat.SetFloat("_Cover", cover);
        wipeMat.SetColor("_Fill", fill);
        wipeMat.SetColor("_Accent", accent);
    }

    // ---------------------------------------------------------------- setup
    void Build()
    {
        Shader shader = Shader.Find("Custom/PortalWipe");
        if (shader == null)
            Debug.LogWarning("[PortalTransition] PortalWipe shader not found: put PortalWipe.shader in Assets/Resources. The portal will work without the screen wipe.");
        else
        {
            wipeMat = new Material(shader);
            GameObject canvasGo = new GameObject("Wipe");
            canvasGo.transform.SetParent(transform, false);
            Canvas canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 5000;                   // on top of everything, even the dialogue boxes

            GameObject img = new GameObject("Cover");
            img.transform.SetParent(canvasGo.transform, false);
            RectTransform rt = img.AddComponent<RectTransform>();
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
            wipe = img.AddComponent<RawImage>();
            wipe.texture = Texture2D.whiteTexture;
            wipe.material = wipeMat;
            wipe.raycastTarget = false;
            wipe.enabled = false;
        }

        sfx = gameObject.AddComponent<AudioSource>();     // plain 2D sound: it is "in your head"
        sfx.spatialBlend = 0f;
        sfx.playOnAwake = false;
        whooshIn = MakeWhoosh(0.95f, true);
        whooshOut = MakeWhoosh(1.1f, false);
    }

    // Noise that gets brighter (rising) or duller (falling) while fading in and out; made in code, no audio files
    static AudioClip MakeWhoosh(float seconds, bool rising)
    {
        const int rate = 44100;
        int n = Mathf.CeilToInt(rate * seconds);
        float[] data = new float[n];
        float smooth = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = (float)i / rate / seconds;
            float k = rising ? Mathf.Lerp(0.03f, 0.5f, t) : Mathf.Lerp(0.5f, 0.03f, t);
            smooth += (UnityEngine.Random.Range(-1f, 1f) - smooth) * k;
            float tone = Mathf.Sin(2f * Mathf.PI * Mathf.Lerp(rising ? 180f : 700f, rising ? 700f : 180f, t) * t * seconds) * 0.25f;
            data[i] = (smooth * 0.9f + tone) * Mathf.Sin(Mathf.PI * t);
        }
        AudioClip clip = AudioClip.Create(rising ? "portalIn" : "portalOut", n, 1, rate, false);
        clip.SetData(data, 0);
        return clip;
    }
}
