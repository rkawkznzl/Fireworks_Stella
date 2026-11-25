// Firework 트랙에 놓는 클립 하나 = "불꽃 한 묶음".
// 클립 Inspector에서 어떤 불꽃을, 어디서, 몇 발, 몇 초 간격으로 쏠지 정한다.
// 실제 발사는 FireworkTrack의 믹서(FireworkMixerBehaviour)가 Play 중에만 처리한다.

using System;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

public enum FireworkSpreadMode
{
    Random,            // 흩어짐 범위 안에서 무작위
    SweepLeftToRight,  // 왼쪽 → 오른쪽으로 차례로
    SweepRightToLeft,  // 오른쪽 → 왼쪽으로 차례로
    CenterOut,         // 가운데에서 바깥으로 번갈아
}

public enum FireworkTiltMode
{
    Fixed,      // 모든 발을 같은 각도로
    Alternate,  // 발마다 +각도, -각도 번갈아 (V자)
    Sweep,      // 첫 발 -각도 → 마지막 발 +각도로 차례로 (부채 펼치기)
}

public enum FireworkPalette
{
    Prefab,     // 프리팹에 설정된 색 그대로
    Gold,       // 연한 금
    Silver,     // 은백
    Lavender,   // 라벤더
    Pink,       // 옅은 분홍
    Sky,        // 옅은 하늘
    Mint,       // 민트
    Starlight,  // 금 + 은백
    Twilight,   // 라벤더 + 분홍
    Rainbow,    // 발마다 다른 색 (금 → 분홍 → 라벤더 → 하늘 → 민트 → 은백)

    // Leo/need 멤버 상징색 (기존 클립 값이 바뀌지 않도록 맨 뒤에 추가)
    LeoBlue,    // #33AAEE
    LeoGreen,   // #BBDD22
    LeoYellow,  // #FFDD44
    LeoRed,     // #EE6666
    LeoNeed,    // 발마다 파랑 → 초록 → 노랑 → 빨강 순서
}

// 팔레트 색 정의.
// HDR 밝기(머티리얼 Intensity)가 곱해지면 색이 옅어지므로, 화면에 보일 색보다 약간 진하게 잡는다.
public static class FireworkPalettes
{
    static Color Hex(string hex)
    {
        ColorUtility.TryParseHtmlString("#" + hex, out Color c);
        return c;
    }

    static readonly Color GoldA = Hex("FFC97A"), GoldB = Hex("FFE2A8");
    static readonly Color SilverA = Hex("E3EAFF"), SilverB = Hex("FFFFFF");
    static readonly Color LavenderA = Hex("B8A4FF"), LavenderB = Hex("CDBEFF");
    static readonly Color PinkA = Hex("FFB8D6"), PinkB = Hex("FFCCE2");
    static readonly Color SkyA = Hex("A8CCFF"), SkyB = Hex("C4DCFF");
    static readonly Color MintA = Hex("A8F0DA"), MintB = Hex("C6F7E6");

    // 멤버 색은 원래 색을 그대로 살리고, 두 번째 색은 흰빛을 30% 섞어 반짝임에 변화를 준다
    static readonly Color LeoBlueA = Hex("33AAEE"), LeoBlueB = Color.Lerp(Hex("33AAEE"), Color.white, 0.3f);
    static readonly Color LeoGreenA = Hex("BBDD22"), LeoGreenB = Color.Lerp(Hex("BBDD22"), Color.white, 0.3f);
    static readonly Color LeoYellowA = Hex("FFDD44"), LeoYellowB = Color.Lerp(Hex("FFDD44"), Color.white, 0.3f);
    static readonly Color LeoRedA = Hex("EE6666"), LeoRedB = Color.Lerp(Hex("EE6666"), Color.white, 0.3f);

    static readonly FireworkPalette[] LeoNeedOrder =
    {
        FireworkPalette.LeoBlue, FireworkPalette.LeoGreen, FireworkPalette.LeoYellow, FireworkPalette.LeoRed,
    };

    static readonly FireworkPalette[] RainbowOrder =
    {
        FireworkPalette.Gold, FireworkPalette.Pink, FireworkPalette.Lavender,
        FireworkPalette.Sky, FireworkPalette.Mint, FireworkPalette.Silver,
    };

    /// <summary>팔레트의 두 색을 돌려준다. Prefab이면 false.</summary>
    public static bool TryGet(FireworkPalette p, int shotIndex, out Color a, out Color b)
    {
        if (p == FireworkPalette.Rainbow)
        {
            p = RainbowOrder[shotIndex % RainbowOrder.Length];
        }
        else if (p == FireworkPalette.LeoNeed)
        {
            p = LeoNeedOrder[shotIndex % LeoNeedOrder.Length];
        }

        switch (p)
        {
            case FireworkPalette.Gold:      a = GoldA;     b = GoldB;     return true;
            case FireworkPalette.Silver:    a = SilverA;   b = SilverB;   return true;
            case FireworkPalette.Lavender:  a = LavenderA; b = LavenderB; return true;
            case FireworkPalette.Pink:      a = PinkA;     b = PinkB;     return true;
            case FireworkPalette.Sky:       a = SkyA;      b = SkyB;      return true;
            case FireworkPalette.Mint:      a = MintA;     b = MintB;     return true;
            case FireworkPalette.Starlight: a = GoldA;     b = SilverA;   return true;
            case FireworkPalette.Twilight:  a = LavenderA; b = PinkA;     return true;
            case FireworkPalette.LeoBlue:   a = LeoBlueA;   b = LeoBlueB;   return true;
            case FireworkPalette.LeoGreen:  a = LeoGreenA;  b = LeoGreenB;  return true;
            case FireworkPalette.LeoYellow: a = LeoYellowA; b = LeoYellowB; return true;
            case FireworkPalette.LeoRed:    a = LeoRedA;    b = LeoRedB;    return true;
            default:                        a = b = Color.white;          return false;
        }
    }
}

[Serializable]
public class FireworkClip : PlayableAsset, ITimelineClipAsset
{
    [Header("무엇을")]
    [Tooltip("쏠 불꽃 프리팹 (FW_Peony 등)")]
    public GameObject prefab;

    [Header("어디서")]
    [Tooltip("발사 기준 위치의 이름. 트랙에 연결한 Transform(Fireworks) 아래에서 이 이름의 오브젝트를 찾는다. 비워 두면 Fireworks 위치를 쓴다")]
    public string launchPointName = "LaunchPoint_Center";

    [Tooltip("기준 위치에서 더 옮길 거리 (발사 지점의 로컬 축 기준)")]
    public Vector3 offset;

    [Tooltip("흩어짐 범위 (±값, 발사 지점의 로컬 축 기준). X = 좌우, Y = 위아래, Z = 앞뒤")]
    public Vector3 spread = new Vector3(20f, 0f, 10f);

    public FireworkSpreadMode spreadMode = FireworkSpreadMode.Random;

    [Header("몇 발, 언제")]
    [Min(1)] public int count = 1;

    [Tooltip("발과 발 사이 간격 (초)")]
    [Min(0f)] public float interval = 0.5f;

    [Tooltip("간격에 섞을 무작위 흔들림 (초). 0이면 정확히 박자대로")]
    [Min(0f)] public float intervalJitter = 0f;

    [Tooltip("켜면 '클립 위치 = 하늘에서 터지는 순간'. 로켓이 날아가는 시간만큼 미리 발사한다")]
    public bool alignToExplosion = true;

    [Tooltip("로켓이 발사부터 폭발까지 걸리는 시간 (프리팹의 Start Lifetime 평균)")]
    [Min(0f)] public float flightTime = 2f;

    [Header("색과 크기")]
    [Tooltip("폭발 색. Prefab이면 프리팹 색 그대로, Rainbow면 발마다 다른 색")]
    public FireworkPalette palette = FireworkPalette.Prefab;

    [Tooltip("폭발 크기 배수 (퍼지는 반지름, 알갱이 크기, 섬광, 교실 빛 세기가 함께 바뀐다)")]
    [Range(0.5f, 1.8f)] public float sizeScale = 1f;

    [Tooltip("발마다 크기를 ±이 비율만큼 무작위로 흔든다 (0.2 = ±20%)")]
    [Range(0f, 0.5f)] public float sizeJitter = 0f;

    [Header("발사 각도")]
    [Tooltip("화면에서 좌우로 기울여 쏘는 각도 (+는 오른쪽, -는 왼쪽). 상승 코멧을 비스듬히 쏠 때 사용")]
    [Range(-60f, 60f)] public float tilt = 0f;

    public FireworkTiltMode tiltMode = FireworkTiltMode.Fixed;

    [Header("변형")]
    [Tooltip("로켓 속도 배수. 1보다 크면 더 높이, 작으면 더 낮게 터진다")]
    [Range(0.5f, 1.5f)] public float heightScale = 1f;

    [Tooltip("생성한 불꽃을 이 시간 뒤에 지운다 (초)")]
    [Min(1f)] public float cleanupDelay = 8f;

    public ClipCaps clipCaps => ClipCaps.None;

    // 클립 길이를 발사 횟수에 맞춰 보여 준다 (처음 놓을 때 기본 길이)
    public override double duration => Math.Max(0.5, (count - 1) * interval + 0.5);

    public override Playable CreatePlayable(PlayableGraph graph, GameObject owner)
    {
        // 발사는 믹서가 처리하므로 클립 자체는 빈 Playable
        return Playable.Create(graph);
    }
}
