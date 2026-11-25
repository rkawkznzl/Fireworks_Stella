// Particle System의 입자를 프레임마다 추적하는 공용 도우미.
// 입자마다 고유한 randomSeed로 "이번 프레임에 새로 태어난 입자"와 "지난 프레임 이후 사라진 입자"를 알려 준다.
//
// 사용하는 곳
//   FireworkEvents : 사라진 로켓 입자 = 폭발 순간 / 새로 생긴 입자 = 지상 불꽃 발사 순간
//   ShapedBurst    : 새로 태어난 폭발 입자에 별·부채꼴 모양 속도를 배정

using System.Collections.Generic;
using UnityEngine;

public class ParticleTracker
{
    readonly ParticleSystem _ps;
    ParticleSystem.Particle[] _buffer;

    // randomSeed → 마지막으로 본 월드 위치
    readonly Dictionary<uint, Vector3> _alive = new Dictionary<uint, Vector3>();
    readonly HashSet<uint> _seen = new HashSet<uint>();
    readonly List<uint> _gone = new List<uint>();

    /// <summary>GetParticles로 받은 입자 배열 (Count개까지 유효). 수정 후 Apply()로 되돌려 쓴다</summary>
    public ParticleSystem.Particle[] Buffer => _buffer;
    public int Count { get; private set; }

    /// <summary>이번 프레임에 새로 태어난 입자의 Buffer 번호</summary>
    public readonly List<int> NewIndices = new List<int>();

    /// <summary>지난 프레임 이후 사라진 입자들의 마지막 월드 위치</summary>
    public readonly List<Vector3> VanishedPositions = new List<Vector3>();

    public ParticleTracker(ParticleSystem ps)
    {
        _ps = ps;
    }

    /// <summary>LateUpdate에서 한 번 호출한다</summary>
    public void Update()
    {
        int max = _ps.main.maxParticles;
        if (_buffer == null || _buffer.Length < max) _buffer = new ParticleSystem.Particle[max];

        bool local = _ps.main.simulationSpace == ParticleSystemSimulationSpace.Local;
        Transform t = _ps.transform;
        Count = _ps.GetParticles(_buffer);

        NewIndices.Clear();
        VanishedPositions.Clear();
        _seen.Clear();

        for (int i = 0; i < Count; i++)
        {
            uint id = _buffer[i].randomSeed;
            Vector3 pos = local ? t.TransformPoint(_buffer[i].position) : _buffer[i].position;
            if (!_alive.ContainsKey(id)) NewIndices.Add(i);
            _alive[id] = pos;
            _seen.Add(id);
        }

        if (_alive.Count == _seen.Count) return;

        _gone.Clear();
        foreach (KeyValuePair<uint, Vector3> kv in _alive)
        {
            if (!_seen.Contains(kv.Key)) _gone.Add(kv.Key);
        }
        foreach (uint id in _gone)
        {
            VanishedPositions.Add(_alive[id]);
            _alive.Remove(id);
        }
    }

    /// <summary>Buffer를 수정했으면 호출해서 Particle System에 반영한다</summary>
    public void Apply()
    {
        _ps.SetParticles(_buffer, Count);
    }

    /// <summary>추적 기록을 지운다 (비활성화될 때 등)</summary>
    public void Clear()
    {
        _alive.Clear();
        NewIndices.Clear();
        VanishedPositions.Clear();
    }
}
