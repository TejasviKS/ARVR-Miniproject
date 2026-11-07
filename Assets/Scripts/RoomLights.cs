using System.Collections;
using UnityEngine;

// VERSION 1
// The ceiling lights of ONE room (the tutorial cabin). Put it on an empty object and fill in the lights
// and the glowing tube fixtures. SwitchOff() makes them flicker, then go out, and darkens the room's ambient light.
// (Used by LightSwitch.)
public class RoomLights : MonoBehaviour
{
    [Header("What is lit")]
    public Light[] lights;
    [Tooltip("The glowing tube fixtures on the ceiling: their glow goes out with the lights (needs a material with Emission ON)")]
    public Renderer[] fixtures;
    [ColorUsage(false, true)] public Color fixtureGlow = new Color(1.1f, 1.05f, 0.95f);

    [Header("Ambient light of the room")]
    public Color ambientColor = new Color(0.78f, 0.82f, 0.9f);
    [Range(0f, 1f)] public float ambientOn = 0.5f;
    [Tooltip("How much light is left when everything is off (a little, so the room is still readable)")]
    [Range(0f, 1f)] public float ambientOff = 0.07f;

    public bool IsOn { get; private set; } = true;

    float[] baseIntensity;
    Material[] fixtureMats;
    AudioSource sfx;
    AudioClip thunk, crackle;

    void Awake()
    {
        baseIntensity = new float[lights.Length];
        for (int i = 0; i < lights.Length; i++) baseIntensity[i] = lights[i] != null ? lights[i].intensity : 0f;

        fixtureMats = new Material[fixtures.Length];
        for (int i = 0; i < fixtures.Length; i++)
        {
            if (fixtures[i] == null) continue;
            fixtureMats[i] = fixtures[i].material;          // own copy, so only this room's tubes change
            fixtureMats[i].EnableKeyword("_EMISSION");
        }

        sfx = gameObject.AddComponent<AudioSource>();
        sfx.playOnAwake = false;
        sfx.spatialBlend = 0f;
        thunk = MakeClip("thunk", 0.35f, t => Mathf.Sin(2f * Mathf.PI * Mathf.Lerp(160f, 45f, t / 0.35f) * t) * 0.6f * Mathf.Exp(-t * 11f));
        crackle = MakeClip("crackle", 0.06f, t => (Random.value * 2f - 1f) * 0.25f * (1f - t / 0.06f));
        SetLevel(1f);
    }

    // 1 = fully on, 0 = off
    public void SetLevel(float f)
    {
        for (int i = 0; i < lights.Length; i++)
            if (lights[i] != null) lights[i].intensity = baseIntensity[i] * f;
        foreach (Material m in fixtureMats)
            if (m != null) m.SetColor("_EmissionColor", fixtureGlow * f);
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = ambientColor * Mathf.Lerp(ambientOff, ambientOn, f);
    }

    // Flicker, then out. Use with StartCoroutine / yield return.
    public IEnumerator SwitchOff()
    {
        IsOn = false;
        sfx.PlayOneShot(thunk, 0.9f);
        float[] level = { 0.15f, 1f, 0.05f, 0.7f, 0.2f, 0.5f };
        float[] hold  = { 0.07f, 0.09f, 0.06f, 0.10f, 0.05f, 0.12f };
        for (int i = 0; i < level.Length; i++)
        {
            SetLevel(level[i]);
            sfx.PlayOneShot(crackle, 0.6f);
            yield return new WaitForSeconds(hold[i]);
        }
        SetLevel(0f);
    }

    static AudioClip MakeClip(string name, float seconds, System.Func<float, float> wave)
    {
        const int rate = 44100;
        int n = Mathf.CeilToInt(rate * seconds);
        float[] data = new float[n];
        for (int i = 0; i < n; i++) data[i] = wave((float)i / rate);
        AudioClip clip = AudioClip.Create(name, n, 1, rate, false);
        clip.SetData(data, 0);
        return clip;
    }
}
