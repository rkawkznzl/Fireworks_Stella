// 메뉴 Tools > Stella > Build Classroom 을 누르면
// 기본 Cube / Cylinder 로 low-poly 교실을 현재 신에 생성한다.
// 생성 결과는 신에 저장되므로, 이 스크립트는 다시 실행하거나 지워도 결과에 영향이 없다.
//
// 좌표 기준 (단위: m)
//   X: -4.5(왼쪽, 창문 벽) ~ +4.5(오른쪽, 책장 벽)
//   Z: -4(뒤쪽) ~ +4(앞쪽, 칠판)
//   Y: 0(바닥) ~ 3.2(천장)

using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class ClassroomBuilder
{
    const string RootName = "Classroom";
    const string MaterialFolder = "Assets/Materials/Classroom";

    // 교실 크기
    const float HalfW = 4.5f;   // X 방향 절반
    const float HalfD = 4f;     // Z 방향 절반
    const float Height = 3.2f;
    const float WallT = 0.1f;   // 벽 두께

    // 창문 벽
    const float WaistTop = 0.8f;   // 창 아래 허리벽 높이
    const float WindowTop = 2.9f;  // 창 윗선

    static readonly Dictionary<string, Material> _mats = new Dictionary<string, Material>();

    [MenuItem("Tools/Stella/Build Classroom")]
    public static void Build()
    {
        GameObject old = GameObject.Find(RootName);
        if (old != null)
        {
            bool replace = EditorUtility.DisplayDialog(
                "Build Classroom",
                "이미 'Classroom' 오브젝트가 있습니다. 지우고 새로 만들까요?",
                "새로 만들기", "취소");
            if (!replace) return;
            Undo.DestroyObjectImmediate(old);
        }

        PrepareMaterials();

        GameObject root = new GameObject(RootName);
        Undo.RegisterCreatedObjectUndo(root, "Build Classroom");

        BuildShell(root.transform);
        BuildWindowWall(root.transform);
        BuildBlackboard(root.transform);
        BuildDesks(root.transform);
        BuildBookshelf(root.transform);
        BuildCeilingLamps(root.transform);
        BuildTelescope(root.transform);
        PlaceCamera();

        Selection.activeGameObject = root;
        EditorSceneManager.MarkSceneDirty(root.scene);
        Debug.Log("[ClassroomBuilder] 교실 생성 완료. Ctrl+S 로 신을 저장하세요.");
    }

    // ───────────────────────── 머티리얼 ─────────────────────────

    static void PrepareMaterials()
    {
        _mats.Clear();
        MakeMat("Wall",       new Color(0.78f, 0.75f, 0.86f));  // 연한 라벤더 회색
        MakeMat("Floor",      new Color(0.55f, 0.38f, 0.26f));  // 나무 바닥
        MakeMat("Ceiling",    new Color(0.85f, 0.84f, 0.90f));
        MakeMat("Blackboard", new Color(0.12f, 0.22f, 0.20f));  // 어두운 청록
        MakeMat("Wood",       new Color(0.82f, 0.66f, 0.45f));  // 책상 상판, 의자
        MakeMat("Metal",      new Color(0.70f, 0.70f, 0.74f), 0.6f);
        MakeMat("Frame",      new Color(0.90f, 0.90f, 0.93f));  // 창틀
        MakeMat("Curtain",    new Color(0.95f, 0.93f, 0.97f));
        MakeMat("Lamp",       new Color(0.92f, 0.92f, 0.95f));
        MakeMat("Telescope",  new Color(0.20f, 0.22f, 0.30f), 0.7f);
        MakeMat("BookA",      new Color(0.55f, 0.62f, 0.80f));
        MakeMat("BookB",      new Color(0.85f, 0.70f, 0.75f));
        MakeMat("BookC",      new Color(0.90f, 0.86f, 0.70f));
    }

    static void MakeMat(string name, Color color, float smoothness = 0.15f)
    {
        if (!AssetDatabase.IsValidFolder(MaterialFolder))
        {
            AssetDatabase.CreateFolder("Assets/Materials", "Classroom");
        }

        string path = $"{MaterialFolder}/M_{name}.mat";
        Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            mat = new Material(shader);
            mat.SetColor("_BaseColor", color);
            mat.SetFloat("_Smoothness", smoothness);
            AssetDatabase.CreateAsset(mat, path);
        }
        // 이미 있으면 사용자가 바꾼 색을 그대로 유지한다.
        _mats[name] = mat;
    }

    // ───────────────────────── 헬퍼 ─────────────────────────

    static GameObject Group(string name, Transform parent, Vector3 localPos = default, Vector3 localEuler = default)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        go.transform.localEulerAngles = localEuler;
        return go;
    }

    static GameObject Box(string name, Transform parent, Vector3 pos, Vector3 scale, string mat)
    {
        return Prim(PrimitiveType.Cube, name, parent, pos, scale, Quaternion.identity, mat);
    }

    static GameObject Prim(PrimitiveType type, string name, Transform parent, Vector3 pos, Vector3 scale, Quaternion rot, string mat)
    {
        GameObject go = GameObject.CreatePrimitive(type);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        go.transform.localRotation = rot;
        go.transform.localScale = scale;

        // 충돌은 필요 없으므로 Collider 제거
        Collider col = go.GetComponent<Collider>();
        if (col != null) Object.DestroyImmediate(col);

        go.GetComponent<MeshRenderer>().sharedMaterial = _mats[mat];
        return go;
    }

    // 두 점 사이를 잇는 막대 (Cylinder 기본 높이는 2)
    static GameObject Rod(string name, Transform parent, Vector3 from, Vector3 to, float thickness, string mat)
    {
        Vector3 dir = to - from;
        Quaternion rot = Quaternion.FromToRotation(Vector3.up, dir.normalized);
        Vector3 scale = new Vector3(thickness, dir.magnitude * 0.5f, thickness);
        return Prim(PrimitiveType.Cylinder, name, parent, (from + to) * 0.5f, scale, rot, mat);
    }

    // ───────────────────────── 방 껍데기 ─────────────────────────

    static void BuildShell(Transform root)
    {
        Transform t = Group("Room", root).transform;

        Box("Floor",   t, new Vector3(0, -WallT * 0.5f, 0),        new Vector3(HalfW * 2 + WallT * 2, WallT, HalfD * 2 + WallT * 2), "Floor");
        Box("Ceiling", t, new Vector3(0, Height + WallT * 0.5f, 0), new Vector3(HalfW * 2 + WallT * 2, WallT, HalfD * 2 + WallT * 2), "Ceiling");

        Box("Wall_Front", t, new Vector3(0, Height * 0.5f,  HalfD + WallT * 0.5f), new Vector3(HalfW * 2, Height, WallT), "Wall");
        Box("Wall_Back",  t, new Vector3(0, Height * 0.5f, -HalfD - WallT * 0.5f), new Vector3(HalfW * 2, Height, WallT), "Wall");
        Box("Wall_Right", t, new Vector3(HalfW + WallT * 0.5f, Height * 0.5f, 0),  new Vector3(WallT, Height, HalfD * 2 + WallT * 2), "Wall");

        // 걸레받이 (바닥과 벽 경계선)
        Box("Baseboard_Front", t, new Vector3(0, 0.05f,  HalfD - 0.01f), new Vector3(HalfW * 2, 0.1f, 0.02f), "Wood");
        Box("Baseboard_Back",  t, new Vector3(0, 0.05f, -HalfD + 0.01f), new Vector3(HalfW * 2, 0.1f, 0.02f), "Wood");
        Box("Baseboard_Right", t, new Vector3(HalfW - 0.01f, 0.05f, 0),  new Vector3(0.02f, 0.1f, HalfD * 2), "Wood");
    }

    // 왼쪽 벽 전체 창문: 허리벽 + 윗벽 + 기둥 + 가로 창살. 유리는 넣지 않는다.
    static void BuildWindowWall(Transform root)
    {
        float x = -HalfW - WallT * 0.5f;
        float depth = HalfD * 2 + WallT * 2;
        Transform t = Group("WindowWall", root).transform;

        Box("Waist", t, new Vector3(x, WaistTop * 0.5f, 0), new Vector3(WallT, WaistTop, depth), "Wall");
        float headerH = Height - WindowTop;
        Box("Header", t, new Vector3(x, WindowTop + headerH * 0.5f, 0), new Vector3(WallT, headerH, depth), "Wall");

        // 창턱
        Box("Sill", t, new Vector3(-HalfW + 0.1f, WaistTop + 0.015f, 0), new Vector3(0.3f, 0.03f, HalfD * 2), "Frame");

        // 세로 기둥: 2m 간격
        float openH = WindowTop - WaistTop;
        for (float z = -HalfD; z <= HalfD + 0.01f; z += 2f)
        {
            Box($"Mullion_{z:0}", t, new Vector3(x, WaistTop + openH * 0.5f, z), new Vector3(0.14f, openH, 0.14f), "Frame");
        }
        // 창문 사이 얇은 세로 창살 (각 칸 가운데)
        for (float z = -HalfD + 1f; z < HalfD; z += 2f)
        {
            Box($"Muntin_{z:0.0}", t, new Vector3(x, WaistTop + openH * 0.5f, z), new Vector3(0.05f, openH, 0.04f), "Frame");
        }
        // 가로 창살
        Box("Transom", t, new Vector3(x, 2.25f, 0), new Vector3(0.06f, 0.05f, depth), "Frame");

        // 커튼 (칠판 쪽 끝에 묶어 둔 상태)
        Box("Curtain", t, new Vector3(-HalfW + 0.12f, (WaistTop + WindowTop) * 0.5f + 0.1f, HalfD - 0.3f),
            new Vector3(0.12f, openH + 0.2f, 0.45f), "Curtain");
    }

    // ───────────────────────── 칠판 ─────────────────────────

    static void BuildBlackboard(Transform root)
    {
        float z = HalfD - 0.03f;
        Transform t = Group("Blackboard", root).transform;

        Box("Board", t, new Vector3(-0.5f, 1.65f, z), new Vector3(4.2f, 1.2f, 0.03f), "Blackboard");
        Box("Frame_Top",    t, new Vector3(-0.5f, 2.27f, z - 0.01f), new Vector3(4.3f, 0.05f, 0.05f), "Metal");
        Box("Frame_Bottom", t, new Vector3(-0.5f, 1.03f, z - 0.01f), new Vector3(4.3f, 0.05f, 0.05f), "Metal");
        Box("Frame_Left",   t, new Vector3(-2.63f, 1.65f, z - 0.01f), new Vector3(0.05f, 1.25f, 0.05f), "Metal");
        Box("Frame_Right",  t, new Vector3(1.63f, 1.65f, z - 0.01f), new Vector3(0.05f, 1.25f, 0.05f), "Metal");
        Box("ChalkTray",    t, new Vector3(-0.5f, 1.0f, z - 0.07f), new Vector3(4.2f, 0.03f, 0.12f), "Metal");

        // 교탁 (칠판 앞, 창문 쪽)
        Box("TeacherDesk", t, new Vector3(-2.2f, 0.45f, HalfD - 0.9f), new Vector3(1.2f, 0.9f, 0.6f), "Wood");
    }

    // ───────────────────────── 책상 + 뒤집은 의자 ─────────────────────────

    static void BuildDesks(Transform root)
    {
        Transform t = Group("Desks", root).transform;

        float[] xs = { -2.6f, -1.2f, 0.2f, 1.6f };
        float[] zs = { 1.9f, 0.5f, -0.9f };
        int index = 0;
        foreach (float z in zs)
        {
            foreach (float x in xs)
            {
                // 카메라 자리(오른쪽 뒤)는 비워 둔다
                if (x > 1f && z < 0f) continue;

                Transform set = Group($"DeskSet_{index++:00}", t, new Vector3(x, 0, z)).transform;
                BuildDesk(set);
                // 의자를 뒤집어서 책상 위에 올림. 등받이는 학생 뒤쪽(-Z)으로 걸쳐 내려온다.
                Transform chair = Group("Chair", set, new Vector3(0, 0.735f, -0.06f), new Vector3(0, 0, 180)).transform;
                BuildChair(chair);
            }
        }
    }

    static void BuildDesk(Transform p)
    {
        const float topY = 0.7f;
        Box("Top", p, new Vector3(0, topY, 0), new Vector3(0.65f, 0.03f, 0.45f), "Wood");
        Box("Drawer", p, new Vector3(0, topY - 0.08f, 0.02f), new Vector3(0.58f, 0.1f, 0.38f), "Metal");
        for (int i = 0; i < 4; i++)
        {
            float lx = (i % 2 == 0 ? -1 : 1) * 0.29f;
            float lz = (i < 2 ? -1 : 1) * 0.19f;
            Box($"Leg_{i}", p, new Vector3(lx, topY * 0.5f, lz), new Vector3(0.03f, topY, 0.03f), "Metal");
        }
    }

    // 의자 로컬 좌표: 좌판 중심이 원점, 다리는 아래(-Y), 등받이는 위(+Y)·뒤(-Z)
    static void BuildChair(Transform p)
    {
        Box("Seat", p, Vector3.zero, new Vector3(0.4f, 0.03f, 0.38f), "Wood");
        for (int i = 0; i < 4; i++)
        {
            float lx = (i % 2 == 0 ? -1 : 1) * 0.18f;
            float lz = (i < 2 ? -1 : 1) * 0.17f;
            Box($"Leg_{i}", p, new Vector3(lx, -0.21f, lz), new Vector3(0.025f, 0.42f, 0.025f), "Metal");
        }
        Box("BackPost_L", p, new Vector3(-0.18f, 0.2f, -0.19f), new Vector3(0.025f, 0.4f, 0.025f), "Metal");
        Box("BackPost_R", p, new Vector3( 0.18f, 0.2f, -0.19f), new Vector3(0.025f, 0.4f, 0.025f), "Metal");
        Box("BackRest",   p, new Vector3(0, 0.3f, -0.19f), new Vector3(0.4f, 0.16f, 0.02f), "Wood");
    }

    // ───────────────────────── 책장 (오른쪽 벽) ─────────────────────────

    static void BuildBookshelf(Transform root)
    {
        const float w = 2.0f, h = 1.2f, d = 0.32f;
        float x = HalfW - d * 0.5f;
        float zc = 1.2f;
        Transform t = Group("Bookshelf", root, new Vector3(x, 0, zc)).transform;

        Box("Back",   t, new Vector3(d * 0.5f - 0.01f, h * 0.5f, 0), new Vector3(0.02f, h, w), "Wood");
        Box("Side_A", t, new Vector3(0, h * 0.5f, -w * 0.5f), new Vector3(d, h, 0.03f), "Wood");
        Box("Side_B", t, new Vector3(0, h * 0.5f,  w * 0.5f), new Vector3(d, h, 0.03f), "Wood");

        float[] shelfYs = { 0.02f, 0.4f, 0.8f, h };
        foreach (float y in shelfYs)
        {
            Box($"Shelf_{y:0.00}", t, new Vector3(0, y, 0), new Vector3(d, 0.03f, w), "Wood");
        }

        // 책: 시드 고정 랜덤으로 높이와 색을 섞는다 (실행할 때마다 같은 결과)
        Random.State saved = Random.state;
        Random.InitState(2026);
        string[] bookMats = { "BookA", "BookB", "BookC" };
        for (int s = 0; s < shelfYs.Length - 1; s++)
        {
            float baseY = shelfYs[s] + 0.015f;
            float z = -w * 0.5f + 0.06f;
            while (z < w * 0.5f - 0.1f)
            {
                float thick = Random.Range(0.03f, 0.07f);
                float bh = Random.Range(0.22f, 0.32f);
                if (Random.value > 0.12f) // 가끔 빈칸
                {
                    Box("Book", t, new Vector3(-0.02f, baseY + bh * 0.5f, z + thick * 0.5f),
                        new Vector3(0.22f, bh, thick), bookMats[Random.Range(0, bookMats.Length)]);
                }
                z += thick + 0.004f;
            }
        }
        Random.state = saved;
    }

    // ───────────────────────── 천장 형광등 ─────────────────────────

    static void BuildCeilingLamps(Transform root)
    {
        Transform t = Group("CeilingLamps", root).transform;
        float[] xs = { -1.8f, 1.2f };
        float[] zs = { -1.8f, 1.6f };
        int i = 0;
        foreach (float x in xs)
        {
            foreach (float z in zs)
            {
                Transform lamp = Group($"Lamp_{i++}", t, new Vector3(x, Height, z)).transform;
                Box("Body", lamp, new Vector3(0, -0.35f, 0), new Vector3(0.14f, 0.05f, 1.4f), "Lamp");
                Box("Wire_A", lamp, new Vector3(0, -0.17f, -0.55f), new Vector3(0.01f, 0.34f, 0.01f), "Metal");
                Box("Wire_B", lamp, new Vector3(0, -0.17f,  0.55f), new Vector3(0.01f, 0.34f, 0.01f), "Metal");
            }
        }
    }

    // ───────────────────────── 망원경 (칠판 오른쪽) ─────────────────────────

    static void BuildTelescope(Transform root)
    {
        Transform t = Group("Telescope", root, new Vector3(2.6f, 0, HalfD - 0.7f)).transform;

        Vector3 apex = new Vector3(0, 1.15f, 0);
        for (int i = 0; i < 3; i++)
        {
            float a = (i * 120f + 30f) * Mathf.Deg2Rad;
            Vector3 foot = new Vector3(Mathf.Cos(a) * 0.35f, 0, Mathf.Sin(a) * 0.35f);
            Rod($"Leg_{i}", t, apex, foot, 0.025f, "Metal");
        }
        Box("Mount", t, apex + new Vector3(0, 0.04f, 0), new Vector3(0.08f, 0.08f, 0.08f), "Telescope");

        // 경통: 창문(-X) 쪽 밤하늘을 향해 비스듬히
        Vector3 aim = new Vector3(-1f, 0.65f, -0.35f).normalized;
        Vector3 center = apex + new Vector3(0, 0.1f, 0);
        Rod("Tube", t, center - aim * 0.3f, center + aim * 0.45f, 0.11f, "Telescope");
        Rod("Lens", t, center + aim * 0.45f, center + aim * 0.5f, 0.13f, "Metal");
        Rod("Eyepiece", t, center - aim * 0.3f, center - aim * 0.4f, 0.04f, "Metal");
    }

    // ───────────────────────── 카메라 ─────────────────────────

    static void PlaceCamera()
    {
        Camera cam = Camera.main;
        if (cam == null) return;

        Undo.RecordObject(cam.transform, "Place Camera");
        Undo.RecordObject(cam, "Place Camera");
        cam.transform.position = new Vector3(3.3f, 1.3f, -3.3f);
        cam.transform.LookAt(new Vector3(-2.2f, 1.75f, 2.2f));
        cam.fieldOfView = 60f;
        cam.nearClipPlane = 0.05f;
        cam.farClipPlane = 2000f; // 멀리서 터지는 불꽃까지 보이도록
    }
}
