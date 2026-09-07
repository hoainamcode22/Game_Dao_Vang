using System.IO;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Tilemaps;
using UnityEngine.EventSystems;
using TMPro;
using UnityEngine.TextCore.LowLevel;

#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Tilemaps;
using UnityEditor.EditorTools;
using PlatformerGame.Core;
using PlatformerGame.CameraSystem;
using PlatformerGame.Managers;
using PlatformerGame.UI;
using PlatformerGame.Gameplay;

namespace PlatformerGame.EditorTools
{
    /// <summary>
    /// Công cụ Editor tự động thiết lập toàn bộ dự án 2D Mario Platformer:
    /// 1. Quét và tạo Tile assets cho TOÀN BỘ 65 Tiles và 18 Items từ Kenney Pack.
    /// 2. Tự động tạo Palette Prefab (Assets/_PlatformerGame/Palettes/Kenney_Platformer_Palette.prefab) với Grid & Layer1 Tilemap xếp toàn bộ 83 tiles theo lưới 8 cột.
    /// 3. Tạo Scene_Adventure với:
    ///    - Camera Background Mario Sky Blue: Color(0.45f, 0.76f, 0.98f).
    ///    - 5 lớp Tilemap: Background (-10), Ground (0), Platforms (1), Hazards (2 - HazardDamager), Props (3).
    ///    - Màn chơi mẫu sinh động (mây trời, đồi xanh, đảo đất cỏ, bục bay, hộp ?, gai, xu, cột cờ).
    /// 4. Tạo Scene_Home với UI Menu hoàn chỉnh.
    /// 5. Cấu hình EventSystem chuẩn InputSystemUIInputModule và đăng ký Build Settings.
    /// </summary>
    public static class PlatformerSetupEditorTool
    {
        private const string SCENES_DIR = "Assets/Scenes";
        private const string ROOT_GAME_DIR = "Assets/_PlatformerGame";
        private const string TILES_DIR = "Assets/_PlatformerGame/Tiles";
        private const string PALETTES_DIR = "Assets/_PlatformerGame/Palettes";
        private const string PALETTE_PREFAB_PATH = "Assets/_PlatformerGame/Palettes/Kenney_Platformer_Palette.prefab";
        private const string PREFABS_DIR = "Assets/_PlatformerGame/Prefabs";

        private const string KENNEY_ROOT = "Assets/kenney_simplified-platformer-pack/PNG";
        private const string KENNEY_TILES_DIR = "Assets/kenney_simplified-platformer-pack/PNG/Tiles";
        private const string KENNEY_ITEMS_DIR = "Assets/kenney_simplified-platformer-pack/PNG/Items";
        private const string KENNEY_CHARS_DIR = "Assets/kenney_simplified-platformer-pack/PNG/Characters";

        private const string HOME_SCENE_PATH = "Assets/Scenes/Scene_Home.unity";
        private const string ADVENTURE_SCENE_PATH = "Assets/Scenes/Scene_Adventure.unity";

        private const string FONT_TTF_PATH = "Assets/_PlatformerGame/Fredoka.ttf";
        private const string FONT_ASSET_PATH = "Assets/_PlatformerGame/Fredoka_SDF.asset";
        private const string TITLE_MAT_PATH = "Assets/_PlatformerGame/Fredoka_Title_Shadow.mat";
        private const string BUTTON_MAT_PATH = "Assets/_PlatformerGame/Fredoka_Button_Shadow.mat";

        private static readonly Color MARIO_SKY_BLUE = new Color(0.45f, 0.76f, 0.98f, 1f);

        [MenuItem("Tools/Platformer Game/Setup Full Adventure Game (Màn chơi mẫu)", false, 1)]
        public static void SetupFullGame()
        {
            if (!EditorUtility.DisplayDialog("Setup 2D Platformer Game",
                "Bạn có muốn tự động tạo Scene_Adventure với Màn chơi mẫu đầy đủ để tham khảo?",
                "Bắt đầu Setup", "Hủy"))
            {
                return;
            }

            SetupGameInternal(isCleanMap: false);
        }

        [MenuItem("Tools/Platformer Game/Setup Clean Empty Map (Tạo Map Trống Tự Vẽ)", false, 2)]
        public static void SetupCleanEmptyMap()
        {
            if (!EditorUtility.DisplayDialog("Setup Map Trống",
                "Bạn có muốn tạo Scene_Adventure dạng Map Trống sạch sẽ (chỉ giữ Player, Camera, HUD và Grid 5 lớp) để tự do vẽ từ đầu?",
                "Tạo Map Trống", "Hủy"))
            {
                return;
            }

            SetupGameInternal(isCleanMap: true);
        }

        [MenuItem("Tools/Platformer Game/Clear All Painted Tiles (Xóa sạch gạch trong Scene)", false, 3)]
        public static void ClearAllPaintedTiles()
        {
            var tilemaps = Object.FindObjectsOfType<Tilemap>();
            if (tilemaps == null || tilemaps.Length == 0)
            {
                EditorUtility.DisplayDialog("Thông báo", "Không tìm thấy Tilemap nào trong Scene hiện tại!", "OK");
                return;
            }

            foreach (var tm in tilemaps)
            {
                tm.ClearAllTiles();
                EditorUtility.SetDirty(tm);
            }
            foreach (var comp in Object.FindObjectsOfType<CompositeCollider2D>())
            {
                comp.GenerateGeometry();
            }

            EditorUtility.DisplayDialog("Thành công", "Đã xóa sạch toàn bộ các ô gạch trên tất cả Tilemap trong Scene!\nBây giờ Scene hoàn toàn trống để Sếp tự vẽ.", "Tuyệt vời");
        }

        [MenuItem("Tools/Platformer Game/Create All Kenney Tiles & Palette", false, 4)]
        public static void CreateTilesAndPaletteOnly()
        {
            try
            {
                EditorUtility.DisplayProgressBar("Tile Generator", "Đang tạo thư mục...", 0.1f);
                EnsureDirectories();

                EditorUtility.DisplayProgressBar("Tile Generator", "Đang quét và tạo Tiles & Items...", 0.4f);
                Dictionary<string, Tile> tilesDict = CreateAllTileAssets(out List<Tile> allTilesList);

                EditorUtility.DisplayProgressBar("Tile Generator", "Đang tạo Tile Palette Prefab...", 0.8f);
                CreatePalettePrefab(allTilesList);

                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                EditorUtility.DisplayDialog("Thành công",
                    $"Đã tạo thành công {allTilesList.Count} Tiles & Items và Palette Prefab tại:\n{PALETTE_PREFAB_PATH}", "OK");
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        [MenuItem("Tools/Platformer Game/Update UI (Chỉ Scene Hiện Tại - Tuyệt Đối An Toàn)", false, 5)]
        public static void UpdateCurrentSceneUIOnly()
        {
            try
            {
                var existingCanvas = GameObject.Find("UI_Canvas");
                if (existingCanvas != null)
                {
                    Object.DestroyImmediate(existingCanvas);
                }

                var playerObj = GameObject.Find("Player_Character");
                
                string currentSceneName = EditorSceneManager.GetActiveScene().name;
                int stageIndex = 1;
                if (currentSceneName.Contains("Stage_") && int.TryParse(currentSceneName.Replace("Stage_", ""), out int idx))
                {
                    stageIndex = idx;
                }

                CreateCanvasWithHUD(playerObj, stageIndex);

                var gm = Object.FindObjectOfType<PlatformerGameManager>();
                if (gm != null && playerObj != null)
                {
                    SetPrivateField(gm, "player", playerObj.GetComponent<PlayerController2D>());
                }

                CreateEventSystem();

                EditorUtility.DisplayDialog("Thành công!",
                    "✅ Đã cập nhật xong UI HUD & Nút bấm mới cho Scene hiện tại!\n" +
                    "🛡️ Giữ nguyên 100% tất cả các vật phẩm, coins, gạch decor bạn đang đặt.", "Tuyệt vời");
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[UpdateUI] Lỗi: {ex.Message}");
            }
        }

        [MenuItem("Tools/Platformer Game/Update All Stage UIs & Touch Controls (Nâng Cấp UI Cho Toàn Bộ 8 Stage)", false, 6)]
        public static void UpdateAllStageScenesUI()
        {
            if (!EditorUtility.DisplayDialog("Xác nhận cập nhật 8 Stage",
                "⚠️ Lệnh này sẽ mở lần lượt 8 file Scene từ Stage_1 đến Stage_8 để cập nhật UI Canvas.\n\n" +
                "👉 Hãy đảm bảo bạn đã bấm Ctrl + S để lưu công việc hiện tại trước khi chạy!", "Tiếp tục", "Hủy"))
            {
                return;
            }

            try
            {
                string activeScenePath = EditorSceneManager.GetActiveScene().path;

                for (int i = 1; i <= 8; i++)
                {
                    string scenePath = $"Assets/Scenes/Stage_{i}.unity";
                    if (!System.IO.File.Exists(scenePath)) continue;

                    var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
                    
                    var existingCanvas = GameObject.Find("UI_Canvas");
                    if (existingCanvas != null)
                    {
                        Object.DestroyImmediate(existingCanvas);
                    }

                    var playerObj = GameObject.Find("Player_Character");
                    CreateCanvasWithHUD(playerObj, i);

                    var gm = Object.FindObjectOfType<PlatformerGameManager>();
                    if (gm != null && playerObj != null)
                    {
                        SetPrivateField(gm, "player", playerObj.GetComponent<PlayerController2D>());
                    }

                    CreateEventSystem();
                    EditorSceneManager.SaveScene(scene);
                }

                if (!string.IsNullOrEmpty(activeScenePath) && System.IO.File.Exists(activeScenePath))
                {
                    EditorSceneManager.OpenScene(activeScenePath, OpenSceneMode.Single);
                }

                EditorUtility.DisplayDialog("Thành công!",
                    "✅ Đã nâng cấp toàn bộ UI HUD và Nút bấm cảm ứng ảo cho tất cả 8 Stage!\n\n" +
                    "- 💎 Icon Kim Cương Xanh Dương cho Scene 1.\n" +
                    "- 🖼️ Khung HUD Key & Gem tách biệt sang trọng.\n" +
                    "- 🎮 Nút cảm ứng Trái, Phải, Nhảy siêu nét, mượt mà!", "Tuyệt vời");
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[UpdateUI] Lỗi: {ex.Message}");
            }
        }

        [MenuItem("Tools/Platformer Game/Fix Brush & Activate Ground Painter (Sửa Cọ & Mở Bảng Vẽ Chuẩn)", false, 6)]
        public static void FixBrushAndActivatePainter()
        {
            try
            {
                // 0. Tạo lại Palette chuẩn Unity (GridPalette) để đảm bảo không bị lỗi nested prefab
                EnsureDirectories();
                CreateAllTileAssets(out List<Tile> allTilesList);
                CreatePalettePrefab(allTilesList);

                // 1. Chuyển Brush về Default Brush (GridBrush tiêu chuẩn để vẽ tự do)
                try
                {
                    var defaultBrush = GridPaintingState.brushes.FirstOrDefault(b => b != null && b.GetType() == typeof(GridBrush))
                                    ?? GridPaintingState.brushes.FirstOrDefault(b => b != null && (b.name == "Default Brush" || b is GridBrush));
                    if (defaultBrush != null)
                    {
                        GridPaintingState.gridBrush = defaultBrush;
                    }
                }
                catch { }

                // 2. Load và đặt Palette hợp lệ có trong danh sách palettes của Unity
                try
                {
                    var allPalettes = GridPaintingState.palettes;
                    if (allPalettes != null && allPalettes.Count > 0)
                    {
                        var targetPalette = allPalettes.FirstOrDefault(p => p != null && p.name.Contains("Kenney")) ?? allPalettes[0];
                        if (targetPalette != null)
                        {
                            GridPaintingState.palette = targetPalette;
                        }
                    }
                }
                catch { }

                // 3. Chọn đúng Layer Tilemap_Ground
                var groundObj = GameObject.Find("Tilemap_Ground");
                if (groundObj != null)
                {
                    Selection.activeGameObject = groundObj;
                    GridPaintingState.scenePaintTarget = groundObj;
                }

                // 4. Kích hoạt cọ vẽ PaintTool
                try
                {
                    ToolManager.SetActiveTool(typeof(PaintTool));
                }
                catch { }

                // 5. Mở cửa sổ Tile Palette
                EditorApplication.ExecuteMenuItem("Window/2D/Tile Palette");

                EditorUtility.DisplayDialog("Đã sửa cọ vẽ chuẩn!",
                    "✅ Đã đưa Brush về Default Brush (Không còn bị Line Brush kẹt).\n" +
                    "✅ Đã chọn layer Tilemap_Ground và kích hoạt sẵn Cọ Vẽ.\n\n" +
                    "👉 Bây giờ Sếp click 1 viên gạch trong Palette rồi rê vào Scene vẽ thỏa thích!", "Tuyệt vời");
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[FixBrush] Lỗi: {ex.Message}");
            }
        }

        [MenuItem("Tools/Platformer Game/Create All Item Prefabs (Tạo Thư Mục Prefabs Kim Cương, Tim, Chìa Khóa...)", false, 6)]
        public static void CreateAllItemPrefabs()
        {
            EnsureDirectories();

            Sprite coinGoldRound = LoadSprite("Assets/_PlatformerGame/ASSETS/coin_gold.png");
            Sprite gemBlueSprite = LoadSprite($"{KENNEY_ITEMS_DIR}/platformPack_item007.png");   // Xanh Dương (Blue Sapphire)
            Sprite gemGreenSprite = LoadSprite($"{KENNEY_ITEMS_DIR}/platformPack_item009.png");  // Xanh Lá (Green Emerald)
            Sprite gemRedSprite = LoadSprite($"{KENNEY_ITEMS_DIR}/platformPack_item010.png");    // Đỏ / Cam (Red Ruby)
            Sprite gemYellowSprite = LoadSprite($"{KENNEY_ITEMS_DIR}/platformPack_item008.png"); // Vàng (Yellow Topaz)

            // 1. Prefab Kim Cương Xanh Dương (Item_Gem_Blue & Item_Gem) - +5 Coins
            GameObject gemBlueObj = new GameObject("Item_Gem_Blue");
            var gbsr = gemBlueObj.AddComponent<SpriteRenderer>();
            gbsr.sprite = gemBlueSprite ?? LoadSprite($"{KENNEY_ITEMS_DIR}/platformPack_item007.png");
            gbsr.sortingOrder = 6;
            var gbcol = gemBlueObj.AddComponent<CircleCollider2D>();
            gbcol.radius = 0.4f;
            gbcol.isTrigger = true;
            var gbcomp = gemBlueObj.AddComponent<GemPickup>();
            gbcomp.SetupGem(GemColorType.Blue_Sapphire, 5, false);
            PrefabUtility.SaveAsPrefabAsset(gemBlueObj, $"{PREFABS_DIR}/Item_Gem_Blue.prefab");
            PrefabUtility.SaveAsPrefabAsset(gemBlueObj, $"{PREFABS_DIR}/Item_Gem.prefab");
            Object.DestroyImmediate(gemBlueObj);

            // 2. Prefab Kim Cương Xanh Lá (Item_Gem_Green) - +10 Coins & Hồi +1 Tim Máu ❤️
            GameObject gemGreenObj = new GameObject("Item_Gem_Green");
            var ggsr = gemGreenObj.AddComponent<SpriteRenderer>();
            ggsr.sprite = gemGreenSprite ?? LoadSprite($"{KENNEY_ITEMS_DIR}/platformPack_item009.png");
            ggsr.sortingOrder = 6;
            var ggcol = gemGreenObj.AddComponent<CircleCollider2D>();
            ggcol.radius = 0.4f;
            ggcol.isTrigger = true;
            var ggcomp = gemGreenObj.AddComponent<GemPickup>();
            ggcomp.SetupGem(GemColorType.Green_Emerald, 10, true);
            PrefabUtility.SaveAsPrefabAsset(gemGreenObj, $"{PREFABS_DIR}/Item_Gem_Green.prefab");
            Object.DestroyImmediate(gemGreenObj);

            // 3. Prefab Kim Cương Đỏ (Item_Gem_Red) - +20 Coins
            GameObject gemRedObj = new GameObject("Item_Gem_Red");
            var grsr = gemRedObj.AddComponent<SpriteRenderer>();
            grsr.sprite = gemRedSprite ?? LoadSprite($"{KENNEY_ITEMS_DIR}/platformPack_item010.png");
            grsr.sortingOrder = 6;
            var grcol = gemRedObj.AddComponent<CircleCollider2D>();
            grcol.radius = 0.4f;
            grcol.isTrigger = true;
            var grcomp = gemRedObj.AddComponent<GemPickup>();
            grcomp.SetupGem(GemColorType.Red_Ruby, 20, false);
            PrefabUtility.SaveAsPrefabAsset(gemRedObj, $"{PREFABS_DIR}/Item_Gem_Red.prefab");
            Object.DestroyImmediate(gemRedObj);

            // 4. Prefab Kim Cương Vàng (Item_Gem_Yellow) - +5 Coins
            GameObject gemYellowObj = new GameObject("Item_Gem_Yellow");
            var gysr = gemYellowObj.AddComponent<SpriteRenderer>();
            gysr.sprite = gemYellowSprite ?? LoadSprite($"{KENNEY_ITEMS_DIR}/platformPack_item008.png");
            gysr.sortingOrder = 6;
            var gycol = gemYellowObj.AddComponent<CircleCollider2D>();
            gycol.radius = 0.4f;
            gycol.isTrigger = true;
            var gycomp = gemYellowObj.AddComponent<GemPickup>();
            gycomp.SetupGem(GemColorType.Yellow_Topaz, 5, false);
            PrefabUtility.SaveAsPrefabAsset(gemYellowObj, $"{PREFABS_DIR}/Item_Gem_Yellow.prefab");
            Object.DestroyImmediate(gemYellowObj);

            // 5. Prefab Đồng Xu Vàng Tròn Mới (Item_Coin) - +1 Coin
            GameObject coinObj = new GameObject("Item_Coin");
            var coinSr = coinObj.AddComponent<SpriteRenderer>();
            coinSr.sprite = coinGoldRound ?? LoadSprite($"{KENNEY_ITEMS_DIR}/platformPack_item008.png");
            coinSr.sortingOrder = 6;
            var coinCol = coinObj.AddComponent<CircleCollider2D>();
            coinCol.radius = 0.35f;
            coinCol.isTrigger = true;
            coinObj.AddComponent<CollectibleCoin>();
            PrefabUtility.SaveAsPrefabAsset(coinObj, $"{PREFABS_DIR}/Item_Coin.prefab");
            Object.DestroyImmediate(coinObj);

            // 6. Prefab Tim hồi máu (Item_Heart)
            GameObject heartObj = new GameObject("Item_Heart");
            var heartSr = heartObj.AddComponent<SpriteRenderer>();
            heartSr.sprite = LoadSprite($"{KENNEY_ITEMS_DIR}/platformPack_item017.png");
            heartSr.sortingOrder = 6;
            var heartCol = heartObj.AddComponent<CircleCollider2D>();
            heartCol.radius = 0.4f;
            heartCol.isTrigger = true;
            heartObj.AddComponent<HeartPickup>();
            PrefabUtility.SaveAsPrefabAsset(heartObj, $"{PREFABS_DIR}/Item_Heart.prefab");
            Object.DestroyImmediate(heartObj);

            // 7. Prefab Chìa Khóa (Item_Key)
            GameObject keyObj = new GameObject("Item_Key");
            var keySr = keyObj.AddComponent<SpriteRenderer>();
            keySr.sprite = LoadSprite($"{KENNEY_ITEMS_DIR}/platformPack_item014.png");
            keySr.sortingOrder = 6;
            var keyCol = keyObj.AddComponent<CircleCollider2D>();
            keyCol.radius = 0.5f;
            keyCol.isTrigger = true;
            keyObj.AddComponent<KeyPickup>();
            PrefabUtility.SaveAsPrefabAsset(keyObj, $"{PREFABS_DIR}/Item_Key.prefab");
            Object.DestroyImmediate(keyObj);

            // 8. Prefab Lò Xo (Item_Spring)
            GameObject springObj = new GameObject("Item_Spring");
            var springSr = springObj.AddComponent<SpriteRenderer>();
            springSr.sprite = LoadSprite($"{KENNEY_TILES_DIR}/platformPack_tile046.png");
            springSr.sortingOrder = 2;
            var springCol = springObj.AddComponent<BoxCollider2D>();
            springCol.size = new Vector2(0.8f, 0.6f);
            springCol.isTrigger = true;
            springObj.AddComponent<SpringPad>();
            PrefabUtility.SaveAsPrefabAsset(springObj, $"{PREFABS_DIR}/Item_Spring.prefab");
            Object.DestroyImmediate(springObj);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            EditorUtility.DisplayDialog("Thành công",
                "✅ Đã tạo đầy đủ các Prefab trong thư mục:\nAssets/_PlatformerGame/Prefabs/\n\n" +
                "💎 Item_Gem_Blue.prefab (+5 Xu, Kim cương Xanh Dương)\n" +
                "🟢 Item_Gem_Green.prefab (+10 Xu & Hồi +1 Tim Máu ❤️)\n" +
                "🔴 Item_Gem_Red.prefab (+20 Xu, Ruby Đỏ hiếm)\n" +
                "🟡 Item_Gem_Yellow.prefab (+5 Xu, Topaz Vàng)\n" +
                "🪙 Item_Coin.prefab (Đồng Tiền Vàng Tròn Mới dập sao)\n" +
                "❤️ Item_Heart.prefab (Tim hồi máu)\n" +
                "🔑 Item_Key.prefab (Chìa khóa qua màn)\n" +
                "🌀 Item_Spring.prefab (Lò xo bật nhảy)\n\n" +
                "👉 Sếp có thể kéo thả Prefab từ Project vào Scene hoặc Ctrl + D để nhân đôi tùy thích!", "Tuyệt vời");
        }

        [MenuItem("Tools/Platformer Game/Fix Tile Scaling (Khít 100% không hở viền - PPU 64)", false, 7)]
        public static void FixAllSpritesPPU()
        {
            try
            {
                EditorUtility.DisplayProgressBar("Tile Scaling Fix", "Đang chuyển đổi PPU 64 cho toàn bộ Sprite...", 0.3f);
                int count = FixAllSpritesPPUInternal();

                // Tạo lại Tiles và Palette
                EnsureDirectories();
                Dictionary<string, Tile> tilesDict = CreateAllTileAssets(out List<Tile> allTilesList);
                CreatePalettePrefab(allTilesList);

                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                EditorUtility.DisplayDialog("Thành công!",
                    $"✅ Đã chuẩn hóa {count} Sprite sang PPU 64 (khít sát 100% ô lưới 1x1)!\n" +
                    $"✅ Đã làm mới bảng gạch Kenney_Platformer_Palette.\n\n" +
                    "Bây giờ Sếp vẽ gạch trên Scene sẽ dính liền lạc full khung, không còn khe hở.", "Tuyệt vời");
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        [MenuItem("Tools/Platformer Game/Setup 8 Stages Designer Framework (Dàn dựng 8 Map Tự Vẽ)", false, 8)]
        public static void Setup8StagesDesignerFramework()
        {
            if (!EditorUtility.DisplayDialog("Dàn dựng 8 Map Tự Vẽ",
                "Hệ thống sẽ tự động cấu hình 8 Khu Vực Màn Chơi (Stage 1 -> Stage 8) với:\n\n" +
                "1. 8 Checkpoint / Điểm xuất phát của từng Map.\n" +
                "2. Hệ thống Chìa Khóa & Cửa Lâu Đài mở màn liên hoàn.\n" +
                "3. Kho máu (Tim hồi máu), Kim cương, Tiền vàng, Lò xo bật nhảy.\n" +
                "4. HUD hiển thị Chìa Khóa & Tên Stage.\n" +
                "5. Grid sạch sẽ chuẩn PPU 64 để Sếp tự do vẽ địa hình cho từng map.\n\n" +
                "Bắt đầu thiết lập?", "Dàn dựng ngay", "Hủy"))
            {
                return;
            }

            try
            {
                EditorUtility.DisplayProgressBar("8 Stages Setup", "Chuẩn hóa Sprite PPU 64...", 0.1f);
                FixAllSpritesPPUInternal();

                EditorUtility.DisplayProgressBar("8 Stages Setup", "Đang khởi tạo Scene 8 Màn chơi...", 0.3f);
                EnsureDirectories();
                EnsureGroundLayer();

                Dictionary<string, Tile> tilesDict = CreateAllTileAssets(out List<Tile> allTilesList);
                CreatePalettePrefab(allTilesList);

                Build8StagesScene(tilesDict);
                BuildHomeScene();
                UpdateBuildSettings();

                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                EditorSceneManager.OpenScene(ADVENTURE_SCENE_PATH);

                EditorUtility.DisplayDialog("Dàn dựng 8 Map Hoàn tất!",
                    "✅ Đã tạo xong khung sườn 8 Map liên hoàn từ Trái sang Phải!\n" +
                    "✅ Mỗi Map đã có sẵn: Cổng Xuất Phát, Chìa Khóa, Cửa Qua Màn, Tim Máu & Kim Cương.\n" +
                    "✅ Tỉ lệ gạch đã khít 100%.\n\n" +
                    "👉 Bây giờ Sếp chỉ việc chọn Tilemap_Ground và vẽ địa hình cho từng Map theo ý muốn. Vẽ xong bấm Play là chơi được luôn!", "Bắt đầu vẽ ngay");
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        [MenuItem("Tools/Platformer Game/Setup 8 Separate Scenes (Tạo 8 Scene Riêng Biệt Stage_1 -> Stage_8)", false, 9)]
        public static void Setup8SeparateScenes()
        {
            if (!EditorUtility.DisplayDialog("Tạo 8 Scene Riêng Biệt",
                "Hệ thống sẽ tạo 8 file Scene riêng biệt trong thư mục Assets/Scenes/:\n\n" +
                "- Stage_1.unity -> Stage_8.unity\n" +
                "- Mỗi Scene độc lập, có sẵn Camera, Player, Grid để Sếp tự vẽ.\n" +
                "- Cửa ở Stage 1 tự chuyển sang Stage 2, Stage 2 sang Stage 3... đến Stage 8 Chiến Thắng.\n" +
                "- Tự động đăng ký toàn bộ vào Build Settings.\n\n" +
                "Bắt đầu tạo?", "Tạo ngay 8 Scene", "Hủy"))
            {
                return;
            }

            try
            {
                EditorUtility.DisplayProgressBar("8 Separate Scenes", "Chuẩn hóa Sprite PPU 64...", 0.1f);
                FixAllSpritesPPUInternal();

                EnsureDirectories();
                EnsureGroundLayer();

                Dictionary<string, Tile> tilesDict = CreateAllTileAssets(out List<Tile> allTilesList);
                CreatePalettePrefab(allTilesList);

                string[] stageNames = new string[]
                {
                    "Thung Lũng Cỏ Xanh",
                    "Hang Đất Nâu & Bục Gỗ",
                    "Thung Lũng Lò Xo",
                    "Hẻm Núi Chông Gai",
                    "Đỉnh Mây Trời",
                    "Hầm Ngục Cưa Xoay",
                    "Mê Cung Ống Nước",
                    "Pháo Đài Tối Thượng"
                };

                var allScenePaths = new List<string> { HOME_SCENE_PATH };

                for (int i = 1; i <= 8; i++)
                {
                    EditorUtility.DisplayProgressBar("8 Separate Scenes", $"Đang tạo Scene Stage_{i}...", 0.2f + (i * 0.08f));
                    string scenePath = $"Assets/Scenes/Stage_{i}.unity";
                    string nextSceneName = (i < 8) ? $"Stage_{i + 1}" : "";
                    BuildSingleStageScene(scenePath, i, stageNames[i - 1], nextSceneName, tilesDict);
                    allScenePaths.Add(scenePath);
                }

                BuildHomeScene();

                // Cập nhật Build Settings với đầy đủ 8 scenes
                var buildScenes = new List<EditorBuildSettingsScene>();
                foreach (var p in allScenePaths)
                {
                    buildScenes.Add(new EditorBuildSettingsScene(p, true));
                }
                EditorBuildSettings.scenes = buildScenes.ToArray();

                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                // Mở Stage_1 để vẽ ngay
                EditorSceneManager.OpenScene("Assets/Scenes/Stage_1.unity");

                EditorUtility.DisplayDialog("Hoàn tất tạo 8 Scene!",
                    "✅ Đã tạo thành công 8 file Scene từ Stage_1.unity đến Stage_8.unity trong Assets/Scenes/!\n" +
                    "✅ Đã đăng ký tất cả vào Build Settings.\n" +
                    "✅ Đã kết nối Cửa chuyển Scene tự động giữa các Màn.\n\n" +
                    "👉 Đang mở Stage_1.unity. Sếp chỉ việc chọn Tilemap_Ground và vẽ Map 1, sau đó mở Stage_2 vẽ tiếp!", "Tuyệt vời");
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        private static int FixAllSpritesPPUInternal()
        {
            string[] pngGuids = AssetDatabase.FindAssets("t:Texture2D", new[] { KENNEY_ROOT });
            int count = 0;
            foreach (string guid in pngGuids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer != null)
                {
                    bool changed = false;
                    if (Mathf.Abs(importer.spritePixelsPerUnit - 64f) > 0.01f)
                    {
                        importer.spritePixelsPerUnit = 64f;
                        changed = true;
                    }
                    if (importer.filterMode != FilterMode.Point)
                    {
                        importer.filterMode = FilterMode.Point;
                        changed = true;
                    }
                    if (changed)
                    {
                        importer.SaveAndReimport();
                        count++;
                    }
                }
            }
            return count;
        }

        private static void SetupGameInternal(bool isCleanMap)
        {
            try
            {
                EditorUtility.DisplayProgressBar("Platformer Setup", "Đang chuẩn bị thư mục & Layer...", 0.05f);
                EnsureDirectories();
                EnsureGroundLayer();

                EditorUtility.DisplayProgressBar("Platformer Setup", "Đang quét & tạo 80+ Tile Assets...", 0.2f);
                Dictionary<string, Tile> tilesDict = CreateAllTileAssets(out List<Tile> allTilesList);

                EditorUtility.DisplayProgressBar("Platformer Setup", "Đang tạo Tile Palette Prefab...", 0.35f);
                CreatePalettePrefab(allTilesList);

                EditorUtility.DisplayProgressBar("Platformer Setup", "Đang xây dựng Scene_Adventure...", 0.55f);
                BuildAdventureScene(tilesDict, isCleanMap);

                EditorUtility.DisplayProgressBar("Platformer Setup", "Đang xây dựng Scene_Home...", 0.8f);
                BuildHomeScene();

                EditorUtility.DisplayProgressBar("Platformer Setup", "Đang cập nhật Build Settings...", 0.95f);
                UpdateBuildSettings();

                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                // Mở lại Scene Adventure để dev test ngay
                EditorSceneManager.OpenScene(ADVENTURE_SCENE_PATH);

                string msg = isCleanMap
                    ? "Đã tạo Map Trống thành công!\n\n- Grid 5 lớp Tilemap, Player, Camera, Mobile HUD đã kết nối.\n- Scene sạch sẽ 100% để Sếp tự do cầm cọ vẽ theo ý muốn!"
                    : "Đã tạo Game với Màn chơi mẫu thành công!\n\n- Đầy đủ 3 màn chơi mẫu sống động để Sếp tham khảo và bắt chước.";

                EditorUtility.DisplayDialog("Hoàn tất!", msg, "Bắt đầu");
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[PlatformerSetup] Lỗi trong quá trình thiết lập: {ex.Message}\n{ex.StackTrace}");
                EditorUtility.DisplayDialog("Lỗi Setup", $"Đã có lỗi xảy ra: {ex.Message}", "Đóng");
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        #region Directory & Layer Setup

        private static void EnsureDirectories()
        {
            if (!AssetDatabase.IsValidFolder(SCENES_DIR))
                AssetDatabase.CreateFolder("Assets", "Scenes");

            if (!AssetDatabase.IsValidFolder(ROOT_GAME_DIR))
                AssetDatabase.CreateFolder("Assets", "_PlatformerGame");

            if (!AssetDatabase.IsValidFolder(TILES_DIR))
                AssetDatabase.CreateFolder(ROOT_GAME_DIR, "Tiles");

            if (!AssetDatabase.IsValidFolder(PALETTES_DIR))
                AssetDatabase.CreateFolder(ROOT_GAME_DIR, "Palettes");

            if (!AssetDatabase.IsValidFolder(PREFABS_DIR))
                AssetDatabase.CreateFolder(ROOT_GAME_DIR, "Prefabs");
        }

        private static void EnsureGroundLayer()
        {
            UnityEngine.Object[] tagManagerAssets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset");
            if (tagManagerAssets == null || tagManagerAssets.Length == 0) return;

            SerializedObject tagManager = new SerializedObject(tagManagerAssets[0]);
            SerializedProperty layersProp = tagManager.FindProperty("layers");

            bool groundLayerFound = false;
            int emptyLayerIndex = -1;

            for (int i = 8; i < 32; i++)
            {
                SerializedProperty layer = layersProp.GetArrayElementAtIndex(i);
                if (layer.stringValue == "Ground")
                {
                    groundLayerFound = true;
                    break;
                }
                if (emptyLayerIndex == -1 && string.IsNullOrEmpty(layer.stringValue))
                {
                    emptyLayerIndex = i;
                }
            }

            if (!groundLayerFound && emptyLayerIndex != -1)
            {
                SerializedProperty newLayer = layersProp.GetArrayElementAtIndex(emptyLayerIndex);
                newLayer.stringValue = "Ground";
                tagManager.ApplyModifiedProperties();
                Debug.Log($"[PlatformerSetup] Đã thêm Layer 'Ground' tại Layer Index {emptyLayerIndex}");
            }
        }

        #endregion

        #region Step 1 & 2: Create All 65 Tiles, 18 Items & Palette Prefab

        /// <summary>
        /// Quét toàn bộ file PNG trong thư mục Tiles (65 files) và Items (18 files) để tạo Tile ScriptableObject
        /// </summary>
        private static Dictionary<string, Tile> CreateAllTileAssets(out List<Tile> allTilesList)
        {
            var tilesDict = new Dictionary<string, Tile>(System.StringComparer.OrdinalIgnoreCase);
            allTilesList = new List<Tile>();

            // 1. Quét Tiles (65 files)
            ProcessFolderPngs(KENNEY_TILES_DIR, "tile", tilesDict, allTilesList, Tile.ColliderType.Grid);

            // 2. Quét Items (18 files)
            ProcessFolderPngs(KENNEY_ITEMS_DIR, "item", tilesDict, allTilesList, Tile.ColliderType.None);

            // 3. Đăng ký các alias tiện dụng cho Level Builder
            RegisterSemanticAliases(tilesDict);

            AssetDatabase.SaveAssets();
            return tilesDict;
        }

        private static void ProcessFolderPngs(string folderPath, string prefix, Dictionary<string, Tile> dict, List<Tile> list, Tile.ColliderType colliderType)
        {
            if (!Directory.Exists(folderPath))
            {
                Debug.LogWarning($"[PlatformerSetup] Không tìm thấy thư mục: {folderPath}");
                return;
            }

            string[] pngFiles = Directory.GetFiles(folderPath, "*.png", SearchOption.TopDirectoryOnly);
            System.Array.Sort(pngFiles);

            foreach (string filePath in pngFiles)
            {
                string unityPath = filePath.Replace("\\", "/");
                string fileName = Path.GetFileNameWithoutExtension(unityPath); // e.g. platformPack_tile001

                Sprite sprite = LoadSprite(unityPath);
                if (sprite == null) continue;

                string tileAssetPath = $"{TILES_DIR}/Tile_{fileName}.asset";
                Tile tile = AssetDatabase.LoadAssetAtPath<Tile>(tileAssetPath);

                if (tile == null)
                {
                    tile = ScriptableObject.CreateInstance<Tile>();
                    tile.sprite = sprite;
                    tile.colliderType = colliderType;
                    AssetDatabase.CreateAsset(tile, tileAssetPath);
                }
                else
                {
                    tile.sprite = sprite;
                    tile.colliderType = colliderType;
                    EditorUtility.SetDirty(tile);
                }

                // Lưu vào danh sách Palette và Dictionary tra cứu
                list.Add(tile);
                dict[fileName] = tile;

                // Alias ngắn: tile001, item001, 001...
                int idx = fileName.IndexOf(prefix, System.StringComparison.OrdinalIgnoreCase);
                if (idx >= 0)
                {
                    string shortKey = fileName.Substring(idx); // tile001
                    dict[shortKey] = tile;
                }
            }
        }

        private static void RegisterSemanticAliases(Dictionary<string, Tile> dict)
        {
            void MapAlias(string alias, string targetKey)
            {
                if (dict.TryGetValue(targetKey, out Tile t))
                {
                    dict[alias] = t;
                }
            }

            // Grass & Dirt
            MapAlias("grass_left", "tile001");
            MapAlias("grass_mid", "tile002");
            MapAlias("grass_right", "tile003");
            MapAlias("dirt_mid", "tile004");
            MapAlias("dirt_bottom_left", "tile005");
            MapAlias("dirt_bottom_mid", "tile006");
            MapAlias("dirt_bottom_right", "tile007");

            // Floating platforms
            MapAlias("platform_left", "tile016");
            MapAlias("platform_mid", "tile017");
            MapAlias("platform_right", "tile018");
            MapAlias("platform_single", "tile019");

            // Wood / Stone platforms
            MapAlias("wood_left", "tile020");
            MapAlias("wood_mid", "tile021");
            MapAlias("wood_right", "tile022");
            MapAlias("stone_left", "tile023");
            MapAlias("stone_mid", "tile024");
            MapAlias("stone_right", "tile025");

            // Pipes
            MapAlias("pipe_top", "tile036");
            MapAlias("pipe_mid", "tile037");
            MapAlias("pipe_bottom", "tile038");
            MapAlias("pipe_h_left", "tile039");
            MapAlias("pipe_h_right", "tile040");

            // Crates & Question Blocks
            MapAlias("crate_wood", "tile041");
            MapAlias("box_question", "tile042");
            MapAlias("crate_metal", "tile043");

            // Hazards
            MapAlias("spikes", "tile044");
            MapAlias("spikes_hanging", "tile045");
            MapAlias("spring", "tile046");

            // Ladder & Flag & Castle
            MapAlias("ladder_top", "tile047");
            MapAlias("ladder_mid", "tile048");
            MapAlias("flag_top", "tile049");
            MapAlias("flag_mid", "tile050");
            MapAlias("flag_base", "tile051");
            MapAlias("brick_block", "tile052");
            MapAlias("castle_window", "tile053");
            MapAlias("castle_door", "tile054");

            // Hills & Bushes
            MapAlias("hill_left", "tile055");
            MapAlias("hill_mid", "tile056");
            MapAlias("hill_right", "tile057");
            MapAlias("bush", "tile058");
            MapAlias("mushroom", "tile065");

            // Clouds
            MapAlias("cloud_tl", "tile059");
            MapAlias("cloud_tm", "tile060");
            MapAlias("cloud_tr", "tile061");
            MapAlias("cloud_bl", "tile062");
            MapAlias("cloud_bm", "tile063");
            MapAlias("cloud_br", "tile064");

            // Items
            MapAlias("item_coin", "item008");
            MapAlias("item_coin_silver", "item009");
            MapAlias("item_coin_bronze", "item010");
            MapAlias("item_gem_blue", "item005");
            MapAlias("item_gem_green", "item006");
            MapAlias("item_gem_red", "item007");
            MapAlias("item_key_yellow", "item001");
            MapAlias("item_star", "item011");
            MapAlias("item_heart_full", "item017");
            MapAlias("item_heart_empty", "item018");
        }

        /// <summary>
        /// Tự động tạo Prefab Tile Palette chuẩn Unity (kèm GridPalette ScriptableObject sub-asset)
        /// </summary>
        private static void CreatePalettePrefab(List<Tile> allTilesList)
        {
            if (allTilesList == null || allTilesList.Count == 0) return;

            string oldNestedPalette = "Assets/_PlatformerGame/Palettes/New Tile Palette.prefab";
            if (File.Exists(oldNestedPalette))
            {
                AssetDatabase.DeleteAsset(oldNestedPalette);
            }

            if (File.Exists(PALETTE_PREFAB_PATH))
            {
                AssetDatabase.DeleteAsset(PALETTE_PREFAB_PATH);
            }

            GameObject palettePrefab = GridPaletteUtility.CreateNewPalette(
                PALETTES_DIR,
                "Kenney_Platformer_Palette",
                GridLayout.CellLayout.Rectangle,
                GridPalette.CellSizing.Manual,
                new Vector3(1f, 1f, 0f),
                GridLayout.CellSwizzle.XYZ
            );

            if (palettePrefab != null)
            {
                Tilemap tilemap = palettePrefab.GetComponentInChildren<Tilemap>();
                if (tilemap != null)
                {
                    int columns = 8;
                    for (int i = 0; i < allTilesList.Count; i++)
                    {
                        Tile t = allTilesList[i];
                        if (t == null) continue;

                        int col = i % columns;
                        int row = -(i / columns);

                        tilemap.SetTile(new Vector3Int(col, row, 0), t);
                    }

                    EditorUtility.SetDirty(tilemap);
                    EditorUtility.SetDirty(palettePrefab);
                    AssetDatabase.SaveAssets();
                    AssetDatabase.Refresh();
                }

                Debug.Log($"[PlatformerSetup] Đã tạo chuẩn Unity Tile Palette Prefab với {allTilesList.Count} tiles tại: {PALETTE_PREFAB_PATH}");
            }
        }

        #endregion

        #region Step 3: Build Adventure Scene (5 Tilemap Layers, Mario Sky Blue Camera, Rich Level)

        private static void BuildAdventureScene(Dictionary<string, Tile> tiles, bool isCleanMap = false)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // 1. Setup Camera với Mario Sky Blue
            GameObject cameraObj = new GameObject("Main Camera");
            Camera cam = cameraObj.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 5.5f;
            cam.backgroundColor = MARIO_SKY_BLUE;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cameraObj.AddComponent<AudioListener>();
            cameraObj.tag = "MainCamera";
            cameraObj.transform.position = new Vector3(0f, 2f, -10f);

            CameraFollow2D camFollow = cameraObj.AddComponent<CameraFollow2D>();

            // 2. Setup Grid & 5 Tilemap Layers
            GameObject gridObj = new GameObject("Grid");
            Grid grid = gridObj.AddComponent<Grid>();
            grid.cellSize = new Vector3(1f, 1f, 0f);

            int groundLayer = LayerMask.NameToLayer("Ground");
            if (groundLayer == -1) groundLayer = LayerMask.NameToLayer("Default");

            // Layer 1: Tilemap_Background (Order -10, Không collider)
            Tilemap bgTilemap = CreateTilemapLayer(gridObj.transform, "Tilemap_Background", -10, LayerMask.NameToLayer("Default"), false, false);

            // Layer 2: Tilemap_Ground (Order 0, Layer Ground, CompositeCollider2D)
            Tilemap groundTilemap = CreateTilemapLayer(gridObj.transform, "Tilemap_Ground", 0, groundLayer, true, false);

            // Layer 3: Tilemap_Platforms (Order 1, Layer Ground, CompositeCollider2D)
            Tilemap platformTilemap = CreateTilemapLayer(gridObj.transform, "Tilemap_Platforms", 1, groundLayer, true, false);

            // Layer 4: Tilemap_Hazards (Order 2, CompositeCollider2D isTrigger, gắn HazardDamager)
            Tilemap hazardTilemap = CreateTilemapLayer(gridObj.transform, "Tilemap_Hazards", 2, LayerMask.NameToLayer("Default"), true, true);
            hazardTilemap.gameObject.AddComponent<HazardDamager>();

            // Layer 5: Tilemap_Props (Order 3, Props/Pipes/Bushes/Crates)
            Tilemap propsTilemap = CreateTilemapLayer(gridObj.transform, "Tilemap_Props", 3, LayerMask.NameToLayer("Default"), false, false);

            if (isCleanMap)
            {
                // Nếu là Map Trống: Chỉ đặt 1 bệ cỏ nhỏ 4 ô ngay dưới chân Player (X: -2 đến 2) để không bị rơi lúc bấm Play
                Tile gLeft = tiles.ContainsKey("grass_left") ? tiles["grass_left"] : (tiles.ContainsKey("tile001") ? tiles["tile001"] : null);
                Tile gMid = tiles.ContainsKey("grass_mid") ? tiles["grass_mid"] : (tiles.ContainsKey("tile002") ? tiles["tile002"] : null);
                Tile gRight = tiles.ContainsKey("grass_right") ? tiles["grass_right"] : (tiles.ContainsKey("tile003") ? tiles["tile003"] : null);
                Tile dirt = tiles.ContainsKey("dirt_mid") ? tiles["dirt_mid"] : (tiles.ContainsKey("tile004") ? tiles["tile004"] : null);

                if (gMid != null)
                {
                    for (int x = -3; x <= 3; x++)
                    {
                        Tile t = (x == -3) ? (gLeft ?? gMid) : ((x == 3) ? (gRight ?? gMid) : gMid);
                        groundTilemap.SetTile(new Vector3Int(x, 0, 0), t);
                        if (dirt != null) groundTilemap.SetTile(new Vector3Int(x, -1, 0), dirt);
                    }
                }
            }
            else
            {
                // Vẽ màn chơi mẫu sinh động 3 màn
                PaintRichSampleLevel(bgTilemap, groundTilemap, platformTilemap, hazardTilemap, propsTilemap, tiles);
            }

            // Rebuild geometry cho các Composite Colliders
            RegenerateComposite(groundTilemap.gameObject);
            RegenerateComposite(platformTilemap.gameObject);
            RegenerateComposite(hazardTilemap.gameObject);

            // 3. Setup Player
            GameObject playerObj = CreatePlayerObject(groundLayer);
            camFollow.SetTarget(playerObj.transform);
            camFollow.SnapToTarget();

            // 4. Setup Fall Kill Zone
            GameObject killZoneObj = new GameObject("FallKillZone");
            killZoneObj.transform.position = new Vector3(25f, -8f, 0f);
            BoxCollider2D killBox = killZoneObj.AddComponent<BoxCollider2D>();
            killBox.isTrigger = true;
            killBox.size = new Vector2(300f, 4f);
            killZoneObj.AddComponent<FallKillZone>();

            // 5. Setup Collectible Coins dọc đường nhảy
            CreateCoins();

            // 6. Setup UI Canvas (HUD, Mobile Controls, Popup Game Over / Victory)
            GameObject canvasObj = CreateCanvasWithHUD(playerObj);

            // 7. Setup GameManager
            GameObject gmObj = new GameObject("[GameManager]");
            PlatformerGameManager gm = gmObj.AddComponent<PlatformerGameManager>();
            var playerCtrl = playerObj.GetComponent<PlayerController2D>();
            SetPrivateField(gm, "player", playerCtrl);

            // 8. Setup EventSystem
            CreateEventSystem();

            // Save Scene
            EditorSceneManager.SaveScene(scene, ADVENTURE_SCENE_PATH);
            Debug.Log($"[PlatformerSetup] Đã lưu Scene_Adventure tại {ADVENTURE_SCENE_PATH}");
        }

        private static void Build8StagesScene(Dictionary<string, Tile> tiles)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // 1. Setup Camera với Mario Sky Blue
            GameObject cameraObj = new GameObject("Main Camera");
            Camera cam = cameraObj.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 6.0f;
            cam.backgroundColor = MARIO_SKY_BLUE;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cameraObj.AddComponent<AudioListener>();
            cameraObj.tag = "MainCamera";
            cameraObj.transform.position = new Vector3(0f, 2f, -10f);

            CameraFollow2D camFollow = cameraObj.AddComponent<CameraFollow2D>();

            // 2. Setup Grid & 5 Tilemap Layers
            GameObject gridObj = new GameObject("Grid");
            Grid grid = gridObj.AddComponent<Grid>();
            grid.cellSize = new Vector3(1f, 1f, 0f);

            int groundLayer = LayerMask.NameToLayer("Ground");
            if (groundLayer == -1) groundLayer = LayerMask.NameToLayer("Default");

            CreateTilemapLayer(gridObj.transform, "Tilemap_Background", -10, LayerMask.NameToLayer("Default"), false, false);
            Tilemap groundTilemap = CreateTilemapLayer(gridObj.transform, "Tilemap_Ground", 0, groundLayer, true, false);
            CreateTilemapLayer(gridObj.transform, "Tilemap_Platforms", 1, groundLayer, true, false);
            Tilemap hazardTilemap = CreateTilemapLayer(gridObj.transform, "Tilemap_Hazards", 2, LayerMask.NameToLayer("Default"), true, true);
            hazardTilemap.gameObject.AddComponent<HazardDamager>();
            CreateTilemapLayer(gridObj.transform, "Tilemap_Props", 3, LayerMask.NameToLayer("Default"), false, false);

            // 3. Setup Player
            GameObject playerObj = CreatePlayerObject(groundLayer);
            playerObj.transform.position = new Vector3(2f, 1.5f, 0f);

            camFollow.SetTarget(playerObj.transform);
            camFollow.SnapToTarget();

            // 4. Setup Fall Kill Zone rộng suốt 8 map
            GameObject killZoneObj = new GameObject("FallKillZone");
            killZoneObj.transform.position = new Vector3(250f, -12f, 0f);
            BoxCollider2D killBox = killZoneObj.AddComponent<BoxCollider2D>();
            killBox.isTrigger = true;
            killBox.size = new Vector2(700f, 4f);
            killZoneObj.AddComponent<FallKillZone>();

            // 5. Stage Manager
            GameObject stageMgrObj = new GameObject("StageManager");
            stageMgrObj.AddComponent<StageManager>();

            // 6. Setup 8 Stage Zones Container
            GameObject stagesRoot = new GameObject("8_STAGES_FRAMEWORK");
            string[] stageNames = new string[]
            {
                "Stage 1: Thung Lũng Cỏ Xanh",
                "Stage 2: Hang Đất Nâu & Bục Gỗ",
                "Stage 3: Thung Lũng Lò Xo",
                "Stage 4: Hẻm Núi Chông Gai",
                "Stage 5: Đỉnh Mây Trời",
                "Stage 6: Hầm Ngục Cưa Xoay",
                "Stage 7: Mê Cung Ống Nước",
                "Stage 8: Pháo Đài Tối Thượng"
            };

            float stageWidth = 65f;
            StageDoor prevStageDoor = null;

            Tile gLeft = tiles.ContainsKey("grass_left") ? tiles["grass_left"] : (tiles.ContainsKey("tile001") ? tiles["tile001"] : null);
            Tile gMid = tiles.ContainsKey("grass_mid") ? tiles["grass_mid"] : (tiles.ContainsKey("tile002") ? tiles["tile002"] : null);
            Tile gRight = tiles.ContainsKey("grass_right") ? tiles["grass_right"] : (tiles.ContainsKey("tile003") ? tiles["tile003"] : null);

            for (int i = 1; i <= 8; i++)
            {
                float stageStartX = (i - 1) * stageWidth;
                GameObject stageZone = new GameObject($"--- [ STAGE {i} - {stageNames[i-1]} ] ---");
                stageZone.transform.SetParent(stagesRoot.transform);

                // A. Spawn Point
                GameObject spawnPoint = new GameObject($"Stage{i}_SpawnPoint");
                spawnPoint.transform.SetParent(stageZone.transform);
                spawnPoint.transform.position = new Vector3(stageStartX + 2f, 1f, 0f);

                if (prevStageDoor != null)
                {
                    prevStageDoor.SetNextSpawnPoint(spawnPoint.transform);
                }

                // B. Bục xuất phát nhỏ 6 ô đất để không bị rơi lúc vừa vào Stage
                int startTileX = (int)stageStartX;
                if (gMid != null)
                {
                    for (int x = startTileX; x <= startTileX + 5; x++)
                    {
                        Tile t = (x == startTileX) ? (gLeft ?? gMid) : ((x == startTileX + 5) ? (gRight ?? gMid) : gMid);
                        groundTilemap.SetTile(new Vector3Int(x, -1, 0), t);
                    }
                }

                // C. Chìa Khóa (Key Pickup)
                GameObject keyObj = new GameObject($"Stage{i}_Key");
                keyObj.transform.SetParent(stageZone.transform);
                keyObj.transform.position = new Vector3(stageStartX + 25f, 3f, 0f);
                var keySr = keyObj.AddComponent<SpriteRenderer>();
                keySr.sprite = LoadSprite($"{KENNEY_ITEMS_DIR}/platformPack_item014.png");
                var keyCol = keyObj.AddComponent<CircleCollider2D>();
                keyCol.radius = 0.5f;
                keyCol.isTrigger = true;
                var keyComp = keyObj.AddComponent<KeyPickup>();
                SetPrivateField(keyComp, "stageIndex", i);

                // D. Kho Máu / Tim hồi máu (Heart Pickup)
                GameObject heartObj = new GameObject($"Stage{i}_Heart");
                heartObj.transform.SetParent(stageZone.transform);
                heartObj.transform.position = new Vector3(stageStartX + 12f, 2.5f, 0f);
                var heartSr = heartObj.AddComponent<SpriteRenderer>();
                heartSr.sprite = LoadSprite($"{KENNEY_ITEMS_DIR}/platformPack_item017.png");
                var heartCol = heartObj.AddComponent<CircleCollider2D>();
                heartCol.radius = 0.4f;
                heartCol.isTrigger = true;
                heartObj.AddComponent<HeartPickup>();

                // E. Kim Cương thưởng (Gem Pickup)
                GameObject gemObj = new GameObject($"Stage{i}_Gem");
                gemObj.transform.SetParent(stageZone.transform);
                gemObj.transform.position = new Vector3(stageStartX + 38f, 3f, 0f);
                var gemSr = gemObj.AddComponent<SpriteRenderer>();
                gemSr.sprite = LoadSprite($"{KENNEY_ITEMS_DIR}/platformPack_item009.png");
                var gemCol = gemObj.AddComponent<CircleCollider2D>();
                gemCol.radius = 0.4f;
                gemCol.isTrigger = true;
                gemObj.AddComponent<GemPickup>();

                // F. Lò Xo Bật Nhảy (Spring Pad)
                if (i >= 3)
                {
                    GameObject springObj = new GameObject($"Stage{i}_Spring");
                    springObj.transform.SetParent(stageZone.transform);
                    springObj.transform.position = new Vector3(stageStartX + 20f, -0.5f, 0f);
                    var springSr = springObj.AddComponent<SpriteRenderer>();
                    springSr.sprite = LoadSprite($"{KENNEY_TILES_DIR}/platformPack_tile046.png");
                    var springCol = springObj.AddComponent<BoxCollider2D>();
                    springCol.size = new Vector2(0.8f, 0.6f);
                    springCol.isTrigger = true;
                    springObj.AddComponent<SpringPad>();
                }

                // G. Cửa Qua Màn (Stage Door)
                GameObject doorObj = new GameObject($"Stage{i}_Door");
                doorObj.transform.SetParent(stageZone.transform);
                doorObj.transform.position = new Vector3(stageStartX + 55f, 0.5f, 0f);
                var doorSr = doorObj.AddComponent<SpriteRenderer>();
                doorSr.sprite = LoadSprite($"{KENNEY_TILES_DIR}/platformPack_tile054.png"); // Castle Door Closed
                var doorCol = doorObj.AddComponent<BoxCollider2D>();
                doorCol.size = new Vector2(1f, 1.5f);
                doorCol.isTrigger = true;
                var doorComp = doorObj.AddComponent<StageDoor>();
                SetPrivateField(doorComp, "stageIndex", i);
                SetPrivateField(doorComp, "isFinalVictoryDoor", i == 8);
                SetPrivateField(doorComp, "closedDoorSprite", doorSr.sprite);
                SetPrivateField(doorComp, "openDoorSprite", LoadSprite($"{KENNEY_TILES_DIR}/platformPack_tile053.png"));

                // Bục đất dưới chân Cửa
                int doorTileX = (int)doorObj.transform.position.x;
                if (gMid != null)
                {
                    for (int dx = doorTileX - 1; dx <= doorTileX + 1; dx++)
                    {
                        groundTilemap.SetTile(new Vector3Int(dx, -1, 0), gMid);
                    }
                }

                prevStageDoor = doorComp;
            }

            // Cập nhật Composite Collider cho Ground
            var composite = groundTilemap.GetComponent<CompositeCollider2D>();
            if (composite != null) composite.GenerateGeometry();

            // 7. Setup UI Canvas
            CreateCanvasWithHUD(playerObj);

            // 8. Setup GameManager
            GameObject gmObj = new GameObject("[GameManager]");
            PlatformerGameManager gm = gmObj.AddComponent<PlatformerGameManager>();
            var playerCtrl = playerObj.GetComponent<PlayerController2D>();
            SetPrivateField(gm, "player", playerCtrl);

            // 9. Setup EventSystem
            CreateEventSystem();

            // Save Scene
            EditorSceneManager.SaveScene(scene, ADVENTURE_SCENE_PATH);
            Debug.Log($"[PlatformerSetup] Đã lưu Scene 8 Stages tại {ADVENTURE_SCENE_PATH}");
        }

        private static void BuildSingleStageScene(string scenePath, int stageIndex, string stageTitle, string nextSceneName, Dictionary<string, Tile> tiles)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // 1. Camera
            GameObject cameraObj = new GameObject("Main Camera");
            Camera cam = cameraObj.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 6.0f;
            cam.backgroundColor = MARIO_SKY_BLUE;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cameraObj.AddComponent<AudioListener>();
            cameraObj.tag = "MainCamera";
            cameraObj.transform.position = new Vector3(0f, 2f, -10f);

            CameraFollow2D camFollow = cameraObj.AddComponent<CameraFollow2D>();

            // 2. Grid & 5 Tilemap Layers
            GameObject gridObj = new GameObject("Grid");
            Grid grid = gridObj.AddComponent<Grid>();
            grid.cellSize = new Vector3(1f, 1f, 0f);

            int groundLayer = LayerMask.NameToLayer("Ground");
            if (groundLayer == -1) groundLayer = LayerMask.NameToLayer("Default");

            CreateTilemapLayer(gridObj.transform, "Tilemap_Background", -10, LayerMask.NameToLayer("Default"), false, false);
            Tilemap groundTilemap = CreateTilemapLayer(gridObj.transform, "Tilemap_Ground", 0, groundLayer, true, false);
            CreateTilemapLayer(gridObj.transform, "Tilemap_Platforms", 1, groundLayer, true, false);
            Tilemap hazardTilemap = CreateTilemapLayer(gridObj.transform, "Tilemap_Hazards", 2, LayerMask.NameToLayer("Default"), true, true);
            hazardTilemap.gameObject.AddComponent<HazardDamager>();
            CreateTilemapLayer(gridObj.transform, "Tilemap_Props", 3, LayerMask.NameToLayer("Default"), false, false);

            // 3. Player
            GameObject playerObj = CreatePlayerObject(groundLayer);
            playerObj.transform.position = new Vector3(2f, 1.5f, 0f);

            camFollow.SetTarget(playerObj.transform);
            camFollow.SnapToTarget();

            // 4. Fall Kill Zone
            GameObject killZoneObj = new GameObject("FallKillZone");
            killZoneObj.transform.position = new Vector3(30f, -12f, 0f);
            BoxCollider2D killBox = killZoneObj.AddComponent<BoxCollider2D>();
            killBox.isTrigger = true;
            killBox.size = new Vector2(200f, 4f);
            killZoneObj.AddComponent<FallKillZone>();

            // 5. Stage Manager
            GameObject stageMgrObj = new GameObject("StageManager");
            var sm = stageMgrObj.AddComponent<StageManager>();
            SetPrivateField(sm, "currentStageIndex", stageIndex);

            // 6. Starting Ground Platform (6 tiles)
            Tile gLeft = tiles.ContainsKey("grass_left") ? tiles["grass_left"] : (tiles.ContainsKey("tile001") ? tiles["tile001"] : null);
            Tile gMid = tiles.ContainsKey("grass_mid") ? tiles["grass_mid"] : (tiles.ContainsKey("tile002") ? tiles["tile002"] : null);
            Tile gRight = tiles.ContainsKey("grass_right") ? tiles["grass_right"] : (tiles.ContainsKey("tile003") ? tiles["tile003"] : null);

            if (gMid != null)
            {
                for (int x = 0; x <= 6; x++)
                {
                    Tile t = (x == 0) ? (gLeft ?? gMid) : ((x == 6) ? (gRight ?? gMid) : gMid);
                    groundTilemap.SetTile(new Vector3Int(x, -1, 0), t);
                }
            }

            // 7. Interactive Objects: Key, Heart, Gem, Spring, Door
            GameObject itemsRoot = new GameObject("STAGE_INTERACTIVE_ITEMS");

            // Key
            GameObject keyObj = new GameObject("Stage_Key");
            keyObj.transform.SetParent(itemsRoot.transform);
            keyObj.transform.position = new Vector3(25f, 3f, 0f);
            var keySr = keyObj.AddComponent<SpriteRenderer>();
            keySr.sprite = LoadSprite($"{KENNEY_ITEMS_DIR}/platformPack_item014.png");
            var keyCol = keyObj.AddComponent<CircleCollider2D>();
            keyCol.radius = 0.5f;
            keyCol.isTrigger = true;
            var keyComp = keyObj.AddComponent<KeyPickup>();
            SetPrivateField(keyComp, "stageIndex", stageIndex);

            // Heart
            GameObject heartObj = new GameObject("Stage_Heart");
            heartObj.transform.SetParent(itemsRoot.transform);
            heartObj.transform.position = new Vector3(12f, 2.5f, 0f);
            var heartSr = heartObj.AddComponent<SpriteRenderer>();
            heartSr.sprite = LoadSprite($"{KENNEY_ITEMS_DIR}/platformPack_item017.png");
            var heartCol = heartObj.AddComponent<CircleCollider2D>();
            heartCol.radius = 0.4f;
            heartCol.isTrigger = true;
            heartObj.AddComponent<HeartPickup>();

            // Gem
            GameObject gemObj = new GameObject("Stage_Gem");
            gemObj.transform.SetParent(itemsRoot.transform);
            gemObj.transform.position = new Vector3(38f, 3f, 0f);
            var gemSr = gemObj.AddComponent<SpriteRenderer>();
            gemSr.sprite = LoadSprite($"{KENNEY_ITEMS_DIR}/platformPack_item009.png");
            var gemCol = gemObj.AddComponent<CircleCollider2D>();
            gemCol.radius = 0.4f;
            gemCol.isTrigger = true;
            gemObj.AddComponent<GemPickup>();

            // Spring
            if (stageIndex >= 3)
            {
                GameObject springObj = new GameObject("Stage_Spring");
                springObj.transform.SetParent(itemsRoot.transform);
                springObj.transform.position = new Vector3(20f, -0.5f, 0f);
                var springSr = springObj.AddComponent<SpriteRenderer>();
                springSr.sprite = LoadSprite($"{KENNEY_TILES_DIR}/platformPack_tile046.png");
                var springCol = springObj.AddComponent<BoxCollider2D>();
                springCol.size = new Vector2(0.8f, 0.6f);
                springCol.isTrigger = true;
                springObj.AddComponent<SpringPad>();
            }

            // Door
            GameObject doorObj = new GameObject("Stage_Door");
            doorObj.transform.SetParent(itemsRoot.transform);
            doorObj.transform.position = new Vector3(50f, 0.5f, 0f);
            var doorSr = doorObj.AddComponent<SpriteRenderer>();
            doorSr.sprite = LoadSprite($"{KENNEY_TILES_DIR}/platformPack_tile054.png");
            var doorCol = doorObj.AddComponent<BoxCollider2D>();
            doorCol.size = new Vector2(1f, 1.5f);
            doorCol.isTrigger = true;
            var doorComp = doorObj.AddComponent<StageDoor>();
            SetPrivateField(doorComp, "stageIndex", stageIndex);
            SetPrivateField(doorComp, "nextSceneName", nextSceneName);
            SetPrivateField(doorComp, "isFinalVictoryDoor", stageIndex == 8);
            SetPrivateField(doorComp, "closedDoorSprite", doorSr.sprite);
            SetPrivateField(doorComp, "openDoorSprite", LoadSprite($"{KENNEY_TILES_DIR}/platformPack_tile053.png"));

            // Bục đất dưới chân Cửa
            if (gMid != null)
            {
                for (int dx = 49; dx <= 51; dx++)
                {
                    groundTilemap.SetTile(new Vector3Int(dx, -1, 0), gMid);
                }
            }

            var composite = groundTilemap.GetComponent<CompositeCollider2D>();
            if (composite != null) composite.GenerateGeometry();

            // 8. Setup UI Canvas
            CreateCanvasWithHUD(playerObj, stageIndex);

            // 9. GameManager
            GameObject gmObj = new GameObject("[GameManager]");
            PlatformerGameManager gm = gmObj.AddComponent<PlatformerGameManager>();
            var playerCtrl = playerObj.GetComponent<PlayerController2D>();
            SetPrivateField(gm, "player", playerCtrl);

            // 10. EventSystem
            CreateEventSystem();

            EditorSceneManager.SaveScene(scene, scenePath);
            Debug.Log($"[PlatformerSetup] Đã lưu {scenePath}");
        }

        private static Tilemap CreateTilemapLayer(Transform parent, string name, int sortingOrder, int layer, bool addCollider, bool isTrigger)
        {
            GameObject mapObj = new GameObject(name);
            mapObj.transform.SetParent(parent, false);
            mapObj.layer = layer;

            Tilemap tilemap = mapObj.AddComponent<Tilemap>();
            TilemapRenderer renderer = mapObj.AddComponent<TilemapRenderer>();
            renderer.sortingOrder = sortingOrder;

            if (addCollider)
            {
                TilemapCollider2D tileCollider = mapObj.AddComponent<TilemapCollider2D>();
                tileCollider.usedByComposite = true;

                CompositeCollider2D composite = mapObj.AddComponent<CompositeCollider2D>();
                composite.geometryType = CompositeCollider2D.GeometryType.Polygons;
                composite.isTrigger = isTrigger;

                Rigidbody2D rb = mapObj.GetComponent<Rigidbody2D>();
                if (rb != null)
                {
                    rb.bodyType = RigidbodyType2D.Static;
                }
            }

            return tilemap;
        }

        private static void RegenerateComposite(GameObject go)
        {
            var comp = go.GetComponent<CompositeCollider2D>();
            if (comp != null)
            {
                comp.GenerateGeometry();
            }
        }

        /// <summary>
        /// Vẽ một màn chơi Mario Platformer hoàn chỉnh, sinh động với mây trời, đồi xanh, bục nhảy, chướng ngại vật, hộp ? và ống nước
        /// </summary>
        private static void PaintRichSampleLevel(Tilemap bgMap, Tilemap groundMap, Tilemap platMap, Tilemap hazardMap, Tilemap propsMap, Dictionary<string, Tile> tiles)
        {
            Tile GetTile(string key) => tiles.TryGetValue(key, out Tile t) ? t : null;

            // Tiles lấy theo Alias hoặc mã số
            Tile gLeft = GetTile("grass_left") ?? GetTile("tile001");
            Tile gMid = GetTile("grass_mid") ?? GetTile("tile002");
            Tile gRight = GetTile("grass_right") ?? GetTile("tile003");
            Tile dirt = GetTile("dirt_mid") ?? GetTile("tile004");
            Tile dirtBL = GetTile("dirt_bottom_left") ?? GetTile("tile005") ?? dirt;
            Tile dirtBM = GetTile("dirt_bottom_mid") ?? GetTile("tile006") ?? dirt;
            Tile dirtBR = GetTile("dirt_bottom_right") ?? GetTile("tile007") ?? dirt;

            Tile pLeft = GetTile("platform_left") ?? GetTile("tile016") ?? gLeft;
            Tile pMid = GetTile("platform_mid") ?? GetTile("tile017") ?? gMid;
            Tile pRight = GetTile("platform_right") ?? GetTile("tile018") ?? gRight;
            Tile pSingle = GetTile("platform_single") ?? GetTile("tile019") ?? pMid;

            Tile woodLeft = GetTile("wood_left") ?? GetTile("tile020") ?? pLeft;
            Tile woodMid = GetTile("wood_mid") ?? GetTile("tile021") ?? pMid;
            Tile woodRight = GetTile("wood_right") ?? GetTile("tile022") ?? pRight;

            Tile spike = GetTile("spikes") ?? GetTile("tile044");

            Tile pipeTop = GetTile("pipe_top") ?? GetTile("tile036");
            Tile pipeMid = GetTile("pipe_mid") ?? GetTile("tile037");

            Tile boxQ = GetTile("box_question") ?? GetTile("tile042");
            Tile crate = GetTile("crate_wood") ?? GetTile("tile041");
            Tile brick = GetTile("brick_block") ?? GetTile("tile052") ?? crate;

            Tile hillL = GetTile("hill_left") ?? GetTile("tile055");
            Tile hillM = GetTile("hill_mid") ?? GetTile("tile056");
            Tile hillR = GetTile("hill_right") ?? GetTile("tile057");
            Tile bush = GetTile("bush") ?? GetTile("tile058");
            Tile mushroom = GetTile("mushroom") ?? GetTile("tile065");

            Tile cTL = GetTile("cloud_tl") ?? GetTile("tile059");
            Tile cTM = GetTile("cloud_tm") ?? GetTile("tile060");
            Tile cTR = GetTile("cloud_tr") ?? GetTile("tile061");
            Tile cBL = GetTile("cloud_bl") ?? GetTile("tile062");
            Tile cBM = GetTile("cloud_bm") ?? GetTile("tile063");
            Tile cBR = GetTile("cloud_br") ?? GetTile("tile064");

            Tile flagTop = GetTile("flag_top") ?? GetTile("tile049");
            Tile flagMid = GetTile("flag_mid") ?? GetTile("tile050");
            Tile flagBase = GetTile("flag_base") ?? GetTile("tile051");

            // ==========================================
            // 1. BACKGROUND LAYER: Mây trời & Đồi núi xa
            // ==========================================
            void PaintCloud(int startX, int startY, int midWidth)
            {
                bgMap.SetTile(new Vector3Int(startX, startY + 1, 0), cTL);
                for (int x = 1; x <= midWidth; x++)
                    bgMap.SetTile(new Vector3Int(startX + x, startY + 1, 0), cTM);
                bgMap.SetTile(new Vector3Int(startX + midWidth + 1, startY + 1, 0), cTR);

                bgMap.SetTile(new Vector3Int(startX, startY, 0), cBL);
                for (int x = 1; x <= midWidth; x++)
                    bgMap.SetTile(new Vector3Int(startX + x, startY, 0), cBM);
                bgMap.SetTile(new Vector3Int(startX + midWidth + 1, startY, 0), cBR);
            }

            PaintCloud(-3, 6, 2);
            PaintCloud(8, 7, 3);
            PaintCloud(20, 6, 2);
            PaintCloud(32, 8, 3);
            PaintCloud(45, 6, 2);

            void PaintHill(int startX, int startY, int midWidth)
            {
                bgMap.SetTile(new Vector3Int(startX, startY, 0), hillL);
                for (int x = 1; x <= midWidth; x++)
                    bgMap.SetTile(new Vector3Int(startX + x, startY, 0), hillM);
                bgMap.SetTile(new Vector3Int(startX + midWidth + 1, startY, 0), hillR);
            }

            PaintHill(-5, 1, 3);
            PaintHill(11, 1, 4);
            PaintHill(33, 2, 5);

            // ==========================================
            // 2. GROUND LAYER: Các hòn đảo đất cỏ
            // ==========================================
            void PaintIsland(int startX, int endX, int topY, int bottomY)
            {
                for (int x = startX; x <= endX; x++)
                {
                    Tile top = (x == startX) ? gLeft : ((x == endX) ? gRight : gMid);
                    groundMap.SetTile(new Vector3Int(x, topY, 0), top);

                    for (int y = topY - 1; y > bottomY; y--)
                    {
                        groundMap.SetTile(new Vector3Int(x, y, 0), dirt);
                    }

                    if (bottomY < topY)
                    {
                        Tile bot = (x == startX) ? dirtBL : ((x == endX) ? dirtBR : dirtBM);
                        groundMap.SetTile(new Vector3Int(x, bottomY, 0), bot);
                    }
                }
            }

            // Đảo xuất phát: X[-7 .. 6], Y[0 .. -4]
            PaintIsland(-7, 6, 0, -4);

            // Đảo thứ hai: X[10 .. 19], Y[0 .. -4]
            PaintIsland(10, 19, 0, -4);

            // Đảo về đích / Lâu đài: X[32 .. 48], Y[1 .. -4]
            PaintIsland(32, 48, 1, -4);

            // ==========================================
            // 3. PLATFORMS LAYER: Bục lơ lửng & Cầu gỗ
            // ==========================================
            void PaintPlatform(Tilemap map, int startX, int length, int y, Tile tL, Tile tM, Tile tR, Tile tSingle)
            {
                if (length == 1)
                {
                    map.SetTile(new Vector3Int(startX, y, 0), tSingle);
                    return;
                }
                for (int i = 0; i < length; i++)
                {
                    int x = startX + i;
                    Tile t = (i == 0) ? tL : ((i == length - 1) ? tR : tM);
                    map.SetTile(new Vector3Int(x, y, 0), t);
                }
            }

            // Bục nhảy qua hố 1 (giữa đảo 1 và đảo 2)
            PaintPlatform(platMap, 7, 2, 1, pLeft, pMid, pRight, pSingle);

            // Bục bay trên Đảo 2
            PaintPlatform(platMap, 13, 4, 3, pLeft, pMid, pRight, pSingle);

            // Bậc thang lơ lửng vượt vực lớn (X[20 .. 31])
            PaintPlatform(platMap, 20, 3, 2, woodLeft, woodMid, woodRight, pSingle);
            PaintPlatform(platMap, 24, 3, 4, pLeft, pMid, pRight, pSingle);
            PaintPlatform(platMap, 28, 3, 6, woodLeft, woodMid, woodRight, pSingle);

            // ==========================================
            // 4. HAZARDS LAYER: Chông gai (Gắn HazardDamager)
            // ==========================================
            // Bãi gai dưới hố vực 1
            for (int x = 7; x <= 9; x++)
            {
                hazardMap.SetTile(new Vector3Int(x, -4, 0), spike);
            }

            // Bãi gai bẫy trên Đảo 2 (buộc người chơi phải nhảy lên bục bay hoặc nhảy qua)
            hazardMap.SetTile(new Vector3Int(15, 1, 0), spike);

            // Bãi gai dưới vực lớn
            for (int x = 20; x <= 31; x++)
            {
                hazardMap.SetTile(new Vector3Int(x, -4, 0), spike);
            }

            // ==========================================
            // 5. PROPS LAYER: Ống nước, Hộp ?, Thùng gỗ, Cây cỏ, Cột cờ
            // ==========================================
            // Ống nước xanh trên đảo 1 (X: 4, Y: 1..2)
            propsMap.SetTile(new Vector3Int(4, 2, 0), pipeTop);
            propsMap.SetTile(new Vector3Int(4, 1, 0), pipeMid);

            // Ống nước trên đảo 2 (X: 18, Y: 1..3)
            propsMap.SetTile(new Vector3Int(18, 3, 0), pipeTop);
            propsMap.SetTile(new Vector3Int(18, 2, 0), pipeMid);
            propsMap.SetTile(new Vector3Int(18, 1, 0), pipeMid);

            // Hàng Hộp ? và Gạch Mario trên Đảo 1 (X: -1 .. 2, Y: 3)
            propsMap.SetTile(new Vector3Int(-1, 3, 0), brick);
            propsMap.SetTile(new Vector3Int(0, 3, 0), boxQ);
            propsMap.SetTile(new Vector3Int(1, 3, 0), brick);
            propsMap.SetTile(new Vector3Int(2, 3, 0), boxQ);

            // Hộp bí mật trên bục cao Đảo 2 (X: 14, 15, Y: 6)
            propsMap.SetTile(new Vector3Int(14, 6, 0), boxQ);
            propsMap.SetTile(new Vector3Int(15, 6, 0), crate);

            // Thùng gỗ trang trí
            propsMap.SetTile(new Vector3Int(-4, 1, 0), crate);
            propsMap.SetTile(new Vector3Int(34, 2, 0), crate);
            propsMap.SetTile(new Vector3Int(34, 3, 0), crate);
            propsMap.SetTile(new Vector3Int(35, 2, 0), crate);

            // Cây cỏ & nấm trang trí trên mặt đất
            propsMap.SetTile(new Vector3Int(-6, 1, 0), bush);
            propsMap.SetTile(new Vector3Int(-2, 1, 0), mushroom);
            propsMap.SetTile(new Vector3Int(2, 1, 0), bush);
            propsMap.SetTile(new Vector3Int(11, 1, 0), mushroom);
            propsMap.SetTile(new Vector3Int(37, 2, 0), bush);
            propsMap.SetTile(new Vector3Int(39, 2, 0), mushroom);

            // Cột cờ chiến thắng ở cuối màn (X: 44, Y: 2 .. 6)
            propsMap.SetTile(new Vector3Int(44, 6, 0), flagTop);
            propsMap.SetTile(new Vector3Int(44, 5, 0), flagMid);
            propsMap.SetTile(new Vector3Int(44, 4, 0), flagMid);
            propsMap.SetTile(new Vector3Int(44, 3, 0), flagMid);
            propsMap.SetTile(new Vector3Int(44, 2, 0), flagBase);
        }

        private static GameObject CreatePlayerObject(int groundLayer)
        {
            GameObject player = new GameObject("Player");
            player.tag = "Player";
            player.layer = LayerMask.NameToLayer("Default");
            player.transform.position = new Vector3(-4f, 1.5f, 0f);

            // Sprite Renderer
            SpriteRenderer sr = player.AddComponent<SpriteRenderer>();
            sr.sprite = LoadSprite($"{KENNEY_CHARS_DIR}/platformChar_idle.png");
            sr.sortingOrder = 10;

            // Rigidbody 2D
            Rigidbody2D rb = player.AddComponent<Rigidbody2D>();
            rb.bodyType = RigidbodyType2D.Dynamic;
            rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            rb.constraints = RigidbodyConstraints2D.FreezeRotation;
            rb.gravityScale = 3.5f;

            // Capsule Collider 2D
            CapsuleCollider2D col = player.AddComponent<CapsuleCollider2D>();
            col.size = new Vector2(0.7f, 0.9f);
            col.offset = new Vector2(0f, 0f);

            // Ground Check Point
            GameObject groundCheck = new GameObject("GroundCheckPoint");
            groundCheck.transform.SetParent(player.transform);
            groundCheck.transform.localPosition = new Vector3(0f, -0.45f, 0f);

            // Scripts
            PlayerController2D controller = player.AddComponent<PlayerController2D>();
            SetPrivateField(controller, "groundCheckPoint", groundCheck.transform);
            SetPrivateField(controller, "spriteRenderer", sr);
            SetPrivateField(controller, "groundLayer", (LayerMask)(1 << groundLayer | 1 << LayerMask.NameToLayer("Default")));

            PlayerHealth health = player.AddComponent<PlayerHealth>();
            SetPrivateField(health, "spriteRenderer", sr);

            // Visual Animation (Đổi Sprite walk1/walk2 khi bấm A/D, jump khi bấm W, squash & stretch)
            PlayerVisualAnimator2D visualAnim = player.AddComponent<PlayerVisualAnimator2D>();
            Sprite charIdle = LoadSprite($"{KENNEY_CHARS_DIR}/platformChar_idle.png");
            Sprite charWalk1 = LoadSprite($"{KENNEY_CHARS_DIR}/platformChar_walk1.png");
            Sprite charWalk2 = LoadSprite($"{KENNEY_CHARS_DIR}/platformChar_walk2.png");
            Sprite charJump = LoadSprite($"{KENNEY_CHARS_DIR}/platformChar_jump.png");
            Sprite charHappy = LoadSprite($"{KENNEY_CHARS_DIR}/platformChar_happy.png");
            visualAnim.SetSprites(charIdle, charWalk1, charWalk2, charJump, charHappy);

            return player;
        }

        private static void CreateCoins()
        {
            GameObject coinsGroup = new GameObject("Coins");
            Sprite coinSprite = LoadSprite($"{KENNEY_ITEMS_DIR}/platformPack_item008.png");

            // Danh sách tọa độ đồng xu theo các vòng cung nhảy và bục bay
            Vector2[] coinPositions = new Vector2[]
            {
                // Trên đầu đảo 1
                new Vector2(-1f, 4.2f),
                new Vector2(0f, 4.2f),
                new Vector2(1f, 4.2f),
                new Vector2(2f, 4.2f),

                // Vòng cung nhảy qua hố 1
                new Vector2(6.5f, 2.2f),
                new Vector2(7.5f, 2.8f),
                new Vector2(8.5f, 2.2f),

                // Trên bục bay đảo 2
                new Vector2(13.5f, 4.2f),
                new Vector2(14.5f, 4.2f),
                new Vector2(15.5f, 4.2f),

                // Bậc thang lơ lửng vực lớn
                new Vector2(21f, 3.2f),
                new Vector2(25f, 5.2f),
                new Vector2(29f, 7.2f),

                // Về đích trước cột cờ
                new Vector2(36f, 3f),
                new Vector2(38f, 3f),
                new Vector2(40f, 3f),
                new Vector2(42f, 3f)
            };

            for (int i = 0; i < coinPositions.Length; i++)
            {
                GameObject coin = new GameObject($"Coin_{i + 1:D2}");
                coin.transform.SetParent(coinsGroup.transform);
                coin.transform.position = coinPositions[i];

                SpriteRenderer sr = coin.AddComponent<SpriteRenderer>();
                sr.sprite = coinSprite;
                sr.sortingOrder = 6;

                CircleCollider2D col = coin.AddComponent<CircleCollider2D>();
                col.isTrigger = true;
                col.radius = 0.35f;

                coin.AddComponent<CollectibleCoin>();
            }
        }

        #endregion

        #region UI Canvas & HUD Creation

        private static GameObject CreateCanvasWithHUD(GameObject playerObj, int stageIndex = 1)
        {
            GameObject canvasObj = new GameObject("UI_Canvas");
            Canvas canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            canvasObj.AddComponent<GraphicRaycaster>();
            UIManager_Platformer uiManager = canvasObj.AddComponent<UIManager_Platformer>();

            Sprite fullHeartSprite = LoadSprite($"{KENNEY_ITEMS_DIR}/platformPack_item017.png");
            Sprite emptyHeartSprite = LoadSprite($"{KENNEY_ITEMS_DIR}/platformPack_item018.png");
            Sprite keySprite = LoadSprite($"{KENNEY_ITEMS_DIR}/platformPack_item014.png");
            Sprite hudBadgeBg = LoadSprite("Assets/_PlatformerGame/ASSETS/hud_badge_bg.png");
            Sprite btnLeftSprite = LoadSprite("Assets/_PlatformerGame/ASSETS/btn_touch_left.png");
            Sprite btnRightSprite = LoadSprite("Assets/_PlatformerGame/ASSETS/btn_touch_right.png");
            Sprite btnJumpSprite = LoadSprite("Assets/_PlatformerGame/ASSETS/btn_touch_jump.png");

            // Kim Cương / Coin Icon cho HUD: Vòng Scene 1 chuyển sang kim cương xanh dương theo yêu cầu
            Sprite gemHudSprite = (stageIndex == 1)
                ? (LoadSprite($"{KENNEY_ITEMS_DIR}/platformPack_item007.png") ?? LoadSprite("Assets/_PlatformerGame/ASSETS/coin_gold.png"))
                : (LoadSprite("Assets/_PlatformerGame/ASSETS/coin_gold.png") ?? LoadSprite($"{KENNEY_ITEMS_DIR}/platformPack_item008.png"));

            TMP_FontAsset gameFont = GetOrCreateGameFontAsset();
            Material btnMat = GetOrCreateButtonMaterial(gameFont);
            Material titleMat = GetOrCreateTitleMaterial(gameFont);

            // 1. HUD Container (Top Bar)
            GameObject hudPanel = CreateUIElement("HUD_Panel", canvasObj.transform);
            RectTransform hudRect = hudPanel.GetComponent<RectTransform>();
            hudRect.anchorMin = new Vector2(0, 1);
            hudRect.anchorMax = new Vector2(1, 1);
            hudRect.pivot = new Vector2(0.5f, 1);
            hudRect.anchoredPosition = Vector2.zero;
            hudRect.sizeDelta = new Vector2(0, 120);

            // --- Hearts Container (Top Left) ---
            GameObject heartsContainer = CreateUIElement("Hearts_Container", hudPanel.transform);
            RectTransform heartsRect = heartsContainer.GetComponent<RectTransform>();
            heartsRect.anchorMin = new Vector2(0, 0.5f);
            heartsRect.anchorMax = new Vector2(0, 0.5f);
            heartsRect.pivot = new Vector2(0, 0.5f);
            heartsRect.anchoredPosition = new Vector2(30, 0);
            heartsRect.sizeDelta = new Vector2(320, 60);

            List<Image> heartImages = new List<Image>();
            for (int i = 0; i < 5; i++)
            {
                GameObject heartObj = CreateUIElement($"Heart_{i + 1}", heartsContainer.transform);
                RectTransform hr = heartObj.GetComponent<RectTransform>();
                hr.anchorMin = new Vector2(0, 0.5f);
                hr.anchorMax = new Vector2(0, 0.5f);
                hr.pivot = new Vector2(0.5f, 0.5f);
                hr.anchoredPosition = new Vector2(30 + i * 60, 0);
                hr.sizeDelta = new Vector2(50, 50);

                Image heartImg = heartObj.AddComponent<Image>();
                heartImg.sprite = fullHeartSprite;
                heartImg.preserveAspect = true;
                heartImages.Add(heartImg);
            }

            // --- Score Text (Top Center) ---
            GameObject scoreObj = CreateUIElement("Score_Text", hudPanel.transform);
            RectTransform scoreRect = scoreObj.GetComponent<RectTransform>();
            scoreRect.anchorMin = new Vector2(0.5f, 0.5f);
            scoreRect.anchorMax = new Vector2(0.5f, 0.5f);
            scoreRect.pivot = new Vector2(0.5f, 0.5f);
            scoreRect.anchoredPosition = Vector2.zero;
            scoreRect.sizeDelta = new Vector2(400, 60);

            TextMeshProUGUI scoreTmp = scoreObj.AddComponent<TextMeshProUGUI>();
            if (gameFont != null) scoreTmp.font = gameFont;
            if (btnMat != null) scoreTmp.fontSharedMaterial = btnMat;
            scoreTmp.text = "SCORE: 0";
            scoreTmp.fontSize = 38;
            scoreTmp.fontStyle = FontStyles.Bold;
            scoreTmp.color = Color.white;
            scoreTmp.alignment = TextAlignmentOptions.Center;

            // --- 1. Gem Badge (Top Right, góc ngoài cùng bên phải) ---
            GameObject gemBadgeObj = CreateUIElement("Gem_Badge", hudPanel.transform);
            RectTransform gemBadgeRect = gemBadgeObj.GetComponent<RectTransform>();
            gemBadgeRect.anchorMin = new Vector2(1, 0.5f);
            gemBadgeRect.anchorMax = new Vector2(1, 0.5f);
            gemBadgeRect.pivot = new Vector2(1, 0.5f);
            gemBadgeRect.anchoredPosition = new Vector2(-30, 0);
            gemBadgeRect.sizeDelta = new Vector2(160, 58);

            if (hudBadgeBg != null)
            {
                Image bg = gemBadgeObj.AddComponent<Image>();
                bg.sprite = hudBadgeBg;
                bg.color = Color.white;
            }

            // Gem Icon inside Badge
            GameObject gemIconObj = CreateUIElement("Gem_Icon", gemBadgeObj.transform);
            RectTransform gemIconRect = gemIconObj.GetComponent<RectTransform>();
            gemIconRect.anchorMin = new Vector2(0, 0.5f);
            gemIconRect.anchorMax = new Vector2(0, 0.5f);
            gemIconRect.pivot = new Vector2(0.5f, 0.5f);
            gemIconRect.anchoredPosition = new Vector2(32, 0);
            gemIconRect.sizeDelta = new Vector2(40, 40);
            Image gemIconImg = gemIconObj.AddComponent<Image>();
            gemIconImg.sprite = gemHudSprite;
            gemIconImg.preserveAspect = true;

            // Gem Text inside Badge
            GameObject gemTextObj = CreateUIElement("Gem_Text", gemBadgeObj.transform);
            RectTransform gemTextRect = gemTextObj.GetComponent<RectTransform>();
            gemTextRect.anchorMin = new Vector2(0, 0);
            gemTextRect.anchorMax = new Vector2(1, 1);
            gemTextRect.pivot = new Vector2(0.5f, 0.5f);
            gemTextRect.offsetMin = new Vector2(58, 0);
            gemTextRect.offsetMax = new Vector2(-10, 0);
            TextMeshProUGUI gemTmp = gemTextObj.AddComponent<TextMeshProUGUI>();
            if (gameFont != null) gemTmp.font = gameFont;
            if (btnMat != null) gemTmp.fontSharedMaterial = btnMat;
            gemTmp.text = "00";
            gemTmp.fontSize = 32;
            gemTmp.fontStyle = FontStyles.Bold;
            gemTmp.color = new Color(0.92f, 0.98f, 1f); // Bright ice blue
            gemTmp.alignment = TextAlignmentOptions.MidlineLeft;

            // --- 2. Coin Badge (Top Right, ở giữa Gem và Key) ---
            GameObject coinBadgeObj = CreateUIElement("Coin_Badge", hudPanel.transform);
            RectTransform coinBadgeRect = coinBadgeObj.GetComponent<RectTransform>();
            coinBadgeRect.anchorMin = new Vector2(1, 0.5f);
            coinBadgeRect.anchorMax = new Vector2(1, 0.5f);
            coinBadgeRect.pivot = new Vector2(1, 0.5f);
            coinBadgeRect.anchoredPosition = new Vector2(-210, 0);
            coinBadgeRect.sizeDelta = new Vector2(160, 58);

            if (hudBadgeBg != null)
            {
                Image bg = coinBadgeObj.AddComponent<Image>();
                bg.sprite = hudBadgeBg;
                bg.color = Color.white;
            }

            // Coin Icon inside Badge
            GameObject coinIconObj = CreateUIElement("Coin_Icon", coinBadgeObj.transform);
            RectTransform coinIconRect = coinIconObj.GetComponent<RectTransform>();
            coinIconRect.anchorMin = new Vector2(0, 0.5f);
            coinIconRect.anchorMax = new Vector2(0, 0.5f);
            coinIconRect.pivot = new Vector2(0.5f, 0.5f);
            coinIconRect.anchoredPosition = new Vector2(32, 0);
            coinIconRect.sizeDelta = new Vector2(40, 40);
            Image coinIconImg = coinIconObj.AddComponent<Image>();
            coinIconImg.sprite = LoadSprite("Assets/_PlatformerGame/ASSETS/coin_gold.png") ?? LoadSprite($"{KENNEY_ITEMS_DIR}/platformPack_item008.png");
            coinIconImg.preserveAspect = true;

            // Coin Text inside Badge
            GameObject coinTextObj = CreateUIElement("Coin_Text", coinBadgeObj.transform);
            RectTransform coinTextRect = coinTextObj.GetComponent<RectTransform>();
            coinTextRect.anchorMin = new Vector2(0, 0);
            coinTextRect.anchorMax = new Vector2(1, 1);
            coinTextRect.pivot = new Vector2(0.5f, 0.5f);
            coinTextRect.offsetMin = new Vector2(58, 0);
            coinTextRect.offsetMax = new Vector2(-10, 0);
            TextMeshProUGUI coinTmp = coinTextObj.AddComponent<TextMeshProUGUI>();
            if (gameFont != null) coinTmp.font = gameFont;
            if (btnMat != null) coinTmp.fontSharedMaterial = btnMat;
            coinTmp.text = "00";
            coinTmp.fontSize = 32;
            coinTmp.fontStyle = FontStyles.Bold;
            coinTmp.color = new Color(1f, 0.92f, 0.45f); // Vivid bright gold
            coinTmp.alignment = TextAlignmentOptions.MidlineLeft;

            // --- 3. Key Badge (Top Right, bên trái Coin Badge) ---
            GameObject keyBadgeObj = CreateUIElement("Key_Badge", hudPanel.transform);
            RectTransform keyBadgeRect = keyBadgeObj.GetComponent<RectTransform>();
            keyBadgeRect.anchorMin = new Vector2(1, 0.5f);
            keyBadgeRect.anchorMax = new Vector2(1, 0.5f);
            keyBadgeRect.pivot = new Vector2(1, 0.5f);
            keyBadgeRect.anchoredPosition = new Vector2(-390, 0);
            keyBadgeRect.sizeDelta = new Vector2(150, 58);

            if (hudBadgeBg != null)
            {
                Image bg = keyBadgeObj.AddComponent<Image>();
                bg.sprite = hudBadgeBg;
                bg.color = Color.white;
            }

            // Key Icon
            GameObject keyIconObj = CreateUIElement("Key_Icon", keyBadgeObj.transform);
            RectTransform keyIconRect = keyIconObj.GetComponent<RectTransform>();
            keyIconRect.anchorMin = new Vector2(0, 0.5f);
            keyIconRect.anchorMax = new Vector2(0, 0.5f);
            keyIconRect.pivot = new Vector2(0.5f, 0.5f);
            keyIconRect.anchoredPosition = new Vector2(30, 0);
            keyIconRect.sizeDelta = new Vector2(38, 38);
            Image keyIconImg = keyIconObj.AddComponent<Image>();
            keyIconImg.sprite = keySprite;
            keyIconImg.preserveAspect = true;
            keyIconImg.color = Color.white; // 100% sắc nét rõ ràng

            // Key Text
            GameObject keyTextObj = CreateUIElement("Key_Text", keyBadgeObj.transform);
            RectTransform keyTextRect = keyTextObj.GetComponent<RectTransform>();
            keyTextRect.anchorMin = new Vector2(0, 0);
            keyTextRect.anchorMax = new Vector2(1, 1);
            keyTextRect.pivot = new Vector2(0.5f, 0.5f);
            keyTextRect.offsetMin = new Vector2(56, 0);
            keyTextRect.offsetMax = new Vector2(-10, 0);
            TextMeshProUGUI keyTmp = keyTextObj.AddComponent<TextMeshProUGUI>();
            if (gameFont != null) keyTmp.font = gameFont;
            if (btnMat != null) keyTmp.fontSharedMaterial = btnMat;
            keyTmp.text = "0/1";
            keyTmp.fontSize = 28;
            keyTmp.fontStyle = FontStyles.Bold;
            keyTmp.color = new Color(0.92f, 0.94f, 0.98f, 0.85f);
            keyTmp.alignment = TextAlignmentOptions.MidlineLeft;

            // --- Stage Name / Notification Banner (Top Center) ---
            GameObject bannerObj = CreateUIElement("Stage_Banner", canvasObj.transform);
            RectTransform bannerRect = bannerObj.GetComponent<RectTransform>();
            bannerRect.anchorMin = new Vector2(0.5f, 1f);
            bannerRect.anchorMax = new Vector2(0.5f, 1f);
            bannerRect.pivot = new Vector2(0.5f, 1f);
            bannerRect.anchoredPosition = new Vector2(0, -90);
            bannerRect.sizeDelta = new Vector2(700, 60);

            Image bannerBg = bannerObj.AddComponent<Image>();
            bannerBg.color = new Color(0.1f, 0.12f, 0.18f, 0.9f);

            GameObject bannerTextObj = CreateUIElement("Banner_Text", bannerObj.transform);
            RectTransform btRect = bannerTextObj.GetComponent<RectTransform>();
            btRect.anchorMin = Vector2.zero;
            btRect.anchorMax = Vector2.one;
            btRect.sizeDelta = Vector2.zero;
            TextMeshProUGUI bannerTmp = bannerTextObj.AddComponent<TextMeshProUGUI>();
            if (gameFont != null) bannerTmp.font = gameFont;
            if (btnMat != null) bannerTmp.fontSharedMaterial = btnMat;
            bannerTmp.text = $"🚩 STAGE {stageIndex}: KHÁM PHÁ THẾ GIỚI";
            bannerTmp.fontSize = 28;
            bannerTmp.fontStyle = FontStyles.Bold;
            bannerTmp.color = new Color(1f, 0.9f, 0.2f);
            bannerTmp.alignment = TextAlignmentOptions.Center;
            bannerObj.SetActive(false);

            // 2. Countdown Text (Center Screen)
            GameObject countdownObj = CreateUIElement("Countdown_Text", canvasObj.transform);
            RectTransform countdownRect = countdownObj.GetComponent<RectTransform>();
            countdownRect.anchorMin = new Vector2(0.5f, 0.5f);
            countdownRect.anchorMax = new Vector2(0.5f, 0.5f);
            countdownRect.pivot = new Vector2(0.5f, 0.5f);
            countdownRect.anchoredPosition = new Vector2(0, 50);
            countdownRect.sizeDelta = new Vector2(800, 200);

            TextMeshProUGUI countdownTmp = countdownObj.AddComponent<TextMeshProUGUI>();
            if (gameFont != null) countdownTmp.font = gameFont;
            if (titleMat != null) countdownTmp.fontSharedMaterial = titleMat;
            countdownTmp.text = "3";
            countdownTmp.fontSize = 110;
            countdownTmp.fontStyle = FontStyles.Bold;
            countdownTmp.color = new Color(1f, 0.9f, 0.1f);
            countdownTmp.alignment = TextAlignmentOptions.Center;

            // 3. Mobile Virtual Controls (Sử dụng Sprite Nút Bấm Xịn)
            GameObject mobileControls = CreateUIElement("Mobile_Controls", canvasObj.transform);
            RectTransform mcRect = mobileControls.GetComponent<RectTransform>();
            mcRect.anchorMin = Vector2.zero;
            mcRect.anchorMax = Vector2.one;
            mcRect.sizeDelta = Vector2.zero;

            // Left / Right Group (Bottom Left)
            GameObject leftRightGroup = CreateUIElement("Move_Group", mobileControls.transform);
            RectTransform lrRect = leftRightGroup.GetComponent<RectTransform>();
            lrRect.anchorMin = new Vector2(0, 0);
            lrRect.anchorMax = new Vector2(0, 0);
            lrRect.pivot = new Vector2(0, 0);
            lrRect.anchoredPosition = new Vector2(50, 50);
            lrRect.sizeDelta = new Vector2(280, 120);

            HorizontalLayoutGroup lrLayout = leftRightGroup.AddComponent<HorizontalLayoutGroup>();
            lrLayout.spacing = 25;
            lrLayout.childControlWidth = false;
            lrLayout.childControlHeight = false;

            // Button Left
            GameObject btnLeft = CreateUIElement("Btn_Left", leftRightGroup.transform);
            RectTransform blRect = btnLeft.GetComponent<RectTransform>();
            blRect.sizeDelta = new Vector2(120, 120);
            Image blImg = btnLeft.AddComponent<Image>();
            blImg.sprite = btnLeftSprite;
            blImg.preserveAspect = true;
            blImg.color = Color.white;
            MobileTouchButton mtbLeft = btnLeft.AddComponent<MobileTouchButton>();

            // Button Right
            GameObject btnRight = CreateUIElement("Btn_Right", leftRightGroup.transform);
            RectTransform brRect = btnRight.GetComponent<RectTransform>();
            brRect.sizeDelta = new Vector2(120, 120);
            Image brImg = btnRight.AddComponent<Image>();
            brImg.sprite = btnRightSprite;
            brImg.preserveAspect = true;
            brImg.color = Color.white;
            MobileTouchButton mtbRight = btnRight.AddComponent<MobileTouchButton>();

            // Jump Button (Bottom Right)
            GameObject btnJump = CreateUIElement("Btn_Jump", mobileControls.transform);
            RectTransform jumpRect = btnJump.GetComponent<RectTransform>();
            jumpRect.anchorMin = new Vector2(1, 0);
            jumpRect.anchorMax = new Vector2(1, 0);
            jumpRect.pivot = new Vector2(1, 0);
            jumpRect.anchoredPosition = new Vector2(-50, 50);
            jumpRect.sizeDelta = new Vector2(140, 140);
            Image jumpImg = btnJump.AddComponent<Image>();
            jumpImg.sprite = btnJumpSprite;
            jumpImg.preserveAspect = true;
            jumpImg.color = Color.white;
            MobileTouchButton mtbJump = btnJump.AddComponent<MobileTouchButton>();

            // 4. Game Over Popup
            GameObject gameOverModal = CreateModalPopup("Popup_GameOver", canvasObj.transform, "GAME OVER", out TMP_Text goScore, out TMP_Text goCoin, out Button goRestart, out Button goHome);
            gameOverModal.SetActive(false);

            // 5. Victory Popup
            GameObject victoryModal = CreateModalPopup("Popup_Victory", canvasObj.transform, "VICTORY!", out TMP_Text vicScore, out TMP_Text vicCoin, out Button vicRestart, out Button vicHome);
            victoryModal.SetActive(false);

            // Auto-wire references to UIManager_Platformer
            if (playerObj != null)
            {
                SetPrivateField(uiManager, "playerController", playerObj.GetComponent<PlayerController2D>());
                SetPrivateField(uiManager, "playerHealth", playerObj.GetComponent<PlayerHealth>());
            }
            SetPrivateField(uiManager, "heartImages", heartImages);
            SetPrivateField(uiManager, "fullHeartSprite", fullHeartSprite);
            SetPrivateField(uiManager, "emptyHeartSprite", emptyHeartSprite);
            SetPrivateField(uiManager, "coinIconTransform", coinIconRect);
            SetPrivateField(uiManager, "coinText", coinTmp);
            SetPrivateField(uiManager, "gemIconTransform", gemIconRect);
            SetPrivateField(uiManager, "gemText", gemTmp);
            SetPrivateField(uiManager, "scoreText", scoreTmp);
            SetPrivateField(uiManager, "keyIconImage", keyIconImg);
            SetPrivateField(uiManager, "keyText", keyTmp);
            SetPrivateField(uiManager, "keyActiveSprite", keySprite);
            SetPrivateField(uiManager, "keyInactiveSprite", keySprite);
            SetPrivateField(uiManager, "bannerContainer", bannerObj);
            SetPrivateField(uiManager, "bannerText", bannerTmp);
            SetPrivateField(uiManager, "countdownText", countdownTmp);
            SetPrivateField(uiManager, "countdownContainer", countdownObj);
            SetPrivateField(uiManager, "mobileControlsPanel", mobileControls);
            SetPrivateField(uiManager, "leftButton", mtbLeft);
            SetPrivateField(uiManager, "rightButton", mtbRight);
            SetPrivateField(uiManager, "jumpButton", mtbJump);
            SetPrivateField(uiManager, "gameOverPanel", gameOverModal);
            SetPrivateField(uiManager, "gameOverScoreText", goScore);
            SetPrivateField(uiManager, "gameOverCoinText", goCoin);
            SetPrivateField(uiManager, "gameOverRestartButton", goRestart);
            SetPrivateField(uiManager, "gameOverHomeButton", goHome);
            SetPrivateField(uiManager, "victoryPanel", victoryModal);
            SetPrivateField(uiManager, "victoryScoreText", vicScore);
            SetPrivateField(uiManager, "victoryCoinText", vicCoin);
            SetPrivateField(uiManager, "victoryRestartButton", vicRestart);
            SetPrivateField(uiManager, "victoryHomeButton", vicHome);

            return canvasObj;
        }

        private static GameObject CreateModalPopup(string name, Transform parent, string titleText,
            out TMP_Text scoreText, out TMP_Text coinText, out Button restartBtn, out Button homeBtn)
        {
            GameObject modal = CreateUIElement(name, parent);
            RectTransform modalRect = modal.GetComponent<RectTransform>();
            modalRect.anchorMin = Vector2.zero;
            modalRect.anchorMax = Vector2.one;
            modalRect.sizeDelta = Vector2.zero;

            // Semi-transparent backdrop
            Image backdrop = modal.AddComponent<Image>();
            backdrop.color = new Color(0f, 0f, 0f, 0.75f);

            // Dialog Window
            GameObject dialog = CreateUIElement("Dialog_Card", modal.transform);
            RectTransform dialogRect = dialog.GetComponent<RectTransform>();
            dialogRect.anchorMin = new Vector2(0.5f, 0.5f);
            dialogRect.anchorMax = new Vector2(0.5f, 0.5f);
            dialogRect.pivot = new Vector2(0.5f, 0.5f);
            dialogRect.sizeDelta = new Vector2(650, 480);
            Image dialogBg = dialog.AddComponent<Image>();
            dialogBg.color = new Color(0.12f, 0.14f, 0.2f, 0.95f);

            // Title
            GameObject titleObj = CreateUIElement("Title", dialog.transform);
            RectTransform titleRect = titleObj.GetComponent<RectTransform>();
            titleRect.anchorMin = new Vector2(0.5f, 1);
            titleRect.anchorMax = new Vector2(0.5f, 1);
            titleRect.pivot = new Vector2(0.5f, 1);
            titleRect.anchoredPosition = new Vector2(0, -30);
            titleRect.sizeDelta = new Vector2(550, 70);
            TextMeshProUGUI titleTmp = titleObj.AddComponent<TextMeshProUGUI>();
            TMP_FontAsset gameFont = GetOrCreateGameFontAsset();
            Material titleMat = GetOrCreateTitleMaterial(gameFont);
            Material btnMat = GetOrCreateButtonMaterial(gameFont);

            if (gameFont != null) titleTmp.font = gameFont;
            if (titleMat != null) titleTmp.fontSharedMaterial = titleMat;
            titleTmp.text = titleText;
            titleTmp.fontSize = 52;
            titleTmp.fontStyle = FontStyles.Bold;
            titleTmp.color = titleText.Contains("OVER") ? new Color(1f, 0.3f, 0.3f) : new Color(0.3f, 1f, 0.4f);
            titleTmp.alignment = TextAlignmentOptions.Center;

            // Score Text
            GameObject scoreObj = CreateUIElement("Score_Result", dialog.transform);
            RectTransform scoreRect = scoreObj.GetComponent<RectTransform>();
            scoreRect.anchorMin = new Vector2(0.5f, 0.5f);
            scoreRect.anchorMax = new Vector2(0.5f, 0.5f);
            scoreRect.pivot = new Vector2(0.5f, 0.5f);
            scoreRect.anchoredPosition = new Vector2(0, 45);
            scoreRect.sizeDelta = new Vector2(500, 50);
            TextMeshProUGUI scoreTmp = scoreObj.AddComponent<TextMeshProUGUI>();
            if (gameFont != null) scoreTmp.font = gameFont;
            if (btnMat != null) scoreTmp.fontSharedMaterial = btnMat;
            scoreTmp.text = "SCORE: 0";
            scoreTmp.fontSize = 38;
            scoreTmp.color = Color.white;
            scoreTmp.alignment = TextAlignmentOptions.Center;
            scoreText = scoreTmp;

            // Coin Text
            GameObject coinObj = CreateUIElement("Coin_Result", dialog.transform);
            RectTransform coinRect = coinObj.GetComponent<RectTransform>();
            coinRect.anchorMin = new Vector2(0.5f, 0.5f);
            coinRect.anchorMax = new Vector2(0.5f, 0.5f);
            coinRect.pivot = new Vector2(0.5f, 0.5f);
            coinRect.anchoredPosition = new Vector2(0, -15);
            coinRect.sizeDelta = new Vector2(500, 50);
            TextMeshProUGUI coinTmp = coinObj.AddComponent<TextMeshProUGUI>();
            if (gameFont != null) coinTmp.font = gameFont;
            if (btnMat != null) coinTmp.fontSharedMaterial = btnMat;
            coinTmp.text = "COINS: 0";
            coinTmp.fontSize = 34;
            coinTmp.color = new Color(1f, 0.85f, 0.2f);
            coinTmp.alignment = TextAlignmentOptions.Center;
            coinText = coinTmp;

            // Button Container
            GameObject btnGroup = CreateUIElement("Button_Group", dialog.transform);
            RectTransform bgRect = btnGroup.GetComponent<RectTransform>();
            bgRect.anchorMin = new Vector2(0.5f, 0);
            bgRect.anchorMax = new Vector2(0.5f, 0);
            bgRect.pivot = new Vector2(0.5f, 0);
            bgRect.anchoredPosition = new Vector2(0, 40);
            bgRect.sizeDelta = new Vector2(520, 80);

            HorizontalLayoutGroup bgLayout = btnGroup.AddComponent<HorizontalLayoutGroup>();
            bgLayout.spacing = 30;
            bgLayout.childControlWidth = false;
            bgLayout.childControlHeight = false;
            bgLayout.childAlignment = TextAnchor.MiddleCenter;

            // Restart Button
            GameObject rBtnObj = CreateUIButton("Btn_Restart", btnGroup.transform, "CHƠI LẠI", new Vector2(230, 75), new Color(0.18f, 0.65f, 0.25f), 32);
            restartBtn = rBtnObj.GetComponent<Button>();

            // Home Button
            GameObject hBtnObj = CreateUIButton("Btn_Home", btnGroup.transform, "VỀ MENU", new Vector2(230, 75), new Color(0.35f, 0.4f, 0.5f), 32);
            homeBtn = hBtnObj.GetComponent<Button>();

            return modal;
        }

        public static TMP_FontAsset GetOrCreateGameFontAsset()
        {
            TMP_FontAsset fontAsset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FONT_ASSET_PATH);
            if (fontAsset != null && fontAsset.material != null && fontAsset.atlasTexture != null)
            {
                return fontAsset;
            }

            Font sourceFont = AssetDatabase.LoadAssetAtPath<Font>(FONT_TTF_PATH);
            if (sourceFont != null)
            {
                try
                {
                    if (fontAsset != null)
                    {
                        AssetDatabase.DeleteAsset(FONT_ASSET_PATH);
                    }

                    fontAsset = TMP_FontAsset.CreateFontAsset(sourceFont, 64, 4, GlyphRenderMode.SDFAA, 512, 512, AtlasPopulationMode.Dynamic);
                    if (fontAsset != null)
                    {
                        AssetDatabase.CreateAsset(fontAsset, FONT_ASSET_PATH);

                        // Nhúng Material và Atlas Texture vào bên trong Asset để không bị Unity giải phóng (destroyed)
                        if (fontAsset.material != null)
                        {
                            fontAsset.material.name = fontAsset.name + " Material";
                            AssetDatabase.AddObjectToAsset(fontAsset.material, fontAsset);
                        }

                        if (fontAsset.atlasTextures != null)
                        {
                            for (int i = 0; i < fontAsset.atlasTextures.Length; i++)
                            {
                                var tex = fontAsset.atlasTextures[i];
                                if (tex != null)
                                {
                                    tex.name = $"{fontAsset.name} Atlas {i}";
                                    AssetDatabase.AddObjectToAsset(tex, fontAsset);
                                }
                            }
                        }

                        AssetDatabase.SaveAssets();
                        AssetDatabase.ImportAsset(FONT_ASSET_PATH, ImportAssetOptions.ForceUpdate);
                        return fontAsset;
                    }
                }
                catch (System.Exception ex)
                {
                    Debug.LogWarning($"[FontSetup] Không thể tạo FontAsset từ TTF: {ex.Message}");
                }
            }

            return Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
        }

        public static Material GetOrCreateTitleMaterial(TMP_FontAsset fontAsset)
        {
            if (fontAsset == null || fontAsset.material == null) return null;

            Material mat = AssetDatabase.LoadAssetAtPath<Material>(TITLE_MAT_PATH);
            if (mat != null && mat.mainTexture != null) return mat;

            try
            {
                if (mat != null) AssetDatabase.DeleteAsset(TITLE_MAT_PATH);

                mat = new Material(fontAsset.material);
                mat.name = "Fredoka_Title_Shadow";

                // Bật Underlay (Đổ bóng 3D màu nâu cam đậm chuẩn hoạt hình)
                mat.EnableKeyword("UNDERLAY_ON");
                mat.SetColor("_UnderlayColor", new Color(0.55f, 0.22f, 0.02f, 0.95f)); // #8C3805
                mat.SetFloat("_UnderlayOffsetX", 0f);
                mat.SetFloat("_UnderlayOffsetY", -0.75f);
                mat.SetFloat("_UnderlayDilate", 0.22f);
                mat.SetFloat("_UnderlaySoftness", 0.08f);

                // Viền chữ (Outline)
                mat.EnableKeyword("OUTLINE_ON");
                mat.SetColor("_OutlineColor", new Color(0.85f, 0.48f, 0.05f, 1f));
                mat.SetFloat("_OutlineWidth", 0.16f);

                AssetDatabase.CreateAsset(mat, TITLE_MAT_PATH);
                AssetDatabase.SaveAssets();
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[TitleMaterial] {ex.Message}");
            }

            return mat;
        }

        public static Material GetOrCreateButtonMaterial(TMP_FontAsset fontAsset)
        {
            if (fontAsset == null || fontAsset.material == null) return null;

            Material mat = AssetDatabase.LoadAssetAtPath<Material>(BUTTON_MAT_PATH);
            if (mat != null && mat.mainTexture != null) return mat;

            try
            {
                if (mat != null) AssetDatabase.DeleteAsset(BUTTON_MAT_PATH);

                mat = new Material(fontAsset.material);
                mat.name = "Fredoka_Button_Shadow";

                // Đổ bóng chữ nút
                mat.EnableKeyword("UNDERLAY_ON");
                mat.SetColor("_UnderlayColor", new Color(0.04f, 0.08f, 0.15f, 0.85f));
                mat.SetFloat("_UnderlayOffsetX", 0f);
                mat.SetFloat("_UnderlayOffsetY", -0.65f);
                mat.SetFloat("_UnderlayDilate", 0.15f);
                mat.SetFloat("_UnderlaySoftness", 0.05f);

                // Viền chữ
                mat.EnableKeyword("OUTLINE_ON");
                mat.SetColor("_OutlineColor", new Color(0.1f, 0.15f, 0.22f, 1f));
                mat.SetFloat("_OutlineWidth", 0.12f);

                AssetDatabase.CreateAsset(mat, BUTTON_MAT_PATH);
                AssetDatabase.SaveAssets();
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[ButtonMaterial] {ex.Message}");
            }

            return mat;
        }

        private static GameObject CreateUIButton(string name, Transform parent, string label, Vector2 size, Color bgColor, float fontSize = 28)
        {
            GameObject btnObj = CreateUIElement(name, parent);
            RectTransform rect = btnObj.GetComponent<RectTransform>();
            rect.sizeDelta = size;

            Image img = btnObj.AddComponent<Image>();
            img.color = bgColor;

            Button btn = btnObj.AddComponent<Button>();
            ColorBlock cb = btn.colors;
            cb.highlightedColor = bgColor * 1.15f;
            cb.pressedColor = bgColor * 0.8f;
            btn.colors = cb;

            GameObject textObj = CreateUIElement("Label", btnObj.transform);
            RectTransform textRect = textObj.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.sizeDelta = Vector2.zero;

            TextMeshProUGUI tmp = textObj.AddComponent<TextMeshProUGUI>();
            TMP_FontAsset fontAsset = GetOrCreateGameFontAsset();
            if (fontAsset != null)
            {
                tmp.font = fontAsset;
                Material btnMat = GetOrCreateButtonMaterial(fontAsset);
                if (btnMat != null) tmp.fontSharedMaterial = btnMat;
            }

            tmp.text = label;
            tmp.fontSize = fontSize;
            tmp.fontStyle = FontStyles.Bold;
            tmp.color = Color.white;
            tmp.alignment = TextAlignmentOptions.Center;

            return btnObj;
        }

        private static GameObject CreateUIElement(string name, Transform parent)
        {
            GameObject obj = new GameObject(name);
            obj.AddComponent<RectTransform>();
            if (parent != null)
            {
                obj.transform.SetParent(parent, false);
            }
            return obj;
        }

        private static void CreateEventSystem()
        {
            var es = UnityEngine.Object.FindObjectOfType<EventSystem>();
            if (es == null)
            {
                GameObject esObj = new GameObject("EventSystem");
                es = esObj.AddComponent<EventSystem>();
            }

#if ENABLE_INPUT_SYSTEM
            var standalone = es.GetComponent<StandaloneInputModule>();
            if (standalone != null)
            {
                UnityEngine.Object.DestroyImmediate(standalone);
            }
            if (es.GetComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>() == null)
            {
                es.gameObject.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
            }
#else
            if (es.GetComponent<StandaloneInputModule>() == null)
            {
                es.gameObject.AddComponent<StandaloneInputModule>();
            }
#endif
        }

        #endregion

        #region Step 4: Build Home Scene

        [MenuItem("Tools/Platformer Game/Setup Home Menu (Tạo Scene_Home Đẹp Kèm Grid Tự Vẽ)", false, 10)]
        public static void BuildHomeSceneMenu()
        {
            EnsureDirectories();
            Dictionary<string, Tile> tilesDict = CreateAllTileAssets(out List<Tile> allTilesList);
            CreatePalettePrefab(allTilesList);
            BuildHomeScene();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            EditorSceneManager.OpenScene(HOME_SCENE_PATH);
            EditorUtility.DisplayDialog("Thành công", "Đã tạo Scene_Home với Grid 5 lớp Tilemap và Giao diện Menu theo đúng ảnh mẫu!\n\nBây giờ Sếp có thể dùng Tile Palette để vẽ thêm cỏ cây, bục quà trang trí cho Home.", "Tuyệt vời");
        }

        private static void BuildHomeScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // 1. Camera Sky Blue
            GameObject cameraObj = new GameObject("Main Camera");
            Camera cam = cameraObj.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 6f;
            cam.backgroundColor = MARIO_SKY_BLUE;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cameraObj.AddComponent<AudioListener>();
            cameraObj.tag = "MainCamera";
            cameraObj.transform.position = new Vector3(0f, 1f, -10f);

            // 2. Grid & 5 Tilemap Layers (để tự vẽ địa hình, cây cỏ, hộp quà trang trí cho Home)
            GameObject gridObj = new GameObject("Grid");
            Grid grid = gridObj.AddComponent<Grid>();
            grid.cellSize = new Vector3(1f, 1f, 0f);

            int groundLayer = LayerMask.NameToLayer("Ground");
            if (groundLayer == -1) groundLayer = LayerMask.NameToLayer("Default");

            CreateTilemapLayer(gridObj.transform, "Tilemap_Background", -10, LayerMask.NameToLayer("Default"), false, false);
            Tilemap groundTilemap = CreateTilemapLayer(gridObj.transform, "Tilemap_Ground", 0, groundLayer, false, false);
            CreateTilemapLayer(gridObj.transform, "Tilemap_Platforms", 1, groundLayer, false, false);
            CreateTilemapLayer(gridObj.transform, "Tilemap_Hazards", 2, LayerMask.NameToLayer("Default"), false, false);
            CreateTilemapLayer(gridObj.transform, "Tilemap_Props", 3, LayerMask.NameToLayer("Default"), false, false);

            // Vẽ mẫu 1 hàng đất cỏ phía dưới màn hình Home (từ X: -15 đến 15, Y: -4)
            Tile gLeft = AssetDatabase.LoadAssetAtPath<Tile>($"{TILES_DIR}/Tile_grass_left.asset");
            Tile gMid = AssetDatabase.LoadAssetAtPath<Tile>($"{TILES_DIR}/Tile_grass_mid.asset");
            Tile gRight = AssetDatabase.LoadAssetAtPath<Tile>($"{TILES_DIR}/Tile_grass_right.asset");
            Tile dirtMid = AssetDatabase.LoadAssetAtPath<Tile>($"{TILES_DIR}/Tile_dirt_mid.asset");

            if (gMid != null)
            {
                for (int x = -15; x <= 15; x++)
                {
                    groundTilemap.SetTile(new Vector3Int(x, -4, 0), gMid);
                    if (dirtMid != null)
                    {
                        groundTilemap.SetTile(new Vector3Int(x, -5, 0), dirtMid);
                        groundTilemap.SetTile(new Vector3Int(x, -6, 0), dirtMid);
                    }
                }
            }

            // 2.5. Nhân vật trang trí tự động chạy nhảy, nhún nhảy trên nền đất Home
            Sprite charIdle = LoadSprite($"{KENNEY_CHARS_DIR}/platformChar_idle.png");
            Sprite charHappy = LoadSprite($"{KENNEY_CHARS_DIR}/platformChar_happy.png");
            Sprite charWalk1 = LoadSprite($"{KENNEY_CHARS_DIR}/platformChar_walk1.png");
            Sprite charWalk2 = LoadSprite($"{KENNEY_CHARS_DIR}/platformChar_walk2.png");
            Sprite charJump = LoadSprite($"{KENNEY_CHARS_DIR}/platformChar_jump.png");

            if (charIdle != null || charHappy != null)
            {
                GameObject charsRoot = new GameObject("Home_Characters");

                // Nhân vật bên trái (tự động chạy tuần tra bên trái và bật nhảy tung tăng)
                GameObject charLeft = new GameObject("Char_Left_Runner");
                charLeft.transform.SetParent(charsRoot.transform);
                charLeft.transform.position = new Vector3(-5f, -3.2f, 0f);
                var srLeft = charLeft.AddComponent<SpriteRenderer>();
                srLeft.sprite = charHappy ?? charIdle;
                srLeft.sortingOrder = 5;

                var animLeft = charLeft.AddComponent<HomeCharacterAnimator>();
                animLeft.SetSprites(charIdle, charWalk1, charWalk2, charJump, charHappy);
                animLeft.SetupPatrolBounds(min: -8.5f, max: -2f, speed: 2.3f, scale: 1.35f, jump: true);

                // Nhân vật bên phải (tự động chạy tuần tra bên phải và bật nhảy nhún nhảy)
                GameObject charRight = new GameObject("Char_Right_Jumper");
                charRight.transform.SetParent(charsRoot.transform);
                charRight.transform.position = new Vector3(5f, -3.2f, 0f);
                var srRight = charRight.AddComponent<SpriteRenderer>();
                srRight.sprite = charIdle ?? charHappy;
                srRight.sortingOrder = 5;

                var animRight = charRight.AddComponent<HomeCharacterAnimator>();
                animRight.SetSprites(charIdle, charWalk1, charWalk2, charJump, charHappy);
                animRight.SetupPatrolBounds(min: 2f, max: 8.5f, speed: 2.6f, scale: 1.35f, jump: true);
            }

            // 3. UI Canvas
            GameObject canvasObj = new GameObject("UI_Canvas");
            Canvas canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            canvasObj.AddComponent<GraphicRaycaster>();
            HomeSceneController homeCtrl = canvasObj.AddComponent<HomeSceneController>();

            TMP_FontAsset gameFont = GetOrCreateGameFontAsset();
            Material titleMat = GetOrCreateTitleMaterial(gameFont);
            Material btnMat = GetOrCreateButtonMaterial(gameFont);

            // --- Top Bar (Pill Info: Gems & Hearts - Top Left) ---
            GameObject topPill = CreateUIElement("Top_Info_Pill", canvasObj.transform);
            RectTransform pillRect = topPill.GetComponent<RectTransform>();
            pillRect.anchorMin = new Vector2(0, 1);
            pillRect.anchorMax = new Vector2(0, 1);
            pillRect.pivot = new Vector2(0, 1);
            pillRect.anchoredPosition = new Vector2(60, -50);
            pillRect.sizeDelta = new Vector2(280, 70);

            Image pillBg = topPill.AddComponent<Image>();
            pillBg.color = new Color(0.12f, 0.18f, 0.28f, 0.9f);

            GameObject pillTextObj = CreateUIElement("Pill_Text", topPill.transform);
            RectTransform ptRect = pillTextObj.GetComponent<RectTransform>();
            ptRect.anchorMin = Vector2.zero;
            ptRect.anchorMax = Vector2.one;
            ptRect.sizeDelta = Vector2.zero;
            TextMeshProUGUI ptTmp = pillTextObj.AddComponent<TextMeshProUGUI>();
            if (gameFont != null) ptTmp.font = gameFont;
            if (btnMat != null) ptTmp.fontSharedMaterial = btnMat;
            ptTmp.text = "💎 128   ❤️ 5";
            ptTmp.fontSize = 28;
            ptTmp.fontStyle = FontStyles.Bold;
            ptTmp.color = Color.white;
            ptTmp.alignment = TextAlignmentOptions.Center;

            // --- Top Right Settings & Audio Icons ---
            GameObject settingsBtnObj = CreateUIButton("Btn_Settings", canvasObj.transform, "⚙", new Vector2(65, 65), new Color(0.18f, 0.24f, 0.35f, 0.9f), 30);
            RectTransform sRect = settingsBtnObj.GetComponent<RectTransform>();
            sRect.anchorMin = new Vector2(1, 1);
            sRect.anchorMax = new Vector2(1, 1);
            sRect.pivot = new Vector2(1, 1);
            sRect.anchoredPosition = new Vector2(-140, -50);

            GameObject audioBtnObj = CreateUIButton("Btn_Audio", canvasObj.transform, "♪", new Vector2(65, 65), new Color(0.18f, 0.24f, 0.35f, 0.9f), 30);
            RectTransform aRect = audioBtnObj.GetComponent<RectTransform>();
            aRect.anchorMin = new Vector2(1, 1);
            aRect.anchorMax = new Vector2(1, 1);
            aRect.pivot = new Vector2(1, 1);
            aRect.anchoredPosition = new Vector2(-60, -50);

            // --- Main Title: SUPER SKYBOUND (Đổ bóng 3D vàng cam chuẩn phong cách) ---
            GameObject titleObj = CreateUIElement("Title_Game", canvasObj.transform);
            RectTransform titleRect = titleObj.GetComponent<RectTransform>();
            titleRect.anchorMin = new Vector2(0.5f, 0.74f);
            titleRect.anchorMax = new Vector2(0.5f, 0.74f);
            titleRect.pivot = new Vector2(0.5f, 0.5f);
            titleRect.anchoredPosition = Vector2.zero;
            titleRect.sizeDelta = new Vector2(1000, 180);

            TextMeshProUGUI titleTmp = titleObj.AddComponent<TextMeshProUGUI>();
            if (gameFont != null) titleTmp.font = gameFont;
            if (titleMat != null) titleTmp.fontSharedMaterial = titleMat;
            titleTmp.text = "SUPER\nSKYBOUND";
            titleTmp.fontSize = 88;
            titleTmp.lineSpacing = -15f;
            titleTmp.characterSpacing = 3f;
            titleTmp.fontStyle = FontStyles.Bold;
            titleTmp.color = new Color(1f, 0.82f, 0.18f); // Golden Yellow #FFD12E
            titleTmp.alignment = TextAlignmentOptions.Center;

            // --- Nút CHƠI PHIÊU LƯU (Xanh lá to, căn giữa) ---
            GameObject playBtnObj = CreateUIButton("Btn_PlayAdventure", canvasObj.transform, "▶ CHƠI PHIÊU LƯU", new Vector2(440, 92), new Color(0.18f, 0.68f, 0.28f), 34);
            RectTransform pbr = playBtnObj.GetComponent<RectTransform>();
            pbr.anchorMin = new Vector2(0.5f, 0.44f);
            pbr.anchorMax = new Vector2(0.5f, 0.44f);
            pbr.pivot = new Vector2(0.5f, 0.5f);
            pbr.anchoredPosition = Vector2.zero;
            Button playBtn = playBtnObj.GetComponent<Button>();

            // --- Nút CHỌN MÀN (Xám đậm, bên trái) ---
            GameObject selectBtnObj = CreateUIButton("Btn_SelectStage", canvasObj.transform, "CHỌN MÀN", new Vector2(210, 75), new Color(0.24f, 0.32f, 0.42f), 26);
            RectTransform sbr = selectBtnObj.GetComponent<RectTransform>();
            sbr.anchorMin = new Vector2(0.5f, 0.31f);
            sbr.anchorMax = new Vector2(0.5f, 0.31f);
            sbr.pivot = new Vector2(0.5f, 0.5f);
            sbr.anchoredPosition = new Vector2(-115, 0);

            // --- Nút THOÁT GAME (Đỏ, bên phải) ---
            GameObject quitBtnObj = CreateUIButton("Btn_Quit", canvasObj.transform, "THOÁT GAME", new Vector2(210, 75), new Color(0.82f, 0.24f, 0.24f), 26);
            RectTransform qbr = quitBtnObj.GetComponent<RectTransform>();
            qbr.anchorMin = new Vector2(0.5f, 0.31f);
            qbr.anchorMax = new Vector2(0.5f, 0.31f);
            qbr.pivot = new Vector2(0.5f, 0.5f);
            qbr.anchoredPosition = new Vector2(115, 0);
            Button quitBtn = quitBtnObj.GetComponent<Button>();

            SetPrivateField(homeCtrl, "playButton", playBtn);
            SetPrivateField(homeCtrl, "quitButton", quitBtn);
            SetPrivateField(homeCtrl, "adventureSceneName", "Stage_1");

            CreateEventSystem();

            EditorSceneManager.SaveScene(scene, HOME_SCENE_PATH);
            Debug.Log($"[PlatformerSetup] Đã lưu Scene_Home tại {HOME_SCENE_PATH}");
        }

        #endregion

        #region Step 5: Build Settings

        private static void UpdateBuildSettings()
        {
            var scenes = new List<EditorBuildSettingsScene>
            {
                new EditorBuildSettingsScene(HOME_SCENE_PATH, true),
                new EditorBuildSettingsScene(ADVENTURE_SCENE_PATH, true)
            };

            EditorBuildSettings.scenes = scenes.ToArray();
            Debug.Log("[PlatformerSetup] Đã đăng ký Scene_Home (Index 0) & Scene_Adventure (Index 1) vào Build Settings.");
        }

        #endregion

        #region Helper Utilities

        private static Sprite LoadSprite(string assetPath)
        {
            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
            if (sprite == null)
            {
                UnityEngine.Object[] subAssets = AssetDatabase.LoadAllAssetsAtPath(assetPath);
                foreach (var sub in subAssets)
                {
                    if (sub is Sprite s)
                    {
                        return s;
                    }
                }
            }
            return sprite;
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            if (target == null) return;
            var field = target.GetType().GetField(fieldName, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public);
            if (field != null)
            {
                field.SetValue(target, value);
            }
            else
            {
                Debug.LogWarning($"[PlatformerSetup] Không tìm thấy field '{fieldName}' trên {target.GetType().Name}");
            }
        }

        #endregion
    }
}
#endif
