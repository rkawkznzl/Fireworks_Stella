// Timeline 커스텀 트랙: 곡 구간 표시용.
// 아무 동작도 하지 않고, 클립 이름(Intro, Chorus 등)을 타임라인 위에 글자로 보여 주는 용도.
// 바인딩이 없으므로 Director에 아무것도 연결하지 않아도 된다.

using UnityEngine.Timeline;

[TrackColor(0.62f, 0.55f, 0.95f)]
[TrackClipType(typeof(SectionClip))]
public class SectionTrack : TrackAsset
{
}
