using FloVMP.Core.Combat;
using Xunit;

namespace FloVMP.Core.Tests;

/// <summary>
/// Посредник по урону между платформой и геймодом. Решение приходит в
/// следующем тике (рассылка событий между ресурсами alt:V асинхронная).
/// Главное, что здесь проверяется: молчание геймода ничего не меняет,
/// ответ успевает до закрытия, а опоздавший ответ не достаётся чужому
/// выстрелу.
/// </summary>
public class DamageArbiterTests
{
    private static SettledDamage Single(List<SettledDamage> settled)
    {
        Assert.Single(settled);
        return settled[0];
    }

    [Fact]
    public void Молчание_геймода_оставляет_урон_как_был()
    {
        var arbiter = new DamageArbiter();
        arbiter.Ask(1, 2, 0, 35, tick: 10);

        var s = Single(arbiter.Settle(11));
        Assert.Equal(35, s.Damage);
        Assert.False(s.Answered);
    }

    [Fact]
    public void Ответ_после_Emit_но_до_следующего_тика_применяется()
    {
        // Ровно тот порядок, что наблюдался на живом сервере: вопрос задан,
        // платформа вышла из тика, геймод ответил, следующий тик забирает.
        var arbiter = new DamageArbiter();
        var request = arbiter.Ask(1, 2, 0, 40, tick: 10);
        Assert.True(arbiter.Answer(request, allow: true, damage: 20));

        var s = Single(arbiter.Settle(11));
        Assert.Equal(20, s.Damage);
        Assert.True(s.Answered);
    }

    [Fact]
    public void Попадания_текущего_тика_не_закрываются()
    {
        var arbiter = new DamageArbiter();
        arbiter.Ask(1, 2, 0, 35, tick: 10);

        Assert.Empty(arbiter.Settle(10));
        Assert.Equal(1, arbiter.PendingCount);
        Assert.Single(arbiter.Settle(11));
    }

    [Fact]
    public void Запрет_обнуляет_урон()
    {
        var arbiter = new DamageArbiter();
        var request = arbiter.Ask(1, 2, 0, 35, tick: 1);
        arbiter.Answer(request, allow: false, damage: 999);

        Assert.Equal(0, Single(arbiter.Settle(2)).Damage);
    }

    [Fact]
    public void Урон_из_геймода_зажимается_в_допустимые_границы()
    {
        var arbiter = new DamageArbiter();
        var low = arbiter.Ask(1, 2, 0, 35, tick: 1);
        var high = arbiter.Ask(1, 3, 0, 35, tick: 1);
        arbiter.Answer(low, allow: true, damage: -50);
        arbiter.Answer(high, allow: true, damage: 10_000);

        var settled = arbiter.Settle(2);
        Assert.Equal(0, settled.Single(x => x.Hit.Request == low).Damage);
        Assert.Equal(DamageArbiter.MaxDamage, settled.Single(x => x.Hit.Request == high).Damage);
    }

    [Fact]
    public void Ответ_на_чужой_вопрос_игнорируется()
    {
        var arbiter = new DamageArbiter();
        var request = arbiter.Ask(1, 2, 0, 35, tick: 1);

        Assert.False(arbiter.Answer(request + 7, allow: false, damage: 0));
        Assert.Equal(35, Single(arbiter.Settle(2)).Damage);
    }

    [Fact]
    public void Опоздавший_ответ_не_достаётся_следующему_выстрелу()
    {
        var arbiter = new DamageArbiter();
        var first = arbiter.Ask(1, 2, 0, 35, tick: 1);
        arbiter.Settle(2);

        // Геймод ответил, когда вопрос уже закрыт.
        Assert.False(arbiter.Answer(first, allow: false, damage: 0));

        arbiter.Ask(1, 2, 0, 42, tick: 2);
        Assert.Equal(42, Single(arbiter.Settle(3)).Damage);
    }

    [Fact]
    public void Ответ_ноль_никогда_не_принимается()
    {
        var arbiter = new DamageArbiter();
        arbiter.Ask(1, 2, 0, 35, tick: 1);

        Assert.False(arbiter.Answer(0, allow: false, damage: 0));
    }

    [Fact]
    public void Несколько_попаданий_решаются_независимо_и_по_порядку()
    {
        var arbiter = new DamageArbiter();
        var a = arbiter.Ask(1, 2, 0, 30, tick: 5);
        var b = arbiter.Ask(3, 2, 0, 50, tick: 5);
        var c = arbiter.Ask(4, 5, 0, 10, tick: 5);
        arbiter.Answer(b, allow: false, damage: 0);
        arbiter.Answer(c, allow: true, damage: 99);

        var settled = arbiter.Settle(6);
        Assert.Equal(new[] { a, b, c }, settled.Select(x => x.Hit.Request));
        Assert.Equal(new[] { 30, 0, 99 }, settled.Select(x => x.Damage));
    }

    [Fact]
    public void Повторный_ответ_заменяет_предыдущий()
    {
        var arbiter = new DamageArbiter();
        var request = arbiter.Ask(1, 2, 0, 35, tick: 1);
        arbiter.Answer(request, allow: false, damage: 0);
        arbiter.Answer(request, allow: true, damage: 12);

        Assert.Equal(12, Single(arbiter.Settle(2)).Damage);
    }

    [Fact]
    public void Вышедший_игрок_забирает_свои_попадания_с_собой()
    {
        var arbiter = new DamageArbiter();
        arbiter.Ask(1, 2, 0, 30, tick: 1);   // 1 стреляет по 2
        arbiter.Ask(3, 1, 0, 30, tick: 1);   // 3 стреляет по 1
        var kept = arbiter.Ask(3, 4, 0, 30, tick: 1);

        arbiter.ForgetPlayer(1);

        var s = Single(arbiter.Settle(2));
        Assert.Equal(kept, s.Hit.Request);
    }

    [Fact]
    public void Переполненная_очередь_отдаёт_ноль_и_не_растёт()
    {
        var arbiter = new DamageArbiter();
        for (var i = 0; i < DamageArbiter.MaxPending; i++)
            Assert.NotEqual(0, arbiter.Ask(1, 2, 0, 1, tick: 1));

        Assert.Equal(0, arbiter.Ask(1, 2, 0, 1, tick: 1));
        Assert.Equal(DamageArbiter.MaxPending, arbiter.PendingCount);
    }

    [Fact]
    public void Платформа_не_может_предложить_урон_за_границами()
    {
        var arbiter = new DamageArbiter();
        arbiter.Ask(1, 2, 0, -10, tick: 1);
        arbiter.Ask(1, 3, 0, 5_000, tick: 1);

        var settled = arbiter.Settle(2);
        Assert.Equal(new[] { 0, DamageArbiter.MaxDamage }, settled.Select(x => x.Damage));
    }
}
