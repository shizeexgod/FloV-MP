using FloVMP.Core.Combat;
using Xunit;

namespace FloVMP.Core.Tests;

/// <summary>
/// Посредник по урону между платформой и геймодом. Главное, что здесь
/// проверяется: молчание геймода ничего не меняет, а опоздавший ответ не
/// достаётся чужому выстрелу.
/// </summary>
public class DamageArbiterTests
{
    [Fact]
    public void Молчание_геймода_оставляет_урон_как_был()
    {
        var arbiter = new DamageArbiter();
        var request = arbiter.Ask();

        Assert.Equal(35, arbiter.Resolve(request, 35));
    }

    [Fact]
    public void Запрет_обнуляет_урон()
    {
        var arbiter = new DamageArbiter();
        var request = arbiter.Ask();

        Assert.True(arbiter.Answer(request, allow: false, damage: 999));
        Assert.Equal(0, arbiter.Resolve(request, 35));
    }

    [Fact]
    public void Геймод_может_уменьшить_урон()
    {
        var arbiter = new DamageArbiter();
        var request = arbiter.Ask();

        arbiter.Answer(request, allow: true, damage: 17);
        Assert.Equal(17, arbiter.Resolve(request, 35));
    }

    [Fact]
    public void Урон_из_геймода_зажимается_в_допустимые_границы()
    {
        var arbiter = new DamageArbiter();

        var first = arbiter.Ask();
        arbiter.Answer(first, allow: true, damage: -50);
        Assert.Equal(0, arbiter.Resolve(first, 35));

        var second = arbiter.Ask();
        arbiter.Answer(second, allow: true, damage: 10_000);
        Assert.Equal(DamageArbiter.MaxDamage, arbiter.Resolve(second, 35));
    }

    [Fact]
    public void Ответ_на_чужой_вопрос_игнорируется()
    {
        var arbiter = new DamageArbiter();
        var request = arbiter.Ask();

        Assert.False(arbiter.Answer(request + 7, allow: false, damage: 0));
        Assert.Equal(35, arbiter.Resolve(request, 35));
    }

    [Fact]
    public void Опоздавший_ответ_не_достаётся_следующему_выстрелу()
    {
        var arbiter = new DamageArbiter();
        var first = arbiter.Ask();
        arbiter.Resolve(first, 35);

        // Геймод ответил, когда вопрос уже закрыт.
        Assert.False(arbiter.Answer(first, allow: false, damage: 0));

        var second = arbiter.Ask();
        Assert.Equal(42, arbiter.Resolve(second, 42));
    }

    [Fact]
    public void Ответ_ноль_никогда_не_принимается()
    {
        var arbiter = new DamageArbiter();
        arbiter.Ask();

        Assert.False(arbiter.Answer(0, allow: false, damage: 0));
    }

    [Fact]
    public void Вложенный_вопрос_отменяет_предыдущий()
    {
        var arbiter = new DamageArbiter();
        var outer = arbiter.Ask();
        var inner = arbiter.Ask();   // геймод в обработчике нанёс урон кому-то ещё

        arbiter.Answer(inner, allow: false, damage: 0);
        Assert.Equal(0, arbiter.Resolve(inner, 35));

        // Брошенный вопрос закрывается тем, что предложила платформа.
        Assert.Equal(35, arbiter.Resolve(outer, 35));
    }

    [Fact]
    public void Номера_вопросов_не_повторяются_подряд()
    {
        var arbiter = new DamageArbiter();
        var first = arbiter.Ask();
        var second = arbiter.Ask();

        Assert.NotEqual(first, second);
        Assert.Equal(2, arbiter.AskedTotal);
    }

    [Fact]
    public void Платформа_не_может_предложить_урон_за_границами()
    {
        var arbiter = new DamageArbiter();

        var first = arbiter.Ask();
        Assert.Equal(0, arbiter.Resolve(first, -10));

        var second = arbiter.Ask();
        Assert.Equal(DamageArbiter.MaxDamage, arbiter.Resolve(second, 5_000));
    }
}
