// Timeline 커스텀 트랙: 불꽃 발사.
// 트랙 바인딩에는 Fireworks 같은 Transform을 연결한다 (생성된 불꽃의 부모 + 기본 발사 위치).
//
// 발사 시각 계산
//   발사 시각 = 클립 시작 + k × 간격  (k = 0 .. count-1)
//   alignToExplosion 이 켜져 있으면 flightTime 만큼 앞당겨 쏜다 → 클립 시작 순간에 하늘에서 터진다.
// Play 중에만 실제로 생성하고, Timeline을 앞뒤로 건너뛰면(스크럽) 그 사이 발사는 건너뛴다.

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

[TrackColor(1f, 0.78f, 0.45f)]
[TrackClipType(typeof(FireworkClip))]
[TrackBindingType(typeof(Transform))]
public class FireworkTrack : TrackAsset
{
    public override Playable CreateTrackMixer(PlayableGraph graph, GameObject go, int inputCount)
    {
        ScriptPlayable<FireworkMixerBehaviour> mixer = ScriptPlayable<FireworkMixerBehaviour>.Create(graph, inputCount);
        FireworkMixerBehaviour b = mixer.GetBehaviour();
        b.clips = new List<TimelineClip>(GetClips());
        return mixer;
    }
}

public class FireworkMixerBehaviour : PlayableBehaviour
{
    public List<TimelineClip> clips;

    // 이름 → 발사 지점 (찾지 못한 이름도 null로 기억해서 경고를 한 번만 낸다)
    readonly Dictionary<string, Transform> _pointCache = new Dictionary<string, Transform>();

    // 프리팹 → (본 폭발, 섬광) Sub Emitter 번호. 같은 프리팹은 구조가 같으므로 한 번만 찾는다
    readonly Dictionary<GameObject, Vector2Int> _subEmitterCache = new Dictionary<GameObject, Vector2Int>();

    double _lastTime;
    bool _started;

    // 이보다 크게 앞으로 건너뛰면(스크럽) 그 사이 발사는 생략한다 (초).
    // 녹화나 로딩으로 프레임이 잠깐 길어지는 정도는 밀린 발사를 모두 쏜다.
    const double JumpThreshold = 2.0;

    public override void OnGraphStart(Playable playable)
    {
        _started = false;
    }

    public override void ProcessFrame(Playable playable, FrameData info, object playerData)
    {
        if (!Application.isPlaying || clips == null) return;

        double now = playable.GetTime();

        if (!_started)
        {
            // 첫 프레임: 직전 한 프레임 구간부터 검사해서 0초에 놓인 발사도 놓치지 않게 한다
            _lastTime = now - Mathf.Max(info.deltaTime, 0.0001f);
            _started = true;
        }
        else if (now < _lastTime || now - _lastTime > JumpThreshold)
        {
            _lastTime = now; // 뒤로 가거나 크게 건너뛴 구간은 발사하지 않음
            return;
        }
        // 그 밖에는 프레임이 길어졌더라도 (_lastTime, now] 사이의 발사를 모두 처리한다

        Transform root = playerData as Transform;

        foreach (TimelineClip clip in clips)
        {
            FireworkClip asset = clip.asset as FireworkClip;
            if (asset == null || asset.prefab == null) continue;

            double lead = asset.alignToExplosion ? asset.flightTime : 0.0;
            for (int k = 0; k < asset.count; k++)
            {
                double fireTime = clip.start + k * asset.interval - lead;
                if (asset.intervalJitter > 0f && k > 0)
                {
                    // 같은 클립·같은 k면 항상 같은 흔들림 (재생할 때마다 같은 연출)
                    float h = Hash01(clip.start, k);
                    fireTime += (h * 2f - 1f) * asset.intervalJitter;
                }

                if (fireTime > _lastTime && fireTime <= now)
                {
                    Fire(asset, root, k);
                }
            }
        }

        _lastTime = now;
    }

    void Fire(FireworkClip asset, Transform root, int k)
    {
        Transform lp = FindPoint(root, asset.launchPointName);
        Transform basis = lp != null ? lp : root;

        Vector3 basePos = basis != null ? basis.position : Vector3.zero;
        Vector3 local = asset.offset + SpreadOffset(asset, k);
        Vector3 worldOffset = basis != null ? basis.TransformDirection(local) : local;

        Quaternion rot = asset.prefab.transform.rotation;
        float tilt = TiltFor(asset, k);
        if (!Mathf.Approximately(tilt, 0f))
        {
            // 카메라 쪽에서 볼 때 좌우로 기울도록, 발사 지점의 앞(카메라 시선) 축을 기준으로 회전
            Vector3 axis = basis != null ? basis.forward : Vector3.forward;
            rot = Quaternion.AngleAxis(-tilt, axis) * rot;
        }

        GameObject go = Object.Instantiate(asset.prefab, basePos + worldOffset, rot, root);

        ParticleSystem ps = go.GetComponent<ParticleSystem>();
        if (ps != null)
        {
            ParticleSystem.MainModule main = ps.main;
            main.loop = false; // 프리팹이 테스트용으로 Looping이어도 한 발만
            main.startSpeedMultiplier *= asset.heightScale;

            ApplyLook(asset, ps, go, k, SubEmittersOf(asset.prefab, ps));
            ps.Play(true);
        }

        Object.Destroy(go, asset.cleanupDelay);
    }

    static float TiltFor(FireworkClip a, int k)
    {
        switch (a.tiltMode)
        {
            case FireworkTiltMode.Alternate:
                return (k % 2 == 0) ? a.tilt : -a.tilt;
            case FireworkTiltMode.Sweep:
                if (a.count <= 1) return 0f;
                return Mathf.Lerp(-a.tilt, a.tilt, (float)k / (a.count - 1));
            default:
                return a.tilt;
        }
    }

    Vector2Int SubEmittersOf(GameObject prefab, ParticleSystem rocket)
    {
        if (!_subEmitterCache.TryGetValue(prefab, out Vector2Int idx))
        {
            FireworkEvents.FindBurstAndFlash(rocket, out int burst, out int flash);
            idx = new Vector2Int(burst, flash);
            _subEmitterCache[prefab] = idx;
        }
        return idx;
    }

    // 클립의 팔레트·크기 설정을 생성된 불꽃에 적용한다. idx = (본 폭발, 섬광) Sub Emitter 번호, 없으면 -1
    static void ApplyLook(FireworkClip asset, ParticleSystem rocket, GameObject go, int k, Vector2Int idx)
    {
        ParticleSystem.SubEmittersModule sub = rocket.subEmitters;
        ParticleSystem burst = idx.x >= 0 ? sub.GetSubEmitterSystem(idx.x) : null;
        ParticleSystem flash = idx.y >= 0 && idx.y != idx.x ? sub.GetSubEmitterSystem(idx.y) : null;

        // 폭발이 없는 지상 불꽃(부채꼴, 코멧)은 자기 자신에 색과 크기를 적용
        bool groundType = burst == null;
        if (groundType) burst = rocket;

        // 색
        if (burst != null && FireworkPalettes.TryGet(asset.palette, k, out Color a, out Color b))
        {
            ParticleSystem.MainModule bm = burst.main;
            bm.startColor = new ParticleSystem.MinMaxGradient(a, b);
        }

        // 크기
        float s = asset.sizeScale;
        if (asset.sizeJitter > 0f) s *= 1f + Random.Range(-asset.sizeJitter, asset.sizeJitter);
        if (Mathf.Approximately(s, 1f)) return;

        if (burst != null)
        {
            ParticleSystem.MainModule bm = burst.main;
            bm.startSpeedMultiplier *= s;                     // 퍼지는 반지름
            bm.startSizeMultiplier *= Mathf.Lerp(1f, s, 0.5f); // 알갱이는 절반만큼만 키운다
        }
        if (flash != null)
        {
            ParticleSystem.MainModule fm = flash.main;
            fm.startSizeMultiplier *= s;
        }

        FireworkEvents ev = go.GetComponent<FireworkEvents>();
        if (ev != null) ev.lightStrengthMultiplier = s;
    }

    Transform FindPoint(Transform root, string pointName)
    {
        if (root == null || string.IsNullOrEmpty(pointName)) return null;
        if (_pointCache.TryGetValue(pointName, out Transform cached)) return cached; // 못 찾은 이름은 null로 기억됨

        Transform found = FindRecursive(root, pointName);
        if (found == null)
        {
            Debug.LogWarning($"[FireworkTrack] '{root.name}' 아래에서 발사 지점 '{pointName}'을 찾지 못해 {root.name} 위치에서 발사합니다.");
        }
        _pointCache[pointName] = found;
        return found;
    }

    static Transform FindRecursive(Transform t, string pointName)
    {
        if (t.name == pointName) return t;
        foreach (Transform child in t)
        {
            Transform r = FindRecursive(child, pointName);
            if (r != null) return r;
        }
        return null;
    }

    static Vector3 SpreadOffset(FireworkClip a, int k)
    {
        Vector3 s = a.spread;
        Vector3 rnd = new Vector3(Random.Range(-s.x, s.x), Random.Range(-s.y, s.y), Random.Range(-s.z, s.z));
        if (a.count <= 1 || a.spreadMode == FireworkSpreadMode.Random) return rnd;

        float t = (float)k / (a.count - 1); // 0 → 1
        float x;
        switch (a.spreadMode)
        {
            case FireworkSpreadMode.SweepLeftToRight:
                x = Mathf.Lerp(-s.x, s.x, t);
                break;
            case FireworkSpreadMode.SweepRightToLeft:
                x = Mathf.Lerp(s.x, -s.x, t);
                break;
            default: // CenterOut: 0, +1, -1, +2, -2 ...
                int step = (k + 1) / 2;
                int maxStep = Mathf.Max(1, a.count / 2);
                float side = (k % 2 == 1) ? 1f : -1f;
                x = k == 0 ? 0f : side * s.x * step / maxStep;
                break;
        }
        // 좌우는 순서대로, 높이·앞뒤는 약간만 흔든다
        return new Vector3(x, rnd.y * 0.3f, rnd.z * 0.3f);
    }

    static float Hash01(double t, int k)
    {
        float x = Mathf.Sin((float)(t * 12.9898 + k * 78.233)) * 43758.5453f;
        return x - Mathf.Floor(x);
    }
}
