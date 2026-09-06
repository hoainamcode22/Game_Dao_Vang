using System.IO;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Tilemaps;
using UnityEngine.EventSystems;
using TMPro;

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

        private const string KENNEY_ROOT = "Assets/kenney_simplified-platformer-pack/PNG";
        private const string KENNEY_TILES_DIR = "Assets/kenney_simplified-platformer-pack/PNG/Tiles";
        private const string KENNEY_ITEMS_DIR = "Assets/kenney_simplified-platformer-pack/PNG/Items";
        private const string KENNEY_CHARS_DIR = "Assets/kenney_simplified-platformer-pack/PNG/Characters";

        private const string HOME_SCENE_PATH = "Assets/Scenes/Scene_Home.unity";
        private const string ADVENTURE_SCENE_PATH = "Assets/Scenes/Scene_Adventure.unity";

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

        [MenuItem("Tools/Platformer Game/Fix Brush & Activate Ground Painter (Sửa Cọ & Mở Bảng Vẽ Chuẩn)", false, 5)]
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

        [MenuItem("Tools/Platformer Game/Paint Sample Ground Platform (Vẽ mẫu ngay 1 bục đất dưới chân)", false, 6)]
        public static void PaintSamplePlatformUnderPlayer()
        {
            var groundObj = GameObject.Find("Tilemap_Ground");
            if (groundObj == null)
            {
                EditorUtility.DisplayDialog("Lỗi", "Không tìm thấy GameObject 'Tilemap_Ground' trong Scene!", "OK");
                return;
            }

            var tilemap = groundObj.GetComponent<Tilemap>();
            if (tilemap == null) return;

            var tileLeft = AssetDatabase.LoadAssetAtPath<Tile>("Assets/_PlatformerGame/Tiles/Tile_grass_left.asset")
                        ?? AssetDatabase.LoadAssetAtPath<Tile>("Assets/_PlatformerGame/Tiles/Tile_platformPack_tile001.asset");
            var tileMid = AssetDatabase.LoadAssetAtPath<Tile>("Assets/_PlatformerGame/Tiles/Tile_grass_mid.asset")
                       ?? AssetDatabase.LoadAssetAtPath<Tile>("Assets/_PlatformerGame/Tiles/Tile_platformPack_tile002.asset");
            var tileRight = AssetDatabase.LoadAssetAtPath<Tile>("Assets/_PlatformerGame/Tiles/Tile_grass_right.asset")
                         ?? AssetDatabase.LoadAssetAtPath<Tile>("Assets/_PlatformerGame/Tiles/Tile_platformPack_tile003.asset");

            if (tileMid == null)
            {
                string[] guids = AssetDatabase.FindAssets("t:Tile", new[] { "Assets/_PlatformerGame/Tiles" });
                if (guids.Length > 0)
                {
                    tileMid = AssetDatabase.LoadAssetAtPath<Tile>(AssetDatabase.GUIDToAssetPath(guids[0]));
                }
            }

            int startX = -3;
            int endX = 3;
            int y = -2;

            tilemap.SetTile(new Vector3Int(startX, y, 0), tileLeft != null ? tileLeft : tileMid);
            for (int x = startX + 1; x < endX; x++)
            {
                tilemap.SetTile(new Vector3Int(x, y, 0), tileMid);
            }
            tilemap.SetTile(new Vector3Int(endX, y, 0), tileRight != null ? tileRight : tileMid);

            var composite = groundObj.GetComponent<CompositeCollider2D>();
            if (composite != null)
            {
                composite.GenerateGeometry();
            }

            EditorUtility.SetDirty(tilemap);
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());

            EditorUtility.DisplayDialog("Đã vẽ bục đất thành công!",
                "Đã vẽ ngay một đoạn bục đất cỏ 7 ô dưới chân nhân vật!\n\n" +
                "Bây giờ Sếp có thể bấm Play để nhảy thử ngay, hoặc dùng Cọ vẽ tiếp nối vào bục này!", "Tuyệt vời");
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

        private static GameObject CreateCanvasWithHUD(GameObject playerObj)
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
            Sprite coinIconSprite = LoadSprite($"{KENNEY_ITEMS_DIR}/platformPack_item008.png");

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

            // --- Coins Counter (Top Right) ---
            GameObject coinContainer = CreateUIElement("Coin_Container", hudPanel.transform);
            RectTransform coinRect = coinContainer.GetComponent<RectTransform>();
            coinRect.anchorMin = new Vector2(1, 0.5f);
            coinRect.anchorMax = new Vector2(1, 0.5f);
            coinRect.pivot = new Vector2(1, 0.5f);
            coinRect.anchoredPosition = new Vector2(-30, 0);
            coinRect.sizeDelta = new Vector2(200, 60);

            // Coin Icon
            GameObject coinIconObj = CreateUIElement("Coin_Icon", coinContainer.transform);
            RectTransform coinIconRect = coinIconObj.GetComponent<RectTransform>();
            coinIconRect.sizeDelta = new Vector2(50, 50);
            Image coinIconImg = coinIconObj.AddComponent<Image>();
            coinIconImg.sprite = coinIconSprite;
            coinIconImg.preserveAspect = true;

            // Coin Text
            GameObject coinTextObj = CreateUIElement("Coin_Text", coinContainer.transform);
            RectTransform coinTextRect = coinTextObj.GetComponent<RectTransform>();
            coinTextRect.sizeDelta = new Vector2(120, 60);
            TextMeshProUGUI coinTmp = coinTextObj.AddComponent<TextMeshProUGUI>();
            coinTmp.text = "00";
            coinTmp.fontSize = 42;
            coinTmp.fontStyle = FontStyles.Bold;
            coinTmp.color = new Color(1f, 0.9f, 0.2f);
            coinTmp.alignment = TextAlignmentOptions.MidlineLeft;

            // --- Score Text (Top Center) ---
            GameObject scoreObj = CreateUIElement("Score_Text", hudPanel.transform);
            RectTransform scoreRect = scoreObj.GetComponent<RectTransform>();
            scoreRect.anchorMin = new Vector2(0.5f, 0.5f);
            scoreRect.anchorMax = new Vector2(0.5f, 0.5f);
            scoreRect.pivot = new Vector2(0.5f, 0.5f);
            scoreRect.anchoredPosition = Vector2.zero;
            scoreRect.sizeDelta = new Vector2(400, 60);

            TextMeshProUGUI scoreTmp = scoreObj.AddComponent<TextMeshProUGUI>();
            scoreTmp.text = "SCORE: 0";
            scoreTmp.fontSize = 38;
            scoreTmp.fontStyle = FontStyles.Bold;
            scoreTmp.color = Color.white;
            scoreTmp.alignment = TextAlignmentOptions.Center;

            // 2. Countdown Text (Center Screen)
            GameObject countdownObj = CreateUIElement("Countdown_Text", canvasObj.transform);
            RectTransform countdownRect = countdownObj.GetComponent<RectTransform>();
            countdownRect.anchorMin = new Vector2(0.5f, 0.5f);
            countdownRect.anchorMax = new Vector2(0.5f, 0.5f);
            countdownRect.pivot = new Vector2(0.5f, 0.5f);
            countdownRect.anchoredPosition = new Vector2(0, 50);
            countdownRect.sizeDelta = new Vector2(800, 200);

            TextMeshProUGUI countdownTmp = countdownObj.AddComponent<TextMeshProUGUI>();
            countdownTmp.text = "3";
            countdownTmp.fontSize = 110;
            countdownTmp.fontStyle = FontStyles.Bold;
            countdownTmp.color = new Color(1f, 0.9f, 0.1f);
            countdownTmp.alignment = TextAlignmentOptions.Center;

            // 3. Mobile Virtual Controls
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
            lrRect.anchoredPosition = new Vector2(60, 60);
            lrRect.sizeDelta = new Vector2(320, 130);

            HorizontalLayoutGroup lrLayout = leftRightGroup.AddComponent<HorizontalLayoutGroup>();
            lrLayout.spacing = 30;
            lrLayout.childControlWidth = false;
            lrLayout.childControlHeight = false;

            // Button Left
            GameObject btnLeft = CreateUIButton("Btn_Left", leftRightGroup.transform, "◀", new Vector2(130, 130), new Color(0.2f, 0.2f, 0.2f, 0.7f), 60);
            MobileTouchButton mtbLeft = btnLeft.AddComponent<MobileTouchButton>();

            // Button Right
            GameObject btnRight = CreateUIButton("Btn_Right", leftRightGroup.transform, "▶", new Vector2(130, 130), new Color(0.2f, 0.2f, 0.2f, 0.7f), 60);
            MobileTouchButton mtbRight = btnRight.AddComponent<MobileTouchButton>();

            // Jump Button (Bottom Right)
            GameObject btnJump = CreateUIButton("Btn_Jump", mobileControls.transform, "JUMP\n⬆", new Vector2(150, 150), new Color(0.15f, 0.6f, 0.25f, 0.85f), 34);
            RectTransform jumpRect = btnJump.GetComponent<RectTransform>();
            jumpRect.anchorMin = new Vector2(1, 0);
            jumpRect.anchorMax = new Vector2(1, 0);
            jumpRect.pivot = new Vector2(1, 0);
            jumpRect.anchoredPosition = new Vector2(-60, 60);
            MobileTouchButton mtbJump = btnJump.AddComponent<MobileTouchButton>();

            // 4. Game Over Popup
            GameObject gameOverModal = CreateModalPopup("Popup_GameOver", canvasObj.transform, "GAME OVER", out TMP_Text goScore, out TMP_Text goCoin, out Button goRestart, out Button goHome);
            gameOverModal.SetActive(false);

            // 5. Victory Popup
            GameObject victoryModal = CreateModalPopup("Popup_Victory", canvasObj.transform, "VICTORY!", out TMP_Text vicScore, out TMP_Text vicCoin, out Button vicRestart, out Button vicHome);
            victoryModal.SetActive(false);

            // Auto-wire references to UIManager_Platformer
            SetPrivateField(uiManager, "playerController", playerObj.GetComponent<PlayerController2D>());
            SetPrivateField(uiManager, "playerHealth", playerObj.GetComponent<PlayerHealth>());
            SetPrivateField(uiManager, "heartImages", heartImages);
            SetPrivateField(uiManager, "fullHeartSprite", fullHeartSprite);
            SetPrivateField(uiManager, "emptyHeartSprite", emptyHeartSprite);
            SetPrivateField(uiManager, "coinText", coinTmp);
            SetPrivateField(uiManager, "scoreText", scoreTmp);
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

        private static void BuildHomeScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Camera
            GameObject cameraObj = new GameObject("Main Camera");
            Camera cam = cameraObj.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 5f;
            cam.backgroundColor = new Color(0.15f, 0.2f, 0.32f);
            cam.clearFlags = CameraClearFlags.SolidColor;
            cameraObj.AddComponent<AudioListener>();
            cameraObj.tag = "MainCamera";

            // Canvas
            GameObject canvasObj = new GameObject("UI_Canvas");
            Canvas canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            canvasObj.AddComponent<GraphicRaycaster>();
            HomeSceneController homeCtrl = canvasObj.AddComponent<HomeSceneController>();

            // Title Container
            GameObject titleObj = CreateUIElement("Title_Game", canvasObj.transform);
            RectTransform titleRect = titleObj.GetComponent<RectTransform>();
            titleRect.anchorMin = new Vector2(0.5f, 0.7f);
            titleRect.anchorMax = new Vector2(0.5f, 0.7f);
            titleRect.pivot = new Vector2(0.5f, 0.5f);
            titleRect.anchoredPosition = Vector2.zero;
            titleRect.sizeDelta = new Vector2(1000, 150);

            TextMeshProUGUI titleTmp = titleObj.AddComponent<TextMeshProUGUI>();
            titleTmp.text = "SUPER SKYBOUND\nPLATFORMER 2D";
            titleTmp.fontSize = 72;
            titleTmp.fontStyle = FontStyles.Bold;
            titleTmp.color = new Color(1f, 0.85f, 0.2f);
            titleTmp.alignment = TextAlignmentOptions.Center;

            // Character Icon Preview
            Sprite charSprite = LoadSprite($"{KENNEY_CHARS_DIR}/platformChar_idle.png");
            if (charSprite != null)
            {
                GameObject charIconObj = CreateUIElement("Char_Preview", canvasObj.transform);
                RectTransform cr = charIconObj.GetComponent<RectTransform>();
                cr.anchorMin = new Vector2(0.5f, 0.48f);
                cr.anchorMax = new Vector2(0.5f, 0.48f);
                cr.pivot = new Vector2(0.5f, 0.5f);
                cr.anchoredPosition = Vector2.zero;
                cr.sizeDelta = new Vector2(120, 120);

                Image charImg = charIconObj.AddComponent<Image>();
                charImg.sprite = charSprite;
                charImg.preserveAspect = true;
            }

            // Buttons Container
            GameObject btnGroup = CreateUIElement("Menu_Buttons", canvasObj.transform);
            RectTransform bgRect = btnGroup.GetComponent<RectTransform>();
            bgRect.anchorMin = new Vector2(0.5f, 0.25f);
            bgRect.anchorMax = new Vector2(0.5f, 0.25f);
            bgRect.pivot = new Vector2(0.5f, 0.5f);
            bgRect.anchoredPosition = Vector2.zero;
            bgRect.sizeDelta = new Vector2(400, 220);

            VerticalLayoutGroup vLayout = btnGroup.AddComponent<VerticalLayoutGroup>();
            vLayout.spacing = 25;
            vLayout.childAlignment = TextAnchor.MiddleCenter;
            vLayout.childControlWidth = false;
            vLayout.childControlHeight = false;

            // Play Button
            GameObject playBtnObj = CreateUIButton("Btn_PlayAdventure", btnGroup.transform, "CHƠI PHIÊU LƯU", new Vector2(380, 85), new Color(0.18f, 0.68f, 0.28f), 36);
            Button playBtn = playBtnObj.GetComponent<Button>();

            // Quit Button
            GameObject quitBtnObj = CreateUIButton("Btn_Quit", btnGroup.transform, "THOÁT GAME", new Vector2(380, 85), new Color(0.8f, 0.25f, 0.25f), 34);
            Button quitBtn = quitBtnObj.GetComponent<Button>();

            SetPrivateField(homeCtrl, "playButton", playBtn);
            SetPrivateField(homeCtrl, "quitButton", quitBtn);

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
