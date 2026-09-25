using FloVMP.Core.Resources;
using Xunit;

namespace FloVMP.Core.Tests;

public class ExportSignatureTests
{
    [Fact]
    public void Signature_DescribesArgumentsAndResult()
    {
        Assert.Equal("System.Int32,System.Int64->System.Threading.Tasks.Task`1[[System.Boolean, System.Private.CoreLib, Version=10.0.0.0, Culture=neutral, PublicKeyToken=7cec85d7bea7798e]]",
            ExportSignature.Of(typeof(Func<int, long, Task<bool>>)));
        Assert.Equal("System.String[]->void", ExportSignature.Of(typeof(Action<string[]>)));
        Assert.Equal("->System.Int32", ExportSignature.Of(typeof(Func<int>)));
    }

    [Fact]
    public void Signature_DiffersWhenTypesDiffer()
    {
        // Ровно случай живого прогона: строковый экспорт, вызванный как число.
        Assert.NotEqual(ExportSignature.Of(typeof(Func<string>)), ExportSignature.Of(typeof(Func<int>)));
        Assert.NotEqual(ExportSignature.Of(typeof(Func<int, int>)), ExportSignature.Of(typeof(Func<long, int>)));
    }

    [Fact]
    public void Signature_RefusesTypesThatDoNotCrossResources()
    {
        Assert.Throws<NotSupportedException>(() => ExportSignature.Of(typeof(Func<Dictionary<string, int>>)));
        Assert.Throws<NotSupportedException>(() => ExportSignature.Of(typeof(Action<object>)));
        Assert.Throws<NotSupportedException>(() => ExportSignature.Of(typeof(Func<int[,]>)));
    }

    [Theory]
    [InlineData("balance", true)]
    [InlineData("bank.withdraw-v2", true)]
    [InlineData("", false)]
    [InlineData("с кириллицей", false)]
    [InlineData("a#b", false)]
    public void Names(string name, bool ok) => Assert.Equal(ok, ExportSignature.ValidName(name));

    [Fact]
    public void Describe_NamesTheProblem()
    {
        Assert.Contains("не экспортирует", ExportSignature.Describe("bank", "balance", "x", null));
        Assert.Contains("подпись", ExportSignature.Describe("bank", "balance", "x", "y"));
    }
}
