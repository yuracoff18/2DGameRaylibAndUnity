namespace Garden.Model
{
    public enum PlantKind { Sunflower, Peashooter }
    public enum ZombieMode { Walking, Eating, Dying }

    public static class Rules
    {
        public const int Rows = 5;
        public const int Columns = 9;
        public const int SelectorSlots = 11;
        public const int InitialCurrency = 1000;
        public const int PlantHealth = 3;
        public const int ZombieHealth = 4;
        public const int SunValue = 50;
        public const int MaxSunsPerCell = 3;
        public const double SunInterval = 15.0;
        public const double SunLifetime = 10.0;
        public const double ShotCooldown = 2.0;
        public const double BiteInterval = 1.1;
        public const double HitFlashDuration = 0.16;
        public const double DeathDuration = 0.8;
        public const double FirstSpawnDelay = 4.0;
        public const float PeaSpeed = 420f;
        public const float ZombieSpeed = 28f;

        public static int Cost(PlantKind kind) => kind == PlantKind.Sunflower ? 50 : 100;

        public static double SpawnIntervalAt(double elapsed)
        {
            return System.Math.Max(1.2, 7.0 - System.Math.Max(0.0, elapsed) * 0.02);
        }
    }

    public struct GameInput
    {
        public Vec2 Pointer;
        public bool Click;
        public bool Spawn;
        public bool Increase;
        public bool Decrease;
    }

    public sealed class PlantState
    {
        public int Id;
        public int Row;
        public int Column;
        public int Health = Rules.PlantHealth;
        public PlantKind Kind;
        public double NextSunAt;
        public bool CurrencyPaused;
        public int ActiveProjectileId;
        public double NextShotAt;
    }

    public sealed class SunState
    {
        public int Id;
        public int Row;
        public int Column;
        public int Slot;
        public double BornAt;
    }

    public sealed class PeaState
    {
        public int Id;
        public int OwnerId;
        public int Row;
        public float X;
        public double UpdatedAt;
    }

    public sealed class ZombieState
    {
        public int Id;
        public int Row;
        public int Health = Rules.ZombieHealth;
        public float X;
        public float PreviousX;
        public double SpawnedAt;
        public double UpdatedAt;
        public double EatingSince;
        public double NextBiteAt;
        public double HitFlashUntil = -1.0;
        public double DiedAt = -1.0;
        public int EatingColumn = -1;

        public bool IsAlive => Health > 0;
        public ZombieMode Mode => !IsAlive ? ZombieMode.Dying :
            EatingColumn >= 0 ? ZombieMode.Eating : ZombieMode.Walking;
    }
}
