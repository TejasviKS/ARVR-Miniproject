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

    public enum State { Idle, Chase, TelegraphMelee, Swing, TelegraphChalk, Recover, Stunned, Dead }
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

    public void Say(string line) { Barked?.Invoke(line); }

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
        if (Current != State.Stunned && dist > 0.1f)
            transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(toPlayer), 220f * Time.deltaTime);

        switch (Current)
        {
            case State.Idle:
            case State.Chase:
            {
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
        if (s == State.TelegraphMelee || s == State.TelegraphChalk)
        {
            CombatSfx.Play(CombatSfx.Clip.Telegraph, transform.position, 0.8f);
            Telegraphing?.Invoke(s);
        }
    }

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
            if (Current == State.TelegraphMelee || Current == State.TelegraphChalk) { Begin(State.Recover, 0.9f); }
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
        if (Current == State.Dead) gameObject.SetActive(false);
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
    static readonly Color Rim = new Color(1f, 0.22f, 0.62f), Amber = new Color(1f, 0.72f, 0.15f), Chalky = new Color(1f, 0.95f, 0.8f);
    Transform hat, disc;
    Transform[] shards;
    Light glow;

    void BuildBody()
    {
        Shader hostile = Shader.Find("Custom/HologramHostile");
        Shader additive = Shader.Find("Custom/Hologram");
        if (hostile == null || additive == null) { Debug.LogWarning("[ProfessorAI] Hologram shaders not found: the professor will be invisible.", this); return; }

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
        glow.type = LightType.Point; glow.color = new Color(1f, 0.3f, 0.65f); glow.range = 4f; glow.intensity = 1.4f; glow.shadows = LightShadows.None;
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
                rTarget = Mathf.Lerp(8f, -165f, Mathf.SmoothStep(0f, 1f, t / meleeTelegraph));   // pointer goes up over the head
                speed = 20f; redPointer = true;
                break;
            case State.Swing:
                rTarget = -80f; speed = 60f; redPointer = true;                                   // and down, fast
                break;
            case State.TelegraphChalk:
                lTarget = Mathf.Lerp(4f, -150f, Mathf.SmoothStep(0f, 1f, t / chalkTelegraph));
                speed = 20f; redChalk = true;
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
        rightAngle = Mathf.LerpAngle(rightAngle, rTarget, speed * Time.deltaTime);
        leftAngle = Mathf.LerpAngle(leftAngle, lTarget, speed * Time.deltaTime);
        rightArm.localRotation = Quaternion.Euler(rightAngle, 0f, 0f);
        leftArm.localRotation = Quaternion.Euler(leftAngle, 0f, 0f);

        // head tilts when stunned, and a little bob when he is idle
        float tilt = Current == State.Stunned ? 22f * Mathf.Sin(Time.time * 6f) : 0f;
        head.localRotation = Quaternion.Euler(0f, 0f, tilt);
        float bob = Current == State.Idle || Current == State.Chase ? Mathf.Sin(Time.time * 3f) * 0.012f : 0f;
        body.localPosition = new Vector3(0f, bob, 0f);

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
            if (Current == State.Stunned) SetTint(new Color(1f, 0.9f, 0.3f)); else SetTint(null);
        }

        // hologram life: spinning mortarboard, orbiting shards, pulsing projector disc, flickering light
        bool dead = Current == State.Dead;
        if (hat != null) hat.localRotation = Quaternion.Euler(0f, Time.time * (Current == State.Stunned ? 500f : 90f), 0f);
        if (shards != null)
            for (int i = 0; i < shards.Length; i++)
            {
                float a = Time.time * 1.4f + i * Mathf.PI * 0.5f;
                shards[i].localPosition = new Vector3(Mathf.Cos(a) * 0.62f, 1.25f + 0.35f * Mathf.Sin(a * 1.7f + i), Mathf.Sin(a) * 0.62f);
                shards[i].localRotation = Quaternion.Euler(0f, -a * Mathf.Rad2Deg, 20f * i);
                shards[i].gameObject.SetActive(!dead);
            }
        if (disc != null) disc.localScale = new Vector3(0.9f + 0.08f * Mathf.Sin(Time.time * 4f), 0.01f, 0.9f + 0.08f * Mathf.Sin(Time.time * 4f));
        if (glow != null) glow.intensity = dead ? 0f : (UnityEngine.Random.value < 0.06f ? 0.4f : 1.4f) + (redPointer || redChalk ? 0.8f * pulse : 0f);
    }

    bool flashing;
}
