using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public sealed class SchoolEscapeGame : MonoBehaviour
{
    private const float InteractionDistance = 1.7f;
    private const float AttackRadius = 1.45f;
    private const int MaxHealth = 7;

    private readonly List<StageInfo> stages = new List<StageInfo>
    {
        new StageInfo("옥상(5층)", "주인공이 학교에 온 이유를 확인하고 기본 무기를 획득하세요.", "없음", 0, 0, false, "5층 지도와 기본 무기", "단서 학생", "school_rooftop_5f"),
        new StageInfo("물리실, 지구과학실(4층)", "보스를 모두 물리치고 무기를 추가 획득하세요.", "4층 보스", 2, 2, false, "전기 강화 무기", "4층 구조 대상", "school_4f"),
        new StageInfo("화학실, 창의융합실(3층)", "보스를 모두 물리치고 구조 대상자를 구하세요.", "3층 보스", 2, 2, false, "실험실 방어구", "3층 구조 대상", "school_3f"),
        new StageInfo("도서관(3-2층)", "도서관 보스를 물리치고 숨어 있는 학생을 구조하세요.", "도서관 보스", 1, 3, false, "도서관 열쇠", "도서관 구조 대상", "school_3f"),
        new StageInfo("학년실, 전산실(2층)", "보스를 모두 물리치고 무기를 추가 획득하세요.", "2층 보스", 2, 2, false, "전산실 장비", "2층 구조 대상", "school_2f"),
        new StageInfo("가정실, 생태농장(1층)", "가정실 보스가 순간 이동시키면 생태농장에서 다시 싸워 탈출로를 찾으세요.", "가정실 보스", 1, 3, true, "농장 지도", "1층 구조 대상", "school_1f"),
        new StageInfo("운동장 미로(1층)", "마지막 보스를 처치하고 모든 구조 대상자와 함께 미로를 탈출하세요.", "마지막 보스", 1, 5, false, "출구 열쇠", "운동장 구조 대상", "school_1f"),
    };

    private GameObject stageRoot;
    private PlayerMotor player;
    private Text objectiveText;
    private Text statusText;
    private Text positionText;
    private Text taskText;
    private Text mapText;
    private RawImage mapPreview;
    private Text promptText;
    private Transform exitTarget;
    private bool weaponUnlocked;
    private bool hasFloorMap;
    private int weaponLevel;
    private int rescuedStudents;
    private int requiredRescues;
    private int stageIndex;
    private int playerHealth = 5;
    private float attackCooldown;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Boot()
    {
        if (FindAnyObjectByType<SchoolEscapeGame>() != null)
        {
            return;
        }

        var gameObject = new GameObject("escape from school");
        DontDestroyOnLoad(gameObject);
        gameObject.AddComponent<SchoolEscapeGame>();
    }

    private void Start()
    {
        Application.targetFrameRate = 60;
        requiredRescues = stages.Count;
        EnsureEventSystem();
        CreateCamera();
        CreateUi();
        CreatePlayer();
        LoadStage(0);
    }

    private void Update()
    {
        attackCooldown = Mathf.Max(0f, attackCooldown - Time.deltaTime);

        bool canInteract = exitTarget != null && Vector2.Distance(player.transform.position, exitTarget.position) <= InteractionDistance;
        promptText.enabled = canInteract;

        if (canInteract && Input.GetKeyDown(KeyCode.E))
        {
            TryMoveNextStage();
        }

        if (Input.GetKeyDown(KeyCode.Space))
        {
            Attack();
        }

        UpdateHud();
    }

    public void Attack()
    {
        if (!weaponUnlocked || attackCooldown > 0f)
        {
            return;
        }

        attackCooldown = 0.3f;
        foreach (var enemy in FindObjectsByType<EnemyChaser>())
        {
            if (Vector2.Distance(player.transform.position, enemy.transform.position) <= AttackRadius)
            {
                enemy.TakeDamage(weaponLevel);
            }
        }
    }

    public void CollectItem(string itemName)
    {
        if (itemName.Contains("무기"))
        {
            weaponUnlocked = true;
            weaponLevel = Mathf.Max(1, weaponLevel + 1);
        }
        if (itemName.Contains("지도"))
        {
            hasFloorMap = true;
        }
        else
        {
            playerHealth = Mathf.Min(playerHealth + 1, MaxHealth);
        }

        objectiveText.text = "획득: " + itemName;
    }

    public void RescueStudent(string studentName)
    {
        rescuedStudents++;
        objectiveText.text = "구조 완료: " + studentName;
    }

    public void DamagePlayer()
    {
        playerHealth--;
        if (playerHealth <= 0)
        {
            FailAndRestart("체력이 0이 되었습니다.");
        }
    }

    public void TeleportHomeBossToFarm(EnemyChaser boss)
    {
        if (!stages[stageIndex].TeleportsToFarm)
        {
            return;
        }

        player.transform.position = new Vector3(-6.6f, -2.4f, 0f);
        boss.transform.position = new Vector3(2.8f, 2.1f, 0f);
        objectiveText.text = "가정실 보스가 생태농장으로 순간 이동시켰습니다.";
    }

    private void TryMoveNextStage()
    {
        if (FindObjectsByType<EnemyChaser>().Length > 0)
        {
            objectiveText.text = "아직 보스가 남아 있습니다.";
            return;
        }

        if (stageIndex >= stages.Count - 1)
        {
            if (rescuedStudents < requiredRescues)
            {
                FailAndRestart("구조 대상자를 모두 구하지 못했습니다.");
                return;
            }

            objectiveText.text = "탈출 성공: 모든 구조 대상자와 함께 운동장 미로를 통과했습니다.";
            promptText.enabled = false;
            player.enabled = false;
            if (stageRoot != null)
            {
                Destroy(stageRoot);
            }
            return;
        }

        LoadStage(stageIndex + 1);
    }

    private void FailAndRestart(string reason)
    {
        playerHealth = 5;
        weaponUnlocked = false;
        hasFloorMap = false;
        weaponLevel = 0;
        rescuedStudents = 0;
        objectiveText.text = "실패: " + reason + " 처음부터 다시 시작합니다.";
        LoadStage(0);
    }

    private void LoadStage(int index)
    {
        stageIndex = index;
        playerHealth = Mathf.Max(playerHealth, 3);

        if (stageRoot != null)
        {
            Destroy(stageRoot);
        }

        stageRoot = new GameObject("Stage - " + stages[index].Name);
        player.transform.position = new Vector3(-7.2f, 0f, 0f);
        player.enabled = true;

        DrawMap(index);
        CreateExit(index);
        CreateItem(stages[index]);
        CreateRescueTarget(stages[index]);
        CreateBosses(stages[index]);

        objectiveText.text = stages[index].Objective;
        UpdateHud();
    }

    private void DrawMap(int index)
    {
        bool hasPdfMap = CreateMapBackground(stages[index].MapResource);
        if (!hasPdfMap)
        {
            var floor = CreateBox("Floor", Vector2.zero, new Vector2(18f, 10f), ColorForStage(index));
            SetSorting(floor, -40);
            floor.transform.SetParent(stageRoot.transform);
        }

        CreateStyledFloor(index);
        CreateWalls();

        if (stageIndex == stages.Count - 1)
        {
            CreateMaze();
            return;
        }

        for (int i = 0; i < 4; i++)
        {
            var room = CreateBox("Room " + (i + 1), new Vector2(-4.8f + i * 3.1f, 3.1f), new Vector2(2.2f, 1.25f), new Color(0.82f, 0.86f, 0.76f));
            SetSorting(room, -18);
            room.transform.SetParent(stageRoot.transform);
            CreateClassroomSet(new Vector2(-4.8f + i * 3.1f, 3.1f), i);
        }

        var hallway = CreateBox("Hallway", new Vector2(0f, -0.4f), new Vector2(13f, 1.3f), new Color(0.38f, 0.39f, 0.38f));
        SetSorting(hallway, -19);
        hallway.transform.SetParent(stageRoot.transform);
        CreateRestroomArea(new Vector2(5.8f, 3.05f));
        CreateBlockedStairs(new Vector2(-6.9f, -2.9f));

        if (stages[stageIndex].TeleportsToFarm)
        {
            var farm = CreateBox("생태농장", new Vector2(3.8f, -2.7f), new Vector2(5f, 2.4f), new Color(0.22f, 0.46f, 0.24f));
            farm.transform.SetParent(stageRoot.transform);
        }
    }

    private void CreateStyledFloor(int index)
    {
        if (index == 0)
        {
            CreateTexturedBox("Green Waterproof Paint", "roof_green_paint", Vector2.zero, new Vector2(18f, 10f), -35, new Color(1f, 1f, 1f, 0.86f));
            return;
        }

        if (index == stages.Count - 1)
        {
            CreateTexturedBox("Playground Track", "playground_track", Vector2.zero, new Vector2(18f, 9f), -35, new Color(1f, 1f, 1f, 0.9f));
            return;
        }

        if (CreateClassroomFloorTexture())
        {
            return;
        }

        var floor = CreateBox("Classroom Floor", Vector2.zero, new Vector2(18f, 10f), new Color(0.80f, 0.57f, 0.30f, 0.80f));
        SetSorting(floor, -35);
        floor.transform.SetParent(stageRoot.transform);
    }

    private bool CreateClassroomFloorTexture()
    {
        var texture = Resources.Load<Texture2D>("Textures/classroom_floor");
        if (texture == null)
        {
            return false;
        }

        var sprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f), 100f);
        var floor = new GameObject("Classroom Floor Texture");
        floor.transform.SetParent(stageRoot.transform);
        floor.transform.position = new Vector3(0f, 0f, 0.12f);

        var renderer = floor.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.color = new Color(1f, 1f, 1f, 0.82f);
        renderer.sortingOrder = -35;

        float targetWidth = 18f;
        float spriteWidth = texture.width / 100f;
        float scale = targetWidth / spriteWidth;
        floor.transform.localScale = new Vector3(scale, scale * 0.58f, 1f);
        return true;
    }

    private void CreateClassroomSet(Vector2 center, int index)
    {
        CreateTexturedBox("Whiteboard", "whiteboard_clean", center + new Vector2(0f, 0.45f), new Vector2(1.55f, 0.38f), -8, Color.white);

        CreateTexturedBox("Lectern", "lectern_ref", center + new Vector2(-0.58f, -0.28f), new Vector2(0.66f, 0.38f), -7, Color.white);

        CreateTexturedBox("Teacher Computer Setup", "teacher_setup_ref", center + new Vector2(-0.58f, 0.05f), new Vector2(0.62f, 0.40f), -6, Color.white);

        for (int row = 0; row < 2; row++)
        {
            for (int col = 0; col < 3; col++)
            {
                CreateTexturedBox("Student Desk", "student_desk_ref", center + new Vector2(-0.45f + col * 0.45f, -0.02f - row * 0.32f), new Vector2(0.36f, 0.24f), -9, Color.white);
            }
        }
    }

    private void CreateBlockedStairs(Vector2 center)
    {
        var stairs = CreateBox("Stairs", center, new Vector2(1.35f, 1.05f), new Color(0.78f, 0.78f, 0.72f));
        SetSorting(stairs, -8);
        stairs.transform.SetParent(stageRoot.transform);

        for (int i = 0; i < 5; i++)
        {
            var step = CreateBox("Stair Step", center + new Vector2(0f, -0.38f + i * 0.18f), new Vector2(1.1f, 0.035f), new Color(0.35f, 0.35f, 0.32f));
            SetSorting(step, -7);
            step.transform.SetParent(stageRoot.transform);
        }

        for (int i = 0; i < 4; i++)
        {
            var desk = CreateTexturedBox("Desk Barricade", "desk_barricade_ref", center + new Vector2(-0.54f + i * 0.36f, -0.62f), new Vector2(0.58f, 0.24f), -5, Color.white);
            desk.AddComponent<BoxCollider2D>();
        }
    }

    private void CreateRestroomArea(Vector2 center)
    {
        CreateTexturedBox("Game Restroom Tile", "restroom_tile", center, new Vector2(1.8f, 1.1f), -12, new Color(1f, 1f, 1f, 0.95f));

        for (int i = 0; i < 3; i++)
        {
            var stall = CreateBox("Toilet Stall", center + new Vector2(-0.55f + i * 0.55f, 0.2f), new Vector2(0.34f, 0.42f), new Color(0.94f, 0.94f, 0.90f));
            SetSorting(stall, -6);
            stall.transform.SetParent(stageRoot.transform);
        }

        for (int i = 0; i < 2; i++)
        {
            var sink = CreateBox("Sink", center + new Vector2(-0.3f + i * 0.6f, -0.32f), new Vector2(0.28f, 0.18f), new Color(0.88f, 0.96f, 1f));
            SetSorting(sink, -6);
            sink.transform.SetParent(stageRoot.transform);
        }
    }

    private void CreateMaze()
    {
        Vector2[] positions =
        {
            new Vector2(-5.8f, 2f), new Vector2(-4.1f, -1.7f), new Vector2(-2.1f, 1.6f),
            new Vector2(0.1f, -1.6f), new Vector2(2.1f, 1.7f), new Vector2(4.0f, -1.5f),
            new Vector2(5.7f, 1.9f)
        };

        for (int i = 0; i < positions.Length; i++)
        {
            var maze = CreateBox("Maze Wall " + i, positions[i], new Vector2(1.1f, 3.6f), new Color(0.18f, 0.28f, 0.2f));
            SetSorting(maze, -4);
            maze.AddComponent<BoxCollider2D>();
            maze.transform.SetParent(stageRoot.transform);
        }
    }

    private bool CreateMapBackground(string mapResource)
    {
        var texture = Resources.Load<Texture2D>("Maps/" + mapResource);
        if (texture == null)
        {
            return false;
        }

        var sprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f), 100f);
        var background = new GameObject("PDF Map - " + mapResource);
        background.transform.SetParent(stageRoot.transform);
        background.transform.position = new Vector3(0f, 0f, 0.2f);

        var renderer = background.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.color = new Color(1f, 1f, 1f, 0.28f);
        renderer.sortingOrder = -45;

        float targetWidth = 17.5f;
        float spriteWidth = texture.width / 100f;
        float scale = targetWidth / spriteWidth;
        background.transform.localScale = Vector3.one * scale;
        return true;
    }

    private void CreateWalls()
    {
        Vector2[] positions =
        {
            new Vector2(0f, 5.25f),
            new Vector2(0f, -5.25f),
            new Vector2(-9.25f, 0f),
            new Vector2(9.25f, 0f),
        };
        Vector2[] sizes =
        {
            new Vector2(18.5f, 0.45f),
            new Vector2(18.5f, 0.45f),
            new Vector2(0.45f, 10.5f),
            new Vector2(0.45f, 10.5f),
        };

        for (int i = 0; i < positions.Length; i++)
        {
            var wall = CreateBox("White Wall", positions[i], sizes[i], new Color(0.96f, 0.96f, 0.93f));
            SetSorting(wall, -3);
            wall.AddComponent<BoxCollider2D>();
            wall.transform.SetParent(stageRoot.transform);
        }
    }

    private void CreateExit(int index)
    {
        Vector2 position = new Vector2(7.6f, 0f);
        var trigger = new GameObject(index == stages.Count - 1 ? "Exit Trigger" : "Next Area Trigger");
        trigger.transform.position = position;
        trigger.transform.SetParent(stageRoot.transform);
        exitTarget = trigger.transform;

        var rail = CreateBox("Sliding Door Rail", position + new Vector2(0f, 1.15f), new Vector2(1.35f, 0.08f), new Color(0.32f, 0.34f, 0.35f));
        SetSorting(rail, -2);
        rail.transform.SetParent(stageRoot.transform);

        var leftPanel = CreateBox("Sliding Door Left Panel", position + new Vector2(-0.32f, 0f), new Vector2(0.58f, 2.1f), new Color(0.84f, 0.91f, 0.94f));
        SetSorting(leftPanel, -2);
        leftPanel.transform.SetParent(stageRoot.transform);

        var rightPanel = CreateBox("Sliding Door Right Panel", position + new Vector2(0.32f, 0f), new Vector2(0.58f, 2.1f), new Color(0.76f, 0.86f, 0.90f));
        SetSorting(rightPanel, -2);
        rightPanel.transform.SetParent(stageRoot.transform);

        var handleLeft = CreateBox("Sliding Door Handle", position + new Vector2(-0.06f, 0f), new Vector2(0.05f, 0.28f), new Color(0.48f, 0.52f, 0.54f));
        SetSorting(handleLeft, -1);
        handleLeft.transform.SetParent(stageRoot.transform);

        var handleRight = CreateBox("Sliding Door Handle", position + new Vector2(0.06f, 0f), new Vector2(0.05f, 0.28f), new Color(0.48f, 0.52f, 0.54f));
        SetSorting(handleRight, -1);
        handleRight.transform.SetParent(stageRoot.transform);
    }

    private void CreateItem(StageInfo stage)
    {
        Vector2 itemPosition = stageIndex == 0 ? new Vector2(-1.2f, 2.5f) : new Vector2(-4.6f, -2.7f);
        var item = CreateBox(stage.ItemName, itemPosition, new Vector2(0.55f, 0.55f), new Color(0.96f, 0.78f, 0.18f));
        item.AddComponent<CircleCollider2D>().isTrigger = true;
        item.AddComponent<CollectibleItem>().Initialize(this, stage.ItemName);
        item.transform.SetParent(stageRoot.transform);
    }

    private void CreateRescueTarget(StageInfo stage)
    {
        var target = CreateBox(stage.RescueName, new Vector2(0.2f, 2.65f), new Vector2(0.65f, 0.65f), new Color(0.22f, 0.8f, 0.82f));
        target.AddComponent<CircleCollider2D>().isTrigger = true;
        target.AddComponent<RescueTarget>().Initialize(this, stage.RescueName);
        target.transform.SetParent(stageRoot.transform);
    }

    private void CreateBosses(StageInfo stage)
    {
        for (int i = 0; i < stage.BossCount; i++)
        {
            var boss = CreateBox(stage.BossName, new Vector2(-1.6f + i * 3.2f, i % 2 == 0 ? 1.45f : -1.45f), Vector2.one * 0.95f, new Color(0.78f, 0.16f, 0.18f));
            boss.AddComponent<CircleCollider2D>().isTrigger = true;

            var body = boss.AddComponent<Rigidbody2D>();
            body.gravityScale = 0f;
            body.bodyType = RigidbodyType2D.Kinematic;

            var chaser = boss.AddComponent<EnemyChaser>();
            chaser.Initialize(this, player.transform, stage.BossHealth, 2.8f + stageIndex * 0.15f, stage.TeleportsToFarm);
            boss.transform.SetParent(stageRoot.transform);
        }
    }

    private void CreatePlayer()
    {
        var playerObject = CreateBox("Player", new Vector2(-7.2f, 0f), new Vector2(0.8f, 0.8f), new Color(0.12f, 0.5f, 0.92f));
        var body = playerObject.AddComponent<Rigidbody2D>();
        body.gravityScale = 0f;
        body.freezeRotation = true;
        playerObject.AddComponent<CircleCollider2D>();
        player = playerObject.AddComponent<PlayerMotor>();
    }

    private void CreateCamera()
    {
        Camera camera = Camera.main;
        if (camera == null)
        {
            var cameraObject = new GameObject("Main Camera");
            camera = cameraObject.AddComponent<Camera>();
            camera.tag = "MainCamera";
        }

        camera.orthographic = true;
        camera.orthographicSize = 5.8f;
        camera.backgroundColor = new Color(0.07f, 0.08f, 0.09f);
        camera.transform.position = new Vector3(0f, 0f, -10f);
    }

    private void CreateUi()
    {
        var canvasObject = new GameObject("Game UI");
        var canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvasObject.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        canvasObject.AddComponent<GraphicRaycaster>();

        objectiveText = CreateText(canvas.transform, "Objective", new Vector2(0f, 34f), new Vector2(820f, 46f), 17, TextAnchor.MiddleCenter);
        statusText = CreateText(canvas.transform, "Status", new Vector2(20f, -20f), new Vector2(360f, 54f), 16, TextAnchor.UpperLeft);
        positionText = CreateText(canvas.transform, "Player Position", new Vector2(20f, -76f), new Vector2(360f, 32f), 12, TextAnchor.UpperLeft);
        taskText = CreateText(canvas.transform, "Task", new Vector2(20f, -116f), new Vector2(330f, 72f), 14, TextAnchor.UpperLeft);
        mapText = CreateText(canvas.transform, "Floor Map", new Vector2(20f, -196f), new Vector2(330f, 210f), 12, TextAnchor.UpperLeft);
        mapPreview = CreateRawImage(canvas.transform, "Floor Map Preview", new Vector2(20f, -226f), new Vector2(330f, 120f));
        promptText = CreateText(canvas.transform, "Prompt", new Vector2(0f, 92f), new Vector2(600f, 42f), 20, TextAnchor.MiddleCenter);
        promptText.text = "E: 다음 구역 / Space: 공격";
        promptText.enabled = false;
    }

    private void UpdateHud()
    {
        statusText.text = "상태\n체력 " + playerHealth + " / 구조 " + rescuedStudents + "-" + requiredRescues + " / 무기 Lv." + weaponLevel;
        positionText.text = player == null ? "" : "위치 X " + player.transform.position.x.ToString("0.0") + " / Y " + player.transform.position.y.ToString("0.0");
        taskText.text = "해야 할 일\n" + stages[stageIndex].Objective;
        mapText.enabled = hasFloorMap;
        mapText.text = hasFloorMap ? "해당 층 지도" : "";
        mapPreview.enabled = hasFloorMap;
        mapPreview.texture = hasFloorMap ? Resources.Load<Texture2D>("Maps/" + stages[stageIndex].MapResource) : null;
    }

    private string BuildFloorMap()
    {
        switch (stageIndex)
        {
            case 0:
                return "[옥상]\nP 시작  |  지도/무기\n문 -> 4층";
            case 1:
                return "[4층]\n물리실 -- 복도 -- 지구과학실\n보스 x2  |  다음 구역 ->";
            case 2:
                return "[3층]\n화학실 -- 복도 -- 창의융합실\n구조 대상  |  보스 x2";
            case 3:
                return "[도서관]\n서가  서가  서가\n구조 대상  |  보스";
            case 4:
                return "[2층]\n학년실 -- 복도 -- 전산실\n장비  |  보스 x2";
            case 5:
                return "[1층]\n가정실 -> 순간이동 -> 생태농장\n농장 탈출로 ->";
            default:
                return "[운동장 미로]\nP -> 벽 사이 길 -> 마지막 보스 -> 출구";
        }
    }

    private static void EnsureEventSystem()
    {
        if (FindAnyObjectByType<EventSystem>() == null)
        {
            var eventSystem = new GameObject("EventSystem");
            eventSystem.AddComponent<EventSystem>();
            eventSystem.AddComponent<StandaloneInputModule>();
        }
    }

    private static GameObject CreateBox(string name, Vector2 position, Vector2 size, Color color)
    {
        var gameObject = new GameObject(name);
        gameObject.transform.position = position;
        gameObject.transform.localScale = size;
        var renderer = gameObject.AddComponent<SpriteRenderer>();
        renderer.sprite = SpriteFactory.White;
        renderer.color = color;
        return gameObject;
    }

    private GameObject CreateTexturedBox(string name, string resourceName, Vector2 position, Vector2 size, int sortingOrder, Color color)
    {
        var texture = Resources.Load<Texture2D>("Textures/" + resourceName);
        if (texture == null)
        {
            var fallback = CreateBox(name, position, size, color);
            SetSorting(fallback, sortingOrder);
            fallback.transform.SetParent(stageRoot.transform);
            return fallback;
        }

        var sprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f), 100f);
        var gameObject = new GameObject(name);
        gameObject.transform.position = position;
        gameObject.transform.SetParent(stageRoot.transform);

        var renderer = gameObject.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.color = color;
        renderer.sortingOrder = sortingOrder;

        float spriteWidth = texture.width / 100f;
        float spriteHeight = texture.height / 100f;
        gameObject.transform.localScale = new Vector3(size.x / spriteWidth, size.y / spriteHeight, 1f);
        return gameObject;
    }

    private static void SetSorting(GameObject gameObject, int order)
    {
        var renderer = gameObject.GetComponent<SpriteRenderer>();
        if (renderer != null)
        {
            renderer.sortingOrder = order;
        }
    }

    private static Text CreateText(Transform parent, string name, Vector2 anchoredPosition, Vector2 size, int fontSize, TextAnchor alignment)
    {
        var gameObject = new GameObject(name);
        gameObject.transform.SetParent(parent, false);
        var rect = gameObject.AddComponent<RectTransform>();
        rect.anchorMin = alignment == TextAnchor.UpperRight ? new Vector2(1f, 1f) : alignment == TextAnchor.MiddleCenter ? new Vector2(0.5f, 0.5f) : new Vector2(0f, 1f);
        rect.anchorMax = rect.anchorMin;
        rect.pivot = alignment == TextAnchor.UpperRight ? new Vector2(1f, 1f) : alignment == TextAnchor.MiddleCenter ? new Vector2(0.5f, 0.5f) : new Vector2(0f, 1f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;

        var text = gameObject.AddComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = fontSize;
        text.alignment = alignment;
        text.color = Color.white;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Truncate;
        return text;
    }

    private static RawImage CreateRawImage(Transform parent, string name, Vector2 anchoredPosition, Vector2 size)
    {
        var gameObject = new GameObject(name);
        gameObject.transform.SetParent(parent, false);
        var rect = gameObject.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;

        var image = gameObject.AddComponent<RawImage>();
        image.color = new Color(1f, 1f, 1f, 0.92f);
        image.enabled = false;
        return image;
    }

    private static Color ColorForStage(int index)
    {
        Color[] colors =
        {
            new Color(0.25f, 0.30f, 0.36f),
            new Color(0.34f, 0.37f, 0.42f),
            new Color(0.42f, 0.39f, 0.35f),
            new Color(0.30f, 0.28f, 0.22f),
            new Color(0.29f, 0.36f, 0.44f),
            new Color(0.22f, 0.42f, 0.26f),
            new Color(0.18f, 0.31f, 0.22f),
        };
        return colors[Mathf.Clamp(index, 0, colors.Length - 1)];
    }

    private sealed class StageInfo
    {
        public readonly string Name;
        public readonly string Objective;
        public readonly string BossName;
        public readonly int BossCount;
        public readonly int BossHealth;
        public readonly bool TeleportsToFarm;
        public readonly string ItemName;
        public readonly string RescueName;
        public readonly string MapResource;

        public StageInfo(string name, string objective, string bossName, int bossCount, int bossHealth, bool teleportsToFarm, string itemName, string rescueName, string mapResource)
        {
            Name = name;
            Objective = objective;
            BossName = bossName;
            BossCount = bossCount;
            BossHealth = bossHealth;
            TeleportsToFarm = teleportsToFarm;
            ItemName = itemName;
            RescueName = rescueName;
            MapResource = mapResource;
        }
    }
}

public sealed class PlayerMotor : MonoBehaviour
{
    private Rigidbody2D body;
    private readonly float speed = 5.4f;

    private void Awake()
    {
        body = GetComponent<Rigidbody2D>();
    }

    private void FixedUpdate()
    {
        Vector2 movement = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical")).normalized;
        body.MovePosition(body.position + movement * speed * Time.fixedDeltaTime);
    }
}

public sealed class EnemyChaser : MonoBehaviour
{
    private SchoolEscapeGame game;
    private Transform target;
    private Rigidbody2D body;
    private int health;
    private float hitCooldown;
    private float teleportCooldown = 2.2f;
    private float speed;
    private bool teleportsToFarm;

    public void Initialize(SchoolEscapeGame owner, Transform playerTarget, int startHealth, float chaseSpeed, bool canTeleport)
    {
        game = owner;
        target = playerTarget;
        health = startHealth;
        speed = chaseSpeed;
        teleportsToFarm = canTeleport;
    }

    private void Awake()
    {
        body = GetComponent<Rigidbody2D>();
    }

    private void FixedUpdate()
    {
        if (target == null)
        {
            return;
        }

        Vector2 nextPosition = Vector2.MoveTowards(body.position, target.position, speed * Time.fixedDeltaTime);
        body.MovePosition(nextPosition);
    }

    private void Update()
    {
        if (target == null)
        {
            return;
        }

        hitCooldown = Mathf.Max(0f, hitCooldown - Time.deltaTime);
        teleportCooldown = Mathf.Max(0f, teleportCooldown - Time.deltaTime);

        float distance = Vector2.Distance(transform.position, target.position);
        if (teleportsToFarm && teleportCooldown <= 0f && distance < 1.2f)
        {
            teleportCooldown = 5f;
            game.TeleportHomeBossToFarm(this);
        }

        if (hitCooldown <= 0f && distance < 0.7f)
        {
            hitCooldown = 1f;
            game.DamagePlayer();
        }
    }

    public void TakeDamage(int amount)
    {
        health -= amount;
        transform.localScale *= 0.9f;
        if (health <= 0)
        {
            Destroy(gameObject);
        }
    }
}

public sealed class CollectibleItem : MonoBehaviour
{
    private SchoolEscapeGame game;
    private string itemName;

    public void Initialize(SchoolEscapeGame owner, string displayName)
    {
        game = owner;
        itemName = displayName;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.GetComponent<PlayerMotor>() == null)
        {
            return;
        }

        game.CollectItem(itemName);
        Destroy(gameObject);
    }
}

public sealed class RescueTarget : MonoBehaviour
{
    private SchoolEscapeGame game;
    private string rescueName;
    private bool rescued;

    public void Initialize(SchoolEscapeGame owner, string displayName)
    {
        game = owner;
        rescueName = displayName;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (rescued || other.GetComponent<PlayerMotor>() == null)
        {
            return;
        }

        rescued = true;
        game.RescueStudent(rescueName);
        Destroy(gameObject);
    }
}

internal static class SpriteFactory
{
    private static Sprite white;

    public static Sprite White
    {
        get
        {
            if (white != null)
            {
                return white;
            }

            var texture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            texture.SetPixel(0, 0, Color.white);
            texture.Apply();
            white = Sprite.Create(texture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 1f);
            return white;
        }
    }
}
