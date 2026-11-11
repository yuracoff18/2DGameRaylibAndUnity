using System;
using System.Collections;
using System.IO;
using Garden.Model;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Garden.Game
{
    /// <summary>Se activa solo con -gardenSmoke; prueba la escena real dentro del ejecutable.</summary>
    public sealed class PlayerSmokeProbe : MonoBehaviour
    {
        [Serializable]
        private sealed class Result
        {
            public string unityVersion;
            public string graphicsDevice;
            public bool passed;
            public bool screenshot;
            public bool gameOverScreenshot;
            public bool placementScreenshot;
            public int spriteRenderers;
            public int textObjects;
            public string error;
        }

        private IEnumerator Start()
        {
            yield return null;
            GameRoot root = GetComponent<GameRoot>();
            root.enabled = false;
            Result result = new Result
            {
                unityVersion = Application.unityVersion,
                graphicsDevice = SystemInfo.graphicsDeviceType.ToString()
            };
            string output = ReadArgument("-gardenSmokeOutput") ??
                Path.Combine(Application.persistentDataPath, "GardenSmoke");
            try
            {
                Directory.CreateDirectory(output);
                Require(SystemInfo.graphicsDeviceType != GraphicsDeviceType.Null, "La comprobacion grafica requiere una GPU");
                GameSession game = root.Session;
                game.Retry();
                game.AutoSpawnEnabled = false;
                Click(game, game.Layout.Card(0).Center);
                Click(game, game.Layout.Cell(2, 2).Center);
                Click(game, game.Layout.Card(1).Center);
                Click(game, game.Layout.Cell(1, 2).Center);
                Require(game.Currency == 850 && game.Plants.Count == 2, "Compra y colocacion");
                game.SpawnZombie(1);
                ZombieState zombie = game.Zombies[0];
                zombie.X = zombie.PreviousX = game.Layout.Cell(1, 7).Center.X;
                for (int i = 0; i < 160; i++) game.Step(0.1, default);
                Require(game.Suns.Count == 1, "Produccion del girasol a los 15s");
                // Fixtures visuales: mostrar caminar, destello individual y un guisante
                // aunque el primer enemigo ya haya recibido los cuatro impactos.
                FixtureZombie(game, 3, 6);
                ZombieState flashing = FixtureZombie(game, 0, 7);
                FixtureZombie(game, 1, 6);
                game.Plants[1].NextShotAt = game.Elapsed;
                game.Step(0.0, default);
                game.Step(0.35, default);
                flashing.HitFlashUntil = game.Elapsed + Rules.HitFlashDuration * 0.5;
                root.Render(new Vec2(-1000f, -1000f));
                result.spriteRenderers = FindObjectsByType<SpriteRenderer>().Length;
                result.textObjects = FindObjectsByType<TMPro.TMP_Text>().Length;
                Require(result.spriteRenderers >= 3 && result.textObjects >= 3, "Vistas y HUD activos");
                Require(root.World != null && root.Hud != null, "Referencias de presentacion");
                Require(root.World.PlantViewCount == 2 && root.World.SunViewCount == 1,
                    "Las vistas deben coincidir con las entidades del modelo");
                Require(root.World.ZombieViewCount == 3 && root.World.PeaViewCount == 1,
                    "Zombis y proyectil visibles en la comprobacion grafica");
                foreach (SpriteRenderer renderer in FindObjectsByType<SpriteRenderer>())
                    Require(renderer.sprite != null, "Sprite perdido: " + renderer.name);
                result.passed = true;
            }
            catch (Exception exception)
            {
                result.error = exception.ToString();
                Debug.LogException(exception);
            }

            // Deja que uGUI actualice su geometria y que la camara dibuje antes de capturar.
            yield return null;
            yield return null;
            if (result.passed)
            {
                try
                {
                    CaptureFrame(root, Path.Combine(output, "gameplay.png"));
                    result.screenshot = true;
                    Click(root.Session, root.Session.Layout.Card(0).Center);
                    root.Render(root.Session.Layout.Cell(2, 4).Center);
                    Require(root.Session.Plants.Count == 2 && root.Session.SelectedPlant.HasValue,
                        "La vista previa no coloca una planta");
                    CaptureFrame(root, Path.Combine(output, "placement.png"));
                    result.placementScreenshot = true;
                }
                catch (Exception exception) { result.passed = false; result.error = exception.ToString(); }
            }

            if (result.passed)
            {
                try
                {
                    GameSession game = root.Session;
                    game.Step(0.0, new GameInput { Increase = true });
                    game.SpawnZombie(4);
                    ZombieState escape = game.Zombies[game.Zombies.Count - 1];
                    escape.X = escape.PreviousX = -game.Layout.ZombieHeight;
                    game.Step(0.0, default);
                    Require(game.IsGameOver, "Derrota por escape");
                    root.Render(default);
                }
                catch (Exception exception) { result.passed = false; result.error = exception.ToString(); }
            }
            yield return null;
            yield return null;
            if (result.passed)
            {
                try
                {
                    CaptureFrame(root, Path.Combine(output, "game-over.png"));
                    result.gameOverScreenshot = true;
                }
                catch (Exception exception) { result.passed = false; result.error = exception.ToString(); }
            }
            if (result.passed)
            {
                try
                {
                    GameSession game = root.Session;
                    Click(game, game.Layout.RetryButton.Center);
                    Require(!game.IsGameOver && game.Currency == 1000 && game.Plants.Count == 0 &&
                        game.Suns.Count == 0 && game.Peas.Count == 0 && game.Zombies.Count == 0 &&
                        game.ManualZombieCount == 2, "Retry y persistencia de X");
                    root.Render(default);
                }
                catch (Exception exception) { result.passed = false; result.error = exception.ToString(); }
            }
            result.passed &= result.screenshot && result.gameOverScreenshot && result.placementScreenshot;
            try { File.WriteAllText(Path.Combine(output, "player-validation.json"), JsonUtility.ToJson(result, true)); }
            catch (Exception exception) { result.passed = false; Debug.LogException(exception); }
            Debug.Log("[Jardin smoke] " + (result.passed ? "PASS" : "FAIL"));
            Application.Quit(result.passed ? 0 : 1);
        }

        private static void Click(GameSession game, Vec2 point) =>
            game.Step(0.0, new GameInput { Click = true, Pointer = point });

        private static ZombieState FixtureZombie(GameSession game, int row, int column)
        {
            Require(game.SpawnZombie(row), "Crear enemigo del escenario visual");
            ZombieState zombie = game.Zombies[game.Zombies.Count - 1];
            zombie.X = zombie.PreviousX = game.Layout.Cell(row, column).Center.X;
            return zombie;
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }

        private static void CaptureFrame(GameRoot root, string path)
        {
            // Una ventana oculta puede carecer de backbuffer. Renderizamos con la GPU
            // a una textura real, incluyendo uGUI, en vez de aceptar una captura vacia.
            Camera camera = Camera.main;
            Canvas canvas = root.Hud.GetComponentInChildren<Canvas>();
            Require(camera != null && canvas != null, "Camara y Canvas para captura");
            RenderMode oldMode = canvas.renderMode;
            Camera oldCanvasCamera = canvas.worldCamera;
            float oldDistance = canvas.planeDistance;
            int oldSortingOrder = canvas.sortingOrder;
            RenderTexture oldActive = RenderTexture.active;
            var target = new RenderTexture(root.Session.Layout.Width, root.Session.Layout.Height,
                24, RenderTextureFormat.ARGB32) { name = "Garden validation frame" };
            Texture2D pixels = null;
            try
            {
                target.Create();
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = camera;
                canvas.planeDistance = 1f;
                // Overlay siempre queda delante del mundo; conservar esa prioridad
                // al capturarlo temporalmente como Screen Space - Camera.
                canvas.sortingOrder = 32767;
                Canvas.ForceUpdateCanvases();
                foreach (TMPro.TMP_Text label in FindObjectsByType<TMPro.TMP_Text>())
                    label.ForceMeshUpdate();
                var request = new UniversalRenderPipeline.SingleCameraRequest { destination = target };
                RenderPipeline.SubmitRenderRequest(camera, request);
                RenderTexture.active = target;
                pixels = new Texture2D(target.width, target.height, TextureFormat.RGBA32, false);
                pixels.ReadPixels(new Rect(0f, 0f, target.width, target.height), 0, 0);
                pixels.Apply();
                byte[] png = pixels.EncodeToPNG();
                Require(png != null && png.Length > 1000, "No se genero una imagen valida");
                File.WriteAllBytes(path, png);
            }
            finally
            {
                RenderTexture.active = oldActive;
                canvas.renderMode = oldMode;
                canvas.worldCamera = oldCanvasCamera;
                canvas.planeDistance = oldDistance;
                canvas.sortingOrder = oldSortingOrder;
                Canvas.ForceUpdateCanvases();
                if (pixels != null) Destroy(pixels);
                target.Release();
                Destroy(target);
            }
        }

        private static string ReadArgument(string key)
        {
            string[] arguments = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(arguments, key);
            return index >= 0 && index + 1 < arguments.Length ? arguments[index + 1] : null;
        }
    }
}
