// 메뉴 Tools > Stella > Build Show from CSV
// Assets/Timeline/StellaCues.csv 를 읽어 TL_Stella 타임라인의 Firework 트랙(과 Section 트랙)을 다시 채운다.
// 실행 전에 타임라인을 Assets/Timeline/Backups/ 에 백업한다.
//
// CSV 안의 설정 줄 (#@ 로 시작)
//   #@ bpm=196.7            템포
//   #@ offset=0.365         0마디 첫 박의 시각(초)
//   #@ beatsPerBar=4        한 마디의 박 수
//   #@ section Intro 0 18   Section 트랙 클립 (이름, 시작 마디, 끝 마디 또는 end)
//
// 시각(time) 표기
//   12.5      → 12.5초
//   60:2      → 60마디 2박 (박은 0부터, 소수 가능: 60:1.5)
// 발사 지점(point) 표기
//   LaunchPoint_Left          → 그 자리
//   LaunchPoint_Left~-18      → 그 자리에서 화면 왼쪽으로 18m
//   LaunchPoint_Left~12~4     → 오른쪽 12m, 위로 4m
// 간격(interval) 표기
//   0.4       → 0.4초
//   1b / 2b   → 1박 / 2박
//   1bar      → 1마디

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Timeline;
using UnityEngine;
using UnityEngine.Timeline;

public static class ShowBuilder
{
    const string TimelinePath = "Assets/Timeline/TL_Stella.playable";
    const string CsvPath = "Assets/Timeline/StellaCues.csv";
    const string BackupFolder = "Assets/Timeline/Backups";
    const string PrefabFolder = "Assets/Prefabs/Fireworks";

    // 템포 설정 (CSV의 #@ 줄로 덮어씀)
    static double _bpm = 120, _offset = 0;
    static int _beatsPerBar = 4;

    // 새벽 전환 구간 (#@ dawn 시작마디 끝마디|end). 시작마디 < 0 이면 없음
    static double _dawnStartBar = -1, _dawnEndBar = -1;

    // 자리 순환 (#@ laneCycle, #@ accentHeights). 비어 있으면 적용 안 함
    static readonly List<Vector2> _lanes = new List<Vector2>();      // (좌우 X, 높이 배수)
    static readonly List<float> _accentHeights = new List<float>();
    static double BeatSec => 60.0 / _bpm;
    static double BarSec => BeatSec * _beatsPerBar;

    [MenuItem("Tools/Stella/Build Show from CSV")]
    public static void Build()
    {
        TimelineAsset timeline = AssetDatabase.LoadAssetAtPath<TimelineAsset>(TimelinePath);
        if (timeline == null)
        {
            EditorUtility.DisplayDialog("Build Show", $"타임라인을 찾을 수 없습니다.\n{TimelinePath}", "확인");
            return;
        }
        if (!File.Exists(CsvPath))
        {
            EditorUtility.DisplayDialog("Build Show", $"연출표 CSV를 찾을 수 없습니다.\n{CsvPath}", "확인");
            return;
        }

        List<Cue> cues = ParseCsv(CsvPath, out List<Section> sections, out List<string> errors);
        if (cues.Count == 0)
        {
            EditorUtility.DisplayDialog("Build Show", "CSV에서 읽은 줄이 없습니다.\n" + string.Join("\n", errors.Take(10)), "확인");
            return;
        }

        FireworkTrack track = timeline.GetOutputTracks().OfType<FireworkTrack>().FirstOrDefault();
        SectionTrack sectionTrack = timeline.GetOutputTracks().OfType<SectionTrack>().FirstOrDefault();
        int oldCount = track != null ? track.GetClips().Count() : 0;

        string msg = $"템포 {_bpm} BPM, 한 마디 {BarSec:0.###}초\n\n" +
                     $"Firework 트랙의 기존 클립 {oldCount}개를 지우고\nCSV의 {cues.Count}개 클립으로 다시 채웁니다.";
        if (sections.Count > 0) msg += $"\nSection 트랙도 {sections.Count}개 구간으로 다시 맞춥니다.";
        msg += $"\n\n실행 전에 타임라인을 {BackupFolder}에 백업합니다.";
        if (errors.Count > 0) msg += $"\n\n⚠ 읽지 못한 줄 {errors.Count}개가 있습니다 (Console 확인).";
        if (!EditorUtility.DisplayDialog("Build Show from CSV", msg, "다시 채우기", "취소")) return;

        Backup();

        if (track == null)
        {
            track = timeline.CreateTrack<FireworkTrack>(null, "Fireworks");
            Debug.LogWarning("[ShowBuilder] Firework 트랙이 없어 새로 만들었습니다. Director의 바인딩에 Fireworks를 연결하세요.");
        }

        foreach (TimelineClip old in track.GetClips().ToList())
        {
            timeline.DeleteClip(old);
        }

        int made = 0;
        var prefabCache = new Dictionary<string, GameObject>();
        foreach (Cue c in cues)
        {
            if (!prefabCache.TryGetValue(c.prefab, out GameObject prefab))
            {
                prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabFolder}/{c.prefab}.prefab");
                prefabCache[c.prefab] = prefab;
            }
            if (prefab == null)
            {
                Debug.LogWarning($"[ShowBuilder] {c.line}번째 줄: 프리팹 '{c.prefab}'을 {PrefabFolder}에서 찾지 못해 건너뜁니다.");
                continue;
            }

            TimelineClip clip = track.CreateClip<FireworkClip>();
            FireworkClip a = (FireworkClip)clip.asset;

            a.prefab = prefab;
            a.launchPointName = c.point;
            a.offset = c.offset;
            a.count = Mathf.Max(1, c.count);
            a.interval = Mathf.Max(0f, c.interval);
            a.intervalJitter = Mathf.Max(0f, c.jitter);
            a.spread = c.spread;
            a.spreadMode = c.spreadMode;
            a.palette = c.palette;
            a.sizeScale = Mathf.Clamp(c.size, 0.5f, 1.8f);
            a.sizeJitter = Mathf.Clamp(c.sizeJitter, 0f, 0.5f);
            a.heightScale = Mathf.Clamp(c.height, 0.5f, 1.5f);
            a.tilt = Mathf.Clamp(c.tilt, -60f, 60f);
            a.tiltMode = c.tiltMode;
            a.alignToExplosion = c.align;

            clip.start = c.time;
            clip.duration = Math.Max(0.3, (a.count - 1) * a.interval + 0.3);
            clip.displayName = string.IsNullOrEmpty(c.note)
                ? $"{c.prefab.Replace("FW_", "")} {c.palette}"
                : c.note;
            EditorUtility.SetDirty(a);
            made++;
        }

        if (sections.Count > 0)
        {
            RebuildSections(timeline, sectionTrack, sections);
        }

        if (_dawnStartBar >= 0)
        {
            RebuildDawn(timeline);
        }

        EditorUtility.SetDirty(track);
        EditorUtility.SetDirty(timeline);
        AssetDatabase.SaveAssets();
        TimelineEditor.Refresh(RefreshReason.ContentsAddedOrRemoved);

        foreach (string e in errors) Debug.LogWarning("[ShowBuilder] " + e);
        Debug.Log($"[ShowBuilder] 완료: 클립 {made}개 생성 (기존 {oldCount}개 삭제). 템포 {_bpm} BPM.");
    }

    static void RebuildSections(TimelineAsset timeline, SectionTrack sectionTrack, List<Section> sections)
    {
        if (sectionTrack == null)
        {
            sectionTrack = timeline.CreateTrack<SectionTrack>(null, "Section Track");
        }

        // 기존 메모는 이름이 같은 구간으로 옮겨 준다
        var notes = new Dictionary<string, string>();
        foreach (TimelineClip old in sectionTrack.GetClips().ToList())
        {
            if (old.asset is SectionClip sc && !string.IsNullOrEmpty(sc.note)) notes[old.displayName] = sc.note;
            timeline.DeleteClip(old);
        }

        foreach (Section s in sections)
        {
            TimelineClip clip = sectionTrack.CreateClip<SectionClip>();
            double start = BarToSec(s.startBar, 0);
            double end = s.endBar < 0 ? timeline.duration : BarToSec(s.endBar, 0);
            if (s.startBar == 0) start = 0; // 첫 구간은 곡 시작부터
            clip.start = start;
            clip.duration = Math.Max(0.1, end - start);
            clip.displayName = s.name;
            if (notes.TryGetValue(s.name, out string n)) ((SectionClip)clip.asset).note = n;
            EditorUtility.SetDirty(clip.asset);
        }
        EditorUtility.SetDirty(sectionTrack);
    }

    // 새벽 전환 클립을 하나 두고, 신의 Director에서 Dawn 트랙을 Moonlight에 연결한다
    static void RebuildDawn(TimelineAsset timeline)
    {
        DawnTrack dawnTrack = timeline.GetOutputTracks().OfType<DawnTrack>().FirstOrDefault();
        if (dawnTrack == null) dawnTrack = timeline.CreateTrack<DawnTrack>(null, "Dawn");

        // 기존 클립의 색·곡선 설정은 유지하고 위치만 다시 맞춘다
        TimelineClip clip = dawnTrack.GetClips().FirstOrDefault();
        foreach (TimelineClip extra in dawnTrack.GetClips().Skip(1).ToList()) timeline.DeleteClip(extra);
        if (clip == null)
        {
            clip = dawnTrack.CreateClip<DawnClip>();
            clip.displayName = "Dawn";
        }

        double start = BarToSec(_dawnStartBar, 0);
        double end = _dawnEndBar < 0 ? timeline.duration : BarToSec(_dawnEndBar, 0);
        clip.start = start;
        clip.duration = Math.Max(0.1, end - start);
        EditorUtility.SetDirty(dawnTrack);

        // 바인딩: 이 타임라인을 쓰는 Director를 열린 신에서 찾아 Moonlight Light를 연결
        var director = UnityEngine.Object.FindObjectsByType<UnityEngine.Playables.PlayableDirector>(FindObjectsSortMode.None)
            .FirstOrDefault(d => d.playableAsset == timeline);
        GameObject moon = GameObject.Find("Moonlight");
        Light moonLight = moon != null ? moon.GetComponent<Light>() : null;
        if (director != null && moonLight != null)
        {
            if (director.GetGenericBinding(dawnTrack) as Light != moonLight)
            {
                director.SetGenericBinding(dawnTrack, moonLight);
                EditorUtility.SetDirty(director);
                UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(director.gameObject.scene);
            }
        }
        else
        {
            Debug.LogWarning("[ShowBuilder] Director 또는 Moonlight를 찾지 못해 Dawn 트랙을 연결하지 못했습니다. Timeline에서 Dawn 트랙에 Moonlight를 직접 연결하세요.");
        }
        Debug.Log($"[ShowBuilder] 새벽 전환: {start:0.00}초 ~ {end:0.00}초");
    }

    // ───────────────────────── CSV ─────────────────────────

    class Cue
    {
        public int line;
        public double time;
        public string prefab;
        public string point;
        public Vector3 offset;
        public int count;
        public float interval, jitter;
        public Vector3 spread;
        public FireworkSpreadMode spreadMode;
        public FireworkPalette palette;
        public float size, sizeJitter, height, tilt;
        public FireworkTiltMode tiltMode;
        public bool align;
        public string note;
    }

    class Section
    {
        public string name;
        public double startBar;
        public double endBar; // -1 = 타임라인 끝
    }

    const int Columns = 18;

    static List<Cue> ParseCsv(string path, out List<Section> sections, out List<string> errors)
    {
        errors = new List<string>();
        sections = new List<Section>();
        var cues = new List<Cue>();
        string[] lines = File.ReadAllLines(path);

        _bpm = 120; _offset = 0; _beatsPerBar = 4;
        _lanes.Clear();
        _accentHeights.Clear();
        _dawnStartBar = -1; _dawnEndBar = -1;

        // 1차: 설정 줄 먼저 읽기 (어디에 적어도 적용되도록)
        foreach (string l in lines)
        {
            string raw = l.Trim();
            if (!raw.StartsWith("#@")) continue;
            string body = raw.Substring(2).Trim();

            if (body.StartsWith("section", StringComparison.OrdinalIgnoreCase))
            {
                string[] p = body.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                if (p.Length >= 4)
                {
                    sections.Add(new Section
                    {
                        name = p[1],
                        startBar = D(p[2]),
                        endBar = p[3].Equals("end", StringComparison.OrdinalIgnoreCase) ? -1 : D(p[3]),
                    });
                }
                continue;
            }

            if (body.StartsWith("dawn", StringComparison.OrdinalIgnoreCase))
            {
                string[] p = body.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                if (p.Length >= 3)
                {
                    _dawnStartBar = D(p[1]);
                    _dawnEndBar = p[2].Equals("end", StringComparison.OrdinalIgnoreCase) ? -1 : D(p[2]);
                }
                continue;
            }

            string[] kv = body.Split('=');
            if (kv.Length != 2) continue;
            string key = kv[0].Trim().ToLowerInvariant();
            if (key == "bpm") _bpm = D(kv[1]);
            else if (key == "offset") _offset = D(kv[1]);
            else if (key == "beatsperbar") _beatsPerBar = (int)D(kv[1]);
            else if (key == "lanecycle")
            {
                // 예: 0:1.0 -18:0.9 15:1.12 -8:1.05  (왼쪽 기준 X:높이, 오른쪽은 X를 뒤집어 거울 대칭)
                foreach (string item in kv[1].Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    string[] xh = item.Split(':');
                    _lanes.Add(new Vector2(F(xh[0]), xh.Length > 1 ? F(xh[1]) : 1f));
                }
            }
            else if (key == "accentheights")
            {
                foreach (string item in kv[1].Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    _accentHeights.Add(F(item));
                }
            }
        }

        // 2차: 클립 줄
        bool headerSkipped = false;
        for (int i = 0; i < lines.Length; i++)
        {
            string raw = lines[i].Trim();
            if (raw.Length == 0 || raw.StartsWith("#")) continue;
            if (!headerSkipped && raw.StartsWith("time", StringComparison.OrdinalIgnoreCase))
            {
                headerSkipped = true;
                continue;
            }

            string[] f = raw.Split(',');
            if (f.Length < Columns - 1)
            {
                errors.Add($"{i + 1}번째 줄: 칸 수가 부족합니다 ({f.Length}/{Columns}).");
                continue;
            }

            try
            {
                var c = new Cue
                {
                    line = i + 1,
                    time = Time(f[0]),
                    prefab = f[1].Trim(),
                    point = PointName(f[2]),
                    offset = PointOffset(f[2]),
                    count = (int)D(f[3]),
                    interval = Interval(f[4]),
                    jitter = F(f[5]),
                    spread = new Vector3(F(f[6]), F(f[7]), F(f[8])),
                    spreadMode = E<FireworkSpreadMode>(f[9]),
                    palette = E<FireworkPalette>(f[10]),
                    size = F(f[11]),
                    sizeJitter = F(f[12]),
                    height = F(f[13]),
                    tilt = F(f[14]),
                    tiltMode = E<FireworkTiltMode>(f[15]),
                    align = f[16].Trim() == "1" || f[16].Trim().Equals("true", StringComparison.OrdinalIgnoreCase),
                    note = f.Length > 17 ? string.Join(" ", f.Skip(17)).Trim() : "",
                };
                cues.Add(c);
            }
            catch (Exception ex)
            {
                errors.Add($"{i + 1}번째 줄: {ex.Message}");
            }
        }

        ApplyLaneCycle(cues);
        return cues;
    }

    // 규칙 8 "자리 순환": 같은 자리 반복을 피하려고
    //   좌우 한 발짜리 국화(오프셋 표기 ~ 가 없는 줄)는 laneCycle 자리를 차례로 돌고 (오른쪽은 거울 대칭)
    //   가운데 한 발짜리 국화는 accentHeights 높이를 차례로 돈다.
    // CSV에 ~ 로 직접 자리를 적은 줄은 건드리지 않는다.
    static void ApplyLaneCycle(List<Cue> cues)
    {
        int left = 0, right = 0, center = 0;
        foreach (Cue c in cues)
        {
            if (c.prefab != "FW_Peony" || c.count != 1 || c.offset != Vector3.zero) continue;

            bool isLeft = c.point == "LaunchPoint_Left";
            bool isRight = c.point == "LaunchPoint_Right";
            if ((isLeft || isRight) && _lanes.Count > 0)
            {
                Vector2 lane = _lanes[(isLeft ? left++ : right++) % _lanes.Count];
                c.offset = new Vector3(isLeft ? lane.x : -lane.x, 0f, 0f);
                c.height = lane.y;
            }
            else if (c.point == "LaunchPoint_Center" && _accentHeights.Count > 0)
            {
                c.height = _accentHeights[center++ % _accentHeights.Count];
            }
        }
    }

    // 발사 지점 표기: "LaunchPoint_Left" 또는 "LaunchPoint_Left~-18" (좌우 X) 또는 "LaunchPoint_Left~-18~5" (X, 높이 Y)
    // X, Y는 발사 지점의 로컬 축 기준 미터 (X+ = 화면 오른쪽)
    static string PointName(string s) => s.Split('~')[0].Trim();

    static Vector3 PointOffset(string s)
    {
        string[] p = s.Split('~');
        float x = p.Length > 1 ? F(p[1]) : 0f;
        float y = p.Length > 2 ? F(p[2]) : 0f;
        return new Vector3(x, y, 0f);
    }

    static double BarToSec(double bar, double beat) => _offset + (bar * _beatsPerBar + beat) * BeatSec;

    // "12.5" → 초, "60:2" → 60마디 2박
    static double Time(string s)
    {
        s = s.Trim();
        int colon = s.IndexOf(':');
        if (colon < 0) return D(s);
        return BarToSec(D(s.Substring(0, colon)), D(s.Substring(colon + 1)));
    }

    // "0.4" → 초, "2b" → 2박, "1bar" → 1마디
    static float Interval(string s)
    {
        s = s.Trim().ToLowerInvariant();
        if (s.Length == 0) return 0f;
        if (s.EndsWith("bar")) return (float)(D(s.Substring(0, s.Length - 3)) * BarSec);
        if (s.EndsWith("b")) return (float)(D(s.Substring(0, s.Length - 1)) * BeatSec);
        return F(s);
    }

    static double D(string s) => double.Parse(s.Trim(), CultureInfo.InvariantCulture);
    static float F(string s) => string.IsNullOrWhiteSpace(s) ? 0f : float.Parse(s.Trim(), CultureInfo.InvariantCulture);

    static T E<T>(string s) where T : struct
    {
        if (Enum.TryParse(s.Trim(), true, out T v)) return v;
        throw new FormatException($"'{s.Trim()}'은(는) {typeof(T).Name}에 없는 값입니다.");
    }

    // ───────────────────────── 백업 ─────────────────────────

    static void Backup()
    {
        if (!AssetDatabase.IsValidFolder(BackupFolder))
        {
            AssetDatabase.CreateFolder("Assets/Timeline", "Backups");
        }
        string stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        string dest = $"{BackupFolder}/TL_Stella_{stamp}.playable";
        if (AssetDatabase.CopyAsset(TimelinePath, dest))
        {
            Debug.Log($"[ShowBuilder] 백업: {dest}");
        }
        else
        {
            Debug.LogWarning("[ShowBuilder] 백업에 실패했습니다. 계속 진행합니다.");
        }
    }
}
