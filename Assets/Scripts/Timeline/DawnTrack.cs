// Timeline 커스텀 트랙: 새벽 전환.
// 트랙 바인딩에는 달빛(Moonlight) Light를 연결한다.
//
// - 하늘: Play를 시작하면 하늘 머티리얼의 복사본을 만들어 RenderSettings.skybox로 쓰고, 복사본의 _Dawn 값만 바꾼다.
//         원본 머티리얼 파일(M_StellaSky)은 바뀌지 않는다.
// - 달빛, 교실 기본광: 원래 값을 기억해 두었다가 Play가 끝나면 되돌린다.
// - 클립이 끝난 뒤에도 새벽 상태를 유지한다 (Director의 Wrap Mode를 Hold로 두면 곡이 끝난 뒤에도 그대로 남는다).

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

[TrackColor(1f, 0.62f, 0.78f)]
[TrackClipType(typeof(DawnClip))]
[TrackBindingType(typeof(Light))]
public class DawnTrack : TrackAsset
{
    public override Playable CreateTrackMixer(PlayableGraph graph, GameObject go, int inputCount)
    {
        ScriptPlayable<DawnMixerBehaviour> mixer = ScriptPlayable<DawnMixerBehaviour>.Create(graph, inputCount);
        mixer.GetBehaviour().clips = new List<TimelineClip>(GetClips());
        return mixer;
    }
}

public class DawnMixerBehaviour : PlayableBehaviour
{
    public List<TimelineClip> clips;

    static readonly int DawnId = Shader.PropertyToID("_Dawn");

    bool _captured;
    Light _light;
    Color _origLightColor;
    float _origLightIntensity;
    Quaternion _origLightRotation;
    Color _origAmbient;
    Material _origSky;
    Material _skyInstance;

    public override void ProcessFrame(Playable playable, FrameData info, object playerData)
    {
        if (!Application.isPlaying || clips == null || clips.Count == 0) return;

        Light light = playerData as Light;
        if (!_captured) Capture(light);

        // 첫 번째 Dawn 클립 기준 진행도. 클립 전 = 0, 클립 뒤 = 1 (새벽 유지)
        TimelineClip clip = clips[0];
        DawnClip d = clip.asset as DawnClip;
        if (d == null) return;

        double now = playable.GetTime();
        float t = clip.duration > 0 ? Mathf.Clamp01((float)((now - clip.start) / clip.duration)) : 1f;
        float p = Mathf.Clamp01(d.curve.Evaluate(t));

        if (_skyInstance != null && _skyInstance.HasProperty(DawnId))
        {
            _skyInstance.SetFloat(DawnId, Mathf.Lerp(0f, d.skyDawn, p));
        }

        if (_light != null)
        {
            _light.color = Color.Lerp(_origLightColor, d.lightColor, p);
            _light.intensity = Mathf.Lerp(_origLightIntensity, d.lightIntensity, p);
            _light.transform.rotation = Quaternion.Slerp(_origLightRotation, Quaternion.Euler(d.lightEuler), p);
        }

        RenderSettings.ambientLight = Color.Lerp(_origAmbient, d.ambient, p);
    }

    void Capture(Light light)
    {
        _captured = true;
        _light = light;
        if (_light != null)
        {
            _origLightColor = _light.color;
            _origLightIntensity = _light.intensity;
            _origLightRotation = _light.transform.rotation;
        }
        _origAmbient = RenderSettings.ambientLight;

        _origSky = RenderSettings.skybox;
        if (_origSky != null)
        {
            _skyInstance = new Material(_origSky) { name = _origSky.name + " (Dawn)" };
            _skyInstance.SetFloat(DawnId, 0f);
            RenderSettings.skybox = _skyInstance;
        }
    }

    public override void OnPlayableDestroy(Playable playable)
    {
        if (!_captured) return;

        if (_light != null)
        {
            _light.color = _origLightColor;
            _light.intensity = _origLightIntensity;
            _light.transform.rotation = _origLightRotation;
        }
        RenderSettings.ambientLight = _origAmbient;

        if (_origSky != null) RenderSettings.skybox = _origSky;
        if (_skyInstance != null) Object.Destroy(_skyInstance);

        _captured = false;
    }
}
