using System;
using Garden.Model;
using Garden.Presentation;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Garden.Game
{
    /// <summary>Entrada, reloj y presentacion. La simulacion no depende del orden de varios Update.</summary>
    [DefaultExecutionOrder(-100)]
    public sealed class GameRoot : MonoBehaviour
    {
        public GameArt art;
        [Tooltip("Desactivar solo para probar plantas sin apariciones automaticas.")]
        public bool automaticSpawning = true;
        [Tooltip("Cero usa una semilla distinta en cada ejecucion.")]
        public int randomSeed;

        public GameSession Session { get; private set; }
        public BoardView World { get; private set; }
        public GameHud Hud { get; private set; }

        private Keyboard subscribedKeyboard;
        private bool typedPlus;
        private bool typedMinus;
        private Camera gameCamera;

        private void Awake()
        {
            // En escritorio, quitar VSync y el objetivo de FPS permite renderizar sin limite de Unity.
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = -1;

            if (art == null)
            {
                Debug.LogError("Faltan los recursos GameArt. Ejecuta Jardin/Reconstruir escena.", this);
                enabled = false;
                return;
            }

            gameCamera = Camera.main;
            if (gameCamera == null)
            {
                GameObject cameraObject = new GameObject("Main Camera", typeof(Camera));
                cameraObject.tag = "MainCamera";
                gameCamera = cameraObject.GetComponent<Camera>();
            }
            gameCamera.orthographic = true;
            gameCamera.backgroundColor = new Color32(108, 165, 76, 255);
            gameCamera.clearFlags = CameraClearFlags.SolidColor;

            Session = new GameSession(Math.Max(1, Screen.width), Math.Max(1, Screen.height),
                randomSeed == 0 ? Environment.TickCount : randomSeed);
            Session.AutoSpawnEnabled = automaticSpawning;

            World = CreateChild("Patio y actores").AddComponent<BoardView>();
            World.Initialize(art, gameCamera);
            Hud = CreateChild("Interfaz").AddComponent<GameHud>();
            Hud.Initialize(art);
            Render(new Vec2(-1000f, -1000f));

            // El ejecutable puede comprobar render y ciclo de vida sin automatizar el escritorio.
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-gardenSmoke") >= 0)
                gameObject.AddComponent<PlayerSmokeProbe>();
        }

        private GameObject CreateChild(string childName)
        {
            GameObject child = new GameObject(childName);
            child.transform.SetParent(transform, false);
            return child;
        }

        private void OnEnable() => BindKeyboard();

        private void BindKeyboard()
        {
            if (subscribedKeyboard == Keyboard.current) return;
            if (subscribedKeyboard != null) subscribedKeyboard.onTextInput -= OnTextInput;
            subscribedKeyboard = Keyboard.current;
            if (subscribedKeyboard != null) subscribedKeyboard.onTextInput += OnTextInput;
        }

        private void OnTextInput(char character)
        {
            typedPlus |= character == '+';
            typedMinus |= character == '-';
        }

        private void Update()
        {
            if (Session == null) return;
            BindKeyboard();
            Keyboard keyboard = Keyboard.current;
            Mouse mouse = Mouse.current;
            Vector2 pointer = mouse != null ? mouse.position.ReadValue() : new Vector2(-1000f, -1000f);
            GameInput input = new GameInput
            {
                Pointer = new Vec2(pointer.x, Screen.height - pointer.y),
                Click = mouse != null && mouse.leftButton.wasPressedThisFrame,
                Spawn = keyboard != null && keyboard.xKey.wasPressedThisFrame,
                Increase = typedPlus || (keyboard != null && keyboard.numpadPlusKey.wasPressedThisFrame),
                Decrease = typedMinus || (keyboard != null && keyboard.numpadMinusKey.wasPressedThisFrame)
            };
            typedPlus = typedMinus = false;

            // WindowShouldClose de raylib tambien cerraba el juego al pulsar Escape.
            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
            {
                ExitGame();
                return;
            }
            if (keyboard != null && keyboard.fKey.wasPressedThisFrame)
                Screen.fullScreenMode = Screen.fullScreen ? FullScreenMode.Windowed : FullScreenMode.FullScreenWindow;

            Session.Resize(Math.Max(1, Screen.width), Math.Max(1, Screen.height));
            Session.Step(Time.unscaledDeltaTime, input);
            Render(input.Pointer);
            if (Session.ExitRequested) ExitGame();
        }

        public void Render(Vec2 pointer)
        {
            if (Session == null) return;
            World.Render(Session, pointer);
            Hud.Render(Session, pointer);
        }

        public void Retry()
        {
            Session?.Retry();
            Render(new Vec2(-1000f, -1000f));
        }

        public void ExitGame()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        private void OnDisable()
        {
            if (subscribedKeyboard != null) subscribedKeyboard.onTextInput -= OnTextInput;
            subscribedKeyboard = null;
            typedPlus = typedMinus = false;
        }
    }
}
