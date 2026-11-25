// 교실을 비추는 "불꽃 빛". Light 컴포넌트가 있는 오브젝트에 붙인다.
// FireworkEvents.Exploded 를 받으면 그 불꽃 색으로 확 밝아졌다가 서서히 어두워진다.
// 여러 발이 겹치면 빛의 세기는 더해지고, 색은 밝기 비율로 섞인다.
//
// 권장 구성
//   1) Spot Light + 그림자 + followExplosion 켜기
//      → 폭발 방향의 창밖에서 교실 쪽으로 빛을 쏴서 창틀 그림자가 바닥·책상에 드리운다.
//   2) Point Light (창가 안쪽) + followExplosion 끄기, 약하게
//      → 벽과 천장까지 은은하게 퍼지는 반사광 느낌.

using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Light))]
public class FireworkRoomLight : MonoBehaviour
{
    [Header("밝기")]
    [Tooltip("세기 1짜리 불꽃이 터졌을 때의 최대 Light Intensity")]
    [SerializeField] float peakIntensity = 80f;

    [Tooltip("여러 발이 겹쳐도 이 값을 넘지 않는다")]
    [SerializeField] float maxIntensity = 200f;

    [Tooltip("빛이 최대로 올라가는 시간 (초)")]
    [SerializeField] float attack = 0.04f;

    [Tooltip("빛이 사라지는 빠르기. 클수록 오래 남는다 (초)")]
    [SerializeField] float decay = 0.9f;

    [Header("반짝임")]
    [Tooltip("터진 직후 빛이 파르르 떨리는 정도 (0이면 없음)")]
    [SerializeField, Range(0f, 1f)] float flickerAmount = 0.3f;
    [SerializeField] float flickerDuration = 0.5f;
    [SerializeField] float flickerSpeed = 22f;

    [Header("색")]
    [Tooltip("1이면 불꽃 색 그대로, 0이면 흰빛, 1보다 크면 색을 더 진하게")]
    [SerializeField, Range(0f, 2f)] float colorSaturation = 1f;

    [Header("거리 감쇠")]
    [Tooltip("이 거리에서 터진 불꽃이 세기 1로 비춘다")]
    [SerializeField] float referenceDistance = 150f;
    [SerializeField] Vector2 distanceFactorRange = new Vector2(0.4f, 1.6f);

    [Header("위치 (Spot / Directional 용)")]
    [Tooltip("켜면 폭발 방향의 창밖으로 빛을 옮기고 교실 중심을 비추게 한다")]
    [SerializeField] bool followExplosion = true;

    [Tooltip("교실 중심 (비워 두면 아래 좌표 사용)")]
    [SerializeField] Transform roomCenter;
    [SerializeField] Vector3 roomCenterPosition = new Vector3(0f, 1.2f, 0f);

    [Tooltip("교실 중심에서 폭발 방향으로 이만큼 떨어진 곳에 빛을 둔다 (창밖이 되도록)")]
    [SerializeField] float placeDistance = 9f;

    [Tooltip("방향이 바뀔 때 따라가는 빠르기")]
    [SerializeField] float followSmoothing = 10f;

    struct Pulse
    {
        public float startTime;
        public float peak;
        public Color color;
        public Vector3 position;
        public float seed;
    }

    readonly List<Pulse> _pulses = new List<Pulse>();
    Light _light;
    Vector3 _targetPos;
    bool _hasTarget;

    const int MaxPulses = 24;

    Vector3 Center => roomCenter != null ? roomCenter.position : roomCenterPosition;

    void Awake()
    {
        _light = GetComponent<Light>();
        _light.intensity = 0f;
        _light.enabled = false;
    }

    void OnEnable()  { FireworkEvents.Exploded += HandleExplosion; }
    void OnDisable() { FireworkEvents.Exploded -= HandleExplosion; }

    void HandleExplosion(FireworkExplosion e)
    {
        float dist = Mathf.Max(1f, Vector3.Distance(Center, e.position));
        float distFactor = Mathf.Clamp(referenceDistance / dist, distanceFactorRange.x, distanceFactorRange.y);

        Color c = Color.LerpUnclamped(Color.white, e.color, colorSaturation);
        c.r = Mathf.Max(0f, c.r);
        c.g = Mathf.Max(0f, c.g);
        c.b = Mathf.Max(0f, c.b);

        if (_pulses.Count >= MaxPulses) _pulses.RemoveAt(0);
        _pulses.Add(new Pulse
        {
            startTime = Time.time,
            peak = peakIntensity * e.strength * distFactor,
            color = c,
            position = e.position,
            seed = Random.value * 100f,
        });

        if (followExplosion)
        {
            Vector3 dir = (e.position - Center).normalized;
            _targetPos = Center + dir * placeDistance;
            if (!_hasTarget)
            {
                transform.position = _targetPos; // 첫 폭발은 바로 그 자리로
                transform.rotation = Quaternion.LookRotation(Center - _targetPos);
                _hasTarget = true;
            }
        }
    }

    void Update()
    {
        float now = Time.time;
        float total = 0f;
        Vector3 colorSum = Vector3.zero;

        for (int i = _pulses.Count - 1; i >= 0; i--)
        {
            Pulse p = _pulses[i];
            float t = now - p.startTime;

            float env = t < attack ? t / attack : Mathf.Exp(-(t - attack) / decay);
            if (t > attack && env < 0.003f)
            {
                _pulses.RemoveAt(i);
                continue;
            }

            if (t < flickerDuration && flickerAmount > 0f)
            {
                float fade = 1f - t / flickerDuration;
                float n = Mathf.PerlinNoise(p.seed, now * flickerSpeed) * 2f - 1f;
                env *= 1f + n * flickerAmount * fade;
            }

            float v = p.peak * Mathf.Max(0f, env);
            total += v;
            colorSum += new Vector3(p.color.r, p.color.g, p.color.b) * v;
        }

        if (total <= 0.0001f)
        {
            _light.enabled = false;
            return;
        }

        _light.enabled = true;
        _light.intensity = Mathf.Min(total, maxIntensity);
        Vector3 avg = colorSum / total;
        _light.color = new Color(avg.x, avg.y, avg.z);

        if (followExplosion && _hasTarget)
        {
            float k = 1f - Mathf.Exp(-followSmoothing * Time.deltaTime);
            transform.position = Vector3.Lerp(transform.position, _targetPos, k);
            Vector3 look = Center - transform.position;
            if (look.sqrMagnitude > 0.0001f)
            {
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(look), k);
            }
        }
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.85f, 0.6f, 0.8f);
        Gizmos.DrawWireSphere(Center, 0.3f);
    }
}
