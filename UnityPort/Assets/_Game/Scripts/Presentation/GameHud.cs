using Garden.Game;
using Garden.Model;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Garden.Presentation
{
    /// <summary>
    /// Presentación uGUI del selector y del cartel de derrota.
    /// La sesión es la única que decide qué significa cada pulsación del ratón.
    /// </summary>
    public sealed class GameHud : MonoBehaviour
    {
        private static readonly Color Wood = Rgb(111, 76, 43);
        private static readonly Color Trim = Rgb(196, 149, 83);
        private static readonly Color Cream = Rgb(242, 216, 164);
        private static readonly Color Gold = Rgb(255, 203, 0);

        private GameArt art;
        private RectTransform canvasRect;
        private GameObject gameOverRoot;
        private Image selectorShadow;
        private Panel selector;
        private Panel balance;
        private Image coinShadow;
        private Image coin;
        private TextMeshProUGUI balanceLabel;
        private TextMeshProUGUI amount;
        private TextMeshProUGUI manualAmount;
        private TextMeshProUGUI manualHint;
        private readonly Panel[] cards = new Panel[2];
        private readonly Image[] cardImages = new Image[2];
        private readonly Image[] cardShadows = new Image[2];
        private Panel shovel;
        private Image shovelImage;
        private TextMeshProUGUI shovelLabel;
        private Image dimmer;
        private Panel defeatPanel;
        private TextMeshProUGUI defeatTitle;
        private TextMeshProUGUI defeatedAmount;
        private Panel retry;
        private Panel exit;
        private TextMeshProUGUI retryLabel;
        private TextMeshProUGUI exitLabel;
        private Texture2D roundedTexture;
        private Sprite roundedSprite;
        private int width = -1;
        private int height = -1;
        private int lastCurrency = int.MinValue;
        private int lastManualCount = int.MinValue;
        private int lastDefeatedCount = int.MinValue;

        public void Initialize(GameArt gameArt)
        {
            if (canvasRect != null) return;
            art = gameArt;
            CreateRoundedSprite();

            var canvasObject = new GameObject("Game Canvas", typeof(RectTransform), typeof(Canvas),
                typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(transform, false);
            canvasRect = canvasObject.GetComponent<RectTransform>();
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            canvas.pixelPerfect = true;
            var scaler = canvasObject.GetComponent<CanvasScaler>();
            // El modelo trabaja en píxeles: una unidad de Canvas representa un píxel físico.
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            scaler.scaleFactor = 1f;

            selectorShadow = NewImage("Selector shadow", canvasRect, roundedSprite,
                new Color(0f, 0f, 0f, 0.25f));
            selector = NewPanel("Selector", canvasRect, Trim, Wood, 2f);
            balance = NewPanel("Balance", canvasRect, Trim, Rgb(78, 52, 31), 1.5f);
            balanceLabel = NewText("Balance label", canvasRect, "SOLES", 18f, Cream);
            coinShadow = NewImage("Coin shadow", canvasRect, art.circle, Rgb(159, 104, 21));
            coin = NewImage("Coin", canvasRect, art.circle, Gold);
            amount = NewText("Currency", canvasRect, "1000", 48f, Color.white);

            Sprite[] banners = { art.sunflowerCard, art.peashooterCard };
            for (int i = 0; i < cards.Length; i++)
            {
                cardShadows[i] = NewImage("Card shadow " + i, canvasRect, roundedSprite,
                    new Color(0f, 0f, 0f, 0.28f));
                cards[i] = NewPanel(i == 0 ? "Sunflower card" : "Peashooter card",
                    canvasRect, Trim, Rgb(245, 224, 173), 1.5f);
                cardImages[i] = NewImage("Banner", cards[i].Root, banners[i], Color.white);
                cardImages[i].preserveAspect = true;
            }

            manualAmount = NewText("Manual spawn count", canvasRect, "X: 1 zombis", 28f,
                Rgb(255, 226, 167));
            manualHint = NewText("Manual spawn controls", canvasRect, "+ / -  cantidad", 18f, Cream);
            shovel = NewPanel("Shovel", canvasRect, Trim, Rgb(236, 216, 177), 1.5f);
            shovelImage = NewImage("Shovel icon", canvasRect, art.shovel, Color.white);
            shovelImage.preserveAspect = true;
            shovelLabel = NewText("Shovel label", canvasRect, "PALA", 18f, Wood);

            gameOverRoot = new GameObject("Game Over", typeof(RectTransform));
            gameOverRoot.transform.SetParent(canvasRect, false);
            Stretch(gameOverRoot.GetComponent<RectTransform>(), 0f);
            Transform modal = gameOverRoot.transform;
            dimmer = NewImage("Dimmer", modal, art.white, new Color(0f, 0f, 0f, 0.70f));
            defeatPanel = NewPanel("Game over panel", modal, Rgb(218, 177, 103), Rgb(68, 46, 32), 3f);
            defeatTitle = NewText("Title", modal, "GAME OVER", 64f, Rgb(255, 226, 167));
            defeatedAmount = NewText("Defeated zombies", modal, "Zombis derrotados: 0", 22f, Color.white);
            retry = NewPanel("Retry", modal, Rgb(88, 139, 52), Rgb(88, 139, 52), 0f);
            exit = NewPanel("Salir", modal, Rgb(139, 63, 45), Rgb(139, 63, 45), 0f);
            retryLabel = NewText("Retry label", modal, "Retry", 26f, Color.white);
            exitLabel = NewText("Exit label", modal, "Salir", 26f, Color.white);
            gameOverRoot.SetActive(false);
        }

        public void Render(GameSession game, Vec2 pointer)
        {
            if (canvasRect == null || game == null) return;
            BoardLayout layout = game.Layout;
            if (width != layout.Width || height != layout.Height)
            {
                width = layout.Width;
                height = layout.Height;
                ApplyLayout(layout);
            }

            if (lastCurrency != game.Currency)
            {
                lastCurrency = game.Currency;
                amount.text = lastCurrency.ToString();
            }
            if (lastManualCount != game.ManualZombieCount)
            {
                lastManualCount = game.ManualZombieCount;
                manualAmount.text = "X: " + lastManualCount + " zombis";
            }
            if (lastDefeatedCount != game.DefeatedZombies)
            {
                lastDefeatedCount = game.DefeatedZombies;
                defeatedAmount.text = "Zombis derrotados: " + lastDefeatedCount;
            }

            for (int i = 0; i < cards.Length; i++)
            {
                PlantKind kind = i == 0 ? PlantKind.Sunflower : PlantKind.Peashooter;
                bool selected = game.SelectedPlant.HasValue && game.SelectedPlant.Value == kind;
                bool affordable = game.Currency >= Rules.Cost(kind);
                bool hovered = !game.IsGameOver && layout.Card(i).Contains(pointer);
                cards[i].Border.color = selected ? Gold : hovered && affordable ? Rgb(255, 228, 153) : Trim;
                cards[i].Fill.color = affordable || selected ? Rgb(245, 224, 173) : Rgb(145, 126, 100);
                cards[i].SetBorder(selected || hovered && affordable ? 3f : 1.5f);
                cardImages[i].color = affordable || selected ? Color.white : Rgb(125, 125, 125);
            }

            bool shovelHovered = !game.IsGameOver && layout.Slot(10).Contains(pointer);
            shovel.Border.color = game.ShovelSelected ? Gold : shovelHovered ? Rgb(255, 228, 153) : Trim;
            shovel.SetBorder(game.ShovelSelected || shovelHovered ? 3f : 1.5f);
            if (gameOverRoot.activeSelf != game.IsGameOver)
                gameOverRoot.SetActive(game.IsGameOver);
            if (game.IsGameOver)
            {
                Color retryColor = layout.RetryButton.Contains(pointer) ? Rgb(123, 175, 74) : Rgb(88, 139, 52);
                retry.Border.color = retry.Fill.color = retryColor;
                Color exitColor = layout.ExitButton.Contains(pointer) ? Rgb(177, 85, 60) : Rgb(139, 63, 45);
                exit.Border.color = exit.Fill.color = exitColor;
            }
        }

        private void ApplyLayout(BoardLayout layout)
        {
            RectF bar = layout.Selector;
            SetRect(selectorShadow.rectTransform, new RectF(bar.X + 3f, bar.Y + 5f, bar.Width, bar.Height));
            SetRect(selector.Root, bar);
            RectF slot = layout.Slot(0);
            SetRect(balance.Root, slot);
            SetRect(balanceLabel.rectTransform,
                new RectF(slot.X + 4f, slot.Y + 5f, slot.Width - 8f, slot.Height * 0.16f));
            float radius = Mathf.Max(0f, Mathf.Min(slot.Width * 0.17f, slot.Height * 0.15f));
            float coinX = slot.X + slot.Width * 0.5f - radius;
            float coinY = slot.Y + slot.Height * 0.38f - radius;
            SetRect(coinShadow.rectTransform, new RectF(coinX + 2f, coinY + 2f, radius * 2f, radius * 2f));
            SetRect(coin.rectTransform, new RectF(coinX, coinY, radius * 2f, radius * 2f));
            SetRect(amount.rectTransform,
                new RectF(slot.X + 6f, slot.Y + slot.Height * 0.60f, slot.Width - 12f, slot.Height * 0.32f));

            for (int i = 0; i < cards.Length; i++)
            {
                RectF card = layout.Card(i);
                SetRect(cardShadows[i].rectTransform, new RectF(card.X + 2f, card.Y + 3f, card.Width, card.Height));
                SetRect(cards[i].Root, card);
                // El banner ya contiene su coste; conservar aspecto evita deformar tanto el dibujo como las cifras.
                Stretch(cardImages[i].rectTransform, Mathf.Min(4f, card.Width * 0.08f));
            }

            RectF first = layout.Slot(3);
            RectF last = layout.Slot(9);
            float freeWidth = Mathf.Max(0f, last.Right - first.X);
            SetRect(manualAmount.rectTransform,
                new RectF(first.X, first.Y + first.Height * 0.20f, freeWidth, first.Height * 0.30f));
            SetRect(manualHint.rectTransform,
                new RectF(first.X, first.Y + first.Height * 0.52f, freeWidth, first.Height * 0.22f));

            RectF shovelSlot = layout.Slot(10);
            SetRect(shovel.Root, shovelSlot);
            SetRect(shovelImage.rectTransform, new RectF(shovelSlot.X + 10f, shovelSlot.Y + 8f,
                shovelSlot.Width - 20f, shovelSlot.Height * 0.73f));
            SetRect(shovelLabel.rectTransform, new RectF(shovelSlot.X + 4f, shovelSlot.Y + shovelSlot.Height * 0.80f,
                shovelSlot.Width - 8f, shovelSlot.Height * 0.16f));

            SetRect(dimmer.rectTransform, new RectF(0f, 0f, layout.Width, layout.Height));
            RectF panel = layout.GameOverPanel;
            SetRect(defeatPanel.Root, panel);
            SetRect(defeatTitle.rectTransform,
                new RectF(panel.X + 12f, panel.Y + panel.Height * 0.08f, panel.Width - 24f, panel.Height * 0.27f));
            SetRect(defeatedAmount.rectTransform,
                new RectF(panel.X + 12f, panel.Y + panel.Height * 0.39f, panel.Width - 24f, panel.Height * 0.13f));
            SetRect(retry.Root, layout.RetryButton);
            SetRect(retryLabel.rectTransform, layout.RetryButton);
            SetRect(exit.Root, layout.ExitButton);
            SetRect(exitLabel.rectTransform, layout.ExitButton);
        }

        private Panel NewPanel(string name, Transform parent, Color border, Color fill, float borderWidth)
        {
            Image outline = NewImage(name, parent, roundedSprite, border);
            Image background = NewImage("Fill", outline.transform, roundedSprite, fill);
            var panel = new Panel(outline.rectTransform, outline, background);
            panel.SetBorder(borderWidth);
            return panel;
        }

        private Image NewImage(string name, Transform parent, Sprite sprite, Color color)
        {
            var node = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            node.transform.SetParent(parent, false);
            Image image = node.GetComponent<Image>();
            image.sprite = sprite != null ? sprite : art.white;
            image.color = color;
            image.raycastTarget = false;
            image.type = image.sprite == roundedSprite ? Image.Type.Sliced : Image.Type.Simple;
            return image;
        }

        private TextMeshProUGUI NewText(string name, Transform parent, string value, float size, Color color)
        {
            var node = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer));
            node.transform.SetParent(parent, false);
            TextMeshProUGUI text = node.AddComponent<TextMeshProUGUI>();
            if (art.font != null) text.font = art.font;
            text.text = value;
            text.color = color;
            text.fontSize = size;
            text.enableAutoSizing = true;
            text.fontSizeMin = 10f;
            text.fontSizeMax = size;
            text.alignment = TextAlignmentOptions.Center;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.overflowMode = TextOverflowModes.Ellipsis;
            text.richText = false;
            text.raycastTarget = false;
            text.margin = new Vector4(2f, 0f, 2f, 0f);
            return text;
        }

        private static void SetRect(RectTransform rect, RectF bounds)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(bounds.X, -bounds.Y);
            rect.sizeDelta = new Vector2(Mathf.Max(0f, bounds.Width), Mathf.Max(0f, bounds.Height));
        }

        private static void Stretch(RectTransform rect, float inset)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = new Vector2(inset, inset);
            rect.offsetMax = new Vector2(-inset, -inset);
        }

        private static Color Rgb(byte r, byte g, byte b) => new Color32(r, g, b, 255);

        private void CreateRoundedSprite()
        {
            const int size = 32;
            const float radius = 8f;
            roundedTexture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "HUD rounded rectangle",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float qx = Mathf.Abs(x + 0.5f - size * 0.5f) - (size * 0.5f - radius);
                    float qy = Mathf.Abs(y + 0.5f - size * 0.5f) - (size * 0.5f - radius);
                    float outsideX = Mathf.Max(qx, 0f);
                    float outsideY = Mathf.Max(qy, 0f);
                    float distance = Mathf.Sqrt(outsideX * outsideX + outsideY * outsideY) +
                        Mathf.Min(Mathf.Max(qx, qy), 0f) - radius;
                    pixels[y * size + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(0.5f - distance));
                }
            }
            roundedTexture.SetPixels(pixels);
            roundedTexture.Apply(false, true);
            roundedSprite = Sprite.Create(roundedTexture, new Rect(0f, 0f, size, size),
                new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(8f, 8f, 8f, 8f));
            roundedSprite.name = "HUD rounded rectangle";
        }

        private void OnDestroy()
        {
            if (roundedSprite != null) Destroy(roundedSprite);
            if (roundedTexture != null) Destroy(roundedTexture);
        }

        private sealed class Panel
        {
            public readonly RectTransform Root;
            public readonly Image Border;
            public readonly Image Fill;
            private float width = float.NaN;

            public Panel(RectTransform root, Image border, Image fill)
            {
                Root = root;
                Border = border;
                Fill = fill;
            }

            public void SetBorder(float value)
            {
                if (Mathf.Approximately(width, value)) return;
                width = value;
                Stretch(Fill.rectTransform, value);
            }
        }
    }
}
