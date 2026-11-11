using System.Reflection;
using System.Text;
using System.Text.Json;
using Garden.Model;

namespace Garden.Tests;

internal static class Program
{
    private sealed class CaseResult
    {
        public string label { get; set; }
        public bool passed { get; set; }
        public int assertions { get; set; }
        public string error { get; set; }
    }

    private static readonly List<CaseResult> results = new();
    private static int assertionCount;

    private static int Main()
    {
        Console.OutputEncoding = new UTF8Encoding(false);
        Run("Geometría: 9x5 y bordes exclusivos del patio", TestLayout);
        Run("Compra al seleccionar, cancelación y colocación sin doble cobro", TestPurchase);
        Run("Cancelar fuera y en el fondo del selector no reembolsa", TestCancellation);
        Run("Celda ocupada conserva selección", TestOccupiedCell);
        Run("Pala gratuita con saldo cero y tarjeta impagable", TestShovelWithoutMoney);
        Run("Girasol comienza su intervalo al colocarse", TestSunStartsAtPlacement);
        Run("Sol: generación a 15s, recogida +50 y caducidad a 10s", TestSunTiming);
        Run("Recoger tiene prioridad y conserva planta o pala", TestCollectionPriority);
        Run("Retirar productor conserva sus soles", TestSunsOutlivePlant);
        Run("Límite de tres soles y reanudación sin cola", TestSunCapacity);
        Run("Disparo solo con objetivo vivo delante y en su fila", TestTargetGate);
        Run("Barrido alcanza al objetivo más cercano una sola vez", TestSweptCollision);
        Run("Cooldown medido desde el impacto", TestImpactCooldown);
        Run("Cooldown medido desde la salida completa", TestExitCooldown);
        Run("Cuatro impactos, destello y caída de 0.8s", TestFourHits);
        Run("Retirar lanzador elimina su guisante activo", TestProjectileOwnerRemoval);
        Run("Mordiscos cada 1.1s y planta retirada al tercero", TestBites);
        Run("Escape requiere cruzar completamente el borde izquierdo", TestFullEscape);
        Run("Cadáver fuera de pantalla no provoca derrota", TestDeadZombieCannotLose);
        Run("Impactos se resuelven antes de la derrota", TestHitBeforeDefeat);
        Run("X usa cantidad configurable, mínimo uno y signos simultáneos", TestManualSpawning);
        Run("Game Over congela estado y Retry conserva cantidad manual", TestRetry);
        Run("Salir solo solicita cierre desde el modal", TestExit);
        Run("Aparición automática a 4s y frecuencia creciente", TestAutomaticSpawning);
        Run("Redimensionar conserva entidades fuera de pantalla y celdas", TestResize);

        int failed = results.Count(result => !result.passed);
        var report = new
        {
            generatedAtUtc = DateTimeOffset.UtcNow,
            passedCount = results.Count - failed,
            failedCount = failed,
            assertionCount,
            cases = results
        };
        string validationDirectory = Path.Combine(FindUnityProjectRoot(), "Validation");
        Directory.CreateDirectory(validationDirectory);
        File.WriteAllText(Path.Combine(validationDirectory, "model-tests.json"),
            JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }) + Environment.NewLine,
            new UTF8Encoding(false));
        Console.WriteLine($"\n{(failed == 0 ? "PASS" : "FAIL")}: {results.Count - failed}/{results.Count} casos; " +
            $"{assertionCount} aserciones; {failed} casos fallidos.");
        return failed == 0 ? 0 : 1;
    }

    private static void Run(string label, Action test)
    {
        int initialAssertions = assertionCount;
        try
        {
            test();
            results.Add(new CaseResult { label = label, passed = true, assertions = assertionCount - initialAssertions });
            Console.WriteLine("PASS " + label);
        }
        catch (Exception exception)
        {
            // Un fallo no oculta los resultados de los demás sistemas.
            results.Add(new CaseResult
            {
                label = label, passed = false, assertions = assertionCount - initialAssertions,
                error = exception.Message
            });
            Console.WriteLine("FAIL " + label + ": " + exception.Message);
        }
    }

    private static void Check(bool condition, string message)
    {
        assertionCount++;
        if (!condition) throw new InvalidOperationException(message);
    }

    private static GameSession NewSession() => new(1920, 1080, 42) { AutoSpawnEnabled = false };

    private static void Click(GameSession session, Vec2 pointer) =>
        session.Step(0, new GameInput { Pointer = pointer, Click = true });

    private static void BuyPlace(GameSession session, PlantKind kind, int row, int column)
    {
        Click(session, session.Layout.Card((int)kind).Center);
        Click(session, session.Layout.Cell(row, column).Center);
    }

    private static void SpendRemainingMoney(GameSession session)
    {
        while (session.Currency >= Rules.Cost(PlantKind.Sunflower))
        {
            PlantKind kind = session.Currency >= Rules.Cost(PlantKind.Peashooter)
                ? PlantKind.Peashooter : PlantKind.Sunflower;
            Click(session, session.Layout.Card((int)kind).Center);
            Click(session, new Vec2(-10, -10));
        }
    }

    private static (GameSession Session, ZombieState Near, ZombieState Far) ShootingFixture()
    {
        GameSession session = NewSession();
        BuyPlace(session, PlantKind.Peashooter, 2, 0);
        session.SpawnZombie(2);
        session.SpawnZombie(2);
        ZombieState near = session.Zombies[0];
        ZombieState far = session.Zombies[1];
        float originX = session.Layout.PeaOrigin(session.Plants[0]).X;
        near.X = near.PreviousX = originX + 600;
        far.X = far.PreviousX = originX + 650;
        session.Step(0, default);
        return (session, near, far);
    }

    private static void TestLayout()
    {
        GameSession session = NewSession();
        Check(session.Currency == 1000, "El saldo inicial debe ser 1000.");
        Check(Math.Abs(session.Layout.Selector.X - 96) < 0.01, "Selector centrado al 90%.");
        Check(Math.Abs(session.Layout.Selector.Height - 216) < 0.01, "Selector al 20% del alto.");
        Check(Math.Abs(session.Layout.Yard.Y - 238.96) < 0.01, "Separación bajo selector incorrecta.");
        Check(session.Layout.TryGetCell(session.Layout.Cell(0, 0).Center, out int row, out int column)
            && row == 0 && column == 0, "Fila cero debe estar arriba.");
        Check(session.Layout.TryGetCell(session.Layout.Cell(4, 8).Center, out row, out column)
            && row == 4 && column == 8, "Última celda no encontrada.");
        Check(!session.Layout.TryGetCell(new Vec2(1920, 1080), out _, out _), "Borde exterior debe excluirse.");
        Check(!session.Layout.TryGetCell(session.Layout.Selector.Center, out _, out _), "Selector no es patio.");
    }

    private static void TestPurchase()
    {
        GameSession session = NewSession();
        Click(session, session.Layout.Card(0).Center);
        Check(session.Currency == 950 && session.SelectedPlant == PlantKind.Sunflower, "Cobrar al seleccionar.");
        Click(session, session.Layout.Card(1).Center);
        Check(session.Currency == 950 && session.SelectedPlant == null, "Otra tarjeta primero cancela.");
        Click(session, session.Layout.Card(1).Center);
        Check(session.Currency == 850 && session.SelectedPlant == PlantKind.Peashooter, "Segunda pulsación compra.");
        Click(session, session.Layout.Cell(0, 0).Center);
        Check(session.Currency == 850 && session.Plants.Count == 1, "Colocar no vuelve a cobrar.");
        Check(session.SelectedPlant == null && session.Peas.Count == 0, "Colocación sin objetivo no dispara.");
    }

    private static void TestCancellation()
    {
        GameSession session = NewSession();
        Click(session, session.Layout.Card(0).Center);
        Click(session, new Vec2(-10, -10));
        Check(session.SelectedPlant == null && session.Currency == 950, "Cancelar fuera no reembolsa.");
        Click(session, session.Layout.Card(0).Center);
        Click(session, session.Layout.Slot(0).Center);
        Check(session.SelectedPlant == null && session.Currency == 900, "Saldo/fondo también cancela.");
    }

    private static void TestOccupiedCell()
    {
        GameSession session = NewSession();
        BuyPlace(session, PlantKind.Sunflower, 0, 0);
        Click(session, session.Layout.Card(1).Center);
        Click(session, session.Layout.Cell(0, 0).Center);
        Check(session.SelectedPlant == PlantKind.Peashooter, "Celda ocupada debe conservar selección.");
        Check(session.Plants.Count == 1 && session.Currency == 850, "No duplicar planta ni cobro.");
    }

    private static void TestShovelWithoutMoney()
    {
        GameSession session = NewSession();
        BuyPlace(session, PlantKind.Sunflower, 0, 0);
        SpendRemainingMoney(session);
        Check(session.Currency == 0, "Fixture debe quedarse sin saldo.");
        Click(session, session.Layout.Slot(10).Center);
        Check(session.ShovelSelected, "Pala disponible con saldo cero.");
        Click(session, session.Layout.Card(0).Center);
        Check(session.ShovelSelected && session.Currency == 0, "Tarjeta impagable conserva pala.");
        Click(session, session.Layout.Cell(1, 1).Center);
        Check(session.ShovelSelected, "Celda vacía conserva pala.");
        Click(session, session.Layout.Cell(0, 0).Center);
        Check(session.Plants.Count == 0 && !session.ShovelSelected, "Retirada correcta desactiva pala.");
        Check(session.Currency == 0, "Pala no cobra ni devuelve dinero.");
    }

    private static void TestSunStartsAtPlacement()
    {
        GameSession session = NewSession();
        Click(session, session.Layout.Card(0).Center);
        session.Step(14, default);
        Check(session.Suns.Count == 0, "Vista previa no produce.");
        Click(session, session.Layout.Cell(0, 0).Center);
        Check(session.Plants[0].NextSunAt == 29, "Intervalo empieza al colocar.");
        session.Step(14.99, default);
        Check(session.Suns.Count == 0, "No producir antes del intervalo completo.");
        session.Step(0.02, default);
        Check(session.Suns.Count == 1, "Debe producir después de 15 segundos colocada.");
    }

    private static void TestSunTiming()
    {
        GameSession session = NewSession();
        BuyPlace(session, PlantKind.Sunflower, 0, 0);
        session.Step(15, default);
        Check(session.Suns.Count == 1, "Primer sol a 15 segundos.");
        Click(session, session.Layout.SunPosition(session.Suns[0], session.Elapsed));
        Check(session.Currency == 1000 && session.Suns.Count == 0, "Recoger añade 50 una vez.");
        session.Step(15, default);
        Check(session.Suns.Count == 1, "Nueva generación independiente de recogida.");
        session.Step(10, default);
        Check(session.Suns.Count == 0 && session.Currency == 1000, "Caducar no añade saldo.");
    }

    private static void TestCollectionPriority()
    {
        GameSession session = NewSession();
        BuyPlace(session, PlantKind.Sunflower, 0, 0);
        session.Step(15, default);
        Click(session, session.Layout.Card(1).Center);
        Click(session, session.Layout.SunPosition(session.Suns[0], session.Elapsed));
        Check(session.SelectedPlant == PlantKind.Peashooter && session.Plants.Count == 1,
            "Recoger no coloca ni cancela la planta pendiente.");
        Check(session.Currency == 900, "Compra y recogida deben mantener su contabilidad.");
        Click(session, session.Layout.Slot(10).Center);
        session.Step(15, default);
        Click(session, session.Layout.SunPosition(session.Suns[0], session.Elapsed));
        Check(session.ShovelSelected && session.Plants.Count == 1, "Recoger no retira con la pala.");
    }

    private static void TestSunsOutlivePlant()
    {
        GameSession session = NewSession();
        BuyPlace(session, PlantKind.Sunflower, 0, 0);
        session.Step(15, default);
        Click(session, session.Layout.Slot(10).Center);
        Click(session, session.Layout.Cell(0, 0).Center);
        Check(session.Plants.Count == 0 && session.Suns.Count == 1, "Sol no pertenece a la vida de su planta.");
        session.Step(10, default);
        Check(session.Suns.Count == 0, "Sol huérfano caduca igualmente.");
    }

    private static void TestSunCapacity()
    {
        GameSession session = NewSession();
        BuyPlace(session, PlantKind.Sunflower, 0, 0);
        // 15s de producción y 10s de vida no alcanzan el cap naturalmente.
        // Inyectamos solo el estado de partida de este caso para probar la regla defensiva.
        FieldInfo field = typeof(GameSession).GetField("suns", BindingFlags.Instance | BindingFlags.NonPublic);
        var suns = (List<SunState>)field.GetValue(session);
        for (int slot = 0; slot < 3; slot++)
            suns.Add(new SunState { Id = 100 + slot, Row = 0, Column = 0, Slot = slot, BornAt = 0 });
        session.Step(1, default);
        Check(session.Plants[0].CurrencyPaused && session.CountSunsAt(0, 0) == 3, "Cap por celda pausa.");
        Check(session.CountSunsAt(0, 1) == 0, "Cap de una celda no ocupa otra.");
        Click(session, session.Layout.SunPosition(session.Suns[2], session.Elapsed));
        Check(!session.Plants[0].CurrencyPaused && session.Plants[0].NextSunAt == 16,
            "Liberar espacio reinicia los 15 segundos completos.");
        session.Step(14.99, default);
        Check(session.Suns.Count == 0, "No hay generaciones acumuladas; los viejos ya caducaron.");
        session.Step(0.02, default);
        Check(session.Suns.Count == 1, "Reanudar genera exactamente uno al vencer el intervalo.");
    }

    private static void TestTargetGate()
    {
        GameSession session = NewSession();
        BuyPlace(session, PlantKind.Peashooter, 2, 0);
        session.Step(3, default);
        Check(session.Peas.Count == 0, "Sin objetivos no se dispara.");
        session.SpawnZombie(3);
        session.Step(0, default);
        Check(session.Peas.Count == 0, "Otra fila no activa al lanzador.");
        session.SpawnZombie(2);
        ZombieState zombie = session.Zombies[1];
        zombie.X = zombie.PreviousX = 0;
        session.Step(0, default);
        Check(session.Peas.Count == 0, "Objetivo completamente detrás no activa disparo.");
        zombie.X = zombie.PreviousX = 1000;
        zombie.Health = 0;
        zombie.DiedAt = session.Elapsed;
        session.Step(0, default);
        Check(session.Peas.Count == 0, "Un muerto cayendo no es objetivo.");
        session.SpawnZombie(2);
        session.Step(0, default);
        Check(session.Peas.Count == 1, "Listo sin objetivo espera hasta que entra uno válido.");
    }

    private static void TestSweptCollision()
    {
        var (session, near, far) = ShootingFixture();
        Check(session.Peas.Count == 1, "Un proyectil por lanzador.");
        session.Step(2, default);
        Check(near.Health == 3 && far.Health == 4, "Barrido rápido solo daña al primer objetivo.");
        Check(session.Peas.Count == 0, "Impacto consume el proyectil.");
    }

    private static void TestImpactCooldown()
    {
        var (session, _, _) = ShootingFixture();
        session.Step(2, default);
        Check(session.Plants[0].NextShotAt == 4, "Cooldown empieza al impacto en t=2.");
        session.Step(1.99, default);
        Check(session.Peas.Count == 0, "No disparar antes de los dos segundos.");
        session.Step(0.011, default);
        Check(session.Peas.Count == 1, "Disparar al cumplir la espera si sigue habiendo objetivo.");
    }

    private static void TestExitCooldown()
    {
        var (session, near, far) = ShootingFixture();
        near.Health = far.Health = 0;
        near.DiedAt = far.DiedAt = 0;
        session.Step(5, default);
        Check(session.Peas.Count == 0 && session.Plants[0].NextShotAt == 7,
            "Salir por derecha consume y empieza cooldown.");
        session.SpawnZombie(2);
        session.Step(1.99, default);
        Check(session.Peas.Count == 0, "No disparar pronto aunque aparezca objetivo.");
        session.Step(0.011, default);
        Check(session.Peas.Count == 1, "Disparo habilitado al terminar la espera por salida.");
    }

    private static void TestFourHits()
    {
        var (session, near, _) = ShootingFixture();
        for (int hit = 0; hit < 4; hit++)
        {
            session.Step(2, default);
            Check(near.Health == 3 - hit, "Cada guisante resta exactamente una vida.");
            Check(Math.Abs(near.HitFlashUntil - session.Elapsed - 0.16) < 1e-9,
                "Destello dura 0.16 segundos.");
            if (hit < 3) session.Step(2.001, default);
        }
        Check(!near.IsAlive && near.Mode == ZombieMode.Dying && session.DefeatedZombies == 1,
            "Cuarto golpe mata y cuenta una sola baja.");
        session.Step(0.79, default);
        Check(session.Zombies.Contains(near), "Cadáver continúa durante su caída.");
        session.Step(0.02, default);
        Check(!session.Zombies.Contains(near) && session.DefeatedZombies == 1,
            "Retirar tras 0.8s no cuenta otra baja.");
    }

    private static void TestProjectileOwnerRemoval()
    {
        var (session, _, _) = ShootingFixture();
        Click(session, session.Layout.Slot(10).Center);
        Click(session, session.Layout.Cell(2, 0).Center);
        Check(session.Plants.Count == 0 && session.Peas.Count == 0,
            "Retirar al dueño retira su proyectil sin cooldown pendiente.");
    }

    private static void TestBites()
    {
        GameSession session = NewSession();
        BuyPlace(session, PlantKind.Sunflower, 1, 0);
        session.SpawnZombie(1);
        ZombieState zombie = session.Zombies[0];
        zombie.X = zombie.PreviousX = session.Layout.Cell(1, 0).Center.X +
            session.Layout.PlantHeight / 3 + session.Layout.ZombieHeight * 0.23f;
        session.Step(0, default);
        Check(zombie.Mode == ZombieMode.Eating, "Contacto detiene al zombi y empieza comer.");
        session.Step(1.09, default);
        Check(session.Plants[0].Health == 3, "No hay mordisco inmediato.");
        session.Step(0.02, default);
        Check(session.Plants[0].Health == 2, "Primer mordisco tras 1.1 segundos.");
        session.Step(1.1, default);
        Check(session.Plants[0].Health == 1, "Segundo mordisco.");
        session.Step(1.1, default);
        Check(session.Plants.Count == 0 && zombie.Mode == ZombieMode.Walking,
            "Tercer mordisco retira y el zombi continúa.");
    }

    private static void TestFullEscape()
    {
        GameSession session = NewSession();
        session.SpawnZombie(0);
        ZombieState zombie = session.Zombies[0];
        zombie.X = zombie.PreviousX = -session.Layout.ZombieHeight * 0.23f;
        session.Step(0, default);
        Check(!session.IsGameOver, "Borde derecho exactamente en cero aún no escapó.");
        zombie.X -= 0.01f;
        session.Step(0, default);
        Check(session.IsGameOver, "Solo pierde al salir completamente.");
    }

    private static void TestDeadZombieCannotLose()
    {
        GameSession session = NewSession();
        session.SpawnZombie(0);
        ZombieState zombie = session.Zombies[0];
        zombie.X = zombie.PreviousX = -100;
        zombie.Health = 0;
        zombie.DiedAt = 0;
        session.Step(0, default);
        Check(!session.IsGameOver, "Cadáver fuera de pantalla no amenaza.");
        session.Step(0.81, default);
        Check(!session.IsGameOver && session.Zombies.Count == 0, "Caída se limpia sin derrota.");
    }

    private static void TestHitBeforeDefeat()
    {
        var (session, near, far) = ShootingFixture();
        // Fixture de movimiento extremo: enemigo cruzaría la izquierda en este paso.
        near.X = near.PreviousX = 0;
        near.Health = 1;
        far.Health = 0;
        far.DiedAt = 0;
        session.Peas[0].X = -100;
        session.Step(2, default);
        Check(!near.IsAlive && session.DefeatedZombies == 1, "Barrido debe resolver el cuarto golpe.");
        Check(!session.IsGameOver, "No perder por el enemigo muerto en este mismo paso.");
    }

    private static void TestManualSpawning()
    {
        GameSession session = NewSession();
        session.Step(0, new GameInput { Decrease = true });
        Check(session.ManualZombieCount == 1, "Mínimo de un zombi por tanda.");
        session.Step(0, new GameInput { Increase = true, Decrease = true });
        Check(session.ManualZombieCount == 1, "Signos simultáneos se cancelan.");
        session.Step(0, new GameInput { Increase = true });
        session.Step(0, new GameInput { Spawn = true });
        Check(session.ManualZombieCount == 2 && session.Zombies.Count == 2, "Tanda usa cantidad configurada.");
        Check(session.Zombies.All(zombie => zombie.Row >= 0 && zombie.Row < Rules.Rows), "Filas válidas.");
        Check(session.Currency == 1000 && session.NextZombieAt == 4, "Manual no afecta coste/automático.");
        session.Step(0, default);
        Check(session.Zombies.Count == 2, "Sin otra pulsación no crea nuevas tandas.");
        session.Step(0, new GameInput { Spawn = true });
        Check(session.Zombies.Count == 4, "Otra pulsación no tiene cooldown.");
    }

    private static void TestRetry()
    {
        GameSession session = NewSession();
        BuyPlace(session, PlantKind.Sunflower, 0, 0);
        session.Step(15, default);
        session.Step(0, new GameInput { Increase = true });
        session.Step(0, new GameInput { Increase = true, Spawn = true });
        session.Zombies[0].X = session.Zombies[0].PreviousX = -100;
        session.Step(0, default);
        Check(session.IsGameOver, "Fixture debe perder.");
        double frozen = session.Elapsed;
        session.Step(10, new GameInput { Increase = true, Spawn = true });
        Check(session.Elapsed == frozen && session.ManualZombieCount == 3 && session.Zombies.Count == 3,
            "Derrota congela reloj e ignora atajos.");
        Click(session, session.Layout.RetryButton.Center);
        Check(!session.IsGameOver && session.Currency == 1000 && session.Elapsed == 0, "Retry restaura partida.");
        Check(session.Plants.Count == 0 && session.Suns.Count == 0 && session.Peas.Count == 0 &&
            session.Zombies.Count == 0, "Retry limpia todas las entidades.");
        Check(session.ManualZombieCount == 3 && session.NextZombieAt == 4,
            "Conserva configuración manual y reinicia dificultad.");
    }

    private static void TestExit()
    {
        GameSession session = NewSession();
        Click(session, session.Layout.ExitButton.Center);
        Check(!session.ExitRequested, "Zona modal no cierra durante una partida.");
        session.SpawnZombie(0);
        session.Zombies[0].X = session.Zombies[0].PreviousX = -100;
        session.Step(0, default);
        Click(session, session.Layout.ExitButton.Center);
        Check(session.ExitRequested, "Salir solicita cierre desde Game Over.");
    }

    private static void TestAutomaticSpawning()
    {
        GameSession session = new(1920, 1080, 42);
        session.Step(3.99, default);
        Check(session.Zombies.Count == 0, "No generar antes de cuatro segundos.");
        session.Step(0.02, default);
        Check(session.Zombies.Count == 1, "Primer spawn a los cuatro segundos.");
        Check(Math.Abs(session.NextZombieAt - 10.92) < 1e-9, "Se programa desde la fecha, no desde now.");
        Check(Rules.SpawnIntervalAt(100) < Rules.SpawnIntervalAt(0), "La frecuencia debe aumentar.");
        Check(Rules.SpawnIntervalAt(1000) == 1.2, "Intervalo mínimo es 1.2 segundos.");
    }

    private static void TestResize()
    {
        var (session, _, _) = ShootingFixture();
        session.SpawnZombie(4);
        ZombieState outside = session.Zombies[2];
        float oldX = outside.X;
        float oldPreviousX = outside.PreviousX;
        float oldPeaX = session.Peas[0].X;
        double nextShot = session.Plants[0].NextShotAt;
        session.Resize(3840, 2160);
        Check(outside.X == oldX * 2 && outside.X > 3840, "No recortar posición exterior al redimensionar.");
        Check(outside.PreviousX == oldPreviousX * 2, "Transportar posición previa para el barrido.");
        Check(session.Peas[0].X == oldPeaX * 2, "Conservar posición relativa del guisante.");
        Check(session.Plants[0].Row == 2 && session.Plants[0].Column == 0 &&
            session.Plants[0].NextShotAt == nextShot, "Celda y temporizador no cambian.");
    }

    private static string FindUnityProjectRoot()
    {
        for (DirectoryInfo directory = new(AppContext.BaseDirectory); directory != null; directory = directory.Parent)
            if (Directory.Exists(Path.Combine(directory.FullName, "Assets", "_Game", "Scripts", "Model")))
                return directory.FullName;
        throw new DirectoryNotFoundException("No se encontró la raíz de UnityPort para guardar el informe.");
    }
}
