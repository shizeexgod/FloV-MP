using System.Diagnostics;
using FloVMP.Core.AntiCheat;
using FloVMP.Core.Auth;
using FloVMP.Core.Spatial;
using FloVMP.Core.Voice;

namespace FloVMP.LoadTest;

/// <summary>
/// Нагрузочный стенд серверных горячих путей FloV:MP.
///
/// ЧТО ЭТО ИЗМЕРЯЕТ. Стоимость нашего кода на одном тике при N игроках:
/// пространственную сетку, стриминг с окклюзией, адаптивную синхронизацию,
/// маршрутизацию голоса, стоимость входа (PBKDF2 + хранилище) и запись
/// аккаунтов. Именно здесь живут O(n^2)-ловушки и блокировки игрового тика.
///
/// ЧЕГО ЭТО НЕ ИЗМЕРЯЕТ — и это надо говорить вслух, а не прятать.
/// Сетевой слой alt:V (сериализация, ENet, полоса, потери) — чужой бинарник,
/// его здесь нет. Реального клиента тоже нет: стенд не открывает игровых
/// соединений. Поэтому «стенд держит 2000» означает «наш код на 2000 укладывается
/// в бюджет тика», а не «сервер потянет 2000 живых игроков». Второе проверяется
/// только живым онлайном, и подменять одно другим нельзя.
/// </summary>
public static class Program
{
    public static int Main(string[] args)
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;

        var players = GetInt(args, "--players", 2000);
        var ticks = GetInt(args, "--ticks", 300);
        var tickRate = GetInt(args, "--tick-rate", 60);
        var streamRadius = GetFloat(args, "--stream-radius", 300f);
        var hotspotShare = GetFloat(args, "--hotspot-share", 0.65f);
        var speakingShare = GetFloat(args, "--speaking-share", 0.12f);
        var seed = GetInt(args, "--seed", 1337);
        var skipAuth = args.Contains("--no-auth");
        var json = GetArg(args, "--json");

        if (args.Contains("--help") || args.Contains("-h"))
        {
            PrintHelp();
            return 0;
        }

        var budgetMs = 1000.0 / tickRate;

        Console.WriteLine();
        Console.WriteLine("FloV:MP — нагрузочный стенд серверных горячих путей");
        Console.WriteLine($"игроков: {players} | тиков: {ticks} | частота: {tickRate} Гц " +
                          $"(бюджет тика {budgetMs:F2} мс)");
        Console.WriteLine($"радиус стриминга: {streamRadius} м | в горячих точках: {hotspotShare:P0} | " +
                          $"говорят одновременно: {speakingShare:P0}");
        Console.WriteLine($"GC: {(System.Runtime.GCSettings.IsServerGC ? "серверный" : "рабочей станции")} | " +
                          $"ядер: {Environment.ProcessorCount}");

        var results = new List<Samples>();
        var extra = new Dictionary<string, double>();

        var sim = World.Create(players, hotspotShare, seed);

        results.Add(RunWorldTick(sim, ticks, tickRate, streamRadius, speakingShare, seed, extra));

        if (!skipAuth)
            RunAuthThroughput(extra);

        RunAccountStore(players, extra);

        Report.Header("ИТОГ");
        Report.TableHeader();
        foreach (var s in results) Report.Line(s);

        Console.WriteLine();
        var worldP99 = results[0].P99;
        Report.Verdict($"игровой тик при {players} игроках", worldP99, budgetMs);

        Console.WriteLine();
        Report.Note("Что стенд НЕ проверяет: сетевой слой alt:V, реальных клиентов,");
        Report.Note("полосу и потери пакетов. Это потолок НАШЕГО кода, не потолок сервера.");

        if (json is not null) WriteJson(json, players, ticks, tickRate, budgetMs, results, extra);

        // Ненулевой код возврата — чтобы стенд можно было поставить в CI и
        // ловить регрессию производительности так же, как падение теста.
        return worldP99 <= budgetMs ? 0 : 1;
    }

    // ------------------------------------------------------------------
    // Сценарий 1: полный игровой тик
    // ------------------------------------------------------------------
    private static Samples RunWorldTick(SimPlayer[] sim, int ticks, int tickRate,
                                        float streamRadius, float speakingShare, int seed,
                                        Dictionary<string, double> extra)
    {
        Report.Header($"Сценарий 1 — игровой тик: сетка + стриминг + голос + адаптивная синхронизация");

        var grid = new SpatialHashGrid<ulong>(cellSize: 64f);
        var occlusion = new OcclusionCullingService { DefaultMaxDistance = streamRadius };
        var voice = new VoiceGridRouter(grid);
        var tickManager = new AdaptiveTickManager<ulong>();

        // Интерьеры: пара десятков зон, как на живом RP-сервере.
        for (var i = 0; i < 24; i++)
            occlusion.RegisterZone($"zone{i}", new Vector3D(i * 100f, i * 100f, 0f),
                                   new Vector3D(i * 100f + 40f, i * 100f + 40f, 20f));

        foreach (var p in sim)
        {
            grid.InsertOrUpdate(p.Id, p.Position, p.Dimension);
            tickManager.RegisterEntity(p.Id, p.Position, p.Dimension);
        }

        var rng = new Random(seed);
        var dt = 1f / tickRate;

        var total = new Samples("тик целиком", ticks);
        var gridPhase = new Samples("  сетка (обновление позиций)", ticks);
        var streamPhase = new Samples("  стриминг + окклюзия", ticks);
        var voicePhase = new Samples("  маршрутизация голоса", ticks);
        var syncPhase = new Samples("  адаптивная синхронизация", ticks);

        long neighborTotal = 0, neighborSamples = 0, visibleTotal = 0, voiceRecipients = 0;
        var candidates = new List<(ulong Item, Vector3D Pos, int Dim)>(512);
        var sw = new Stopwatch();
        var phase = new Stopwatch();

        // Прогрев: первые тики платят за раскладку по ячейкам и JIT, они бы
        // испортили p99 шумом, не относящимся к устойчивой нагрузке.
        var warmup = Math.Min(30, ticks / 10 + 1);

        for (var tick = 0; tick < ticks + warmup; tick++)
        {
            var measured = tick >= warmup;
            sw.Restart();

            // --- фаза 1: движение и обновление сетки ---
            phase.Restart();
            World.Step(sim, dt, rng);
            foreach (var p in sim)
            {
                grid.InsertOrUpdate(p.Id, p.Position, p.Dimension);
                tickManager.UpdateEntityState(p.Id, p.Position, p.Velocity, p.InCombat, p.Dimension);
            }
            phase.Stop();
            if (measured) gridPhase.Add(phase.Elapsed.TotalMilliseconds);

            // --- фаза 2: стриминг с окклюзией ---
            // Ключевой момент: кандидаты берутся из сетки (соседи в радиусе),
            // а НЕ перебором всех игроков. Перебор всех дал бы O(n^2) и
            // гарантированный обвал на паре тысяч онлайна.
            phase.Restart();
            foreach (var p in sim)
            {
                var nearby = grid.FindInRadius(p.Position, streamRadius, p.Dimension);
                if (measured) { neighborTotal += nearby.Count; neighborSamples++; }

                candidates.Clear();
                foreach (var id in nearby)
                {
                    if (id == p.Id) continue;
                    if (grid.TryGetPosition(id, out var pos, out var dim))
                        candidates.Add((id, pos, dim));
                }

                var visible = occlusion.FilterVisible(p.Position, p.Heading, p.Dimension, candidates, streamRadius);
                if (measured) visibleTotal += visible.Count;
            }
            phase.Stop();
            if (measured) streamPhase.Add(phase.Elapsed.TotalMilliseconds);

            // --- фаза 3: голос ---
            phase.Restart();
            foreach (var p in sim)
            {
                if (rng.NextDouble() >= speakingShare) continue;
                var recipients = voice.RouteSpatialVoice(p.Id, p.Position, p.Dimension, VoiceRangeMode.Normal);
                if (measured) voiceRecipients += recipients.Count;
            }
            phase.Stop();
            if (measured) voicePhase.Add(phase.Elapsed.TotalMilliseconds);

            // --- фаза 4: решение «синхронизировать ли пару в этом тике» ---
            // Считаем не для всех пар, а для ближнего круга каждого игрока —
            // так же, как это работает в реальном стримере.
            phase.Restart();
            foreach (var p in sim)
            {
                var nearby = grid.FindInRadius(p.Position, 120f, p.Dimension);
                var checkedPairs = 0;
                foreach (var id in nearby)
                {
                    if (id == p.Id) continue;
                    tickManager.ShouldSyncThisTick(id, p.Id, tick);
                    if (++checkedPairs >= 150) break; // предел, как у клиентского лимита сущностей
                }
            }
            phase.Stop();
            if (measured) syncPhase.Add(phase.Elapsed.TotalMilliseconds);

            sw.Stop();
            if (measured) total.Add(sw.Elapsed.TotalMilliseconds);
        }

        var avgNeighbors = neighborSamples == 0 ? 0 : (double)neighborTotal / neighborSamples;
        var avgVisible = neighborSamples == 0 ? 0 : (double)visibleTotal / neighborSamples;

        Report.TableHeader();
        Report.Line(total);
        Report.Line(gridPhase);
        Report.Line(streamPhase);
        Report.Line(voicePhase);
        Report.Line(syncPhase);
        Console.WriteLine();
        Report.Note($"соседей в радиусе стриминга в среднем: {avgNeighbors:F1}");
        Report.Note($"после окклюзии остаётся:               {avgVisible:F1}");
        Report.Note($"получателей голоса за тик:             {(double)voiceRecipients / Math.Max(1, ticks):F0}");
        Report.Note($"аллокации за прогон:                   {GC.GetTotalAllocatedBytes() / 1024 / 1024} МБ, " +
                    $"сборок gen2: {GC.CollectionCount(2)}");

        extra["neighbors_avg"] = avgNeighbors;
        extra["visible_avg"] = avgVisible;
        extra["gc_gen2"] = GC.CollectionCount(2);
        extra["tick_grid_p99"] = gridPhase.P99;
        extra["tick_stream_p99"] = streamPhase.P99;
        extra["tick_voice_p99"] = voicePhase.P99;
        extra["tick_sync_p99"] = syncPhase.P99;

        return total;
    }

    // ------------------------------------------------------------------
    // Сценарий 2: пропускная способность входа
    // ------------------------------------------------------------------
    private static void RunAuthThroughput(Dictionary<string, double> extra)
    {
        Report.Header("Сценарий 2 — вход: стоимость PBKDF2 и пропускная способность");

        var hash = PasswordHasher.Hash("ОченьДлинныйПарольИгрока123");

        // Одиночная стоимость: именно её раньше платил игровой тик на каждом
        // логине, пока проверка пароля не уехала в фон.
        var single = new Samples("одна проверка пароля", 64);
        for (var i = 0; i < 40; i++)
        {
            var sw = Stopwatch.StartNew();
            PasswordHasher.Verify("ОченьДлинныйПарольИгрока123", hash);
            sw.Stop();
            if (i >= 8) single.Add(sw.Elapsed.TotalMilliseconds);
        }

        Report.TableHeader();
        Report.Line(single);

        // Параллельная пропускная способность — сколько входов в секунду
        // вытянет пул потоков при массовом наплыве (рестарт, прайм-тайм).
        const int totalLogins = 400;
        var swAll = Stopwatch.StartNew();
        Parallel.For(0, totalLogins, _ => PasswordHasher.Verify("ОченьДлинныйПарольИгрока123", hash));
        swAll.Stop();

        var perSec = totalLogins / swAll.Elapsed.TotalSeconds;
        Console.WriteLine();
        Report.Note($"одна проверка:            {single.P50:F1} мс (p99 {single.P99:F1} мс)");
        Report.Note($"параллельно:              {perSec:F0} входов/с на {Environment.ProcessorCount} ядрах");
        Report.Note($"наплыв 200 входов займёт: {200 / perSec:F1} с фоновых потоков");
        Report.Note("Важно: с асинхронным входом эта стоимость игрового тика не касается —");
        Report.Note("тик только забирает готовые результаты из очереди (Pump).");

        extra["auth_single_ms"] = single.P50;
        extra["auth_per_sec"] = perSec;
    }

    // ------------------------------------------------------------------
    // Сценарий 3: хранилище аккаунтов
    // ------------------------------------------------------------------
    private static void RunAccountStore(int players, Dictionary<string, double> extra)
    {
        Report.Header("Сценарий 3 — хранилище аккаунтов: запись без блокировки тика");

        var dir = Path.Combine(Path.GetTempPath(), "flovmp-loadtest-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, "accounts.json");

        try
        {
            var store = new JsonAccountStore(path);
            var n = Math.Min(players, 1500);

            var createSw = Stopwatch.StartNew();
            for (var i = 0; i < n; i++)
                store.Create($"loadtest_{i}", "x");
            createSw.Stop();

            // Обновление — то, что сервер делает постоянно (деньги, позиция,
            // предупреждения). Раньше каждый такой вызов переписывал файл
            // целиком прямо в игровом тике.
            var updates = new Samples("одно обновление аккаунта", n);
            for (var i = 0; i < n; i++)
            {
                var acc = store.FindByUsername($"loadtest_{i}");
                if (acc is null) continue;
                acc.Cash += 100;
                var sw = Stopwatch.StartNew();
                store.Update(acc);
                sw.Stop();
                updates.Add(sw.Elapsed.TotalMilliseconds);
            }

            var flushSw = Stopwatch.StartNew();
            store.Flush();
            flushSw.Stop();

            (store as IDisposable)?.Dispose();

            Report.TableHeader();
            Report.Line(updates);
            Console.WriteLine();
            Report.Note($"создание {n} аккаунтов:        {createSw.Elapsed.TotalMilliseconds:F0} мс " +
                        $"({createSw.Elapsed.TotalMilliseconds / n:F2} мс на аккаунт)");
            Report.Note($"сброс {n} изменений на диск:   {flushSw.Elapsed.TotalMilliseconds:F0} мс " +
                        "(за пределами игрового тика, по таймеру)");
            Report.Note($"размер файла:                 {new FileInfo(path).Length / 1024} КБ");
            Report.Note("Обновление стоит доли миллисекунды, потому что пишет в память и");
            Report.Note("помечает состояние грязным — на диск уходит фоновый таймер.");

            extra["store_update_p99"] = updates.P99;
            extra["store_flush_ms"] = flushSw.Elapsed.TotalMilliseconds;
        }
        finally
        {
            try { Directory.Delete(dir, true); } catch { /* временный каталог, не критично */ }
        }
    }

    // ------------------------------------------------------------------

    private static void WriteJson(string path, int players, int ticks, int tickRate, double budget,
                                  List<Samples> results, Dictionary<string, double> extra)
    {
        using var w = new StreamWriter(path, false, System.Text.Encoding.UTF8);
        w.WriteLine("{");
        w.WriteLine($"  \"players\": {players},");
        w.WriteLine($"  \"ticks\": {ticks},");
        w.WriteLine($"  \"tick_rate\": {tickRate},");
        w.WriteLine($"  \"budget_ms\": {budget.ToString("F3", System.Globalization.CultureInfo.InvariantCulture)},");
        w.WriteLine($"  \"tick_p50\": {Num(results[0].P50)},");
        w.WriteLine($"  \"tick_p95\": {Num(results[0].P95)},");
        w.WriteLine($"  \"tick_p99\": {Num(results[0].P99)},");
        w.WriteLine($"  \"tick_max\": {Num(results[0].Max)},");
        foreach (var kv in extra)
            w.WriteLine($"  \"{kv.Key}\": {Num(kv.Value)},");
        w.WriteLine($"  \"passed\": {(results[0].P99 <= budget ? "true" : "false")}");
        w.WriteLine("}");
        Console.WriteLine();
        Report.Note($"отчёт записан: {path}");
    }

    private static string Num(double v) =>
        v.ToString("F3", System.Globalization.CultureInfo.InvariantCulture);

    private static void PrintHelp()
    {
        Console.WriteLine("""
            FloV:MP — нагрузочный стенд серверных горячих путей.

              --players N         сколько игроков симулировать (по умолчанию 2000)
              --ticks N           сколько тиков замерять (300)
              --tick-rate N       частота сервера, Гц — задаёт бюджет тика (60)
              --stream-radius F   радиус стриминга сущностей, м (300)
              --hotspot-share F   доля игроков в горячих точках, 0..1 (0.65)
              --speaking-share F  доля говорящих одновременно, 0..1 (0.12)
              --seed N            зерно генератора, для повторяемости (1337)
              --no-auth           пропустить замер PBKDF2 (он самый долгий)
              --json PATH         записать отчёт в JSON для CI

            Код возврата 1, если p99 игрового тика вышел за бюджет.
            """);
    }

    private static string? GetArg(string[] args, string name)
    {
        var i = Array.IndexOf(args, name);
        return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
    }

    private static int GetInt(string[] args, string name, int fallback) =>
        int.TryParse(GetArg(args, name), out var v) ? v : fallback;

    private static float GetFloat(string[] args, string name, float fallback) =>
        float.TryParse(GetArg(args, name), System.Globalization.NumberStyles.Float,
                       System.Globalization.CultureInfo.InvariantCulture, out var v) ? v : fallback;
}
