using UnityEngine;

// VERSION 1
// A piece of chalk thrown by a professor. Flies in a straight line. If it reaches the player:
//   dodged (during a dodge)  -> flies on past, harmless
//   perfect block            -> flies BACK at the professor and hurts him
//   block / hit              -> hits (the block takes most of the damage)
// Made from one small flat-colour capsule, no model needed.
public class Chalk : MonoBehaviour
{
    public static int InFlight;      // how many pieces are flying right now (the tutorial checks this)

    // "Enter Play Mode Options" is on (no domain reload)
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() => InFlight = 0;

    Vector3 direction;
    float speed, damage, life = 4f;
    ProfessorAI owner;
    PlayerCombat target;
    bool reflected, passedPlayer;

    public static Chalk Spawn(Vector3 position, Vector3 direction, float speed, float damage, ProfessorAI owner, PlayerCombat target)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        go.name = "Chalk";
        Destroy(go.GetComponent<Collider>());
        go.transform.position = position;
        go.transform.localScale = new Vector3(0.05f, 0.11f, 0.05f);
        go.transform.rotation = Quaternion.LookRotation(direction) * Quaternion.Euler(90f, 0f, 0f);
        MeshRenderer mr = go.GetComponent<MeshRenderer>();
        Shader shader = Shader.Find("Custom/ScreenGlow");
        if (shader != null) mr.sharedMaterial = new Material(shader) { color = new Color(1f, 1f, 0.95f) };
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        Chalk c = go.AddComponent<Chalk>();
        c.direction = direction.normalized;
        c.speed = speed;
        c.damage = damage;
        c.owner = owner;
        c.target = target;
        return c;
    }

    void OnEnable() { InFlight++; }
    void OnDisable() { InFlight = Mathf.Max(0, InFlight - 1); }

    void Update()
    {
        life -= Time.deltaTime;
        if (life <= 0f) { Destroy(gameObject); return; }

        float step = speed * Time.deltaTime;
        Vector3 pos = transform.position;

        // walls and furniture
        if (Physics.Raycast(pos, direction, out RaycastHit hit, step + 0.05f, ~0, QueryTriggerInteraction.Ignore))
        {
            bool isPlayer = hit.collider.GetComponentInParent<PlayerCombat>() != null;
            bool isProf = hit.collider.GetComponentInParent<ProfessorAI>() != null;
            if (!isPlayer && !isProf) { Hit(); return; }
        }

        transform.position = pos + direction * step;

        if (!reflected)
        {
            if (target == null) return;
            Vector3 chest = target.transform.position + Vector3.up * 1.2f;
            bool near = (chest - transform.position).magnitude < 0.6f;
            if (!near) { passedPlayer = false; return; }
            if (passedPlayer) return;                           // already dodged this one: let it fly through
            PlayerCombat.HitResult result = target.ReceiveHit(damage, owner != null ? owner.transform.position : transform.position);
            switch (result)
            {
                case PlayerCombat.HitResult.Parried:
                    if (owner != null)
                    {
                        direction = (owner.Centre - transform.position).normalized;        // back at the thrower, faster
                        speed *= 1.6f;
                        reflected = true;
                        life = 3f;
                    }
                    else Hit();
                    break;
                case PlayerCombat.HitResult.Dodged:
                    passedPlayer = true;
                    break;
                default:
                    Hit();
                    break;
            }
        }
        else if (owner != null && (owner.Centre - transform.position).magnitude < 0.7f)
        {
            owner.TakeHit(14f, false, transform.position - direction);                       // his own chalk hurts
            Hit();
        }
    }

    void Hit()
    {
        CombatSfx.Play(CombatSfx.Clip.ChalkHit, transform.position);
        Destroy(gameObject);
    }
}
