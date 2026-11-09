using UnityEngine;

// VERSION 1
// All the fight sounds, made in code (no audio files), like the rest of the project.
public static class CombatSfx
{
    public enum Clip { Swing, Heavy, Hit, Block, Parry, Hurt, Telegraph, Throw, ChalkHit, Dodge, Stun }

    static readonly AudioClip[] clips = new AudioClip[11];

    // "Enter Play Mode Options" is on (no domain reload): the old clips are gone after Play, make new ones
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Forget() { for (int i = 0; i < clips.Length; i++) clips[i] = null; }

    public static void Play(Clip c, Vector3 at, float volume = 1f)
    {
        AudioClip clip = Get(c);
        // block, perfect block and stun play at 20% of their original volume, every other effect at 30%
        volume *= (c == Clip.Block || c == Clip.Parry || c == Clip.Stun) ? 0.2f : 0.3f;
        if (clip != null) AudioSource.PlayClipAtPoint(clip, at, volume);            // (sound effects never dip the music)
    }

    static AudioClip Get(Clip c)
    {
        int i = (int)c;
        if (clips[i] != null) return clips[i];
        switch (c)
        {
            case Clip.Swing:      clips[i] = Whoosh("swing", 0.22f, 0.06f, 0.55f, 0.45f); break;
            case Clip.Heavy:      clips[i] = Whoosh("heavy", 0.40f, 0.04f, 0.35f, 0.7f); break;
            case Clip.Dodge:      clips[i] = Whoosh("dodge", 0.25f, 0.3f, 0.08f, 0.4f); break;
            case Clip.Throw:      clips[i] = Whoosh("throw", 0.16f, 0.12f, 0.6f, 0.35f); break;
            case Clip.Hit:        clips[i] = Make("hit", 0.22f, t => (Mathf.Sin(2f * Mathf.PI * Mathf.Lerp(150f, 50f, t / 0.22f) * t) * 0.8f + (Random.value * 2f - 1f) * 0.25f) * Mathf.Exp(-t * 18f)); break;
            case Clip.Hurt:       clips[i] = Make("hurt", 0.35f, t => (Mathf.Sign(Mathf.Sin(2f * Mathf.PI * Mathf.Lerp(220f, 90f, t / 0.35f) * t)) * 0.3f) * Mathf.Exp(-t * 7f)); break;
            case Clip.Block:      clips[i] = Make("block", 0.3f, t => (Mathf.Sin(2f * Mathf.PI * 620f * t) * 0.4f + Mathf.Sin(2f * Mathf.PI * 940f * t) * 0.25f) * Mathf.Exp(-t * 16f)); break;
            case Clip.Parry:      clips[i] = Make("parry", 0.55f, t => (Mathf.Sin(2f * Mathf.PI * 1320f * t) * 0.35f + Mathf.Sin(2f * Mathf.PI * 1980f * t) * 0.25f) * Mathf.Exp(-t * 6f)); break;
            case Clip.Telegraph:  clips[i] = Make("telegraph", 0.5f, t => Mathf.Sin(2f * Mathf.PI * Mathf.Lerp(220f, 520f, t / 0.5f) * t) * 0.2f * Mathf.Sin(Mathf.PI * t / 0.5f)); break;
            case Clip.ChalkHit:   clips[i] = Make("chalkhit", 0.08f, t => (Random.value * 2f - 1f) * 0.35f * Mathf.Exp(-t * 60f)); break;
            case Clip.Stun:       clips[i] = Make("stun", 0.6f, t => Mathf.Sin(2f * Mathf.PI * (500f + 200f * Mathf.Sin(t * 30f)) * t) * 0.25f * Mathf.Exp(-t * 4f)); break;
        }
        return clips[i];
    }

    // noise that brightens (or dulls) while it fades in and out
    static AudioClip Whoosh(string name, float seconds, float from, float to, float volume)
    {
        const int rate = 44100;
        int n = Mathf.CeilToInt(rate * seconds);
        float[] data = new float[n];
        float smooth = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = (float)i / n;
            float k = Mathf.Lerp(from, to, t);
            smooth += (Random.Range(-1f, 1f) - smooth) * Mathf.Clamp01(k);
            data[i] = smooth * volume * Mathf.Sin(Mathf.PI * t);
        }
        AudioClip clip = AudioClip.Create(name, n, 1, rate, false);
        clip.SetData(data, 0);
        return clip;
    }

    static AudioClip Make(string name, float seconds, System.Func<float, float> wave)
    {
        const int rate = 44100;
        int n = Mathf.CeilToInt(rate * seconds);
        float[] data = new float[n];
        for (int i = 0; i < n; i++) data[i] = Mathf.Clamp(wave((float)i / rate), -1f, 1f);
        AudioClip clip = AudioClip.Create(name, n, 1, rate, false);
        clip.SetData(data, 0);
        return clip;
    }
}
