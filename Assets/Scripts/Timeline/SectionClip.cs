// Section 트랙의 클립. 클립의 Display Name(Inspector 맨 위 이름)이 타임라인에 그대로 보인다.
// 메모 칸에 그 구간의 연출 의도를 적어 두면 README 작성 때 그대로 옮길 수 있다.

using System;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

[Serializable]
public class SectionClip : PlayableAsset, ITimelineClipAsset
{
    [TextArea(2, 6)]
    [Tooltip("이 구간의 연출 메모 (동작에는 영향 없음)")]
    public string note;

    public ClipCaps clipCaps => ClipCaps.None;

    public override double duration => 10.0;

    public override Playable CreatePlayable(PlayableGraph graph, GameObject owner)
    {
        return Playable.Create(graph);
    }
}
