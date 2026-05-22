using AutoFixture;
using FillInTheTextBot.Services.Extensions;
using NUnit.Framework;

namespace FillInTheTextBot.Services.Tests.Extensions;

[TestFixture]
public class StringExtensionsTests
{
    private readonly Fixture _fixture = new();

    [Test]
    public void Sanitize_Null_Null()
    {
        string expected = null;


        // ReSharper disable once ExpressionIsAlwaysNull
        var result = expected.Sanitize();


        Assert.That(result, Is.Null);
    }

    [Test]
    public void Sanitize_Empty_Empty()
    {
        var expected = string.Empty;


        var result = expected.Sanitize();


        Assert.That(string.IsNullOrEmpty(result), Is.True);
    }

    [Test]
    public void Sanitize_AnyString_Same()
    {
        var expected = _fixture.Create<string>();


        var result = expected.Sanitize();


        Assert.That(result, Is.EqualTo(expected));
    }

    [Test]
    public void Sanitize_QuotesAtAnswer_Success()
    {
        var tested = "This text is with &quot;quotes&quot;";


        var result = tested.Sanitize();


        var expected = "This text is with \"quotes\"";
        Assert.That(result, Is.EqualTo(expected));
    }
}