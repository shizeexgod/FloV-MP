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
        // Размер ячейки сетки — главный рычаг стоимости стриминга. Меньше
        // ячейка = больше словарных поисков на запрос, больше = больше лишних
        // проверок дистанции. Оптимум подбирается замером, а не на глаз.
        var cellSize = GetFloat(args, "--cell-size", 64f);
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

        if (args.Contains("--sweep"))
            return RunSweep(ticks, tickRate, streamRadius, hotspotShare, speakingShare, seed, budgetMs, json, cellSize);

        var results = new List<Samples>();
        var extra = new Dictionary<string, double>();

        var sim = World.Create(players, hotspotShare, seed);

        results.Add(RunWorldTick(sim, ticks, tickRate, streamRadius, speakingShare, seed, extra, cellSize));

        if (!skipAuth)
            RunAuthThroughput(extra);

        RunAccountStore(players, extra);
        RunInventoryStore(players, extra);

        Report.Header("ИТОГ");
        Report.TableHeader();
        foreach (var s in results) Report.Line(s);

        Console.WriteLine();
        var worldP99 = results[0].P99;
        Report.Verdict($"игровой тик при {players} игроках", worldP99, budgetMs);
        WarnIfNoisy(results[0]);

        Console.WriteLine();
        Report.Note("Что стенд НЕ проверяет: сетевой слой alt:V, реальных клиентов,");
        Report.Note("полосу и потери пакетов. Это потолок НАШЕГО кода, не потолок сервера.");

        if (json is not null) WriteJson(json, players, ticks, tickRate, budgetMs, results, extra);

        // Ненулевой код возврата — чтобы стенд можно было поставить в CI и
        // ловить регрессию производительности так же, как падение теста.
        return worldP99 <= budgetMs ? 0 : 1;
    }

    // ------------------------------------------------------------------
    // Развёртка по онлайну: до какого числа игроков код укладывается в тик
    // ------------------------------------------------------------------
    /// <summary>
    /// Прогон одного и того же сценария на растущем онлайне.
    ///
    /// Смысл: заменить ответ «масштаб неизвестен» на число. Стоимость тика
    /// растёт быстрее линейной — вместе с онлайном растёт и плотность, то есть
    /// число соседей у каждого игрока. Одна точка замера этого не показывает,
    /// а развёртка показывает, где именно проходит потолок одного инстанса —
    /// и, значит, когда пора шардировать.
    /// </summary>
    private static int RunSweep(int ticks, int tickRate, float streamRadius, float hotspotShare,
                                float speakingShare, int seed, double budgetMs, string? json,
                                float cellSize = 64f)
    {
        int[] steps = { 250, 500, 1000, 1500, 2000, 3000 };

        Report.Header("Развёртка по онлайну — где проходит потолок одного инстанса");
        Console.WriteLine($"{"игроков",10}{"соседей",10}{"p50",10}{"p95",10}{"p99",10}{"вердикт",12}");
        Console.WriteLine(new string('-', 78));

        var lastOk = 0;
        var rows = new List<(int Players, double Neighbors, double P50, double P95, double P99, bool Ok)>();

        foreach (var n in steps)
        {
            // Между шагами обязательно чистим память. Иначе сетка, буферы и
            // мусор предыдущего шага доживают до следующего, и замер растёт на
            // ровном месте: 1500 игроков внутри развёртки показывали 35.8 мс
            // против 19.3 мс в отдельном прогоне. Развёртка для того и нужна,
            // чтобы сравнивать шаги между собой — значит, каждый должен
            // стартовать с одинаково чистой памяти.
            GC.Collect(2, GCCollectionMode.Forced, blocking: true);
            GC.WaitForPendingFinalizers();
            GC.Collect(2, GCCollectionMode.Forced, blocking: true);

            var extra = new Dictionary<string, double>();
            var sim = World.Create(n, hotspotShare, seed);

            // Вывод самого сценария глушим — в развёртке нужна только строка.
            var stdout = Console.Out;
            Console.SetOut(TextWriter.Null);
            Samples s;
            try { s = RunWorldTick(sim, ticks, tickRate, streamRadius, speakingShare, seed, extra, cellSize); }
            finally { Console.SetOut(stdout); }

            var ok = s.P99 <= budgetMs;
            var noisy = s.P50 > 0.01 && s.P99 / s.P50 >= 2.5;
            if (ok) lastOk = n;
            rows.Add((n, extra.GetValueOrDefault("neighbors_avg"), s.P50, s.P95, s.P99, ok));

            var prev = Console.ForegroundColor;
            Console.ForegroundColor = ok ? ConsoleColor.Green : ConsoleColor.Red;
            Console.WriteLine($"{n,10}{extra.GetValueOrDefault("neighbors_avg"),10:F0}" +
                              $"{s.P50,10:F2}{s.P95,10:F2}{s.P99,10:F2}" +
                              $"{(ok ? "в бюджете" : (noisy ? "ШУМ?" : "ПРЕВЫШЕН")),12}");
            Console.ForegroundColor = prev;

            if (!ok && noisy)
            {
                Report.Note("  Разброс p99/p50 велик — замер, похоже, испорчен посторонней");
                Report.Note("  нагрузкой на машине. Повторите на свободной, это может быть не потолок.");
            }

            // Дальше уже бессмысленно: если потолок пробит, следующие шаги
            // только дольше считаются и ничего нового не скажут.
            if (!ok) break;
        }

        Console.WriteLine();
        if (lastOk == 0)
            Report.Note($"В бюджет {budgetMs:F2} мс не уложился даже минимальный онлайн — ищите регрессию.");
        else
            Report.Note($"Один инстанс держит в бюджете тика ({budgetMs:F2} мс) до {lastOk} игроков включительно.");
        Report.Note("Выше этой отметки нужен второй инстанс (шардирование по измерениям/районам),");
        Report.Note("либо снижение частоты тика, либо сокращение радиуса стриминга.");
        Console.WriteLine();
        Report.Note("Ещё раз: это потолок НАШЕГО кода. Сетевой слой alt:V и живые клиенты");
        Report.Note("сюда не входят — реальный потолок сервера не выше этого, но может быть ниже.");

        if (json is not null)
        {
            using var w = new StreamWriter(json, false, System.Text.Encoding.UTF8);
            w.WriteLine("{");
            w.WriteLine($"  \"budget_ms\": {Num(budgetMs)},");
            w.WriteLine($"  \"max_players_within_budget\": {lastOk},");
            w.WriteLine("  \"steps\": [");
            for (var i = 0; i < rows.Count; i++)
            {
                var r = rows[i];
                w.WriteLine($"    {{ \"players\": {r.Players}, \"neighbors_avg\": {Num(r.Neighbors)}, " +
                            $"\"p50\": {Num(r.P50)}, \"p95\": {Num(r.P95)}, \"p99\": {Num(r.P99)}, " +
                            $"\"passed\": {(r.Ok ? "true" : "false")} }}{(i == rows.Count - 1 ? "" : ",")}");
            }
            w.WriteLine("  ]");
            w.WriteLine("}");
            Report.Note($"отчёт записан: {json}");
        }

        return lastOk > 0 ? 0 : 1;
    }

    /// <summary>
    /// Предупредить, если замер похож на испорченный посторонней нагрузкой.
    ///
    /// Стенд считает чистое процессорное время нашего кода, и параллельная
    /// сборка или антивирус на той же машине бьют именно по хвостам: p50
    /// остаётся прежним, а p99 взлетает втрое. Без этой подсказки легко
    /// принять шум за регрессию — у меня самого один такой прогон показал
    /// 59.96 мс там, где на свободной машине 24.18 мс.
    /// </summary>
    private static void WarnIfNoisy(Samples tick)
    {
        if (tick.P50 <= 0.01) return;
        var spread = tick.P99 / tick.P50;
        if (spread < 2.5) return;

        var prev = Console.ForegroundColor;
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine();
        Console.WriteLine($"  [ВНИМАНИЕ] p99 выше p50 в {spread:F1} раза — похоже, машина была занята");
        Console.WriteLine("  чем-то ещё (сборка, антивирус, другой прогон). Хвосты замера ненадёжны,");
        Console.WriteLine("  повторите на свободной машине, прежде чем считать это регрессией.");
        Console.ForegroundColor = prev;
    }

    // ------------------------------------------------------------------
    // Сценарий 1: полный игровой тик
    // ------------------------------------------------------------------
    private static Samples RunWorldTick(SimPlayer[] sim, int ticks, int tickRate,
                                        float streamRadius, float speakingShare, int seed,
                                        Dictionary<string, double> extra, float cellSize = 64f)
    {
        Report.Header($"Сценарий 1 — игровой тик: сетка + стриминг + голос + адаптивная синхронизация");

        var grid = new SpatialHashGrid<ulong>(cellSize: cellSize);
        var occlusion = new OcclusionCullingService { DefaultMaxDistance = streamRadius };
        var voice = new VoiceGridRouter(grid);
        var tickManager = new AdaptiveTickManager<ulong>();

        // Интерьеры ставятся ВНУТРИ горячих точек, а не в случайных координатах:
        // зона, мимо которой никто не ходит, ничего не отсекает, и стенд
        // показал бы работу окклюзии как бесполезную. На живом сервере
        // интерьеры стоят ровно там, где толпа — в банке, мэрии, участке.
        var zoneId = 0;
        foreach (var h in World.Hotspots)
        {
            for (var k = 0; k < 3; k++)
            {
                var offset = new Vector3D(h.X + k * 45f - 45f, h.Y + k * 45f - 45f, h.Z - 2f);
                occlusion.RegisterZone($"zone{zoneId++}", offset,
                                       new Vector3D(offset.X + 30f, offset.Y + 30f, offset.Z + 12f));
            }
        }

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

        // Буферы переиспользуются между тиками — так же, как это обязан делать
        // серверный код. Список на каждый запрос дал бы десятки мегабайт мусора
        // и паузы сборщика прямо в игровом тике.
        var candidates = new List<(ulong Entity, Vector3D Position, int Dimension)>(512);
        var neighbors = new List<ulong>(512);
        var visibleBuffer = new List<ulong>(512);
        var sw = new Stopwatch();
        var phase = new Stopwatch();

        // Прогрев. Первые тики платят не только за раскладку по ячейкам, но и
        // за многоуровневую компиляцию .NET: метод уходит в оптимизированный
        // код лишь после нескольких десятков вызовов. Без достаточного прогрева
        // именно эти тики оседают в p95/p99 и стенд «находит» несуществующий
        // провал производительности.
        var warmup = Math.Min(120, Math.Max(60, ticks / 4));

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
                // Позиции соседей приходят тем же запросом: отдельный
                // TryGetPosition на каждого соседа стоил бы ещё одной
                // блокировки сетки на каждую пару.
                var found = grid.FindInRadiusWithPositions(p.Position, streamRadius, p.Dimension, candidates);
                if (measured) { neighborTotal += found; neighborSamples++; }

                var visible = occlusion.FilterVisibleInto(
                    p.Position, p.Heading, p.Dimension, candidates, visibleBuffer, streamRadius);
                if (measured) visibleTotal += visible;
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
                grid.FindInRadius(p.Position, 120f, p.Dimension, neighbors);
                var checkedPairs = 0;
                foreach (var id in neighbors)
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
    // Сценарий 4: автосейв инвентарей
    // ------------------------------------------------------------------
    /// <summary>
    /// Стоимость автосейва — то место, где сервер замирал на минуты.
    ///
    /// Save() сериализовал словарь целиком (инвентари ВСЕХ аккаунтов) и
    /// переписывал весь файл, а автосейв зовёт Save() на каждого игрока
    /// онлайн: N полных перезаписей файла с N инвентарями, O(n^2) прямо в
    /// игровом тике. В боевом логе это выглядело как
    /// resourceManager.Update() took: 240988 ms.
    /// </summary>
    private static void RunInventoryStore(int players, Dictionary<string, double> extra)
    {
        Report.Header("Сценарий 4 — автосейв инвентарей: полный цикл сохранения");

        var dir = Path.Combine(Path.GetTempPath(), "flovmp-loadtest-inv-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, "inventories.json");

        try
        {
            var store = new FloVMP.Core.Items.JsonInventoryStore(path);
            var n = Math.Min(players, 2000);

            // Наполняем, как на живом сервере: у каждого игрока свой инвентарь.
            for (var i = 0; i < n; i++)
            {
                var inv = new FloVMP.Core.Items.Inventory(slotCount: 24, maxWeight: 40);
                inv.Add("water", (i % 9) + 1);
                inv.Add("phone", 1);
                store.Save(i, inv);
            }
            store.Flush();

            // Собственно автосейв: сохранить всех разом.
            var saves = new Samples("автосейв: одно сохранение", n);
            for (var i = 0; i < n; i++)
            {
                var inv = new FloVMP.Core.Items.Inventory(slotCount: 24, maxWeight: 40);
                inv.Add("water", (i % 9) + 2);
                var sw = Stopwatch.StartNew();
                store.Save(i, inv);
                sw.Stop();
                saves.Add(sw.Elapsed.TotalMilliseconds);
            }

            var flushSw = Stopwatch.StartNew();
            store.Flush();
            flushSw.Stop();

            var cycleMs = saves.Count * saves.Mean + flushSw.Elapsed.TotalMilliseconds;

            store.Dispose();

            Report.TableHeader();
            Report.Line(saves);
            Console.WriteLine();
            Report.Note($"полный автосейв {n} инвентарей: {cycleMs:F0} мс " +
                        $"(из них запись на диск {flushSw.Elapsed.TotalMilliseconds:F0} мс)");
            Report.Note($"размер файла:                 {new FileInfo(path).Length / 1024} КБ");
            Report.Note("Запись на диск — ОДНА на весь автосейв, а не по одной на игрока.");
            Report.Note("Со старым поведением здесь было бы N полных перезаписей файла.");

            extra["inv_save_p99"] = saves.P99;
            extra["inv_full_cycle_ms"] = cycleMs;
        }
        finally
        {
            try { Directory.Delete(dir, true); } catch { /* временный каталог */ }
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
              --cell-size F       размер ячейки пространственной сетки, м (64)
              --no-auth           пропустить замер PBKDF2 (он самый долгий)
              --sweep             прогнать 250/500/1000/1500/2000/3000 и найти
                                  потолок одного инстанса по бюджету тика
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
