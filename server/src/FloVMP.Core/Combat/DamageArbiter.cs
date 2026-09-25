namespace FloVMP.Core.Combat;

/// <summary>Что геймод решил делать с попаданием.</summary>
/// <param name="Allow">false — попадание не засчитывается вовсе.</param>
/// <param name="Damage">Урон, если попадание разрешено.</param>
public readonly record struct DamageVerdict(bool Allow, int Damage);

/// <summary>Попадание, по которому платформа ждёт решения геймода.</summary>
/// <param name="Request">Номер вопроса, он уходит геймоду и возвращается в ответе.</param>
/// <param name="AttackerId">Кто стрелял.</param>
/// <param name="VictimId">По кому попали.</param>
/// <param name="Weapon">Хэш оружия.</param>
/// <param name="Proposed">Урон, который посчитала платформа.</param>
/// <param name="AskedTick">Номер тика, в котором задан вопрос.</param>
public readonly record struct PendingDamage(int Request, uint AttackerId, uint VictimId, uint Weapon,
                                            int Proposed, long AskedTick);

/// <summary>Попадание с окончательным уроном. Damage 0 — попадание отменено.</summary>
public readonly record struct SettledDamage(PendingDamage Hit, int Damage, bool Answered);

/// <summary>
/// Посредник между платформой и геймодом по каждому попаданию.
///
/// Зачем он нужен. Здоровье и броню считает сервер платформы, и геймод должен
/// иметь право вмешаться: отменить выстрел или изменить урон. Без этого не
/// сделать ни брони фракций, ни режимов «без оружия», ни дуэлей на
/// половинном уроне.
///
/// Почему решение приходит в следующем тике. Рассылка событий между
/// ресурсами в alt:V асинхронная: Alt.Emit ставит событие в очередь, и
/// обработчик геймода выполняется уже после того, как платформа вернулась из
/// своего тика. Проверено живым запуском 25.09.2026: вопрос, закрытый в том
/// же вызове, всегда закрывался без ответа, а ответ геймода приходил следом и
/// отбрасывался. Поэтому попадание сначала становится «ожидающим»
/// (<see cref="Ask"/>), геймод отвечает (<see cref="Answer"/>), а платформа
/// забирает решения в начале следующего тика (<see cref="Settle"/>).
/// Задержка — один тик сервера, для урона она незаметна.
///
/// Молчание геймода ничего не меняет: попадание применяется с уроном
/// платформы. Ответ с чужим или уже закрытым номером игнорируется — иначе
/// опоздавший ответ достался бы другому выстрелу.
///
/// Класс рассчитан на один поток: и попадания, и события приходят в главном
/// потоке сервера.
/// </summary>
public sealed class DamageArbiter
{
    /// <summary>Потолок урона: столько же, сколько максимум здоровья в GTA.</summary>
    public const int MaxDamage = 200;

    /// <summary>
    /// Сколько попаданий может ждать решения одновременно. Защита памяти на
    /// случай, если <see cref="Settle"/> по ошибке перестанут вызывать: лишние
    /// попадания применяются сразу, без вопроса.
    /// </summary>
    public const int MaxPending = 4096;

    private readonly Dictionary<int, PendingDamage> _pending = new();
    private readonly Dictionary<int, DamageVerdict> _answers = new();
    private readonly List<int> _order = new();
    private int _lastRequest;

    /// <summary>Сколько вопросов задано с запуска сервера. Для диагностики.</summary>
    public int AskedTotal { get; private set; }

    /// <summary>Сколько попаданий сейчас ждут решения.</summary>
    public int PendingCount => _pending.Count;

    /// <summary>
    /// Поставить попадание в ожидание. Возвращает номер вопроса или 0, если
    /// очередь переполнена — тогда попадание нужно применить сразу.
    /// </summary>
    public int Ask(uint attackerId, uint victimId, uint weapon, int proposed, long tick)
    {
        if (_pending.Count >= MaxPending) return 0;
        // Номер никогда не равен нулю: ноль означает «вопроса нет».
        _lastRequest = _lastRequest == int.MaxValue ? 1 : _lastRequest + 1;
        var request = _lastRequest;
        _pending[request] = new PendingDamage(request, attackerId, victimId, weapon,
                                              Math.Clamp(proposed, 0, MaxDamage), tick);
        _order.Add(request);
        AskedTotal++;
        return request;
    }

    /// <summary>
    /// Ответ геймода. Возвращает false, если такого открытого вопроса нет —
    /// такой ответ ни на что не влияет. Повторный ответ на тот же вопрос
    /// заменяет предыдущий.
    /// </summary>
    public bool Answer(int request, bool allow, int damage)
    {
        if (request == 0 || !_pending.ContainsKey(request)) return false;
        _answers[request] = new DamageVerdict(allow, Math.Clamp(damage, 0, MaxDamage));
        return true;
    }

    /// <summary>
    /// Забрать решения по всем попаданиям, заданным раньше тика
    /// <paramref name="currentTick"/>. Попадания текущего тика остаются ждать:
    /// геймод их ещё не видел.
    /// </summary>
    public List<SettledDamage> Settle(long currentTick)
    {
        var settled = new List<SettledDamage>();
        var keep = 0;
        for (var i = 0; i < _order.Count; i++)
        {
            var request = _order[i];
            if (!_pending.TryGetValue(request, out var hit)) continue;
            if (hit.AskedTick >= currentTick)
            {
                _order[keep++] = request;
                continue;
            }
            _pending.Remove(request);
            if (_answers.Remove(request, out var verdict))
                settled.Add(new SettledDamage(hit, verdict.Allow ? verdict.Damage : 0, true));
            else
                settled.Add(new SettledDamage(hit, hit.Proposed, false));
        }
        _order.RemoveRange(keep, _order.Count - keep);
        return settled;
    }

    /// <summary>
    /// Забыть все ожидающие попадания игрока — он вышел. Попадания по нему и
    /// от него больше не применяются.
    /// </summary>
    public void ForgetPlayer(uint playerId)
    {
        foreach (var request in _pending.Where(p => p.Value.AttackerId == playerId || p.Value.VictimId == playerId)
                                        .Select(p => p.Key).ToList())
        {
            _pending.Remove(request);
            _answers.Remove(request);
        }
        _order.RemoveAll(r => !_pending.ContainsKey(r));
    }
}
