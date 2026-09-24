namespace FloVMP.Core.AntiCheat;

/// <summary>Одна запись журнала подозрений.</summary>
public readonly record struct Suspicion(long AtMs, string Type, float Weight, string Details);

/// <summary>Порог, который пересёк счёт игрока.</summary>
public enum SuspicionLevel { None = 0, Notify = 1, Kick = 2 }

/// <summary>
/// Журнал подозрений с весами (вторая линия античита, пункт 7 roadmap).
///
/// Раньше каждая проверка решала сама: сработала — лог или откат. Единичный
/// лаг и настоящий чит выглядели одинаково, а мелкие нарушения, которые
/// вместе явно говорят о чите, по отдельности ничего не значили. Здесь
/// каждое срабатывание добавляет вес к счёту игрока, счёт со временем тает,
/// а решение принимается по порогам: у честного игрока с плохим интернетом
/// счёт не копится, у читера — растёт быстрее, чем тает.
///
/// Веса и пороги задаёт владелец (anticheat.* в client.cfg или из кода);
/// геймод может добавлять свои подозрения (дюп денег, телепорт в зону).
/// Только главный поток.
/// </summary>
public sealed class SuspicionLedger
{
    public const int HistoryLimit = 20;

    private sealed class Entry
    {
        public float Score;
        public long ScoreAtMs;
        public SuspicionLevel Reached;
        public readonly Queue<Suspicion> History = new();
    }

    private readonly Dictionary<uint, Entry> _players = new();

    /// <summary>Сколько очков тает за минуту (anticheat.decay_per_minute).</summary>
    public float DecayPerMinute { get; set; } = 5f;
    /// <summary>Счёт, при котором предупреждаются администраторы; 0 — никогда.</summary>
    public float NotifyScore { get; set; } = 50f;
    /// <summary>Счёт, при котором игрока отключает; 0 — никогда (по умолчанию).</summary>
    public float KickScore { get; set; }

    /// <summary>
    /// Добавить подозрение. Возвращает новый счёт и порог, пересечённый
    /// именно сейчас (None — порог не пересечён или уже был). Повторное
    /// пересечение того же порога не сообщается, пока счёт не растает ниже.
    /// </summary>
    public (float Score, SuspicionLevel Crossed) Add(uint playerId, string type, float weight, string details, long nowMs)
    {
        var e = Get(playerId, nowMs);
        e.Score = Math.Max(0f, e.Score + Math.Max(0f, weight));
        e.History.Enqueue(new Suspicion(nowMs, type, weight, details));
        while (e.History.Count > HistoryLimit) e.History.Dequeue();

        var level = LevelOf(e.Score);
        var crossed = level > e.Reached ? level : SuspicionLevel.None;
        e.Reached = level;
        return (e.Score, crossed);
    }

    /// <summary>Текущий счёт с учётом затухания.</summary>
    public float Score(uint playerId, long nowMs) =>
        _players.ContainsKey(playerId) ? Get(playerId, nowMs).Score : 0f;

    public IReadOnlyList<Suspicion> History(uint playerId) =>
        _players.TryGetValue(playerId, out var e) ? e.History.ToList() : Array.Empty<Suspicion>();

    /// <summary>Простить (администратор, геймод): счёт и история обнуляются.</summary>
    public void Forgive(uint playerId) => _players.Remove(playerId);

    public void Remove(uint playerId) => _players.Remove(playerId);

    private Entry Get(uint playerId, long nowMs)
    {
        if (!_players.TryGetValue(playerId, out var e))
            _players[playerId] = e = new Entry { ScoreAtMs = nowMs };
        if (nowMs > e.ScoreAtMs)
        {
            e.Score = Math.Max(0f, e.Score - DecayPerMinute * (nowMs - e.ScoreAtMs) / 60_000f);
            e.ScoreAtMs = nowMs;
            // Растаял ниже порога — следующее пересечение снова будет событием.
            var level = LevelOf(e.Score);
            if (level < e.Reached) e.Reached = level;
        }
        return e;
    }

    private SuspicionLevel LevelOf(float score) =>
        KickScore > 0 && score >= KickScore ? SuspicionLevel.Kick :
        NotifyScore > 0 && score >= NotifyScore ? SuspicionLevel.Notify :
        SuspicionLevel.None;
}
