using System;

namespace Garden.Model
{
    // La simulación conserva píxeles y el eje Y descendente del juego original.
    [Serializable]
    public struct Vec2
    {
        public float X;
        public float Y;

        public Vec2(float x, float y) { X = x; Y = y; }
    }

    [Serializable]
    public struct RectF
    {
        public float X;
        public float Y;
        public float Width;
        public float Height;

        public RectF(float x, float y, float width, float height)
        {
            X = x; Y = y; Width = width; Height = height;
        }

        public float Right => X + Width;
        public float Bottom => Y + Height;
        public Vec2 Center => new Vec2(X + Width * 0.5f, Y + Height * 0.5f);

        // Los controles UI incluyen su borde, como CheckCollisionPointRec de raylib.
        public bool Contains(Vec2 point)
        {
            return Width > 0f && Height > 0f && point.X >= X && point.X <= Right &&
                point.Y >= Y && point.Y <= Bottom;
        }
    }

    public sealed class BoardLayout
    {
        public int Width { get; private set; }
        public int Height { get; private set; }
        public RectF Selector { get; private set; }
        public RectF Yard { get; private set; }

        public BoardLayout(int width, int height) { Resize(width, height); }

        public void Resize(int width, int height)
        {
            Width = Math.Max(0, width);
            Height = Math.Max(0, height);
            Selector = new RectF(Width * 0.05f, 10f, Width * 0.9f, Height * 0.2f);
            float top = Selector.Bottom + Clamp(Height * 0.012f, 8f, 16f);
            Yard = new RectF(0f, top, Width, Math.Max(0f, Height - top));
        }

        public RectF Cell(int row, int column)
        {
            float width = Yard.Width / Rules.Columns;
            float height = Yard.Height / Rules.Rows;
            return new RectF(Yard.X + column * width, Yard.Y + row * height, width, height);
        }

        public bool TryGetCell(Vec2 point, out int row, out int column)
        {
            row = column = -1;
            // El extremo derecho/inferior está excluido para no crear otra celda.
            if (Yard.Width <= 0f || Yard.Height <= 0f || point.X < Yard.X ||
                point.X >= Yard.Right || point.Y < Yard.Y || point.Y >= Yard.Bottom)
                return false;
            row = Math.Min(Rules.Rows - 1, (int)((point.Y - Yard.Y) / (Yard.Height / Rules.Rows)));
            column = Math.Min(Rules.Columns - 1, (int)((point.X - Yard.X) / (Yard.Width / Rules.Columns)));
            return true;
        }

        public RectF Slot(int index)
        {
            float gap = Clamp(Selector.Height * 0.05f, 4f, 12f);
            float width = Math.Max(0f, (Selector.Width - gap * (Rules.SelectorSlots + 1)) / Rules.SelectorSlots);
            return new RectF(Selector.X + gap + index * (width + gap), Selector.Y + gap,
                width, Math.Max(0f, Selector.Height - 2f * gap));
        }

        public RectF Card(int index)
        {
            RectF slot = Slot(index + 1);
            float height = Math.Min(slot.Height, slot.Width * 1.5f);
            float width = height / 1.5f;
            return new RectF(slot.X + (slot.Width - width) * 0.5f,
                slot.Y + (slot.Height - height) * 0.5f, width, height);
        }

        public float PlantHeight => Math.Min(Cell(0, 0).Height * 0.82f, Cell(0, 0).Width * 1.3f);
        public float ZombieHeight => Yard.Height / Rules.Rows * 0.95f;
        public float PeaRadius => Clamp(Yard.Height / Rules.Rows * 0.05f, 6f, 11f);
        public float SunRadius => Clamp(Math.Min(Yard.Width / Rules.Columns,
            Yard.Height / Rules.Rows) * 0.16f, 10f, 24f);

        public Vec2 PeaOrigin(PlantState plant)
        {
            Vec2 center = Cell(plant.Row, plant.Column).Center;
            return new Vec2(center.X + PlantHeight * 0.28f, center.Y - PlantHeight * 0.07f);
        }

        public Vec2 SunPosition(SunState sun, double now)
        {
            RectF cell = Cell(sun.Row, sun.Column);
            float fraction = sun.Slot == 0 ? 0.23f : sun.Slot == 1 ? 0.5f : 0.77f;
            float progress = Clamp((float)((now - sun.BornAt) / 0.6), 0f, 1f);
            float startX = cell.X + cell.Width * 0.5f;
            float targetX = cell.X + cell.Width * fraction;
            float startY = cell.Y + cell.Height * 0.28f;
            float targetY = cell.Y + cell.Height * 0.8f;
            return new Vec2(startX + (targetX - startX) * progress,
                startY + (targetY - startY) * progress * progress);
        }

        public Vec2 ZombieFeet(ZombieState zombie)
        {
            RectF cell = Cell(zombie.Row, 0);
            return new Vec2(zombie.X, cell.Y + cell.Height * 0.93f);
        }

        public RectF ZombieBounds(ZombieState zombie)
        {
            Vec2 feet = ZombieFeet(zombie);
            return new RectF(feet.X - ZombieHeight * 0.23f, feet.Y - ZombieHeight,
                ZombieHeight * 0.46f, ZombieHeight);
        }

        public RectF GameOverPanel
        {
            get
            {
                float width = Math.Max(0f, Math.Min(540f, Width - 32f));
                float height = Math.Max(0f, Math.Min(300f, Height - 32f));
                return new RectF((Width - width) * 0.5f, (Height - height) * 0.5f, width, height);
            }
        }

        public RectF RetryButton
        {
            get
            {
                RectF panel = GameOverPanel;
                return new RectF(panel.X + panel.Width * 0.06f, panel.Y + panel.Height * 0.66f,
                    panel.Width * 0.42f, panel.Height * 0.2f);
            }
        }

        public RectF ExitButton
        {
            get
            {
                RectF panel = GameOverPanel;
                return new RectF(panel.X + panel.Width * 0.52f, panel.Y + panel.Height * 0.66f,
                    panel.Width * 0.42f, panel.Height * 0.2f);
            }
        }

        public static float Clamp(float value, float min, float max)
        {
            return Math.Max(min, Math.Min(max, value));
        }
    }
}
