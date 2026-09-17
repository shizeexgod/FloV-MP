using FloVMP.Core.Chat;
using Xunit;

namespace FloVMP.Core.Tests;

/// <summary>
/// Подделка системных сообщений через чат.
///
/// Цвет в чате — привилегия системы. Без очистки игрок мог написать
///   /do {ef4444}[FloV:MP Security] Сервер перезапускается, выйдите из игры
/// и получить красную строку, неотличимую от предупреждения администрации:
/// /do уходит с пустым автором, а /w — вообще системным стилем.
///
/// Невидимые символы нулевой ширины и переопределения направления текста
/// char.IsControl не ловит, поэтому они проходили и позволяли подменить вид
/// ника, развернуть текст и обойти фильтры слов.
/// </summary>
public sealed class ChatSpoofingTests
{
    [Fact]
    public void StripColorCodes_RemovesSystemColorFromPlayerText()
    {
        var spoof = "{ef4444}[FloV:MP Security] Сервер перезапускается";
        Assert.Equal("[FloV:MP Security] Сервер перезапускается",
                     ChatSanitizer.StripColorCodes(spoof));
    }

    [Fact]
    public void StripColorCodes_RemovesEveryCode_NotOnlyTheFirst()
    {
        Assert.Equal("красный зелёный",
                     ChatSanitizer.StripColorCodes("{ff0000}красный {00ff00}зелёный"));
    }

    [Theory]
    [InlineData("{FFFFFF}")]
    [InlineData("{abcdef}")]
    [InlineData("{0A0b0C}")]
    public void StripColorCodes_IsCaseInsensitive(string code)
    {
        Assert.Equal("текст", ChatSanitizer.StripColorCodes(code + "текст"));
    }

    [Theory]
    // Не цветовой код — оставляем как есть: фигурные скобки бывают в обычной речи.
    [InlineData("{ff00}", "{ff00}")]
    [InlineData("{gggggg}", "{gggggg}")]
    [InlineData("{привет}", "{привет}")]
    [InlineData("код {1234567}", "код {1234567}")]
    public void StripColorCodes_LeavesOrdinaryBracesAlone(string input, string expected)
    {
        Assert.Equal(expected, ChatSanitizer.StripColorCodes(input));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void StripColorCodes_HandlesEmpty(string? input)
    {
        Assert.Equal(string.Empty, ChatSanitizer.StripColorCodes(input));
    }

    [Fact]
    public void CleanPlayerText_OnlyColorCode_IsEmpty()
    {
        // Сообщение из одного цветового кода после очистки пустое —
        // пустую строку в чат не шлём.
        Assert.Null(ChatSanitizer.CleanPlayerText("{ff0000}"));
        Assert.Null(ChatSanitizer.CleanPlayerText("  {ff0000}   "));
    }

    [Fact]
    public void CleanPlayerText_CombinesControlAndColorStripping()
    {
        Assert.Equal("строка одинстрока два",
                     ChatSanitizer.CleanPlayerText("{ff0000}строка один\nстрока два"));
    }

    [Theory]
    [InlineData('\u200B')] // zero width space
    [InlineData('\u200C')] // zero width non-joiner
    [InlineData('\u200D')] // zero width joiner
    [InlineData('\u200E')] // left-to-right mark
    [InlineData('\u200F')] // right-to-left mark
    [InlineData('\u202E')] // right-to-left override — разворачивает текст
    [InlineData('\u2066')] // left-to-right isolate
    [InlineData('\u2069')] // pop directional isolate
    [InlineData('\uFEFF')] // byte order mark
    public void Clean_RemovesInvisibleFormatting(char invisible)
    {
        // «admin» с невидимым символом внутри выглядит как «admin», но фильтр
        // запрещённых слов его не узнаёт.
        var sneaky = "ad" + invisible + "min";
        Assert.Equal("admin", ChatSanitizer.Clean(sneaky));
    }

    [Fact]
    public void Clean_RightToLeftOverride_CannotReverseText()
    {
        Assert.Equal("normal", ChatSanitizer.Clean("\u202Enormal"));
    }

    [Theory]
    [InlineData('a')]
    [InlineData('я')]
    [InlineData(' ')]
    [InlineData('€')]
    public void IsInvisibleFormatting_DoesNotTouchVisibleText(char visible)
    {
        Assert.False(ChatSanitizer.IsInvisibleFormatting(visible));
    }

    [Fact]
    public void Clean_KeepsEmojiAndCyrillic()
    {
        // Защита не должна ломать обычную речь игроков.
        Assert.Equal("привет 😀 мир", ChatSanitizer.Clean("привет 😀 мир"));
    }
}
