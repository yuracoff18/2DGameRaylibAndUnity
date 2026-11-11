using System;
using System.Collections.Generic;
using Garden.Game;
using Garden.Model;
using TMPro;
using UnityEngine;

namespace Garden.Presentation
{
    /// <summary>Convierte el estado del tablero en objetos Unity sin modificar sus reglas.</summary>
    public sealed class BoardView : MonoBehaviour
    {
        private const float PixelsPerUnit = 100f;
        private static readonly int FlashId = Shader.PropertyToID("_FlashAmount");
        private readonly Dictionary<int, Actor> plants = new Dictionary<int, Actor>();
        private readonly Dictionary<int, Actor> zombies = new Dictionary<int, Actor>();
        private readonly Dictionary<int, Actor> peas = new Dictionary<int, Actor>();
        private readonly Dictionary<int, Actor> suns = new Dictionary<int, Actor>();
        private readonly List<int> staleIds = new List<int>();
        private GameArt art;
        private Camera worldCamera;
        private GameObject ground;
        private Mesh groundMesh;
        private Material groundMaterial;
        private Actor preview;
        private GameObject highlight;
        private SpriteRenderer highlightFill;
        private readonly SpriteRenderer[] highlightBorders = new SpriteRenderer[4];
        private int width = -1;
        private int height = -1;
        private int frame;
        private float walkingReferenceHeight = 1f;

        public int PlantViewCount => plants.Count;
        public int ZombieViewCount => zombies.Count;
        public int PeaViewCount => peas.Count;
        public int SunViewCount => suns.Count;

        public void Initialize(GameArt gameArt, Camera camera)
        {
            if (ground != null) return;
            art = gameArt;
            worldCamera = camera;
            walkingReferenceHeight = 0.001f;
            if (art.zombieWalk != null)
            {
                foreach (Sprite sprite in art.zombieWalk)
                    if (sprite != null) walkingReferenceHeight = Mathf.Max(walkingReferenceHeight, sprite.bounds.size.y);
            }

            ground = new GameObject("Repeated cell ground", typeof(MeshFilter), typeof(MeshRenderer));
            ground.transform.SetParent(transform, false);
            groundMesh = new Mesh { name = "Ground quads" };
            ground.GetComponent<MeshFilter>().sharedMesh = groundMesh;
            Material source = art.groundMaterial != null ? art.groundMaterial : art.spriteMaterial;
            groundMaterial = source != null ? new Material(source) : new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            groundMaterial.name = "Ground (runtime instance)";
            if (art.grass != null)
            {
                if (groundMaterial.HasProperty("_MainTex")) groundMaterial.SetTexture("_MainTex", art.grass.texture);
                if (groundMaterial.HasProperty("_BaseMap")) groundMaterial.SetTexture("_BaseMap", art.grass.texture);
            }
            MeshRenderer groundRenderer = ground.GetComponent<MeshRenderer>();
            groundRenderer.sharedMaterial = groundMaterial;
            groundRenderer.sortingLayerName = "Default";
            groundRenderer.sortingOrder = 0;

            preview = CreateActor("Cursor preview", null, 900);
            preview.Root.SetActive(false);
            highlight = new GameObject("Cell highlight");
            highlight.transform.SetParent(transform, false);
            highlightFill = NewSprite("Fill", highlight.transform, art.white, 400);
            for (int i = 0; i < highlightBorders.Length; i++)
                highlightBorders[i] = NewSprite("Border " + i, highlight.transform, art.white, 401);
            highlight.SetActive(false);
        }

        public void Render(GameSession game, Vec2 pointer)
        {
            if (ground == null || game == null) return;
            BoardLayout layout = game.Layout;
            if (width != layout.Width || height != layout.Height)
            {
                width = layout.Width;
                height = layout.Height;
                RebuildGround(layout);
                worldCamera.orthographic = true;
                worldCamera.orthographicSize = Mathf.Max(0.01f, height / (2f * PixelsPerUnit));
                worldCamera.transform.SetPositionAndRotation(
                    new Vector3(width / (2f * PixelsPerUnit), height / (2f * PixelsPerUnit), -10f), Quaternion.identity);
            }

            frame++;
            foreach (PlantState plant in game.Plants) RenderPlant(layout, plant);
            foreach (ZombieState zombie in game.Zombies) RenderZombie(layout, zombie, game.Elapsed);
            foreach (PeaState pea in game.Peas) RenderPea(game, pea);
            foreach (SunState sun in game.Suns) RenderSun(layout, sun, game.Elapsed);
            RemoveStale(plants);
            RemoveStale(zombies);
            RemoveStale(peas);
            RemoveStale(suns);
            RenderHighlight(game, pointer);
            RenderPreview(game, pointer);
        }

        public Vector3 PixelToWorld(Vec2 point)
        {
            return new Vector3(point.X / PixelsPerUnit, (height - point.Y) / PixelsPerUnit, 0f);
        }

        private void RebuildGround(BoardLayout layout)
        {
            groundMesh.Clear();
            RectF cell = layout.Cell(0, 0);
            if (cell.Width <= 0f || cell.Height <= 0f || art.grass == null) return;
            int firstRow = -(int)Math.Ceiling(layout.Yard.Y / cell.Height);
            int tileCount = (Rules.Rows - firstRow) * Rules.Columns;
            var vertices = new Vector3[tileCount * 4];
            var uv = new Vector2[vertices.Length];
            var colors = new Color[vertices.Length];
            var triangles = new int[tileCount * 6];
            float sourceAspect = (float)art.grass.texture.width / art.grass.texture.height;
            float cellAspect = cell.Width / cell.Height;
            float uScale = sourceAspect > cellAspect ? cellAspect / sourceAspect : 1f;
            float vScale = sourceAspect > cellAspect ? 1f : sourceAspect / cellAspect;
            float leftUV = (1f - uScale) * 0.5f;
            float rightUV = 1f - leftUV;
            float bottomUV = (1f - vScale) * 0.5f;
            float topUV = 1f - bottomUV;
            int tile = 0;
            // Las filas sobre el selector son decorativas; la simulación conserva sus cinco filas.
            for (int row = firstRow; row < Rules.Rows; row++)
            {
                for (int column = 0; column < Rules.Columns; column++, tile++)
                {
                    RectF bounds = layout.Cell(row, column);
                    int v = tile * 4;
                    vertices[v] = PixelToWorld(new Vec2(bounds.X, bounds.Y));
                    vertices[v + 1] = PixelToWorld(new Vec2(bounds.Right, bounds.Y));
                    vertices[v + 2] = PixelToWorld(new Vec2(bounds.Right, bounds.Bottom));
                    vertices[v + 3] = PixelToWorld(new Vec2(bounds.X, bounds.Bottom));
                    uv[v] = new Vector2(leftUV, topUV);
                    uv[v + 1] = new Vector2(rightUV, topUV);
                    uv[v + 2] = new Vector2(rightUV, bottomUV);
                    uv[v + 3] = new Vector2(leftUV, bottomUV);
                    for (int i = 0; i < 4; i++) colors[v + i] = Color.white;
                    int t = tile * 6;
                    triangles[t] = v;
                    triangles[t + 1] = v + 1;
                    triangles[t + 2] = v + 2;
                    triangles[t + 3] = v;
                    triangles[t + 4] = v + 2;
                    triangles[t + 5] = v + 3;
                }
            }
            groundMesh.indexFormat = vertices.Length > 65535
                ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16;
            groundMesh.vertices = vertices;
            groundMesh.uv = uv;
            groundMesh.colors = colors;
            groundMesh.triangles = triangles;
            groundMesh.RecalculateBounds();
        }

        private void RenderPlant(BoardLayout layout, PlantState plant)
        {
            Actor actor = GetActor(plants, plant.Id, "Plant", art.plantViewPrefab, 100);
            actor.Root.transform.position = PixelToWorld(layout.Cell(plant.Row, plant.Column).Center);
            actor.Visual.sprite = plant.Kind == PlantKind.Sunflower ? art.sunflower : art.peashooter;
            actor.Visual.color = Color.white;
            SetHeight(actor.Visual, layout.PlantHeight);
            if (actor.Shadow == null) actor.Shadow = NewSprite("Shadow", actor.Root.transform, art.circle, 99);
            RectF cell = layout.Cell(plant.Row, plant.Column);
            Vec2 center = cell.Center;
            actor.Shadow.transform.localPosition = new Vector3(0f, (center.Y - cell.Y - cell.Height * 0.91f) / PixelsPerUnit, 0f);
            SetSize(actor.Shadow, cell.Width * 0.32f, cell.Height * 0.07f);
            actor.Shadow.color = new Color(0f, 0f, 0f, 0.18f);
        }

        private void RenderZombie(BoardLayout layout, ZombieState zombie, double now)
        {
            Actor actor = GetActor(zombies, zombie.Id, "Zombie", art.zombieViewPrefab, 200);
            actor.Root.transform.position = PixelToWorld(layout.ZombieFeet(zombie));
            float fall = zombie.IsAlive ? 0f : Mathf.Clamp01((float)((now - zombie.DiedAt) / Rules.DeathDuration));
            Sprite[] frames = !zombie.IsAlive ? art.zombieDie : zombie.EatingColumn >= 0 ? art.zombieEat : art.zombieWalk;
            double start = !zombie.IsAlive ? zombie.DiedAt : zombie.EatingColumn >= 0 ? zombie.EatingSince : zombie.SpawnedAt;
            if (frames != null && frames.Length > 0)
            {
                int index = !zombie.IsAlive ? Mathf.Min((int)(fall * frames.Length), frames.Length - 1)
                    : (int)(Math.Max(0.0, now - start) * 7.0) % frames.Length;
                actor.Visual.sprite = frames[index];
            }
            actor.Visual.sharedMaterial = art.flashMaterial != null ? art.flashMaterial : art.spriteMaterial;
            actor.Visual.transform.localScale = Vector3.one * (layout.ZombieHeight / PixelsPerUnit / walkingReferenceHeight);
            actor.Visual.transform.localRotation = Quaternion.Euler(0f, 0f, fall * 80f);
            actor.Visual.color = new Color(1f, 1f, 1f, 1f - fall);
            if (actor.Block == null) actor.Block = new MaterialPropertyBlock();
            actor.Visual.GetPropertyBlock(actor.Block);
            actor.Block.SetFloat(FlashId, Mathf.Clamp01((float)((zombie.HitFlashUntil - now) / Rules.HitFlashDuration)));
            actor.Visual.SetPropertyBlock(actor.Block);
            if (actor.Shadow == null) actor.Shadow = NewSprite("Shadow", actor.Root.transform, art.circle, 199);
            SetSize(actor.Shadow, layout.ZombieHeight * 0.4f, layout.ZombieHeight * 0.07f);
            actor.Shadow.color = new Color(0f, 0f, 0f, 0.18f * (1f - fall));
        }

        private void RenderPea(GameSession game, PeaState pea)
        {
            PlantState owner = game.ProjectileOwner(pea);
            if (owner == null) return;
            BoardLayout layout = game.Layout;
            Actor actor = GetActor(peas, pea.Id, "Pea", art.peaViewPrefab, 300);
            actor.Root.transform.position = PixelToWorld(new Vec2(pea.X, layout.PeaOrigin(owner).Y));
            actor.Visual.sprite = art.circle;
            actor.Visual.color = Rgb(78, 209, 43);
            float radius = layout.PeaRadius;
            SetSize(actor.Visual, Mathf.Max(1f, radius * 2f - 2f), Mathf.Max(1f, radius * 2f - 2f));
            if (actor.Outline == null) actor.Outline = NewSprite("Outline", actor.Root.transform, art.circle, 299);
            actor.Outline.color = Rgb(31, 100, 27);
            SetSize(actor.Outline, radius * 2f, radius * 2f);
            if (actor.Glow == null) actor.Glow = NewSprite("Shine", actor.Root.transform, art.circle, 301);
            actor.Glow.color = Rgb(180, 243, 123);
            actor.Glow.transform.localPosition = new Vector3(-radius * 0.3f / PixelsPerUnit, radius * 0.3f / PixelsPerUnit, 0f);
            SetSize(actor.Glow, radius * 0.5f, radius * 0.5f);
        }

        private void RenderSun(BoardLayout layout, SunState sun, double now)
        {
            Actor actor = GetActor(suns, sun.Id, "Sun", art.sunViewPrefab, 500);
            actor.Root.transform.position = PixelToWorld(layout.SunPosition(sun, now));
            actor.Visual.sprite = art.sun != null ? art.sun : art.circle;
            actor.Visual.color = art.sun != null ? Color.white : Rgb(255, 203, 0);
            float radius = layout.SunRadius;
            Fit(actor.Visual, radius * 2f, radius * 2f);
            if (actor.Glow == null) actor.Glow = NewSprite("Glow", actor.Root.transform, art.circle, 499);
            SetSize(actor.Glow, (radius + 3f) * 2f, (radius + 3f) * 2f);
            actor.Glow.color = new Color(1f, 0.796f, 0f, 0.3f);
            if (actor.Label == null && art.font != null)
            {
                var labelObject = new GameObject("Value 50", typeof(RectTransform));
                labelObject.transform.SetParent(actor.Root.transform, false);
                actor.Label = labelObject.AddComponent<TextMeshPro>();
                actor.Label.font = art.font;
                actor.Label.text = "50";
                actor.Label.alignment = TextAlignmentOptions.Center;
                actor.Label.textWrappingMode = TextWrappingModes.NoWrap;
                actor.Label.isOrthographic = true;
                actor.Label.color = Rgb(95, 63, 15);
                actor.Label.transform.localScale = Vector3.one / PixelsPerUnit;
                MeshRenderer textRenderer = actor.Label.GetComponent<MeshRenderer>();
                textRenderer.sortingLayerName = "Default";
                textRenderer.sortingOrder = 502;
            }
            if (actor.Label != null)
            {
                actor.Label.fontSize = Mathf.Max(10f, radius * 0.8f);
                actor.Label.rectTransform.sizeDelta = new Vector2(radius * 2f + 10f, radius * 2f + 10f);
            }
        }

        private void RenderHighlight(GameSession game, Vec2 pointer)
        {
            int row = -1;
            int column = -1;
            bool visible = !game.IsGameOver && (game.SelectedPlant.HasValue || game.ShovelSelected) &&
                game.Layout.TryGetCell(pointer, out row, out column);
            if (!visible)
            {
                if (highlight.activeSelf) highlight.SetActive(false);
                return;
            }
            if (!highlight.activeSelf) highlight.SetActive(true);
            RectF cell = game.Layout.Cell(row, column);
            bool occupied = game.PlantAt(row, column) != null;
            Color color = occupied ? Rgb(230, 41, 55) : game.ShovelSelected ? Rgb(130, 130, 130) : Rgb(223, 255, 126);
            highlight.transform.position = PixelToWorld(cell.Center);
            SetSize(highlightFill, cell.Width, cell.Height);
            highlightFill.color = new Color(color.r, color.g, color.b, 0.08f);
            const float thickness = 2f;
            float halfWidth = (cell.Width - thickness) * 0.5f / PixelsPerUnit;
            float halfHeight = (cell.Height - thickness) * 0.5f / PixelsPerUnit;
            highlightBorders[0].transform.localPosition = new Vector3(0f, halfHeight, 0f);
            highlightBorders[1].transform.localPosition = new Vector3(0f, -halfHeight, 0f);
            highlightBorders[2].transform.localPosition = new Vector3(-halfWidth, 0f, 0f);
            highlightBorders[3].transform.localPosition = new Vector3(halfWidth, 0f, 0f);
            for (int i = 0; i < highlightBorders.Length; i++)
            {
                SetSize(highlightBorders[i], i < 2 ? cell.Width : thickness, i < 2 ? thickness : cell.Height);
                highlightBorders[i].color = new Color(color.r, color.g, color.b, 0.65f);
            }
        }

        private void RenderPreview(GameSession game, Vec2 pointer)
        {
            bool visible = !game.IsGameOver && (game.SelectedPlant.HasValue || game.ShovelSelected);
            if (preview.Root.activeSelf != visible) preview.Root.SetActive(visible);
            if (!visible) return;
            preview.Root.transform.position = PixelToWorld(pointer);
            preview.Visual.color = Color.white;
            if (game.ShovelSelected)
            {
                preview.Visual.sprite = art.shovel;
                float h = Mathf.Clamp(game.Layout.Selector.Height * 0.75f, 72f, 160f);
                Fit(preview.Visual, h * 0.7f, h);
            }
            else
            {
                preview.Visual.sprite = game.SelectedPlant.Value == PlantKind.Sunflower ? art.sunflower : art.peashooter;
                SetHeight(preview.Visual, Mathf.Clamp(game.Layout.Selector.Height, 96f, 220f));
            }
        }

        private Actor GetActor(Dictionary<int, Actor> views, int id, string name, GameObject prefab, int sortingOrder)
        {
            if (!views.TryGetValue(id, out Actor actor))
            {
                actor = CreateActor(name + " " + id, prefab, sortingOrder);
                views.Add(id, actor);
            }
            actor.Seen = frame;
            return actor;
        }

        private Actor CreateActor(string name, GameObject prefab, int sortingOrder)
        {
            GameObject root = prefab != null ? Instantiate(prefab, transform) : new GameObject(name);
            root.name = name;
            root.transform.SetParent(transform, false);
            root.transform.localScale = Vector3.one;
            root.transform.localRotation = Quaternion.identity;
            Transform child = root.transform.Find("Visual");
            if (child == null)
            {
                child = new GameObject("Visual").transform;
                child.SetParent(root.transform, false);
            }
            SpriteRenderer visual = child.GetComponent<SpriteRenderer>();
            if (visual == null) visual = child.gameObject.AddComponent<SpriteRenderer>();
            visual.sharedMaterial = art.spriteMaterial;
            // Unificar capas permite que los órdenes relativos también incluyan sombras, texto y preview.
            visual.sortingLayerName = "Default";
            visual.sortingOrder = sortingOrder;
            child.localPosition = Vector3.zero;
            child.localRotation = Quaternion.identity;
            return new Actor { Root = root, Visual = visual };
        }

        private SpriteRenderer NewSprite(string name, Transform parent, Sprite sprite, int order)
        {
            var node = new GameObject(name, typeof(SpriteRenderer));
            node.transform.SetParent(parent, false);
            SpriteRenderer renderer = node.GetComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sharedMaterial = art.spriteMaterial;
            renderer.sortingLayerName = "Default";
            renderer.sortingOrder = order;
            return renderer;
        }

        private void RemoveStale(Dictionary<int, Actor> views)
        {
            staleIds.Clear();
            foreach (KeyValuePair<int, Actor> item in views)
                if (item.Value.Seen != frame) staleIds.Add(item.Key);
            foreach (int id in staleIds)
            {
                GameObject root = views[id].Root;
                root.SetActive(false);
                Destroy(root);
                views.Remove(id);
            }
        }

        private static void SetHeight(SpriteRenderer renderer, float heightPixels)
        {
            if (renderer.sprite == null) return;
            renderer.transform.localScale = Vector3.one * (heightPixels / PixelsPerUnit / Mathf.Max(0.001f, renderer.sprite.bounds.size.y));
        }

        private static void Fit(SpriteRenderer renderer, float widthPixels, float heightPixels)
        {
            if (renderer.sprite == null) return;
            Vector3 size = renderer.sprite.bounds.size;
            float scale = Mathf.Min(widthPixels / Mathf.Max(0.001f, size.x), heightPixels / Mathf.Max(0.001f, size.y)) / PixelsPerUnit;
            renderer.transform.localScale = Vector3.one * scale;
        }

        private static void SetSize(SpriteRenderer renderer, float widthPixels, float heightPixels)
        {
            if (renderer.sprite == null) return;
            Vector3 size = renderer.sprite.bounds.size;
            renderer.transform.localScale = new Vector3(widthPixels / PixelsPerUnit / Mathf.Max(0.001f, size.x),
                heightPixels / PixelsPerUnit / Mathf.Max(0.001f, size.y), 1f);
        }

        private static Color Rgb(byte r, byte g, byte b) => new Color32(r, g, b, 255);

        private void OnDestroy()
        {
            if (groundMaterial != null) Destroy(groundMaterial);
            if (groundMesh != null) Destroy(groundMesh);
        }

        private sealed class Actor
        {
            public GameObject Root;
            public SpriteRenderer Visual;
            public SpriteRenderer Shadow;
            public SpriteRenderer Glow;
            public SpriteRenderer Outline;
            public TextMeshPro Label;
            public MaterialPropertyBlock Block;
            public int Seen;
        }
    }
}
