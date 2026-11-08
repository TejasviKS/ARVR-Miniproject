using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;

// VERSION 12
// Walk into this to go to the next room.
// Put it on a Cube (or any 3D object) that is a thin slab standing in a doorway. A puzzle hides it until it is solved.
// The slab itself is only the "trigger zone": when the game starts it is hidden and replaced by a glowing
// oval vortex in Portal Color, with a soft light and a low hum.
// Two ways to use it:
//  - Scene portal (Classroom): fill in "Scene To Load" -> walking in loads that scene.
//  - Teleport portal (Cad_lab): drag an empty object into "Teleport To" -> walking in moves the
//    player there (e.g. through a blocked doorway). It disappears after one use, so it's one-way.
// Either way, walking in plays a short transition (PortalTransition): rush in, a round wipe in the portal's
// colour, the swap happens while the screen is covered, then the wipe opens on the new place.
[RequireComponent(typeof(Collider))]
public class Portal : MonoBehaviour
{
    [Tooltip("Exact name of the scene to load (it must be in File > Build Profiles > Scene List). Ignored if Teleport To is set.")]
    public string sceneToLoad = "Level3_CADLab";
    [Tooltip("If set: teleport the player to this point instead of loading a scene (the player faces its blue arrow)")]
    public Transform teleportTo;
    [Tooltip("Runs when the player goes through, while the screen is covered by the transition (e.g. start the beeping computers)")]
    public UnityEvent onUsed;

    [Header("Look")]
    [Tooltip("Main colour of the portal. Use the colour of the place it leads to: the transition wipe uses it too, so the screen opens onto that place.")]
    public Color portalColor = new Color(0.62f, 0.3f, 1f);
    [Tooltip("Colour of the rim and the spiral arms. Pick something that contrasts with Portal Color.")]
    public Color accentColor = new Color(0.12f, 0.05f, 0.3f);
    [Range(0.2f, 3f)] public float swirlSpeed = 1f;
    [Tooltip("A soft light in the portal colour on the walls and floor around it")]
    public bool glowLight = true;
    [Tooltip("A low 3D hum you can hear from a few metres")]
    public bool hum = true;
    [Range(0f, 1f)] public float humVolume = 0.25f;

    [Header("Transition")]
    [Tooltip("ON = rush in, wipe, then reveal the new place. OFF = instant (old behaviour).")]
    public bool useTransition = true;
    [Tooltip("Seconds after the portal appears before it works. Normally leave this at 0: a portal that opens while you are standing INSIDE it already waits for you to step out and back in, so it never fires unseen. Any delay is a pause you can feel.")]
    public float armDelay = 0f;

    bool used;
    bool mustLeaveFirst;
    float armedAt;
    PlayerController watchedPlayer;
    GameObject visual;     // the vortex (not a child of this object: this object is a thin, stretched slab)

    void Awake()
    {
        // Walk-through trigger. A kinematic Rigidbody makes sure the player is detected.
        GetComponent<Collider>().isTrigger = true;
        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb == null) rb = gameObject.AddComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;
        Debug.Log("[Scripts] Portal v11 running on \"" + name + "\" (" + (teleportTo != null ? "teleport to " + teleportTo.name : "scene: " + sceneToLoad) + ")", this);
        if (teleportTo == null && string.IsNullOrEmpty(sceneToLoad))
            Debug.LogError("[Portal] \"" + name + "\" has nothing to do: drag an object into Teleport To, or type a scene name in Scene To Load.", this);

        BuildVisual();
    }

    // The vortex follows this object being switched on and off (puzzles hide it until they're solved)
    // the vortex fades in over a blink (50 ms) when the portal opens, so it does not simply pop into existence
    const float FadeInTime = 0.05f;
    Material vortexMat;
    Light vortexLight;
    float lightFull;

    IEnumerator FadeIn()
    {
        for (float t = 0f; t < FadeInTime; t += Time.deltaTime)
        {
            float k = t / FadeInTime;
            if (vortexMat != null) vortexMat.SetFloat("_Fade", k);
            if (vortexLight != null) vortexLight.intensity = lightFull * k;
            yield return null;
        }
        if (vortexMat != null) vortexMat.SetFloat("_Fade", 1f);
        if (vortexLight != null) vortexLight.intensity = lightFull;
    }

    void OnEnable()
    {
        if (visual != null)
        {
            visual.SetActive(true);
            if (vortexMat != null) { vortexMat.SetFloat("_Fade", 0f); if (vortexLight != null) vortexLight.intensity = 0f; }
            StartCoroutine(FadeIn());
        }
        armedAt = Time.time + armDelay;
        // opened right on top of the player (e.g. standing at the switch panel next to the doorway)? Then it only works once they have stepped out of it.
        PlayerController pcNow = FindAnyObjectByType<PlayerController>();
        CharacterController ccNow = pcNow != null ? pcNow.GetComponent<CharacterController>() : null;
        mustLeaveFirst = ccNow != null && GetComponent<Collider>().bounds.Intersects(ccNow.bounds);
        if (teleportTo == null && !string.IsNullOrEmpty(sceneToLoad)) StartCoroutine(PreloadNextScene());
    }

    // ---- preloading: once the portal is open (and has stayed open for a moment), start loading the next scene in the
    // background, but do NOT switch to it yet. Walking in then only has to flip the switch behind the cover.
    AsyncOperation preload;

    IEnumerator PreloadNextScene()
    {
        yield return new WaitForSeconds(0.75f);                 // (a puzzle hides and shows its portal while it sets up: wait until it is really open)
        if (preload != null || used) yield break;
        if (!Application.CanStreamedLevelBeLoaded(sceneToLoad) || sceneToLoad == SceneManager.GetActiveScene().name) yield break;
        // Low priority = the main thread only spends a few milliseconds per frame on finishing the load, so the game keeps its frame rate
        Application.backgroundLoadingPriority = ThreadPriority.Low;
        preload = SceneManager.LoadSceneAsync(sceneToLoad);
        preload.allowSceneActivation = false;                   // load it, but stay here
    }

    // PortalTransition asks for the background load (null if there was none)
    public AsyncOperation TakePreload()
    {
        AsyncOperation op = preload;
        preload = null;
        Application.backgroundLoadingPriority = ThreadPriority.High;      // we are switching now: finish quickly
        return op;
    }

    // The vortex (shader, light, mesh) is drawn invisibly for a few frames when the level starts, so the first time it really
    // appears nothing has to be compiled or uploaded, which was a visible hitch.
    bool warming;
    class PortalWarm : MonoBehaviour
    {
        public Portal owner;
        IEnumerator Start()
        {
            for (int i = 0; i < 4; i++) yield return null;
            if (owner == null) yield break;
            owner.warming = false;
            if (!owner.isActiveAndEnabled) gameObject.SetActive(false);
        }
    }

    void OnDisable() { if (visual != null && !warming) visual.SetActive(false); }

    // With an arm delay, also notice a player who is ALREADY inside when it becomes active (no "enter" event happens for them)
    void Update()
    {
        if (mustLeaveFirst)
        {
            if (watchedPlayer == null) watchedPlayer = FindAnyObjectByType<PlayerController>();
            CharacterController c = watchedPlayer != null ? watchedPlayer.GetComponent<CharacterController>() : null;
            if (c == null || !GetComponent<Collider>().bounds.Intersects(c.bounds)) mustLeaveFirst = false;     // they stepped out: it is live now
            return;
        }
        if (armDelay <= 0f || used || Time.time < armedAt) return;
        if (watchedPlayer == null) watchedPlayer = FindAnyObjectByType<PlayerController>();
        if (watchedPlayer == null) return;
        CharacterController cc = watchedPlayer.GetComponent<CharacterController>();
        if (cc != null && GetComponent<Collider>().bounds.Intersects(cc.bounds)) Use(cc);
    }
    void OnDestroy() { if (visual != null) Destroy(visual); }

    // Which way the player is carried through the portal: from the side they came from to the other side
    public Vector3 TravelDirection(Vector3 playerPosition)
    {
        Bounds b = GetBounds();
        Vector3 normal = b.extents.x < b.extents.z ? Vector3.right : Vector3.forward;
        float side = Vector3.Dot(playerPosition - b.center, normal);
        return side >= 0f ? -normal : normal;
    }

    Bounds GetBounds()
    {
        Renderer r = GetComponent<Renderer>();
        return r != null ? r.bounds : GetComponent<Collider>().bounds;
    }

    void OnTriggerEnter(Collider other)
    {
        if (Time.time < armedAt) return;               // not working yet (see Arm Delay)
        Use(other);
    }

    void Use(Collider other)
    {
        PlayerController pc = other.GetComponentInParent<PlayerController>();
        if (used || pc == null || PortalTransition.Busy || mustLeaveFirst) return;

        if (teleportTo != null)
        {
            used = true;
            Go(pc, () =>
            {
                Teleport(pc);
                onUsed.Invoke();
                gameObject.SetActive(false);      // one-way: the portal is gone after you use it
            }, null);
            return;
        }

        if (string.IsNullOrEmpty(sceneToLoad))
        {
            PlayerInteractor pi = other.GetComponentInParent<PlayerInteractor>();
            if (pi != null) pi.ShowMessage("Portal is not set up: its Teleport To slot is empty", 3f);
            Debug.LogError("[Portal] \"" + name + "\": Teleport To is empty (and no scene name). Drag the arrival point into Teleport To.", this);
            return;
        }

        if (Application.CanStreamedLevelBeLoaded(sceneToLoad))
        {
            used = true;
            Go(pc, () => onUsed.Invoke(), sceneToLoad);
        }
        else
        {
            Debug.LogError("[Portal] Can't load scene \"" + sceneToLoad + "\". Check the name and add it to File > Build Profiles > Scene List.", this);
            PlayerInteractor pi = other.GetComponentInParent<PlayerInteractor>();
            if (pi != null) pi.ShowMessage("Portal can't find scene \"" + sceneToLoad + "\"", 3f);
        }
    }

    // atCover = the real work (teleport / events); with a transition it happens while the screen is covered
    void Go(PlayerController pc, Action atCover, string scene)
    {
        if (useTransition) { PortalTransition.Play(this, pc, atCover, scene); return; }
        atCover();
        if (!string.IsNullOrEmpty(scene)) SceneManager.LoadScene(scene);
    }

    void Teleport(PlayerController pc)
    {
        // A CharacterController ignores position changes while it's on, so switch it off for a moment
        CharacterController cc = pc.GetComponent<CharacterController>();
        if (cc != null) cc.enabled = false;
        pc.transform.SetPositionAndRotation(teleportTo.position, Quaternion.Euler(0f, teleportTo.eulerAngles.y, 0f));
        if (PlayerSpawn.Current != null) PlayerSpawn.Current.SetSpawn(teleportTo.position, teleportTo.rotation);   // R now returns you here
        ObjectiveHUD.NextStage();                                                                                  // (Cad_lab: the next objective)
        if (cc != null) cc.enabled = true;

        // Off + on again -> PlayerController.OnEnable re-syncs the camera to the new facing
        pc.enabled = false;
        pc.enabled = true;
    }

    // ================= The vortex =================
    void BuildVisual()
    {
        Shader shader = Shader.Find("Custom/PortalSwirl");
        if (shader == null)
        {
            Debug.LogWarning("[Portal] PortalSwirl shader not found: put PortalSwirl.shader in Assets/Resources. Keeping the plain slab.", this);
            return;
        }

        // Size and direction from the slab: thin side = the way through, the other flat side = width
        Bounds b = GetBounds();
        Vector3 e = b.extents;
        bool normalIsX = e.x < e.z;
        Vector3 normal = normalIsX ? Vector3.right : Vector3.forward;
        float width = 2f * (normalIsX ? e.z : e.x) * 0.96f;     // a little smaller than the doorway so it doesn't clip
        float height = 2f * e.y * 0.96f;
        const float halo = 1.3f;                                // quad is bigger than the opening: room for the soft glow

        Renderer slab = GetComponent<Renderer>();
        if (slab != null) slab.enabled = false;                 // hide the purple rectangle (still visible in the editor)

        visual = new GameObject(name + "_Visual");
        visual.transform.SetPositionAndRotation(b.center, Quaternion.identity);
        visual.transform.SetParent(transform.parent, true);

        Material mat = new Material(shader) { name = "PortalSwirl (runtime)" };
        mat.SetColor("_Color", portalColor);
        mat.SetColor("_Accent", accentColor);
        mat.SetFloat("_Speed", swirlSpeed);
        mat.SetFloat("_Halo", halo);

        GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
        Destroy(quad.GetComponent<Collider>());
        quad.name = "Vortex";
        quad.transform.SetParent(visual.transform, false);
        quad.transform.localRotation = Quaternion.LookRotation(normal);
        quad.transform.localScale = new Vector3(width * halo, height * halo, 1f);
        MeshRenderer mr = quad.GetComponent<MeshRenderer>();
        mr.sharedMaterial = mat;
        vortexMat = mr.material;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows = false;

        if (glowLight)
        {
            GameObject lightGo = new GameObject("PortalLight");
            lightGo.transform.SetParent(visual.transform, false);
            Light l = lightGo.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = Color.Lerp(portalColor, Color.white, 0.25f);
            l.range = Mathf.Max(width, height) * 2f;
            l.intensity = 1.6f;
            l.shadows = LightShadows.None;                      // cheap on low-end laptops
            vortexLight = l; lightFull = l.intensity;
        }

        warming = true;
        visual.AddComponent<PortalWarm>().owner = this;

        if (hum)
        {
            AudioSource s = visual.AddComponent<AudioSource>();
            s.clip = MakeHum();
            s.loop = true;
            s.volume = humVolume;
            s.spatialBlend = 1f;                                // fully 3D: direction + distance
            s.rolloffMode = AudioRolloffMode.Linear;
            s.minDistance = 1f;
            s.maxDistance = 9f;
            s.dopplerLevel = 0f;
            s.playOnAwake = false;
            s.Play();
        }
    }

    // 2-second loop: a low, slowly pulsing hum with a faint shimmer (all frequencies fit the loop exactly)
    static AudioClip MakeHum()
    {
        const int rate = 44100;
        const float seconds = 2f;
        int n = Mathf.CeilToInt(rate * seconds);
        float[] data = new float[n];
        for (int i = 0; i < n; i++)
        {
            float t = (float)i / rate;
            float swell = 0.75f + 0.25f * Mathf.Sin(2f * Mathf.PI * 1f * t);
            float s = 0.5f * Mathf.Sin(2f * Mathf.PI * 70f * t)
                    + 0.25f * Mathf.Sin(2f * Mathf.PI * 140.5f * t * 1f)   // slightly off: a slow beating shimmer
                    + 0.1f * Mathf.Sin(2f * Mathf.PI * 420f * t) * (0.5f + 0.5f * Mathf.Sin(2f * Mathf.PI * 2f * t));
            data[i] = s * swell * 0.5f;
        }
        AudioClip clip = AudioClip.Create("portalHum", n, 1, rate, false);
        clip.SetData(data, 0);
        return clip;
    }
}
