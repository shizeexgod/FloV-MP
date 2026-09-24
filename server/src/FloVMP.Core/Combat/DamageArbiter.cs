namespace FloVMP.Core.Combat;

/// <summary>Что геймод решил делать с попаданием.</summary>
/// <param name="Allow">false — попадание не засчитывается вовсе.</param>
/// <param name="Damage">Урон, если попадание разрешено.</param>
public readonly record struct DamageVerdict(bool Allow, int Damage);

/// <summary>
/// Посредник между платформой и геймодом по каждому попаданию.
///
/// Зачем он нужен. Здоровье и броню считает сервер платформы, и до сих пор
/// геймод никак не мог вмешаться: ни отменить выстрел, ни уменьшить урон.
/// Без этого не сделать ни брони фракций, ни режимов «без оружия», ни дуэлей
/// на половинном уроне — то есть почти всего, ради чего платформу покупают.
///
/// Как это работает. Платформа открывает вопрос (<see cref="Ask"/>), шлёт
/// геймоду событие с этим номером и тут же закрывает вопрос
/// (<see cref="Resolve"/>). Рассылка событий внутри процесса синхронная,
/// поэтому геймод успевает ответить <see cref="Answer"/> до закрытия.
///
/// Почему с номером, а не просто «последний ответ». Ответ с чужим или
/// устаревшим номером игнорируется. Иначе геймод, ответивший с опозданием на
/// один выстрел, молча изменил бы урон следующего — и искали бы это долго.
///
/// Класс рассчитан на один поток: и попадания, и события платформы приходят
/// в главном потоке сервера.
/// </summary>
public sealed class DamageArbiter
{
    /// <summary>Потолок урона: столько же, сколько максимум здоровья в GTA.</summary>
    public const int MaxDamage = 200;

    private int _lastRequest;
    private int _openRequest;
    private bool _answered;
    private DamageVerdict _answer;

    /// <summary>Сколько вопросов задано с запуска сервера. Для диагностики.</summary>
    public int AskedTotal => _lastRequest;

    /// <summary>
    /// Открыть вопрос по очередному попаданию. Номер уходит геймоду и должен
    /// вернуться в <see cref="Answer"/>.
    ///
    /// Если предыдущий вопрос остался незакрытым (геймод в своём обработчике
    /// нанёс урон кому-то ещё), прежний вопрос считается брошенным: ответить
    /// на него уже нельзя, и урон по нему применится как предложено.
    /// </summary>
    public int Ask()
    {
        // Номер никогда не равен нулю: ноль означает «вопроса нет».
        _lastRequest = _lastRequest == int.MaxValue ? 1 : _lastRequest + 1;
        _openRequest = _lastRequest;
        _answered = false;
        _answer = default;
        return _openRequest;
    }

    /// <summary>
    /// Ответ геймода. Возвращает false, если ответ пришёл не на тот вопрос —
    /// такой ответ не влияет ни на что.
    /// </summary>
    public bool Answer(int request, bool allow, int damage)
    {
        if (request == 0 || request != _openRequest) return false;
        _answered = true;
        _answer = new DamageVerdict(allow, Math.Clamp(damage, 0, MaxDamage));
        return true;
    }

    /// <summary>
    /// Закрыть вопрос и получить итоговый урон.
    ///
    /// Геймод не ответил — урон остаётся тем, что предложила платформа: по
    /// умолчанию сервер ведёт себя ровно как раньше.
    /// </summary>
    public int Resolve(int request, int proposed)
    {
        var answered = _answered && request != 0 && request == _openRequest;
        var verdict = _answer;
        _openRequest = 0;
        _answered = false;
        _answer = default;

        if (!answered) return Math.Clamp(proposed, 0, MaxDamage);
        return verdict.Allow ? verdict.Damage : 0;
    }
}
