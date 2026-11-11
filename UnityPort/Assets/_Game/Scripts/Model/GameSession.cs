using System;
using System.Collections.Generic;

namespace Garden.Model
{
    // No depende del Editor ni de UnityEngine: las mismas reglas se prueban fuera del motor.
    public sealed class GameSession
    {
        private readonly List<PlantState> plants = new List<PlantState>();
        private readonly List<SunState> suns = new List<SunState>();
        private readonly List<PeaState> peas = new List<PeaState>();
        private readonly List<ZombieState> zombies = new List<ZombieState>();
        private readonly Random random;
        private int nextId = 1;

        public BoardLayout Layout { get; }
        public IReadOnlyList<PlantState> Plants => plants;
        public IReadOnlyList<SunState> Suns => suns;
        public IReadOnlyList<PeaState> Peas => peas;
        public IReadOnlyList<ZombieState> Zombies => zombies;
        public int Currency { get; private set; }
        public int ManualZombieCount { get; private set; } = 5000;
        public int DefeatedZombies { get; private set; }
        public bool IsGameOver { get; private set; }
        public bool ExitRequested { get; private set; }
        public double Elapsed { get; private set; }
        public double NextZombieAt { get; private set; }
        public double SpawnInterval => Rules.SpawnIntervalAt(Elapsed);
        public PlantKind? SelectedPlant { get; private set; }
        public bool ShovelSelected { get; private set; }

        // Permite aislar economía/combate en pruebas, sin alterar el juego normal.
        public bool AutoSpawnEnabled { get; set; } = true;

        public GameSession(int width, int height, int seed = 12345)
        {
            Layout = new BoardLayout(width, height);
            random = new Random(seed);
            Retry();
        }

        public void Resize(int width, int height)
        {
            if (Layout.Width > 0 && width > 0 && Layout.Width != width)
            {
                float scale = (float)width / Layout.Width;
                for (int i = 0; i < peas.Count; i++) peas[i].X *= scale;
                for (int i = 0; i < zombies.Count; i++)
                {
                    zombies[i].X *= scale;
                    zombies[i].PreviousX *= scale;
                }
            }
            Layout.Resize(width, height);
        }

        public void Retry()
        {
            plants.Clear();
            suns.Clear();
            peas.Clear();
            zombies.Clear();
            Currency = Rules.InitialCurrency;
            Elapsed = 0.0;
            NextZombieAt = Rules.FirstSpawnDelay;
            DefeatedZombies = 0;
            IsGameOver = false;
            ExitRequested = false;
            ClearSelection();
            // ManualZombieCount y los recursos de presentación sobreviven a Retry.
        }

        public void Step(double deltaSeconds, GameInput input)
        {
            if (IsGameOver)
            {
                if (input.Click && Layout.RetryButton.Contains(input.Pointer)) Retry();
                else if (input.Click && Layout.ExitButton.Contains(input.Pointer)) ExitRequested = true;
                return;
            }
            if (double.IsNaN(deltaSeconds) || double.IsInfinity(deltaSeconds)) deltaSeconds = 0.0;
            Elapsed += Math.Max(0.0, deltaSeconds);

            if (AutoSpawnEnabled)
            {
                while (Elapsed >= NextZombieAt)
                {
                    SpawnZombieAt(random.Next(Rules.Rows), NextZombieAt);
                    NextZombieAt += Rules.SpawnIntervalAt(NextZombieAt);
                }
            }

            UpdateWorld();
            if (IsGameOver)
            {
                ClearSelection();
                return;
            }

            if (input.Increase && !input.Decrease && ManualZombieCount < int.MaxValue) ManualZombieCount++;
            if (input.Decrease && !input.Increase && ManualZombieCount > 1) ManualZombieCount--;
            if (input.Spawn)
                for (int i = 0; i < ManualZombieCount; i++) SpawnZombieAt(random.Next(Rules.Rows), Elapsed);

            if (input.Click) HandleClick(input.Pointer);
        }

        public bool SpawnZombie(int row) => SpawnZombieAt(row, Elapsed);

        private bool SpawnZombieAt(int row, double bornAt)
        {
            if (IsGameOver || row < 0 || row >= Rules.Rows) return false;
            float x = Layout.Width + Layout.ZombieHeight * 0.5f + 12f;
            zombies.Add(new ZombieState
            {
                Id = nextId++, Row = row, X = x, PreviousX = x,
                SpawnedAt = bornAt, UpdatedAt = bornAt
            });
            return true;
        }

        public PlantState PlantAt(int row, int column)
        {
            for (int i = 0; i < plants.Count; i++)
                if (plants[i].Row == row && plants[i].Column == column) return plants[i];
            return null;
        }

        public int CountSunsAt(int row, int column)
        {
            int count = 0;
            for (int i = 0; i < suns.Count; i++)
                if (suns[i].Row == row && suns[i].Column == column) count++;
            return count;
        }

        public bool HasZombieAhead(int row, float x)
        {
            for (int i = 0; i < zombies.Count; i++)
                if (zombies[i].IsAlive && zombies[i].Row == row && Layout.ZombieBounds(zombies[i]).Right >= x)
                    return true;
            return false;
        }

        public PlantState ProjectileOwner(PeaState pea)
        {
            for (int i = 0; i < plants.Count; i++) if (plants[i].Id == pea.OwnerId) return plants[i];
            return null;
        }

        private void ClearSelection()
        {
            SelectedPlant = null;
            ShovelSelected = false;
        }

        private void HandleClick(Vec2 point)
        {
            // Un sol consume todo el clic y conserva la planta o pala seleccionada.
            if (TryCollectSun(point)) return;
            bool inYard = Layout.TryGetCell(point, out int row, out int column);
            if (ShovelSelected && inYard)
            {
                PlantState plant = PlantAt(row, column);
                if (plant != null) { RemovePlant(plant); ClearSelection(); }
                return;
            }
            if (SelectedPlant.HasValue && inYard)
            {
                if (PlantAt(row, column) == null)
                {
                    PlacePlant(row, column, SelectedPlant.Value);
                    ClearSelection();
                }
                return;
            }

            if (Layout.Slot(Rules.SelectorSlots - 1).Contains(point))
            {
                ShovelSelected = !ShovelSelected;
                SelectedPlant = null;
                return;
            }
            // Pulsar otra tarjeta cancela primero: comprarla requiere otro clic.
            if (SelectedPlant.HasValue) { SelectedPlant = null; return; }
            for (int i = 0; i < 2; i++)
            {
                if (!Layout.Card(i).Contains(point)) continue;
                PlantKind kind = (PlantKind)i;
                if (Currency >= Rules.Cost(kind))
                {
                    Currency -= Rules.Cost(kind);
                    SelectedPlant = kind;
                    ShovelSelected = false;
                }
                return;
            }
            ShovelSelected = false;
        }

        private void PlacePlant(int row, int column, PlantKind kind)
        {
            PlantState plant = new PlantState
            {
                Id = nextId++, Row = row, Column = column, Kind = kind,
                NextSunAt = Elapsed + (kind == PlantKind.Sunflower ? Rules.SunInterval : 0.0),
                NextShotAt = Elapsed
            };
            plants.Add(plant);
            if (kind == PlantKind.Peashooter && HasZombieAhead(row, Layout.PeaOrigin(plant).X))
                LaunchPea(plant);
        }

        private void RemovePlant(PlantState plant)
        {
            if (plant.ActiveProjectileId != 0)
                for (int i = peas.Count - 1; i >= 0; i--)
                    if (peas[i].Id == plant.ActiveProjectileId) peas.RemoveAt(i);
            plants.Remove(plant);
            // Los soles tienen celda de origen, no son hijos de la planta retirada.
        }

        private bool TryCollectSun(Vec2 point)
        {
            float radius = Layout.SunRadius;
            for (int i = suns.Count - 1; i >= 0; i--)
            {
                Vec2 center = Layout.SunPosition(suns[i], Elapsed);
                float dx = point.X - center.X;
                float dy = point.Y - center.Y;
                if (dx * dx + dy * dy > radius * radius) continue;
                int row = suns[i].Row;
                int column = suns[i].Column;
                Currency += Rules.SunValue;
                suns.RemoveAt(i);
                ResumeCurrencyAt(row, column);
                return true;
            }
            return false;
        }

        private void ResumeCurrencyAt(int row, int column)
        {
            if (CountSunsAt(row, column) >= Rules.MaxSunsPerCell) return;
            PlantState plant = PlantAt(row, column);
            if (plant == null || !plant.CurrencyPaused) return;
            plant.CurrencyPaused = false;
            plant.NextSunAt = Elapsed + Rules.SunInterval;
        }

        private int FreeSunSlot(int row, int column)
        {
            for (int slot = 0; slot < Rules.MaxSunsPerCell; slot++)
            {
                bool used = false;
                for (int i = 0; i < suns.Count; i++)
                    if (suns[i].Row == row && suns[i].Column == column && suns[i].Slot == slot)
                    { used = true; break; }
                if (!used) return slot;
            }
            return 0;
        }

        private void UpdateWorld()
        {
            UpdateZombies();
            for (int i = suns.Count - 1; i >= 0; i--)
                if (Elapsed - suns[i].BornAt >= Rules.SunLifetime) suns.RemoveAt(i);

            for (int i = 0; i < plants.Count; i++)
            {
                PlantState plant = plants[i];
                if (plant.Kind == PlantKind.Sunflower)
                {
                    if (CountSunsAt(plant.Row, plant.Column) >= Rules.MaxSunsPerCell)
                    {
                        plant.CurrencyPaused = true;
                        plant.NextSunAt = Elapsed + Rules.SunInterval;
                    }
                    else if (plant.CurrencyPaused)
                    {
                        plant.CurrencyPaused = false;
                        plant.NextSunAt = Elapsed + Rules.SunInterval;
                    }
                    else if (Elapsed >= plant.NextSunAt)
                    {
                        suns.Add(new SunState
                        {
                            Id = nextId++, Row = plant.Row, Column = plant.Column,
                            BornAt = Elapsed, Slot = FreeSunSlot(plant.Row, plant.Column)
                        });
                        plant.NextSunAt = Elapsed + Rules.SunInterval;
                        plant.CurrencyPaused = CountSunsAt(plant.Row, plant.Column) >= Rules.MaxSunsPerCell;
                    }
                }
                if (plant.Kind == PlantKind.Peashooter) UpdatePea(plant);
            }

            // Los impactos se resuelven antes del escape: un muerto no puede derrotar al jugador.
            for (int i = 0; i < zombies.Count; i++)
                if (zombies[i].IsAlive && Layout.ZombieBounds(zombies[i]).Right < 0f) IsGameOver = true;
            for (int i = zombies.Count - 1; i >= 0; i--)
                if (!zombies[i].IsAlive && Elapsed - zombies[i].DiedAt >= Rules.DeathDuration) zombies.RemoveAt(i);
        }

        private void UpdateZombies()
        {
            for (int i = 0; i < zombies.Count; i++)
            {
                ZombieState zombie = zombies[i];
                float previousX = zombie.X;
                double previousTime = zombie.UpdatedAt;
                zombie.PreviousX = previousX;
                zombie.UpdatedAt = Elapsed;
                if (!zombie.IsAlive) continue;
                float nextX = previousX - Rules.ZombieSpeed * (float)Math.Max(0.0, Elapsed - previousTime);
                PlantState blockingPlant = null;
                float stopX = nextX;
                for (int p = 0; p < plants.Count; p++)
                {
                    PlantState plant = plants[p];
                    if (plant.Row != zombie.Row) continue;
                    float centerX = Layout.Cell(plant.Row, plant.Column).Center.X;
                    float contactX = centerX + Layout.PlantHeight / 3f + Layout.ZombieHeight * 0.23f;
                    if (previousX < centerX - Layout.PlantHeight / 3f || contactX < nextX) continue;
                    float candidate = Math.Min(previousX, contactX);
                    if (blockingPlant == null || candidate > stopX)
                    { blockingPlant = plant; stopX = candidate; }
                }
                zombie.X = blockingPlant != null ? stopX : nextX;
                if (blockingPlant == null) { zombie.EatingColumn = -1; continue; }

                if (zombie.EatingColumn != blockingPlant.Column)
                {
                    double arrival = previousTime + (previousX - zombie.X) / Rules.ZombieSpeed;
                    zombie.EatingColumn = blockingPlant.Column;
                    zombie.EatingSince = arrival;
                    zombie.NextBiteAt = arrival + Rules.BiteInterval;
                }
                while (Elapsed >= zombie.NextBiteAt)
                {
                    blockingPlant.Health--;
                    zombie.NextBiteAt += Rules.BiteInterval;
                    if (blockingPlant.Health <= 0)
                    {
                        RemovePlant(blockingPlant);
                        zombie.EatingColumn = -1;
                        break;
                    }
                }
            }
        }

        private void LaunchPea(PlantState plant)
        {
            PeaState pea = new PeaState
            {
                Id = nextId++, OwnerId = plant.Id, Row = plant.Row,
                X = Layout.PeaOrigin(plant).X, UpdatedAt = Elapsed
            };
            peas.Add(pea);
            plant.ActiveProjectileId = pea.Id;
        }

        private void UpdatePea(PlantState plant)
        {
            PeaState pea = null;
            for (int i = 0; i < peas.Count; i++)
                if (peas[i].Id == plant.ActiveProjectileId) { pea = peas[i]; break; }
            if (pea == null)
            {
                plant.ActiveProjectileId = 0;
                if (Elapsed >= plant.NextShotAt && HasZombieAhead(plant.Row, Layout.PeaOrigin(plant).X))
                    LaunchPea(plant);
                return;
            }

            float previousX = pea.X;
            float nextX = previousX + Rules.PeaSpeed * (float)Math.Max(0.0, Elapsed - pea.UpdatedAt);
            ZombieState target = null;
            float firstImpact = 2f;
            for (int i = 0; i < zombies.Count; i++)
            {
                ZombieState zombie = zombies[i];
                if (!zombie.IsAlive || zombie.Row != plant.Row) continue;
                RectF bounds = Layout.ZombieBounds(zombie);
                float oldLeft = bounds.X + zombie.PreviousX - zombie.X;
                float radius = Layout.PeaRadius;
                if (previousX > oldLeft + bounds.Width + radius) continue;
                float advance = nextX - previousX - (zombie.X - zombie.PreviousX);
                float distance = oldLeft - radius - previousX;
                if (distance > 0f && advance <= 0f) continue;
                float impact = distance <= 0f ? 0f : distance / advance;
                if (impact >= 0f && impact <= 1f && impact < firstImpact)
                { firstImpact = impact; target = zombie; }
            }
            pea.X = nextX;
            pea.UpdatedAt = Elapsed;
            if (target != null)
            {
                target.Health--;
                target.HitFlashUntil = Elapsed + Rules.HitFlashDuration;
                if (target.Health == 0)
                {
                    target.DiedAt = Elapsed;
                    DefeatedZombies++;
                }
            }
            if (target != null || pea.X - Layout.PeaRadius >= Layout.Width)
            {
                peas.Remove(pea);
                plant.ActiveProjectileId = 0;
                // El descanso comienza al desaparecer, nunca al lanzar.
                plant.NextShotAt = Elapsed + Rules.ShotCooldown;
            }
        }
    }
}
