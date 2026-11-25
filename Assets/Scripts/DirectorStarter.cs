// ▶(Play)를 누른 직후에는 신 로딩과 셰이더 준비 때문에 첫 몇 프레임이 길어져서,
// Timeline을 바로 재생하면 음악 앞부분이 건너뛰어지거나 끊길 수 있다.
// 이 스크립트는 잠깐 기다렸다가 Timeline을 0초부터 재생한다.
//
// 사용법: Director 오브젝트에 붙이고, Playable Director의 Play On Awake는 끈다.

using System.Collections;
using UnityEngine;
using UnityEngine.Playables;

[RequireComponent(typeof(PlayableDirector))]
public class DirectorStarter : MonoBehaviour
{
    [Tooltip("Play 후 Timeline을 시작하기까지 기다리는 시간 (초)")]
    [SerializeField, Min(0f)] float startDelay = 1f;

    [Tooltip("이 시간(초)부터 재생. 특정 구간만 확인할 때 사용하고, 녹화 전에는 0으로 되돌린다")]
    [SerializeField, Min(0f)] float startTime = 0f;

    IEnumerator Start()
    {
        PlayableDirector director = GetComponent<PlayableDirector>();
        director.Stop();

        // 실제 시간 기준으로 기다린 뒤, 한 프레임 더 넘겨서 프레임 시간이 안정되게 한다
        yield return new WaitForSecondsRealtime(startDelay);
        yield return null;

        director.time = startTime;
        director.Play();
    }
}
