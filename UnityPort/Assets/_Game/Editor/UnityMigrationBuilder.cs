using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using Garden.Game;
using TMPro;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace Garden.EditorTools
{
    /// <summary>
    /// Reconstruye únicamente los recursos generados de la migración. Las imágenes
    /// originales permanecen en Art y sus recortes se guardan por separado.
    /// </summary>
    public static class UnityMigrationBuilder
    {
        private const string Root = "Assets/_Game";
        private const string ArtRoot = Root + "/Art";
        private const string PreparedRoot = ArtRoot + "/Prepared";
        private const string DataRoot = Root + "/Data";
        private const string MaterialsRoot = Root + "/Materials";
        private const string PrefabsRoot = Root + "/Prefabs";
        public const string ScenePath = Root + "/Scenes/Game.unity";
        private const string ArtAssetPath = DataRoot + "/GameArt.asset";

        [MenuItem("Jardin/Reconstruir escena y recursos")]
        public static void BuildProject()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            EnsureFolders();
            ConfigureProject();
            ConfigureOriginalImages();
            CreatePrimitiveSprites();

            GameArt art = AssetDatabase.LoadAssetAtPath<GameArt>(ArtAssetPath);
            if (art == null)
            {
                art = ScriptableObject.CreateInstance<GameArt>();
                AssetDatabase.CreateAsset(art, ArtAssetPath);
            }

            art.sunflower = LoadSprite(ArtRoot + "/plants/sprites/girasol.png");
            art.peashooter = LoadSprite(ArtRoot + "/plants/sprites/Guisantes.png");
            art.sunflowerCard = LoadSprite(ArtRoot + "/plants/banners/girasolBanner.png");
            art.peashooterCard = LoadSprite(ArtRoot + "/plants/banners/guisantesBanner.png");
            art.grass = LoadSprite(ArtRoot + "/Cartoon_green_texture_grass.jpg");
            art.white = LoadSprite(PreparedRoot + "/White.png");
            art.circle = LoadSprite(PreparedRoot + "/Circle.png");
            art.sun = CreateAlphaCrop(ArtRoot + "/currency.png", "Sun.png", false);
            art.shovel = CreateAlphaCrop(ArtRoot + "/Pala.png", "Shovel.png", false);

            art.zombieWalk = new Sprite[5];
            for (int index = 0; index < art.zombieWalk.Length; index++)
                art.zombieWalk[index] = CreateAlphaCrop(
                    ZombieSource("zombieCaminando", index), "ZombieWalk" + index + ".png", true);

            art.zombieEat = new Sprite[3];
            for (int index = 0; index < art.zombieEat.Length; index++)
                art.zombieEat[index] = CreateAlphaCrop(
                    ZombieSource("zombieComiendo", index), "ZombieEat" + index + ".png", true);

            art.zombieDie = new Sprite[2];
            for (int index = 0; index < art.zombieDie.Length; index++)
                art.zombieDie[index] = CreateAlphaCrop(
                    ZombieSource("zombieAgachandose", index), "ZombieDie" + index + ".png", true, true);

            art.font = FindOrImportFont();
            art.spriteMaterial = EnsureMaterial("SpriteUnlit", "Universal Render Pipeline/2D/Sprite-Unlit-Default");
            art.flashMaterial = EnsureMaterial("ZombieFlash", "Garden/SpriteFlash");
            art.groundMaterial = EnsureMaterial("GrassCells", "Garden/Ground");
            if (art.groundMaterial.HasProperty("_MainTex"))
                art.groundMaterial.SetTexture("_MainTex", art.grass.texture);
            if (art.groundMaterial.HasProperty("_GrassTex"))
                art.groundMaterial.SetTexture("_GrassTex", art.grass.texture);

            art.plantViewPrefab = CreateViewPrefab("PlantView", art.sunflower, art.spriteMaterial, "Actors");
            CreateViewPrefab("Sunflower", art.sunflower, art.spriteMaterial, "Actors");
            CreateViewPrefab("Peashooter", art.peashooter, art.spriteMaterial, "Actors");
            art.zombieViewPrefab = CreateViewPrefab("ZombieView", art.zombieWalk[0], art.flashMaterial, "Actors");
            art.peaViewPrefab = CreateViewPrefab("PeaView", art.circle, art.spriteMaterial, "Projectiles");
            art.sunViewPrefab = CreateViewPrefab("SunView", art.sun, art.spriteMaterial, "Pickups");
            EditorUtility.SetDirty(art);
            EditorUtility.SetDirty(art.groundMaterial);

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            scene.name = "Game";
            var cameraObject = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
            cameraObject.tag = "MainCamera";
            Camera camera = cameraObject.GetComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 5.4f;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 100f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.28f, 0.46f, 0.18f, 1f);
            cameraObject.transform.position = new Vector3(9.6f, 5.4f, -10f);

            var gameObject = new GameObject("Game");
            GameRoot game = gameObject.AddComponent<GameRoot>();
            game.art = art;
            EditorUtility.SetDirty(game);
            if (!EditorSceneManager.SaveScene(scene, ScenePath))
                throw new InvalidOperationException("No se pudo guardar " + ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            RunSmokeTests();
            Debug.Log("[Jardin] Migración preparada. Abre " + ScenePath + " y pulsa Play.");
        }

        [MenuItem("Jardin/Compilar Windows")]
        public static void BuildWindows()
        {
            ConfigureProject();
            AssetDatabase.SaveAssets();
            if (AssetDatabase.LoadAssetAtPath<GameArt>(ArtAssetPath) == null || !File.Exists(ScenePath))
                BuildProject();
            RunSmokeTests();
            if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Standalone, BuildTarget.StandaloneWindows64))
                throw new InvalidOperationException("Falta el módulo de Windows de este Editor en Unity Hub.");

            string outputDirectory = Path.GetFullPath("Build/Windows");
            Directory.CreateDirectory(outputDirectory);
            BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                target = BuildTarget.StandaloneWindows64,
                locationPathName = Path.Combine(outputDirectory, "Jardin.exe"),
                options = BuildOptions.None
            });
            var validation = new BuildValidation
            {
                unityVersion = Application.unityVersion,
                result = report.summary.result.ToString(),
                errors = report.summary.totalErrors,
                warnings = report.summary.totalWarnings,
                outputPath = report.summary.outputPath,
                sizeBytes = report.summary.totalSize,
                durationSeconds = report.summary.totalTime.TotalSeconds
            };
            WriteValidation("build-validation.json", JsonUtility.ToJson(validation, true));
            if (report.summary.result != BuildResult.Succeeded)
                throw new InvalidOperationException("Falló la compilación Windows: " + report.summary.result);
            Debug.Log("[Jardin] Build Windows correcto: " + report.summary.outputPath);
        }

        [MenuItem("Jardin/Verificar recursos y escena")]
        public static void RunSmokeTests()
        {
            GameArt art = AssetDatabase.LoadAssetAtPath<GameArt>(ArtAssetPath);
            Require(art != null, "No existe GameArt.asset.");
            Sprite[] essential =
            {
                art.sunflower, art.peashooter, art.sunflowerCard, art.peashooterCard,
                art.sun, art.shovel, art.white, art.circle, art.grass
            };
            Require(essential.All(sprite => sprite != null), "Falta una imagen en GameArt.");
            Require(art.zombieWalk != null && art.zombieWalk.Length == 5 && art.zombieWalk.All(sprite => sprite != null), "Deben existir 5 fotogramas de caminar.");
            Require(art.zombieEat != null && art.zombieEat.Length == 3 && art.zombieEat.All(sprite => sprite != null), "Deben existir 3 fotogramas de comer.");
            Require(art.zombieDie != null && art.zombieDie.Length == 2 && art.zombieDie.All(sprite => sprite != null), "Deben existir 2 fotogramas de caída.");
            Require(art.font != null, "Falta la fuente TMP.");
            Require(art.spriteMaterial != null && art.flashMaterial != null && art.groundMaterial != null, "Falta algún material.");
            Require(art.flashMaterial.shader != null && !ShaderUtil.ShaderHasError(art.flashMaterial.shader), "El shader de destello no compiló correctamente.");
            Require(art.groundMaterial.shader != null && !ShaderUtil.ShaderHasError(art.groundMaterial.shader), "El shader de suelo no compiló correctamente.");
            Require(art.flashMaterial.HasProperty("_FlashAmount"), "El shader de zombi no tiene _FlashAmount.");
            Require(art.plantViewPrefab != null && art.zombieViewPrefab != null && art.peaViewPrefab != null && art.sunViewPrefab != null, "Falta algún prefab visual.");
            Require(File.Exists(ScenePath), "Falta Game.unity.");
            Require(EditorBuildSettings.scenes.Any(entry => entry.path == ScenePath && entry.enabled), "Game.unity no está habilitada para el build.");

            RenderPipelineAsset pipeline = GraphicsSettings.defaultRenderPipeline;
            Require(pipeline != null && pipeline.GetType().Name.Contains("UniversalRenderPipelineAsset"), "La escena requiere Universal Render Pipeline.");
            var pipelineObject = new SerializedObject(pipeline);
            SerializedProperty renderers = pipelineObject.FindProperty("m_RendererDataList");
            Require(renderers != null && renderers.arraySize > 0, "La configuración URP no contiene renderers.");
            bool has2DRenderer = false;
            for (int index = 0; index < renderers.arraySize; index++)
            {
                UnityEngine.Object renderer = renderers.GetArrayElementAtIndex(index).objectReferenceValue;
                has2DRenderer |= renderer != null && renderer.GetType().Name == "Renderer2DData";
            }
            Require(has2DRenderer, "La plantilla debe conservar su Renderer 2D.");

            var validation = new ResourceValidation
            {
                unityVersion = Application.unityVersion,
                scene = ScenePath,
                result = "passed",
                spriteCount = essential.Length + art.zombieWalk.Length + art.zombieEat.Length + art.zombieDie.Length,
                walkingFrames = art.zombieWalk.Length,
                eatingFrames = art.zombieEat.Length,
                fallingFrames = art.zombieDie.Length,
                universal2D = true,
                utc = DateTime.UtcNow.ToString("O")
            };
            WriteValidation("editor-validation.json", JsonUtility.ToJson(validation, true));
            Debug.Log("[Jardin] Recursos, prefabs, escena y Renderer 2D verificados.");
        }

        private static void EnsureFolders()
        {
            foreach (string folder in new[] { PreparedRoot, DataRoot, MaterialsRoot, PrefabsRoot, Root + "/Scenes" })
                Directory.CreateDirectory(folder);
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        }

        private static void ConfigureProject()
        {
            RenderPipelineAsset pipeline = AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>("Assets/Settings/UniversalRP.asset");
            Require(pipeline != null, "Falta el asset UniversalRP de la plantilla Universal 2D.");
            GraphicsSettings.defaultRenderPipeline = pipeline;
            QualitySettings.renderPipeline = pipeline;
            UnityEngine.Object renderer2D = AssetDatabase.LoadMainAssetAtPath("Assets/Settings/Renderer2D.asset");
            Require(renderer2D != null, "Falta el Renderer 2D de la plantilla.");
            var rendererSettings = new SerializedObject(renderer2D);
            SerializedProperty postProcessData = rendererSettings.FindProperty("m_PostProcessData");
            Require(postProcessData != null, "El Renderer 2D no expone sus recursos de postprocesado.");
            // El original no usa postefectos: este es el mismo ajuste que desactivar
            // Post-processing/Enabled en el Inspector oficial del Renderer 2D.
            postProcessData.objectReferenceValue = null;
            rendererSettings.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(renderer2D);
            PlayerSettings.companyName = "Javeriana";
            PlayerSettings.productName = "JardinUnity";
            PlayerSettings.defaultScreenWidth = 1920;
            PlayerSettings.defaultScreenHeight = 1080;
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            PlayerSettings.resizableWindow = true;
            PlayerSettings.runInBackground = true;
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone, ScriptingImplementation.Mono2x);
            EditorSettings.serializationMode = SerializationMode.ForceText;
            EnsureSortingLayers();
        }

        private static void EnsureSortingLayers()
        {
            var tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            SerializedProperty layers = tagManager.FindProperty("m_SortingLayers");
            string[] names = { "Ground", "Highlight", "Actors", "Projectiles", "Pickups", "Preview" };
            for (int nameIndex = 0; nameIndex < names.Length; nameIndex++)
            {
                string name = names[nameIndex];
                bool exists = false;
                for (int index = 0; index < layers.arraySize; index++)
                    exists |= layers.GetArrayElementAtIndex(index).FindPropertyRelative("name").stringValue == name;
                if (exists) continue;
                int insertion = layers.arraySize;
                layers.InsertArrayElementAtIndex(insertion);
                SerializedProperty entry = layers.GetArrayElementAtIndex(insertion);
                entry.FindPropertyRelative("name").stringValue = name;
                entry.FindPropertyRelative("uniqueID").intValue = 220001 + nameIndex;
                entry.FindPropertyRelative("locked").boolValue = false;
            }
            tagManager.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void ConfigureOriginalImages()
        {
            foreach (string path in Directory.GetFiles(ArtRoot, "*", SearchOption.AllDirectories))
            {
                string normalized = path.Replace('\\', '/');
                if (normalized.StartsWith(PreparedRoot + "/", StringComparison.Ordinal)) continue;
                string extension = Path.GetExtension(path).ToLowerInvariant();
                if (extension != ".png" && extension != ".jpg" && extension != ".jpeg") continue;
                ConfigureSprite(normalized, new Vector2(0.5f, 0.5f), normalized.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase));
            }
        }

        private static void ConfigureSprite(string path, Vector2 pivot, bool ground = false)
        {
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            Require(importer != null, "No se pudo importar " + path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 100;
            importer.filterMode = FilterMode.Bilinear;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.alphaIsTransparency = !ground;
            importer.mipmapEnabled = ground;
            importer.maxTextureSize = ground ? 8192 : 2048;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.isReadable = false;
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect;
            settings.spriteAlignment = (int)SpriteAlignment.Custom;
            settings.spritePivot = pivot;
            settings.spriteGenerateFallbackPhysicsShape = false;
            importer.SetTextureSettings(settings);
            importer.SaveAndReimport();
        }

        private static Sprite CreateAlphaCrop(string sourcePath, string outputName, bool feetPivot, bool centeredFeet = false)
        {
            // La lectura usa una textura temporal: el asset importado no necesita Read/Write.
            var source = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!ImageConversion.LoadImage(source, File.ReadAllBytes(sourcePath)))
                throw new InvalidOperationException("PNG inválido: " + sourcePath);
            Color32[] pixels = source.GetPixels32();
            int left = source.width;
            int right = -1;
            int bottom = source.height;
            int top = -1;
            for (int y = 0; y < source.height; y++)
            {
                for (int x = 0; x < source.width; x++)
                {
                    if (pixels[y * source.width + x].a <= 25) continue;
                    left = Math.Min(left, x);
                    right = Math.Max(right, x);
                    bottom = Math.Min(bottom, y);
                    top = Math.Max(top, y);
                }
            }
            Require(right >= left && top >= bottom, "No hay píxeles visibles en " + sourcePath);
            int width = right - left + 1;
            int height = top - bottom + 1;
            var cropped = new Texture2D(width, height, TextureFormat.RGBA32, false);
            cropped.SetPixels(source.GetPixels(left, bottom, width, height));
            cropped.Apply();
            string outputPath = PreparedRoot + "/" + outputName;
            File.WriteAllBytes(outputPath, cropped.EncodeToPNG());
            // El centro X de la imagen original ancla caminar/comer; todos los pies usan Y=0.
            Vector2 pivot = feetPivot
                ? new Vector2(centeredFeet ? 0.5f : (source.width * 0.5f - left) / width, 0f)
                : new Vector2(0.5f, 0.5f);
            UnityEngine.Object.DestroyImmediate(cropped);
            UnityEngine.Object.DestroyImmediate(source);
            ConfigureSprite(outputPath, pivot);
            return LoadSprite(outputPath);
        }

        private static void CreatePrimitiveSprites()
        {
            const int size = 64;
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float distance = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(size * 0.5f, size * 0.5f));
                    byte alpha = (byte)Mathf.RoundToInt(Mathf.Clamp01(size * 0.5f - distance) * 255f);
                    pixels[y * size + x] = new Color32(255, 255, 255, alpha);
                }
            }
            var circle = new Texture2D(size, size, TextureFormat.RGBA32, false);
            circle.SetPixels32(pixels);
            circle.Apply();
            File.WriteAllBytes(PreparedRoot + "/Circle.png", circle.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(circle);
            var white = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            white.SetPixels(new[] { Color.white, Color.white, Color.white, Color.white });
            white.Apply();
            File.WriteAllBytes(PreparedRoot + "/White.png", white.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(white);
            ConfigureSprite(PreparedRoot + "/Circle.png", new Vector2(0.5f, 0.5f));
            ConfigureSprite(PreparedRoot + "/White.png", new Vector2(0.5f, 0.5f));
        }

        private static TMP_FontAsset FindOrImportFont()
        {
            TMP_FontAsset font = LoadFontAsset();
            if (font != null) return font;
            UnityEditor.PackageManager.PackageInfo package = UnityEditor.PackageManager.PackageInfo.FindForAssembly(typeof(TMP_Text).Assembly);
            if (package != null && Directory.Exists(package.resolvedPath))
            {
                string essentialPackage = Directory.GetFiles(package.resolvedPath, "TMP Essential Resources.unitypackage", SearchOption.AllDirectories).FirstOrDefault();
                if (!string.IsNullOrEmpty(essentialPackage))
                {
                    ExtractUnityPackageSynchronous(essentialPackage);
                    AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                    font = LoadFontAsset();
                }
            }
            Require(font != null, "No se encontró LiberationSans SDF. Importa TMP Essential Resources y reconstruye.");
            return font;
        }

        private static void ExtractUnityPackageSynchronous(string packagePath)
        {
            // Import() deja el trabajo en una cola del Editor. En batch necesitamos
            // los assets antes de que termine esta misma llamada a executeMethod.
            var entries = new Dictionary<string, byte[]>(StringComparer.Ordinal);
            using (FileStream file = File.OpenRead(packagePath))
            using (var gzip = new GZipStream(file, CompressionMode.Decompress))
            {
                var header = new byte[512];
                while (ReadTarBlock(gzip, header))
                {
                    if (header.All(value => value == 0)) break;
                    string name = Encoding.UTF8.GetString(header, 0, 100).TrimEnd('\0');
                    string prefix = Encoding.UTF8.GetString(header, 345, 155).TrimEnd('\0');
                    if (!string.IsNullOrEmpty(prefix)) name = prefix + "/" + name;
                    string sizeText = Encoding.ASCII.GetString(header, 124, 12).Trim('\0', ' ');
                    long length = string.IsNullOrEmpty(sizeText) ? 0 : Convert.ToInt64(sizeText, 8);
                    if (length < 0 || length > int.MaxValue)
                        throw new InvalidDataException("Entrada de unitypackage demasiado grande: " + name);
                    var contents = new byte[(int)length];
                    ReadExactly(gzip, contents, contents.Length);
                    // Solo se necesitan archivos normales; directorios y extensiones
                    // de tar no representan recursos que debamos instalar.
                    if (header[156] == 0 || header[156] == (byte)'0')
                        entries[name.Replace('\\', '/')] = contents;
                    int padding = (int)((512 - length % 512) % 512);
                    if (padding > 0) ReadExactly(gzip, new byte[padding], padding);
                }
            }

            string assetsRoot = Path.GetFullPath(Application.dataPath);
            string allowedPrefix = assetsRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            string projectRoot = Directory.GetParent(assetsRoot).FullName;
            int extracted = 0;
            foreach (KeyValuePair<string, byte[]> entry in entries)
            {
                if (!entry.Key.EndsWith("/pathname", StringComparison.Ordinal)) continue;
                string assetPath = Encoding.UTF8.GetString(entry.Value).Trim('\ufeff', '\0', '\r', '\n');
                string destination = Path.GetFullPath(Path.Combine(projectRoot, assetPath));
                if (!destination.StartsWith(allowedPrefix, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("El unitypackage intenta escribir fuera de Assets: " + assetPath);

                string key = entry.Key.Substring(0, entry.Key.Length - "/pathname".Length);
                if (entries.TryGetValue(key + "/asset", out byte[] assetBytes))
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(destination));
                    if (!File.Exists(destination)) File.WriteAllBytes(destination, assetBytes);
                }
                else Directory.CreateDirectory(destination);

                if (entries.TryGetValue(key + "/asset.meta", out byte[] metaBytes) && !File.Exists(destination + ".meta"))
                    File.WriteAllBytes(destination + ".meta", metaBytes);
                extracted++;
            }
            Require(extracted > 0, "El paquete TMP no contiene rutas de recursos válidas.");
            Debug.Log("[Jardin] Recursos TMP instalados de forma síncrona: " + extracted + " rutas.");
        }

        private static bool ReadTarBlock(Stream stream, byte[] block)
        {
            int first = stream.ReadByte();
            if (first < 0) return false;
            block[0] = (byte)first;
            int offset = 1;
            while (offset < block.Length)
            {
                int count = stream.Read(block, offset, block.Length - offset);
                if (count <= 0) throw new EndOfStreamException("Cabecera tar incompleta en unitypackage.");
                offset += count;
            }
            return true;
        }

        private static void ReadExactly(Stream stream, byte[] destination, int length)
        {
            int offset = 0;
            while (offset < length)
            {
                int count = stream.Read(destination, offset, length - offset);
                if (count <= 0) throw new EndOfStreamException("Entrada tar incompleta en unitypackage.");
                offset += count;
            }
        }

        private static TMP_FontAsset LoadFontAsset()
        {
            string[] guids = AssetDatabase.FindAssets("LiberationSans SDF t:TMP_FontAsset");
            foreach (string guid in guids)
            {
                TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(AssetDatabase.GUIDToAssetPath(guid));
                if (font != null) return font;
            }
            string[] allFonts = AssetDatabase.FindAssets("t:TMP_FontAsset");
            return allFonts.Select(guid => AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(AssetDatabase.GUIDToAssetPath(guid))).FirstOrDefault(font => font != null);
        }

        private static Material EnsureMaterial(string name, string shaderName)
        {
            Shader shader = Shader.Find(shaderName);
            Require(shader != null, "No se encontró el shader " + shaderName);
            string path = MaterialsRoot + "/" + name + ".mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(shader) { name = name };
                AssetDatabase.CreateAsset(material, path);
            }
            else material.shader = shader;
            EditorUtility.SetDirty(material);
            return material;
        }

        private static GameObject CreateViewPrefab(string name, Sprite sprite, Material material, string sortingLayer)
        {
            var root = new GameObject(name);
            var visual = new GameObject("Visual", typeof(SpriteRenderer));
            visual.transform.SetParent(root.transform, false);
            SpriteRenderer renderer = visual.GetComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sharedMaterial = material;
            renderer.sortingLayerName = sortingLayer;
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabsRoot + "/" + name + ".prefab");
            UnityEngine.Object.DestroyImmediate(root);
            Require(prefab != null, "No se pudo crear el prefab " + name);
            return prefab;
        }

        private static Sprite LoadSprite(string path)
        {
            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            Require(sprite != null, "No se encontró el Sprite " + path);
            return sprite;
        }

        private static string ZombieSource(string name, int index)
        {
            return ArtRoot + "/Zombies/" + name + (index == 0 ? string.Empty : index.ToString()) + ".png";
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException("[Jardin] " + message);
        }

        private static void WriteValidation(string name, string contents)
        {
            Directory.CreateDirectory("Validation");
            File.WriteAllText(Path.Combine("Validation", name), contents);
        }

        [Serializable]
        private sealed class ResourceValidation
        {
            public string unityVersion;
            public string scene;
            public string result;
            public int spriteCount;
            public int walkingFrames;
            public int eatingFrames;
            public int fallingFrames;
            public bool universal2D;
            public string utc;
        }

        [Serializable]
        private sealed class BuildValidation
        {
            public string unityVersion;
            public string result;
            public int errors;
            public int warnings;
            public string outputPath;
            public ulong sizeBytes;
            public double durationSeconds;
        }
    }
}
