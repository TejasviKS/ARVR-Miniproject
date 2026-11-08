using System;
using UnityEngine;

// VERSION 1
// Hit points for anything that can be fought (the player, the professors).
// "Min Fraction" is a floor: while it is above 0 the owner cannot drop below that share of its health and cannot die.
// The combat tutorial uses it so the professor survives until every lesson has been shown.
public class Health : MonoBehaviour
{
    public float max = 100f;
    [Range(0f, 1f)] public float minFraction = 0f;
    public bool invulnerable;
    [Tooltip("Hit points at the start and after Restore(). 0 = full (Max)")]
    public float startAt = 0f;

    public float Current { get; private set; }
    public float Fraction => max > 0f ? Current / max : 0f;
    public bool Dead { get; private set; }

    public event Action<float> Damaged;     // how much was really taken
    public event Action Died;
    public event Action Changed;

    void Awake() { Current = StartValue; }
    float StartValue => startAt > 0f ? Mathf.Min(startAt, max) : max;

    // set the maximum and the starting value (the player: 75 max, starts at 74)
    public void Configure(float newMax, float start) { max = newMax; startAt = start; Current = StartValue; Changed?.Invoke(); }

    // Returns the damage that was really dealt (0 if invulnerable, dead, or already at the floor)
    public float Damage(float amount)
    {
        if (invulnerable || Dead || amount <= 0f) return 0f;
        float before = Current;
        Current = Mathf.Max(max * minFraction, Current - amount);
        float dealt = before - Current;
        if (dealt > 0f) Damaged?.Invoke(dealt);
        Changed?.Invoke();
        if (Current <= 0f)
        {
            Dead = true;
            Died?.Invoke();
        }
        return dealt;
    }

    // Jump to an exact value (the boss's fake-out heals him from 0 to 20%). Also brings a "dead" owner back.
    public void SetCurrent(float value)
    {
        Current = Mathf.Clamp(value, 0f, max);
        Dead = Current <= 0f && Dead;
        if (Current > 0f) Dead = false;
        Changed?.Invoke();
    }

    public void Restore()
    {
        Current = StartValue;
        Dead = false;
        Changed?.Invoke();
    }
}
