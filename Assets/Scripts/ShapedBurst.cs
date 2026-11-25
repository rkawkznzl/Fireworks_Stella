// 폭발(Burst) 파티클이 태어나는 순간 속도를 다시 배정해서 "모양 있는 불꽃"을 만든다.
// - Star: 카메라를 정면으로 향한 별 윤곽 (꼭짓점 수, 안쪽 반지름 비율 조절)
// - Ring: 카메라를 정면으로 향한 완벽한 원
//
// 원리: 별 윤곽선을 따라 입자를 같은 간격으로 놓고, 중심에서 그 점까지의 벡터를 속도로 준다.
//       모든 입자가 같은 시간 동안 같은 비율로 감속(Drag)하므로 퍼지는 동안 모양이 그대로 커진다.
//
// 사용법: 불꽃 프리팹의 Burst(Death Sub Emitter) 오브젝트에 붙인다.
//         Burst의 Start Speed는 무시되고 아래 Speed 값이 쓰인다. Gravity는 작게(0~0.15) 해야 모양이 덜 무너진다.

using System.Collections.Generic;
using UnityEngine;

public enum BurstShape
{
    Star,
    Ring,
    Fan,   // 부채꼴: 오브젝트가 향한 방향(발사 방향)을 가운데로 Fan Angle만큼 펼침. 지상 연발용
    StarOrbit, // 별 + 별을 감싸는 초승달 궤도(호). 입자의 일부(Orbit Ratio)가 궤도를 그린다
}

[RequireComponent(typeof(ParticleSystem))]
public class ShapedBurst : MonoBehaviour
{
    [SerializeField] BurstShape shape = BurstShape.Star;

    [Tooltip("별 꼭짓점 수")]
    [SerializeField, Range(3, 8)] int points = 5;

    [Tooltip("안쪽 꼭짓점 반지름 / 바깥 꼭짓점 반지름. 작을수록 뾰족한 별")]
    [SerializeField, Range(0.2f, 0.9f)] float innerRatio = 0.42f;

    [Header("StarOrbit (별 + 궤도)")]
    [Tooltip("입자 중 궤도(호)를 그리는 비율")]
    [SerializeField, Range(0.1f, 0.6f)] float orbitRatio = 0.35f;

    [Tooltip("궤도 반지름 (별 바깥 꼭짓점 = 1 기준)")]
    [SerializeField, Range(1.1f, 2.5f)] float orbitRadius = 1.7f;

    [Tooltip("궤도 원의 중심 (별 중심 기준, x = 오른쪽, y = 위)")]
    [SerializeField] Vector2 orbitCenter = new Vector2(-0.25f, 0.1f);

    [Tooltip("궤도 시작 각도 (0 = 오른쪽, 90 = 위, 180 = 왼쪽, 270 = 아래)")]
    [SerializeField] float orbitStartDeg = 25f;

    [Tooltip("궤도 끝 각도. 시작에서 반시계 방향으로 여기까지 그린다")]
    [SerializeField] float orbitEndDeg = 262f;

    [Tooltip("궤도 가운데 부분의 두께 (양 끝으로 갈수록 가늘어짐)")]
    [SerializeField, Range(0f, 0.4f)] float orbitThickness = 0.16f;

    [Header("Fan")]
    [Tooltip("Fan 모드: 부채가 펼쳐지는 전체 각도")]
    [SerializeField, Range(10f, 180f)] float fanAngle = 100f;

    [Tooltip("Fan 모드: 줄기마다 각도를 ±이만큼 흔든다 (0이면 자로 잰 듯 일정)")]
    [SerializeField, Range(0f, 20f)] float fanAngleJitter = 0f;

    [Tooltip("바깥 꼭짓점 방향으로 퍼지는 속도")]
    [SerializeField] float speed = 22f;

    [Tooltip("입자마다 속도를 ±이 비율만큼 흔든다 (0.2 = ±20%). Fan에서 줄기 높이가 제각각이 되어 자연스러움")]
    [SerializeField, Range(0f, 0.5f)] float speedJitter = 0f;

    [Tooltip("별을 화면에서 기울이는 각도. 0이면 꼭짓점 하나가 정확히 위를 향한다")]
    [SerializeField] float rotation = 0f;

    [Tooltip("발마다 기울기를 ±이 각도 안에서 무작위로 바꾼다")]
    [SerializeField, Range(0f, 45f)] float rotationJitter = 12f;

    [Tooltip("윤곽에서 살짝 벗어나게 흔드는 정도 (0이면 칼같이 정확한 모양)")]
    [SerializeField, Range(0f, 0.2f)] float wobble = 0.03f;

    [Tooltip("별 안쪽을 채우는 입자 비율 (0이면 윤곽만). 채우면 더 화려하지만 모양은 덜 또렷하다")]
    [SerializeField, Range(0f, 0.5f)] float fillRatio = 0f;

    ParticleSystem _ps;
    ParticleTracker _tracker;
    float _baseSpeedMultiplier = 1f;

    void Awake()
    {
        _ps = GetComponent<ParticleSystem>();
        _tracker = new ParticleTracker(_ps);
        // Timeline 클립의 Size Scale은 Start Speed 배수를 바꾼다. 그 비율을 별 크기에도 반영하기 위해 원래 값을 기억.
        _baseSpeedMultiplier = Mathf.Max(0.0001f, _ps.main.startSpeedMultiplier);
    }

    void OnDisable()
    {
        _tracker?.Clear();
    }

    float EffectiveSpeed => speed * (_ps.main.startSpeedMultiplier / _baseSpeedMultiplier);

    void LateUpdate()
    {
        _tracker.Update();
        if (_tracker.NewIndices.Count == 0) return;

        ShapeNewParticles(_tracker.Buffer, _tracker.NewIndices);
        _tracker.Apply();
    }

    void ShapeNewParticles(ParticleSystem.Particle[] buffer, List<int> newIndices)
    {
        int n = newIndices.Count;

        // 새로 태어난 입자들의 중심 = 폭발 위치
        Vector3 center = Vector3.zero;
        foreach (int i in newIndices) center += buffer[i].position;
        center /= n;

        bool local = _ps.main.simulationSpace == ParticleSystemSimulationSpace.Local;
        Vector3 centerWorld = local ? transform.TransformPoint(center) : center;

        // 카메라를 정면으로 보는 평면
        Transform cam = Camera.main != null ? Camera.main.transform : null;
        Vector3 viewDir = cam != null ? (centerWorld - cam.position).normalized : Vector3.forward;
        Vector3 right = Vector3.Cross(Vector3.up, viewDir).normalized;
        if (right.sqrMagnitude < 0.001f) right = Vector3.right;
        Vector3 up = Vector3.Cross(viewDir, right).normalized;

        float rot = (rotation + Random.Range(-rotationJitter, rotationJitter)) * Mathf.Deg2Rad;

        if (shape == BurstShape.Fan)
        {
            // 발사 방향(오브젝트 앞, 로켓 프리팹은 위쪽)을 카메라 평면에 투영해 부채의 가운데로 쓴다.
            // Timeline 클립의 Tilt로 오브젝트를 기울이면 부채도 함께 기운다.
            Vector3 fwd = transform.forward;
            Vector3 fanUp = (fwd - Vector3.Dot(fwd, viewDir) * viewDir).normalized;
            if (fanUp.sqrMagnitude < 0.001f) fanUp = up;
            Vector3 fanRight = Vector3.Cross(fanUp, viewDir).normalized;
            if (Vector3.Dot(fanRight, right) < 0f) fanRight = -fanRight;
            right = fanRight;
            up = fanUp;
        }

        int orbitCount = shape == BurstShape.StarOrbit ? Mathf.RoundToInt(n * orbitRatio) : 0;
        int fillCount = Mathf.RoundToInt((n - orbitCount) * fillRatio);
        int outlineCount = n - orbitCount - fillCount;

        for (int k = 0; k < n; k++)
        {
            Vector2 p;
            if (shape == BurstShape.Fan)
            {
                // 왼쪽 끝 → 오른쪽 끝까지 같은 각도 간격 (한 줄기씩 또렷한 부채)
                float t = n <= 1 ? 0.5f : (float)k / (n - 1);
                float deg = Mathf.Lerp(-fanAngle * 0.5f, fanAngle * 0.5f, t) + Random.Range(-fanAngleJitter, fanAngleJitter);
                float a = deg * Mathf.Deg2Rad;
                p = new Vector2(Mathf.Sin(a), Mathf.Cos(a));
            }
            else if (k < outlineCount)
            {
                float t = (k + 0.5f) / outlineCount; // 윤곽을 따라 같은 간격
                p = shape == BurstShape.Ring ? CirclePoint(t) : StarOutlinePoint(t);
            }
            else if (k < outlineCount + orbitCount)
            {
                float t = (k - outlineCount + 0.5f) / orbitCount;
                p = OrbitPoint(t);
            }
            else
            {
                float t = Random.value;
                p = (shape == BurstShape.Ring ? CirclePoint(t) : StarOutlinePoint(t)) * Random.Range(0.25f, 0.85f);
            }

            if (wobble > 0f) p *= 1f + Random.Range(-wobble, wobble);
            p = Rotate(p, rot);

            float sp = EffectiveSpeed;
            if (speedJitter > 0f) sp *= 1f + Random.Range(-speedJitter, speedJitter);
            Vector3 v = (right * p.x + up * p.y) * sp;
            int idx = newIndices[k];

            // center는 Buffer 좌표계(Local이면 로컬) 그대로이므로 위치는 변환하지 않는다
            buffer[idx].position = center;
            buffer[idx].velocity = local ? transform.InverseTransformDirection(v) : v;
        }
    }

    // t(0~1)를 별 윤곽선 위의 점으로. 바깥 꼭짓점 하나가 위(+Y)를 향하고 반지름 1.
    Vector2 StarOutlinePoint(float t)
    {
        int vertexCount = points * 2;
        float edgeT = t * vertexCount;
        int e = Mathf.FloorToInt(edgeT) % vertexCount;
        float f = edgeT - Mathf.Floor(edgeT);

        Vector2 a = StarVertex(e);
        Vector2 b = StarVertex((e + 1) % vertexCount);
        return Vector2.Lerp(a, b, f);
    }

    Vector2 StarVertex(int i)
    {
        float angle = Mathf.PI * 0.5f + i * Mathf.PI / points; // 위에서 시작
        float r = (i % 2 == 0) ? 1f : innerRatio;
        return new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * r;
    }

    // 초승달 궤도: 시작 각도 → 끝 각도로 호를 그리고, 가운데가 두껍고 양 끝이 뾰족하게
    Vector2 OrbitPoint(float t)
    {
        float deg = Mathf.Lerp(orbitStartDeg, orbitEndDeg, t);
        float a = deg * Mathf.Deg2Rad;
        Vector2 dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
        float thick = orbitThickness * Mathf.Sin(Mathf.PI * t); // 양 끝 0, 가운데 최대
        float r = orbitRadius + Random.Range(-thick, thick) * 0.5f;
        return orbitCenter + dir * r;
    }

    static Vector2 CirclePoint(float t)
    {
        float a = t * Mathf.PI * 2f;
        return new Vector2(Mathf.Cos(a), Mathf.Sin(a));
    }

    static Vector2 Rotate(Vector2 v, float rad)
    {
        float c = Mathf.Cos(rad), s = Mathf.Sin(rad);
        return new Vector2(v.x * c - v.y * s, v.x * s + v.y * c);
    }
}
