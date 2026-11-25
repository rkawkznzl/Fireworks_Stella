// Dawn 트랙의 클립 = "밤에서 새벽으로 바뀌는 구간".
// 클립 시작에서 끝까지 진행도(0 → 1)에 따라 하늘, 달빛, 교실 기본광을 새벽 값으로 옮긴다.
// 실제 적용은 DawnTrack의 믹서(DawnMixerBehaviour)가 Play 중에만 한다.

using System;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

[Serializable]
public class DawnClip : PlayableAsset, ITimelineClipAsset
{
    [Tooltip("진행 곡선. 가로 = 클립 안의 시간(0~1), 세로 = 새벽 정도(0~1)")]
    public AnimationCurve curve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("하늘 (StellaSky 셰이더)")]
    [Tooltip("하늘 머티리얼의 Dawn 값 최종값 (1 = 완전한 새벽)")]
    [Range(0f, 1f)] public float skyDawn = 1f;

    [Header("달빛 → 새벽 햇빛 (트랙에 연결한 Light)")]
    public Color lightColor = new Color(1f, 0.76f, 0.84f);
    public float lightIntensity = 1.1f;
    [Tooltip("새벽 햇빛 방향. 창문(왼쪽) 쪽 낮은 곳에서 비스듬히 들어오도록")]
    public Vector3 lightEuler = new Vector3(8f, 72f, 0f);

    [Header("교실 기본광 (Environment Lighting Color)")]
    public Color ambient = new Color(0.36f, 0.28f, 0.44f);

    public ClipCaps clipCaps => ClipCaps.None;

    public override double duration => 4.0;

    public override Playable CreatePlayable(PlayableGraph graph, GameObject owner)
    {
        // 적용은 믹서가 처리하므로 클립 자체는 빈 Playable
        return Playable.Create(graph);
    }
}
