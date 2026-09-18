using FloVMP.Core.Chat;
using Xunit;

namespace FloVMP.Core.Tests;

/// <summary>
/// Ник задаёт клиент, а сервер вставляет его в системные строки чата и лога.
/// Проверка при входе не даёт через ник подделать системное сообщение или
/// строку лога.
/// </summary>
public sealed class PlayerNamePolicyTests
{
    [Theory]
    [InlineData("Ivan_Petrov")]
    [InlineData("Alex Mercer")]
    [InlineData("Иван_Петров")]
    [InlineData("xX_Sniper_Xx")]
    [InlineData("Ян")]
    [InlineData("Игрок")]
    public void Normal_names_are_allowed(string name)
    {
        Assert.True(PlayerNamePolicy.IsAllowed(name, out var reason), reason);
    }

    [Theory]
    [InlineData("Bob{ef4444}Сервер перезапускается")]   // подкраска системной строки
    [InlineData("[Администрация] Bob")]                 // подделка префикса
    [InlineData("Bob\n[C#] выданы права")]               // поддельная строка лога
    [InlineData("Bob‮evil")]                        // разворот текста
    [InlineData("Bo​b")]                            // невидимый символ
    [InlineData("<b>Bob</b>")]
    public void Spoofing_names_are_rejected(string name)
    {
        Assert.False(PlayerNamePolicy.IsAllowed(name, out var reason));
        Assert.False(string.IsNullOrEmpty(reason));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData("a")]
    [InlineData(" Bob")]
    [InlineData("Bob ")]
    public void Empty_short_or_padded_names_are_rejected(string? name)
    {
        Assert.False(PlayerNamePolicy.IsAllowed(name, out _));
    }

    [Fact]
    public void Too_long_name_is_rejected()
    {
        Assert.False(PlayerNamePolicy.IsAllowed(new string('a', PlayerNamePolicy.MaxLength + 1), out _));
        Assert.True(PlayerNamePolicy.IsAllowed(new string('a', PlayerNamePolicy.MaxLength), out _));
    }

    [Fact]
    public void Log_form_cannot_inject_a_line()
    {
        var logged = PlayerNamePolicy.ForLog("Bob\n[C#] [FloV:MP Admin] выданы права");
        Assert.DoesNotContain('\n', logged);
        Assert.DoesNotContain('\r', logged);
    }
}
