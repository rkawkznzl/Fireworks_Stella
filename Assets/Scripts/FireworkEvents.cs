// 불꽃 로켓(프리팹 루트 Particle System)에 붙인다.
// 로켓 입자가 수명을 다해 사라지는 순간 = Death Sub Emitter(폭발)가 터지는 순간이므로,
// 매 프레임 살아 있는 로켓 입자를 추적하다가 사라진 입자의 마지막 위치에서 "폭발" 이벤트를 보낸다.
// 입자 추적은 ParticleTracker가 담당한다.
//
// 받는 쪽 예시:
//   FireworkEvents.Exploded += e => Debug.Log(e.position);
// 교실 조명(FireworkRoomLight)이 이 이벤트를 사용한다. 폭발 효과음을 붙일 때도 같은 이벤트를 쓰면 된다.

using System;
using UnityEngine;

public struct FireworkExplosion
{
    public FireworkEvents source; // 어느 불꽃에서 났는지 (효과음 등에서 불꽃 종류를 구분할 때 사용)
    public Vector3 position;      // 월드 좌표
    public Color color;           // 교실을 비출 빛의 색
    public float strength;        // 빛 세기 배수 (불꽃 종류별로 다르게)
}

public enum FireworkLightTrigger
{
    RocketDeath, // 로켓 입자가 사라지는 순간 (하늘에서 터지는 불꽃: 국화, 링, 별 등)
    Emission,    // 입자가 새로 발사되는 순간 (지상 불꽃: 부채꼴, 상승 코멧 등)
}

[RequireComponent(typeof(ParticleSystem))]
public class FireworkEvents : MonoBehaviour
{
    /// <summary>모든 불꽃의 폭발을 한곳에서 받는 전역 이벤트</summary>
    public static event Action<FireworkExplosion> Exploded;

    /// <summary>이 불꽃 하나의 폭발만 받는 이벤트</summary>
    public event Action<FireworkExplosion> OnExploded;

    [Header("언제 빛을 낼지")]
    [Tooltip("RocketDeath: 하늘에서 터질 때 / Emission: 지상에서 발사될 때 (터지지 않는 불꽃용)")]
    [SerializeField] FireworkLightTrigger trigger = FireworkLightTrigger.RocketDeath;

    [Tooltip("Emission 모드에서 빛을 너무 자주 내지 않도록 두는 최소 간격 (초)")]
    [SerializeField, Min(0f)] float emissionMinInterval = 0.12f;

    [Header("교실을 비출 빛")]
    [Tooltip("켜면 폭발(Death Sub Emitter 중 입자가 가장 많은 것)의 Start Color에서 색을 자동으로 가져온다. 폭발이 없는 불꽃은 자기 Start Color를 쓴다")]
    [SerializeField] bool useBurstColor = true;

    [Tooltip("useBurstColor를 껐을 때 쓰는 색")]
    [SerializeField] Color lightColor = new Color(1f, 0.9f, 0.75f);

    [Tooltip("이 불꽃이 교실을 비추는 세기 배수 (큰 불꽃 1.5, 작은 불꽃 0.5 등)")]
    [SerializeField, Range(0f, 3f)] float lightStrength = 1f;

    [Header("디버그")]
    [SerializeField] bool logExplosions = false;

    /// <summary>Timeline 클립 등에서 실행 중에 곱하는 빛 세기 배수 (크기 배수와 함께 바뀜)</summary>
    [NonSerialized] public float lightStrengthMultiplier = 1f;

    ParticleSystem _ps;
    ParticleSystem _colorSource;
    ParticleTracker _tracker;
    float _lastEmissionLight = -999f;

    void Awake()
    {
        _ps = GetComponent<ParticleSystem>();
        _tracker = new ParticleTracker(_ps);

        // 빛 색은 본 폭발에서 가져오고, 폭발이 없는 불꽃(부채꼴, 코멧)은 자기 자신의 색을 쓴다
        FindBurstAndFlash(_ps, out int burst, out _);
        _colorSource = burst >= 0 ? _ps.subEmitters.GetSubEmitterSystem(burst) : _ps;
    }

    void OnDisable()
    {
        // 비활성화로 사라지는 입자는 폭발이 아니므로 그냥 잊는다
        _tracker?.Clear();
    }

    void LateUpdate()
    {
        _tracker.Update();

        if (trigger == FireworkLightTrigger.Emission)
        {
            // 이번 프레임에 새로 발사된 입자가 있으면 그 평균 위치에서 빛을 낸다
            int n = _tracker.NewIndices.Count;
            if (n > 0 && Time.time - _lastEmissionLight >= emissionMinInterval)
            {
                _lastEmissionLight = Time.time;
                bool local = _ps.main.simulationSpace == ParticleSystemSimulationSpace.Local;
                Vector3 sum = Vector3.zero;
                foreach (int i in _tracker.NewIndices)
                {
                    Vector3 p = _tracker.Buffer[i].position;
                    sum += local ? transform.TransformPoint(p) : p;
                }
                Raise(sum / n);
            }
            return;
        }

        // 지난 프레임 이후 사라진 로켓 입자 = 방금 터진 로켓
        foreach (Vector3 pos in _tracker.VanishedPositions)
        {
            Raise(pos);
        }
    }

    void Raise(Vector3 position)
    {
        FireworkExplosion e = new FireworkExplosion
        {
            source = this,
            position = position,
            color = PickColor(),
            strength = lightStrength * lightStrengthMultiplier,
        };

        if (logExplosions)
        {
            Debug.Log($"[FireworkEvents] {name} 폭발 @ {position} 색 {e.color}", this);
        }

        OnExploded?.Invoke(e);
        Exploded?.Invoke(e);
    }

    /// <summary>
    /// 로켓의 Death Sub Emitter 중 본 폭발과 섬광의 번호를 한 번에 찾는다 (없으면 -1).
    /// 본 폭발 = Burst 개수가 가장 많은 것 (예: Burst), 섬광 = 가장 적은 것 (예: Flash).
    /// Emit Probability가 0인 줄은 "꺼 둔 폭발"(지상 불꽃 Variant)로 보고 건너뛴다.
    /// </summary>
    public static void FindBurstAndFlash(ParticleSystem root, out int burst, out int flash)
    {
        burst = -1;
        flash = -1;
        if (root == null) return;
        ParticleSystem.SubEmittersModule sub = root.subEmitters;
        if (!sub.enabled) return;

        float most = float.MinValue, least = float.MaxValue;
        for (int i = 0; i < sub.subEmittersCount; i++)
        {
            if (sub.GetSubEmitterType(i) != ParticleSystemSubEmitterType.Death) continue;
            if (sub.GetSubEmitterEmitProbability(i) <= 0f) continue;
            ParticleSystem s = sub.GetSubEmitterSystem(i);
            if (s == null) continue;

            float total = 0f;
            ParticleSystem.EmissionModule em = s.emission;
            for (int b = 0; b < em.burstCount; b++)
            {
                total += em.GetBurst(b).count.constantMax;
            }
            if (total > most) { most = total; burst = i; }
            if (total < least) { least = total; flash = i; }
        }
        if (flash == burst) flash = -1; // 폭발이 하나뿐이면 섬광은 없음
    }

    Color PickColor()
    {
        if (!useBurstColor || _colorSource == null) return lightColor;

        ParticleSystem.MinMaxGradient c = _colorSource.main.startColor;
        float r = UnityEngine.Random.value;
        switch (c.mode)
        {
            case ParticleSystemGradientMode.Color:
                return c.color;
            case ParticleSystemGradientMode.TwoColors:
                return Color.Lerp(c.colorMin, c.colorMax, r);
            case ParticleSystemGradientMode.Gradient:
            case ParticleSystemGradientMode.RandomColor:
                return c.gradient.Evaluate(r);
            case ParticleSystemGradientMode.TwoGradients:
                return Color.Lerp(c.gradientMin.Evaluate(0.5f), c.gradientMax.Evaluate(0.5f), r);
            default:
                return lightColor;
        }
    }
}
