using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// VERSION 1
// An AI professor you can fight. Put it on an empty object that has a CharacterController and a Health.
// It builds its own body from simple shapes as a hostile hologram (no model needed): the combat cousin of the Cad_lab Keeper.
//
// THE TUTORIAL PROFESSOR is deliberately limited: he only has
//   - a pointer SWING (slow telegraph, one hit), and
//   - one slow chalk throw.
// The switches below (Aggressive / Use Melee / Use Chalk) let the combat tutorial turn each one on only when it is
// being taught. The final boss will be a different, bigger script with many more abilities.
//
// Every attack is TELEGRAPHED (he raises the arm, flashes red and makes a sound) so a fight is always readable.
// A perfect block stuns him. While stunned he takes double damage. A heavy swing breaks his concentration.
[RequireComponent(typeof(CharacterController), typeof(Health))]
public class ProfessorAI : MonoBehaviour
{
    public static readonly List<ProfessorAI> All = new List<ProfessorAI>();

    public string displayName = "Asst. Prof. GPT";
    [Tooltip("Width of the health bar at the top of the screen (pixels). The tutorial professor is small; the boss will be huge.")]
    public float healthBarWidth = 300f;
    [Tooltip("Colour of this opponent's health bar")]
    public Color healthBarColour = new Color(0.58f, 0.27f, 0.92f);
    [Tooltip("Colour of his hologram body (the boss uses a different one)")]
    public Color bodyColour = new Color(1f, 0.22f, 0.62f);
    public enum BodyStyle { Rectangular, Triangular }
    [Tooltip("Rectangular = Prof. GPT. Triangular = Prof. Claude, the boss: a taller, sharper, floating body made of triangles")]
    public BodyStyle bodyStyle = BodyStyle.Rectangular;

    [Header("For the boss fight (BossFight.cs switches these; leave them alone in the tutorial)")]
    [Tooltip("ON = his own decisions are paused: he still faces you, gets stunned, and can be hit, but starts no attack by himself")]
    public bool brainPaused;
    [Tooltip("ON = a few seconds after he is beaten his object switches itself off (GPT). The boss stays lying there: his director script lives on the same object and still has the ending to play.")]
    public bool hideWhenDead = true;
    [Tooltip("0 = the bar is Health Bar Width wide. Above 0 = the bar takes this share of the SCREEN width (0.7 = 70%), whatever the screen size")]
    [Range(0f, 0.95f)] public float barScreenFraction = 0f;
    [Tooltip("Positions (0..1) of the notches drawn on his health bar, and the small tag under it (the phase name)")]
    public float[] barNotches;
    public string barTag = "";
    [Tooltip("Colour of that tag. Clear (alpha 0) = a light version of the bar colour")]
    public Color barTagColour = Color.clear;
    [Tooltip("ON = a heavy hit does not break his wind-up (the boss's blink strike)")]
    public bool uninterruptible;

    [Header("What he is allowed to do (the tutorial switches these)")]
    [Tooltip("OFF = he just stands and talks (no attacks, keeps his distance)")]
    public bool aggressive;
    public bool useMelee = true;
    public bool useChalk = true;

    [Header("Pointer swing")]
    public float meleeDamage = 10f;
    public float meleeRange = 2.1f;
    [Tooltip("Seconds from raising the pointer to the hit: how long you have to react")]
    public float meleeTelegraph = 0.9f;
    public float meleeRecover = 1.1f;
    public float meleeCooldown = 1.2f;

    [Header("Chalk throw")]
    public float chalkDamage = 6f;
    public float chalkTelegraph = 0.95f;
    public float chalkRecover = 1.0f;
    public float chalkCooldown = 3.2f;
    public float chalkSpeed = 6.5f;

    [Header("Movement and stun")]
    public float moveSpeed = 1.7f;
    public float stunTime = 3f;

    public enum State { Idle, Chase, TelegraphMelee, Swing, TelegraphChalk, Recover, Stunned, Dead, TelegraphLunge, Lunge, TelegraphVolley }
    public State Current { get; private set; } = State.Idle;
    public Health Health { get; private set; }
    public bool IsStunned => Current == State.Stunned;
    public Vector3 Centre => transform.position + Vector3.up * 1.1f;

    // for the tutorial and the HUD
    public event Action<string> Barked;                    // a line he says
    public event Action<State> Telegraphing;               // he started winding up (TelegraphMelee / TelegraphChalk)
    public event Action ImpactSoon;                        // his swing lands in a moment: block NOW for a perfect block
    public event Action<float, bool, bool> HitReceived;    // damage dealt, was heavy, was he stunned
    public event Action Parried;                           // you perfect-blocked his swing
    public event Action ChalkThrown;
    public event Action Died;

    CharacterController cc;
    PlayerCombat player;
    float stateStart, nextMeleeAt, nextChalkAt;
    bool impactSoonSent;
    Vector3 startPosition;
    Quaternion startRotation;
    float pushSpeed; Vector3 pushDir;

    // ---- the body
    Transform rightArm, leftArm, head, body;
    readonly List<Renderer> parts = new List<Renderer>();
    readonly List<Color> baseColours = new List<Color>();
    Renderer pointerR, chalkR;
    float rightAngle, leftAngle;
    float flashUntil;

    void OnEnable() { if (!All.Contains(this)) All.Add(this); }
    void OnDisable() { All.Remove(this); }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() => All.Clear();

    void Awake()
    {
        cc = GetComponent<CharacterController>();
        Health = GetComponent<Health>();
        cc.height = 1.9f; cc.radius = 0.4f; cc.center = new Vector3(0f, 0.95f, 0f);
        startPosition = transform.position;
        startRotation = transform.rotation;
        BuildBody();
        Health.Died += OnHealthDied;
    }

    void Start()
    {
        player = FindAnyObjectByType<PlayerCombat>();
        stateStart = Time.time;
    }

    // What he says: shown as a subtitle (typed out letter by letter) and spoken with the same little blips as Lab Asst. Gemini
    public const float LettersPerSecond = 40f;

    // How long a line stays on screen AFTER it has been typed out, by its length: a short fixed pause plus a little per letter, at least 1 s.
    // (You read it while it types, so this is only the time to finish reading; it used to be 0.7 s + length / 16, about 45% longer.)
    // The time to TYPE it out (LettersPerSecond) comes on top of that.
    public static float ReadSeconds(string line) => Mathf.Max(1.0f, 0.4f + (line != null ? line.Length : 0) / 30f);
    // typing + reading: how long until the line is over. Wait this (plus a short pause between speakers) before the next line.
    public static float LineSeconds(string line) => (line != null ? line.Length : 0) / LettersPerSecond + ReadSeconds(line);
    AudioSource voice;
    AudioClip blipClip;
    Coroutine speaking;

    public void Say(string line)
    {
        Barked?.Invoke(line);
        if (!isActiveAndEnabled) return;
        if (speaking != null) StopCoroutine(speaking);
        speaking = StartCoroutine(Speak(line));
    }

    IEnumerator Speak(string line)
    {
        if (voice == null)
        {
            voice = gameObject.AddComponent<AudioSource>();
            voice.spatialBlend = 1f;                          // 3D, like Gemini: direction and distance
            voice.rolloffMode = AudioRolloffMode.Linear;
            voice.minDistance = 1f; voice.maxDistance = 25f;
            voice.dopplerLevel = 0f; voice.playOnAwake = false;
            blipClip = RiddleNPC.MakeBlip();
        }
        float letters = 0f;
        int shown = 0;
        while (shown < line.Length)
        {
            letters += Time.deltaTime * LettersPerSecond;
            while (shown < line.Length && shown < (int)letters)
            {
                shown++;
                if (shown % 3 == 0 && !char.IsWhiteSpace(line[shown - 1]))        // a blip every third letter, random pitch = "alien speech"
                {
                    voice.pitch = UnityEngine.Random.Range(0.85f, 1.3f);
                    voice.PlayOneShot(blipClip, 0.8f);
                }
            }
            yield return null;
        }
        speaking = null;
    }

    // ================================================================ every frame
    void Update()
    {
        if (player == null) player = FindAnyObjectByType<PlayerCombat>();
        if (Current == State.Dead) { AnimateBody(); return; }

        Vector3 toPlayer = player != null ? player.transform.position - transform.position : transform.forward;
        toPlayer.y = 0f;
        float dist = toPlayer.magnitude;
        bool playerAlive = player != null && !player.Health.Dead;
        float inState = Time.time - stateStart;

        // face the player (not while stunned)
        if (Current != State.Stunned && Current != State.Lunge && Current != State.TelegraphLunge && dist > 0.1f)
            transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(toPlayer), 220f * Time.deltaTime);

        switch (Current)
        {
            case State.Idle:
            case State.Chase:
            {
                if (brainPaused) { Walk(Vector3.zero); break; }                                    // the boss director is in charge
                float stopAt = aggressive ? (useMelee ? meleeRange * 0.85f : 4.2f) : 3.2f;        // calm: keeps his distance and talks
                if (dist > stopAt + 0.2f) { Current = State.Chase; Walk(toPlayer.normalized * moveSpeed); }
                else { Current = State.Idle; Walk(Vector3.zero); }

                if (aggressive && playerAlive)
                {
                    if (useMelee && dist <= meleeRange + 0.3f && Time.time >= nextMeleeAt) Begin(State.TelegraphMelee);
                    else if (useChalk && dist >= 2.5f && dist <= 9f && Time.time >= nextChalkAt) Begin(State.TelegraphChalk);
                    else if (useChalk && !useMelee && dist < 2.5f && Time.time >= nextChalkAt)
                    {
                        // too close for a ranged attack and no melee to fall back on (the chalk lesson): step back to make room.
                        // If something is in the way (a wall behind him), he gives up after a moment and throws point-blank.
                        retreatTime += Time.deltaTime;
                        if (retreatTime > 1.2f) Begin(State.TelegraphChalk);
                        else Walk(-toPlayer.normalized * moveSpeed * 1.8f);
                    }
                    else retreatTime = 0f;
                }
                else retreatTime = 0f;
                break;
            }

            case State.TelegraphMelee:
                Walk(Vector3.zero);
                // the "now!" cue comes 0.25 s before the hit: reacting to it lands inside the player's 0.3 s perfect-block window
                if (!impactSoonSent && inState >= meleeTelegraph - 0.25f) { impactSoonSent = true; ImpactSoon?.Invoke(); }
                if (inState >= meleeTelegraph) { Begin(State.Swing); DoSwing(); }
                break;

            case State.Swing:
                Walk(Vector3.zero);
                if (inState >= 0.2f) Begin(State.Recover, meleeRecover);
                break;

            case State.TelegraphChalk:
                Walk(Vector3.zero);
                if (inState >= chalkTelegraph) { ThrowChalk(); Begin(State.Recover, chalkRecover); }
                break;

            case State.Recover:
                Walk(Vector3.zero);
                if (inState >= recoverFor) { Current = State.Idle; stateStart = Time.time; }
                break;

            case State.Stunned:
                Walk(Vector3.zero);
                if (inState >= stunTime) { Current = State.Idle; stateStart = Time.time; nextMeleeAt = Time.time + 1.0f; SetTint(null); }
                break;
        }

        // pushed back by a heavy hit
        if (pushSpeed > 0.05f)
        {
            cc.Move(pushDir * pushSpeed * Time.deltaTime);
            pushSpeed = Mathf.MoveTowards(pushSpeed, 0f, 14f * Time.deltaTime);
        }
        AnimateBody();
    }

    float recoverFor, retreatTime;

    void Begin(State s, float recover = 0f)
    {
        Current = s;
        stateStart = Time.time;
        recoverFor = recover; retreatTime = 0f;
        impactSoonSent = false;
        if (s == State.TelegraphMelee || s == State.TelegraphChalk || s == State.TelegraphLunge || s == State.TelegraphVolley)
        {
            CombatSfx.Play(CombatSfx.Clip.Telegraph, transform.position, 0.8f);
            Telegraphing?.Invoke(s);
        }
    }

    // ---- what the boss director uses (BossFight.cs)
    public void SetState(State s, float recover = 0f) { if (Current != State.Dead) Begin(s, recover); }
    public Vector3 MoveBy(Vector3 delta) { Vector3 before = transform.position; cc.Move(delta); return transform.position - before; }
    public void Teleport(Vector3 position)
    {
        cc.enabled = false;
        transform.position = position;
        cc.enabled = true;
    }
    public void StunNow() { if (Current != State.Dead) Stun(); }
    public void SetBodyVisible(bool visible) { if (body != null) body.gameObject.SetActive(visible); }

    // The "beaten" slouch (the boss's fake-out): the arms hang limp, the head drops, the body sags and leans forward, the light dies and the
    // whole hologram goes grey. It eases in over about a second and out again the same way.
    bool slouching; float slouch;
    public void SetSlouch(bool on)
    {
        slouching = on;
        SetTint(on ? new Color(0.25f, 0.25f, 0.3f) : (Color?)null);
    }
    public static Mesh SigilMesh(float radius) => FlatTriangle(radius);        // a flat glowing triangle for the floor warnings
    public void ResetTimers(float meleeIn, float chalkIn) { nextMeleeAt = Time.time + meleeIn; nextChalkAt = Time.time + chalkIn; }
    public float StateTime => Time.time - stateStart;

    void Walk(Vector3 velocity)
    {
        velocity.y = -3f;                                  // keeps him on the floor
        cc.Move(velocity * Time.deltaTime);
    }

    // ================================================================ his attacks
    void DoSwing()
    {
        nextMeleeAt = Time.time + meleeCooldown;
        CombatSfx.Play(CombatSfx.Clip.Swing, transform.position);
        if (player == null || player.Health.Dead) return;
        Vector3 to = player.transform.position - transform.position; to.y = 0f;
        bool inReach = to.magnitude <= meleeRange + 0.4f;
        bool inFront = Vector3.Angle(transform.forward, to) <= 55f;
        if (!inReach || !inFront) return;                  // you stepped out of the way
        PlayerCombat.HitResult r = player.ReceiveHit(meleeDamage, transform.position);
        if (r == PlayerCombat.HitResult.Parried)
        {
            Parried?.Invoke();
            Stun();
        }
    }

    void ThrowChalk()
    {
        nextChalkAt = Time.time + chalkCooldown;
        if (player == null) return;
        CombatSfx.Play(CombatSfx.Clip.Throw, transform.position);
        Vector3 hand = transform.position + Vector3.up * 1.3f + transform.forward * 0.4f;
        Vector3 aim = (player.transform.position + Vector3.up * 1.2f) - hand;
        Chalk.Spawn(hand, aim, chalkSpeed, chalkDamage, this, player);
        ChalkThrown?.Invoke();
    }

    // ================================================================ being hit
    public void TakeHit(float damage, bool heavy, Vector3 from)
    {
        if (Current == State.Dead) return;
        bool wasStunned = IsStunned;
        float dealt = Health.Damage(damage * (wasStunned ? 2f : 1f));
        flashUntil = Time.time + 0.12f;
        CombatSfx.Play(CombatSfx.Clip.Hit, transform.position);
        HitReceived?.Invoke(dealt, heavy, wasStunned);
        if (Current == State.Dead) return;

        if (heavy)
        {
            Vector3 away = transform.position - from; away.y = 0f;
            pushDir = away.normalized; pushSpeed = 6f;
            // a heavy swing breaks his concentration
            if (!uninterruptible && (Current == State.TelegraphMelee || Current == State.TelegraphChalk || Current == State.TelegraphVolley)) { Begin(State.Recover, 0.9f); }
        }
    }

    void Stun()
    {
        Current = State.Stunned;
        stateStart = Time.time;
        impactSoonSent = false;
        CombatSfx.Play(CombatSfx.Clip.Stun, transform.position);
        SetTint(new Color(1f, 0.9f, 0.3f));
    }

    void OnHealthDied()
    {
        Current = State.Dead;
        stateStart = Time.time;
        SetTint(null);
        Died?.Invoke();
        StartCoroutine(HideLater());
    }

    IEnumerator HideLater()
    {
        yield return new WaitForSeconds(3.5f);
        if (Current == State.Dead && hideWhenDead) gameObject.SetActive(false);
    }

    // back to the start of the fight (the player was beaten and retries)
    public void ResetFight()
    {
        StopAllCoroutines();
        gameObject.SetActive(true);
        cc.enabled = false;
        transform.SetPositionAndRotation(startPosition, startRotation);
        cc.enabled = true;
        Health.Restore();
        Current = State.Idle;
        stateStart = Time.time;
        nextMeleeAt = Time.time + 2f; nextChalkAt = Time.time + 3f;
        pushSpeed = 0f;
        SetTint(null);
    }

    // ================================================================ the body: a hostile hologram
    // Same family as the Lab Asst. Gemini (a glowing projection: scan lines, a projector disc, a floating head and body),
    // but built to look dangerous: angular blocks instead of round shapes, a dark see-through core with a magenta rim
    // instead of soft cyan, an amber visor, and a glitchy signal. Like EVE from WALL-E, but made of edges: one body,
    // one head, two floating arms. (A more complex design with a mortarboard and orbiting shards is saved in
    // seventhFloor/ProfessorAI_complex_design_for_boss.cs.txt, for Prof. Claude.)
    Color Rim => bodyColour;
    static readonly Color Amber = new Color(1f, 0.72f, 0.15f), Chalky = new Color(1f, 0.95f, 0.8f);
    Transform hat, disc;
    Transform[] shards;
    Light glow;
    // how the body is animated (the triangular boss body changes these)
    float shardRadius = 0.62f, shardHeight = 1.25f, hoverHeight = 0f, bobAmount = 0.012f, glowBase = 1.4f, raiseRight = -165f, raiseLeft = -150f;

    void BuildBody()
    {
        Shader hostile = Shader.Find("Custom/HologramHostile");
        Shader additive = Shader.Find("Custom/Hologram");
        if (hostile == null || additive == null) { Debug.LogWarning("[ProfessorAI] Hologram shaders not found: the professor will be invisible.", this); return; }
        if (bodyStyle == BodyStyle.Triangular) { BuildBossBody(hostile, additive, false); return; }

        body = new GameObject("Body").transform;
        body.SetParent(transform, false);

        Part(hostile, PrimitiveType.Cube, body, new Vector3(0f, 1.0f, 0f), new Vector3(0.5f, 0.85f, 0.4f), Rim, new Vector3(0f, 0f, 0f));            // body
        disc = Part(additive, PrimitiveType.Cylinder, body, new Vector3(0f, 0.03f, 0f), new Vector3(0.9f, 0.01f, 0.9f), Rim, Vector3.zero).transform;   // projector disc

        head = new GameObject("Head").transform;                                                                                                   // floats above the body, like EVE's
        head.SetParent(body, false);
        head.localPosition = new Vector3(0f, 1.75f, 0f);
        Part(hostile, PrimitiveType.Cube, head, Vector3.zero, new Vector3(0.38f, 0.28f, 0.32f), Rim, Vector3.zero);
        Part(additive, PrimitiveType.Cube, head, new Vector3(0f, 0.02f, 0.165f), new Vector3(0.3f, 0.08f, 0.02f), Amber, Vector3.zero);            // visor

        rightArm = MakeArm(hostile, new Vector3(0.42f, 1.35f, 0f));
        leftArm = MakeArm(hostile, new Vector3(-0.42f, 1.35f, 0f));
        // the pointer is a laser beam continuing the right arm; the chalk glows in the left hand
        pointerR = Part(additive, PrimitiveType.Cylinder, rightArm, new Vector3(0f, -1.0f, 0f), new Vector3(0.025f, 0.4f, 0.025f), Amber, Vector3.zero);
        chalkR = Part(additive, PrimitiveType.Cylinder, leftArm, new Vector3(0f, -0.66f, 0.02f), new Vector3(0.04f, 0.07f, 0.04f), Chalky, Vector3.zero);

        GameObject lightObj = new GameObject("Glow");
        lightObj.transform.SetParent(body, false);
        lightObj.transform.localPosition = new Vector3(0f, 1.2f, 0.3f);
        glow = lightObj.AddComponent<Light>();
        glow.type = LightType.Point; glow.color = Color.Lerp(bodyColour, Color.white, 0.15f); glow.range = 4f; glow.intensity = 1.4f; glow.shadows = LightShadows.None;
    }

    // ================================================================ Prof. Claude: the boss body, made only of TRIANGLES
    // GPT is rectangular, Gemini is round, so Claude is triangular. He is built to feel like a boss: taller than you, broad
    // at the shoulders and tapering to a point, floating with no legs, a single hard slit for an eye, a broken crown of spikes
    // slowly turning above his head, huge pauldrons, a cloak of hanging blades, floating arms ending in a blade, a glowing core
    // in his chest, shards orbiting him, and a glowing triangle on the floor under him. Everything is a flat-shaded triangular
    // prism built in code (no model), and it uses the same arm / crown / shard fields the animation already drives.
    Color eyeColour => new Color(1f, 0.32f, 0.1f);

    // TWO FORMS. The base form (what he is in Haiku, Sonnet and Opus) is deliberately simple: an inverted-triangle torso, a floating
    // triangle head with one slit for an eye, two floating arms and a sigil. The FULL form (his true shape, shown when he becomes
    // Fable) adds the chest core, the cloak blades, the pauldrons, the broken crown and the orbiting shards.
    // SetForm(true, true) swaps one for the other with a flash and a swell; BossFight does that when Fable begins.
    public bool IsFullForm { get; private set; }
    float formChangedAt = -10f;

    public void SetForm(bool full, bool animate)
    {
        if (bodyStyle != BodyStyle.Triangular || (IsFullForm == full && body != null)) return;
        if (body != null) Destroy(body.gameObject);
        parts.Clear(); baseColours.Clear();
        hat = null; shards = null; disc = null; glow = null;
        Shader hostile = Shader.Find("Custom/HologramHostile"), additive = Shader.Find("Custom/Hologram");
        if (hostile == null || additive == null) return;
        BuildBossBody(hostile, additive, full);
        if (animate) { formChangedAt = Time.time; flashUntil = Time.time + 0.6f; CombatSfx.Play(CombatSfx.Clip.Heavy, transform.position, 1f); }
    }

    void BuildBossBody(Shader hostile, Shader additive, bool full)
    {
        IsFullForm = full;
        // animation settings for this body
        shardRadius = 1.15f; shardHeight = 1.35f; hoverHeight = full ? 0.14f : 0.1f; bobAmount = 0.045f; glowBase = full ? 2.4f : 1.8f;
        raiseRight = -115f; raiseLeft = -110f;                                            // the arms stay inside the room's ceiling

        body = new GameObject("Body").transform;
        body.SetParent(transform, false);

        // torso: an inverted triangle, wide at the shoulders and pointing at the floor
        MeshPart(hostile, TriPrism(full ? 1.3f : 1.15f, full ? 1.2f : 1.3f, full ? 0.45f : 0.4f, false), body, new Vector3(0f, 1.1f, 0f), Vector3.zero, Rim);

        if (full)
        {
            // the core: a small glowing triangle in his chest
            MeshPart(additive, TriPrism(0.34f, 0.38f, 0.05f, false), body, new Vector3(0f, 1.3f, 0.25f), Vector3.zero, eyeColour, 2.2f);
            // the cloak: three blades hanging below, the middle one longest
            MeshPart(hostile, TriPrism(0.32f, 1.05f, 0.07f, false), body, new Vector3(0f, 0.62f, -0.06f), Vector3.zero, Rim);
            MeshPart(hostile, TriPrism(0.30f, 0.80f, 0.07f, false), body, new Vector3(-0.42f, 0.78f, -0.04f), Vector3.zero, Rim);
            MeshPart(hostile, TriPrism(0.30f, 0.80f, 0.07f, false), body, new Vector3(0.42f, 0.78f, -0.04f), Vector3.zero, Rim);
            // pauldrons: two huge spikes pointing up and out
            MeshPart(hostile, TriPrism(0.8f, 0.66f, 0.32f, true), body, new Vector3(-0.9f, 1.78f, 0f), new Vector3(0f, 0f, 26f), Rim);
            MeshPart(hostile, TriPrism(0.8f, 0.66f, 0.32f, true), body, new Vector3(0.9f, 1.78f, 0f), new Vector3(0f, 0f, -26f), Rim);
        }

        // the head: an inverted triangle with ONE hard slit for an eye
        head = new GameObject("Head").transform;
        head.SetParent(body, false);
        head.localPosition = new Vector3(0f, 1.9f, 0f);
        MeshPart(hostile, TriPrism(0.56f, 0.46f, 0.36f, false), head, Vector3.zero, Vector3.zero, Rim);
        Part(additive, PrimitiveType.Cube, head, new Vector3(0f, 0.07f, 0.185f), new Vector3(0.28f, 0.045f, 0.02f), eyeColour, Vector3.zero);

        if (full)
        {
            // the broken crown: five spikes in a ring, turning slowly above him
            hat = new GameObject("Crown").transform;
            hat.SetParent(body, false);
            hat.localPosition = new Vector3(0f, 2.1f, 0f);
            for (int k = 0; k < 5; k++)
            {
                float a = k * 72f;
                Vector3 p = new Vector3(Mathf.Sin(a * Mathf.Deg2Rad), 0f, Mathf.Cos(a * Mathf.Deg2Rad)) * 0.42f;
                MeshPart(hostile, TriPrism(0.2f, k % 2 == 0 ? 0.42f : 0.3f, 0.07f, true), hat, p + Vector3.up * 0.18f, new Vector3(14f, a, 0f), Rim);
            }
        }

        // arms: floating, ending in a blade (right) and a shard of chalk (left)
        float shoulderX = full ? 0.95f : 0.85f;
        rightArm = MakeBossArm(hostile, new Vector3(shoulderX, 1.5f, 0.05f));
        leftArm = MakeBossArm(hostile, new Vector3(-shoulderX, 1.5f, 0.05f));
        pointerR = MeshPart(additive, TriPrism(0.14f, 0.8f, 0.04f, false), rightArm, new Vector3(0f, -0.92f, 0f), Vector3.zero, Amber, 2f);
        chalkR = MeshPart(additive, TriPrism(0.16f, 0.24f, 0.05f, false), leftArm, new Vector3(0f, -0.75f, 0.05f), Vector3.zero, Chalky, 2f);

        if (full)
        {
            // shards orbiting him
            shards = new Transform[4];
            for (int i = 0; i < 4; i++)
                shards[i] = MeshPart(hostile, TriPrism(0.2f, 0.36f, 0.05f, true), body, Vector3.zero, Vector3.zero, Rim).transform;
        }

        // the sigil: a glowing triangle on the floor, pulsing (AnimateBody scales this object); bigger in the full form
        disc = MeshPart(additive, FlatTriangle(full ? 2.0f : 1.5f), body, new Vector3(0f, 0.03f, 0f), Vector3.zero, Rim * 0.85f, 1.0f).transform;

        GameObject lightObj = new GameObject("Glow");
        lightObj.transform.SetParent(body, false);
        lightObj.transform.localPosition = new Vector3(0f, 1.3f, 0.5f);
        glow = lightObj.AddComponent<Light>();
        glow.type = LightType.Point; glow.color = Color.Lerp(bodyColour, new Color(1f, 0.3f, 0.1f), 0.5f); glow.range = 6f; glow.intensity = glowBase; glow.shadows = LightShadows.None;
    }

    Transform MakeBossArm(Shader hostile, Vector3 shoulder)
    {
        Transform pivot = new GameObject("Arm").transform;
        pivot.SetParent(body, false);
        pivot.localPosition = shoulder;
        MeshPart(hostile, TriPrism(0.3f, 0.6f, 0.2f, false), pivot, new Vector3(0f, -0.32f, 0f), Vector3.zero, Rim);
        return pivot;
    }

    Renderer MeshPart(Shader shader, Mesh mesh, Transform parent, Vector3 pos, Vector3 euler, Color colour, float strength = 0f)
    {
        GameObject go = new GameObject("Part");
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        go.transform.localRotation = Quaternion.Euler(euler);
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        MeshRenderer mr = go.AddComponent<MeshRenderer>();
        mr.sharedMaterial = new Material(shader) { color = colour };
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        if (shader.name == "Custom/Hologram") mr.material.SetFloat("_Strength", strength > 0f ? strength : 1.6f);
        else if (shader.name == "Custom/HologramHostile")
        {
            // the boss is a SOLID dark presence (near-black, almost opaque core) with a hard glowing edge and an occasional glitch:
            // it must stay heavy and readable against any room colour
            Material m = mr.material;
            m.SetColor("_CoreColor", new Color(0.02f, 0.0f, 0.02f));
            m.SetFloat("_CoreAlpha", 0.98f);                  // (a little more solid and a little brighter at the edges than before: more contrast with the room)
            m.SetFloat("_RimPower", 3.0f);
            m.SetFloat("_Strength", 1.5f);
            m.SetFloat("_Glitch", 0.35f);
        }
        parts.Add(mr);
        baseColours.Add(colour);
        return mr;
    }

    // ---- triangle meshes (flat shaded). Shared between every part of the same size.
    static readonly Dictionary<string, Mesh> meshCache = new Dictionary<string, Mesh>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetMeshes() => meshCache.Clear();

    // A triangle seen from the front (x = width, y = height), thick along z. pointUp: the tip is at the top; otherwise at the bottom.
    static Mesh TriPrism(float w, float h, float d, bool pointUp)
    {
        string key = "tri" + w + "," + h + "," + d + pointUp;
        if (meshCache.TryGetValue(key, out Mesh cached) && cached != null) return cached;
        float baseY = pointUp ? -h / 2f : h / 2f, tipY = pointUp ? h / 2f : -h / 2f;
        Mesh m = Prism(new Vector3(-w / 2f, baseY, 0f), new Vector3(w / 2f, baseY, 0f), new Vector3(0f, tipY, 0f), new Vector3(0f, 0f, d));
        meshCache[key] = m;
        return m;
    }

    // A thin triangle lying on the floor, tip pointing forward (+z). radius = distance from the centre to a corner.
    static Mesh FlatTriangle(float radius)
    {
        string key = "flat" + radius;
        if (meshCache.TryGetValue(key, out Mesh cached) && cached != null) return cached;
        Mesh m = Prism(new Vector3(0f, 0f, radius), new Vector3(radius * 0.866f, 0f, -radius * 0.5f), new Vector3(-radius * 0.866f, 0f, -radius * 0.5f), new Vector3(0f, 1f, 0f));
        meshCache[key] = m;
        return m;
    }

    // Any triangle (a, b, c) pushed out along ext: five faces, each with its own flat normal facing outwards.
    static Mesh Prism(Vector3 a, Vector3 b, Vector3 c, Vector3 ext)
    {
        Vector3 o = ext * 0.5f, e = ext.normalized, centre = (a + b + c) / 3f;
        var v = new List<Vector3>(); var n = new List<Vector3>(); var t = new List<int>();

        void Tri(Vector3 p, Vector3 q, Vector3 r, Vector3 outward)
        {
            if (Vector3.Dot(Vector3.Cross(q - p, r - p), outward) < 0f) { Vector3 tmp = q; q = r; r = tmp; }    // wind it so the face looks outwards
            Vector3 nn = Vector3.Cross(q - p, r - p).normalized;
            int i = v.Count;
            v.Add(p); v.Add(q); v.Add(r); n.Add(nn); n.Add(nn); n.Add(nn);
            t.Add(i); t.Add(i + 1); t.Add(i + 2);
        }

        Tri(a + o, b + o, c + o, e);
        Tri(a - o, b - o, c - o, -e);
        Vector3[] corner = { a, b, c };
        for (int i = 0; i < 3; i++)
        {
            Vector3 p = corner[i], q = corner[(i + 1) % 3];
            Vector3 outward = (p + q) / 2f - centre;
            outward -= Vector3.Project(outward, e);
            Tri(p - o, q - o, q + o, outward);
            Tri(p - o, q + o, p + o, outward);
        }
        Mesh m = new Mesh { name = "Prism" };
        m.SetVertices(v); m.SetNormals(n); m.SetTriangles(t, 0);
        m.RecalculateBounds();
        return m;
    }

    Transform MakeArm(Shader hostile, Vector3 shoulder)
    {
        Transform pivot = new GameObject("Arm").transform;
        pivot.SetParent(body, false);
        pivot.localPosition = shoulder;
        Part(hostile, PrimitiveType.Cube, pivot, new Vector3(0f, -0.3f, 0f), new Vector3(0.1f, 0.55f, 0.1f), Rim, Vector3.zero);
        return pivot;
    }

    Renderer Part(Shader shader, PrimitiveType type, Transform parent, Vector3 pos, Vector3 scale, Color colour, Vector3 euler)
    {
        Renderer r = Shape(shader, type, parent, pos, scale, colour);
        r.transform.localRotation = Quaternion.Euler(euler);
        if (shader.name == "Custom/Hologram") r.material.SetFloat("_Strength", 1.6f);
        return r;
    }

    Renderer Shape(Shader shader, PrimitiveType type, Transform parent, Vector3 pos, Vector3 scale, Color colour)
    {
        GameObject go = GameObject.CreatePrimitive(type);
        Destroy(go.GetComponent<Collider>());
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        go.transform.localScale = scale;
        MeshRenderer mr = go.GetComponent<MeshRenderer>();
        mr.sharedMaterial = new Material(shader) { color = colour };
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        if (shader.name == "Custom/HologramHostile")
        {
            // a bit darker, more solid and with a brighter edge than the shader's defaults, so he stands out from a flat-coloured room
            Material m = mr.material;
            m.SetColor("_CoreColor", new Color(0.05f, 0.01f, 0.10f));
            m.SetFloat("_CoreAlpha", 0.88f);
            m.SetFloat("_Strength", 1.6f);
        }
        parts.Add(mr);
        baseColours.Add(colour);
        return mr;
    }

    // tint everything one colour (null = back to normal)
    void SetTint(Color? tint)
    {
        for (int i = 0; i < parts.Count; i++)
            if (parts[i] != null) parts[i].material.color = tint.HasValue ? Color.Lerp(baseColours[i], tint.Value, 0.65f) : baseColours[i];
    }

    // hit flash: the body shader lights up, the glowing bits go white
    void SetFlash(bool on)
    {
        for (int i = 0; i < parts.Count; i++)
        {
            if (parts[i] == null) continue;
            Material m = parts[i].material;
            if (m.HasProperty("_Flash")) m.SetFloat("_Flash", on ? 1.2f : 0f);
            else if (on) m.color = Color.white;
        }
    }

    void AnimateBody()
    {
        if (body == null) return;
        float t = Time.time - stateStart;
        float rTarget = 8f, lTarget = 4f;
        float speed = 14f;
        bool redPointer = false, redChalk = false;

        switch (Current)
        {
            case State.TelegraphMelee:
                rTarget = Mathf.Lerp(8f, raiseRight, Mathf.SmoothStep(0f, 1f, t / meleeTelegraph));   // pointer goes up over the head
                speed = 20f; redPointer = true;
                break;
            case State.Swing:
                rTarget = -80f; speed = 60f; redPointer = true;                                   // and down, fast
                break;
            case State.TelegraphChalk:
            case State.TelegraphVolley:
                lTarget = Mathf.Lerp(4f, raiseLeft, Mathf.SmoothStep(0f, 1f, t / chalkTelegraph));
                speed = 20f; redChalk = true;
                break;
            case State.TelegraphLunge:
                rTarget = raiseRight; lTarget = raiseLeft; speed = 18f; redPointer = true;        // both arms up, like winding up a jump
                break;
            case State.Lunge:
                rTarget = 80f; lTarget = 80f; speed = 40f; redPointer = true;                    // both arms swept back behind him as he flies
                break;
            case State.Recover:
                rTarget = 25f; lTarget = 20f;
                if (t < 0.15f) lTarget = -85f;                                                    // the throw follows through
                break;
            case State.Stunned:
                rTarget = 40f; lTarget = 40f; speed = 8f;
                break;
            case State.Dead:
                break;
        }
        slouch = Mathf.MoveTowards(slouch, slouching ? 1f : 0f, Time.deltaTime / 1.1f);
        if (slouch > 0f) { rTarget = Mathf.Lerp(rTarget, 10f, slouch); lTarget = Mathf.Lerp(lTarget, 14f, slouch); speed = Mathf.Lerp(speed, 4f, slouch); }
        rightAngle = Mathf.LerpAngle(rightAngle, rTarget, speed * Time.deltaTime);
        leftAngle = Mathf.LerpAngle(leftAngle, lTarget, speed * Time.deltaTime);
        rightArm.localRotation = Quaternion.Euler(rightAngle, 0f, 0f);
        leftArm.localRotation = Quaternion.Euler(leftAngle, 0f, 0f);

        // head tilts when stunned, and a little bob when he is idle
        float tilt = Current == State.Stunned ? 22f * Mathf.Sin(Time.time * 6f) : 0f;
        head.localRotation = Quaternion.Euler(58f * slouch, 0f, tilt + 14f * slouch);
        float bob = Current == State.Idle || Current == State.Chase ? Mathf.Sin(Time.time * 3f) * bobAmount : 0f;
        body.localPosition = new Vector3(0f, bob + hoverHeight - 0.5f * slouch, 0f);
        if (Current != State.Dead) body.localRotation = Quaternion.Euler(26f * slouch, 0f, 0f);
        // a new form swells up from a smaller size, overshooting a little (SetForm)
        float swell = Mathf.Clamp01((Time.time - formChangedAt) / 0.9f);
        float grow = swell >= 1f ? 1f : Mathf.Lerp(0.55f, 1f, 1f - Mathf.Pow(1f - swell, 3f)) + 0.12f * Mathf.Sin(swell * Mathf.PI);
        body.localScale = Vector3.one * grow;

        // falls over when defeated
        if (Current == State.Dead)
        {
            float fall = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / 0.7f));
            body.localRotation = Quaternion.Euler(-85f * fall, 0f, 0f);
            body.localPosition = new Vector3(0f, 0.15f * fall, -0.3f * fall);
        }

        // warning colours + hit flash
        Color warn = new Color(1f, 0.1f, 0.05f);
        float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * 25f);
        if (!flashing && pointerR != null) pointerR.material.color = redPointer ? Color.Lerp(Amber, warn, pulse) : Amber;
        if (!flashing && chalkR != null) chalkR.material.color = redChalk ? Color.Lerp(Chalky, warn, pulse) : Chalky;
        if (Time.time < flashUntil)
        {
            if (!flashing) { flashing = true; SetFlash(true); }
        }
        else if (flashing)
        {
            flashing = false;                                 // flash over: back to his normal (or stunned yellow) colours
            SetFlash(false);
            if (Current == State.Stunned) SetTint(new Color(1f, 0.9f, 0.3f)); else if (slouching) SetTint(new Color(0.25f, 0.25f, 0.3f)); else SetTint(null);
        }

        // hologram life: spinning mortarboard, orbiting shards, pulsing projector disc, flickering light
        bool dead = Current == State.Dead;
        if (hat != null) hat.localRotation = Quaternion.Euler(0f, Time.time * (Current == State.Stunned ? 500f : 90f), 0f);
        if (shards != null)
            for (int i = 0; i < shards.Length; i++)
            {
                float a = Time.time * 1.4f + i * Mathf.PI * 0.5f;
                shards[i].localPosition = new Vector3(Mathf.Cos(a) * shardRadius, shardHeight + 0.35f * Mathf.Sin(a * 1.7f + i), Mathf.Sin(a) * shardRadius);
                shards[i].localRotation = Quaternion.Euler(0f, -a * Mathf.Rad2Deg, 20f * i);
                shards[i].gameObject.SetActive(!dead);
            }
        if (disc != null) disc.localScale = new Vector3(0.9f + 0.08f * Mathf.Sin(Time.time * 4f), 0.01f, 0.9f + 0.08f * Mathf.Sin(Time.time * 4f));
        if (glow != null) glow.intensity = dead ? 0f : (1f - 0.85f * slouch) * (UnityEngine.Random.value < 0.06f ? 0.4f : glowBase) + (redPointer || redChalk ? 0.8f * pulse : 0f);
    }

    bool flashing;
}
