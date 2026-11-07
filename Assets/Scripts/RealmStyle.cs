using UnityEngine;
using UnityEngine.Rendering.Universal;

// VERSION 1
// The "realm" look for one section of the game: the whole view becomes ONE flat colour and the shape of the
// world is drawn only by thick outlines (like SCHiM, but in 3D).
//
// Put this on an empty object in the scene. While this object is enabled, the look is on. It switches off:
//  - when the Portal in "Ends When Used" is walked through (e.g. the portal from the bench room into Lab 1), or
//  - when this object/scene goes away (Play Again, leaving Play mode).
// Every realm can have its own colours: use one RealmStyle per section.
//
// Needs the Realm Outline renderer feature (Tools > Realm Outline > Install, done once per project).
// It also turns off post-processing (bloom, tonemapping ...) on the player's camera while active, so the
// colours you pick are exactly the colours you see.
public class RealmStyle : MonoBehaviour
{
    [Header("Palette")]
    [Tooltip("The one colour everything is filled with")]
    public Color fillColor = new Color(0.949f, 0.541f, 0.188f);   // warm orange
    [Tooltip("The colour of the outlines")]
    public Color lineColor = new Color(0.106f, 0.082f, 0.188f);   // near-black indigo
    [Tooltip("The colour of open sky / empty space (where nothing is drawn, e.g. above a room with no ceiling)")]
    public Color voidColor = new Color(0.106f, 0.082f, 0.188f);   // same indigo: looks like a black silhouette world

    [Header("Lines")]
    [Tooltip("Outline thickness in pixels on a 1080p screen (scales with the resolution)")]
    [Range(0.5f, 10f)] public float lineThickness = 4f;
    [Tooltip("How big a depth jump must be to get an outline. LOWER = more lines (also on small steps).")]
    [Range(0.005f, 0.3f)] public float depthThreshold = 0.04f;
    [Tooltip("How sharp a corner must be to get an outline. LOWER = more lines (also on gentle curves).")]
    [Range(0.05f, 2f)] public float creaseThreshold = 0.5f;

    [Header("Wiring")]
    [Tooltip("The 'RealmOutline' feature of every renderer (PC and Mobile each have their own copy). Filled in by Tools > Realm Outline > Add To Open Scene")]
    public ScriptableRendererFeature[] outlineFeatures;
    [Tooltip("Optional: walking through this portal ends the look")]
    public Portal endsWhenUsed;
    [Tooltip("Optional: for a realm that starts LATER. Untick this component in the Inspector, then walking through this portal switches the look on (the colours of the realm you arrive in).")]
    public Portal startsWhenUsed;
    [Tooltip("ON = turn off bloom/tonemapping while active, so the palette stays exact")]
    public bool disablePostProcessing = true;

    UniversalAdditionalCameraData camData;
    bool hadPostProcessing;
    bool applied;

    // Only one realm look can be on at a time: the one that switched on last.
    static RealmStyle active;

    // "Enter Play Mode Options" is on (no domain reload): forget the old one from the previous Play
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() => active = null;

    // Awake also runs on a component that is switched off, so a later realm can wait for its portal
    void Awake()
    {
        if (startsWhenUsed != null) startsWhenUsed.onUsed.AddListener(Begin);
    }

    void OnDestroy()
    {
        if (startsWhenUsed != null) startsWhenUsed.onUsed.RemoveListener(Begin);
    }

    void OnEnable()
    {
        if (endsWhenUsed != null) endsWhenUsed.onUsed.AddListener(End);
        Apply(true);
    }

    void OnDisable()
    {
        if (endsWhenUsed != null) endsWhenUsed.onUsed.RemoveListener(End);
        Apply(false);
    }

    // Also callable from a UnityEvent (e.g. a portal's On Used)
    public void Begin() => enabled = true;
    public void End() => enabled = false;

    void Update()
    {
        if (applied) SetShaderValues();   // live tweaking in the Inspector while playing
    }

    void Apply(bool on)
    {
        if (on == applied) return;
        if (outlineFeatures == null || outlineFeatures.Length == 0)
        {
            if (on) Debug.LogWarning("[RealmStyle] Outline Features is empty: run Tools > Realm Outline > Add To Open Scene.", this);
            return;
        }

        if (on)
        {
            // Another realm still on? It hands over first (so the order of a portal's events doesn't matter)
            if (active != null && active != this) active.enabled = false;
            active = this;
        }
        else if (active == this) active = null;

        applied = on;
        foreach (ScriptableRendererFeature f in outlineFeatures)
            if (f != null) f.SetActive(on);

        if (on)
        {
            SetShaderValues();
            if (disablePostProcessing)
            {
                Camera cam = Camera.main;
                PlayerController pc = FindAnyObjectByType<PlayerController>();
                if (pc != null) { Camera c = pc.GetComponentInChildren<Camera>(true); if (c != null) cam = c; }
                if (cam != null && cam.TryGetComponent(out camData))
                {
                    hadPostProcessing = camData.renderPostProcessing;
                    camData.renderPostProcessing = false;
                }
            }
            Debug.Log("[Scripts] RealmStyle v1: realm look ON", this);
        }
        else
        {
            if (camData != null) { camData.renderPostProcessing = hadPostProcessing; camData = null; }
            Debug.Log("[Scripts] RealmStyle v1: realm look OFF", this);
        }
    }

    void SetShaderValues()
    {
        // .linear: the Inspector colour is what you see in the colour picker; the shader needs it in linear space
        Shader.SetGlobalVector("_RealmFill", fillColor.linear);
        Shader.SetGlobalVector("_RealmLine", lineColor.linear);
        Shader.SetGlobalVector("_RealmVoid", voidColor.linear);
        Shader.SetGlobalVector("_RealmParams", new Vector4(lineThickness, depthThreshold, creaseThreshold, 0f));
    }
}
