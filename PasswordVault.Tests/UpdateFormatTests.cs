using PasswordVault.Core.Services;

namespace PasswordVault.Tests;

public class UpdateFormatTests
{
    [Theory]
    [InlineData("v1.2.3", "1.2.3")]
    [InlineData("1.0.10", "1.0.10")]
    public void ParseTag_AcceptsSemver(string tag, string expected)
        => Assert.Equal(Version.Parse(expected), UpdateFormat.ParseTag(tag));

    [Theory]
    [InlineData("v1.2")]
    [InlineData("v1.2.3-beta")]
    [InlineData("v1.-2.3")]
    [InlineData("latest")]
    public void ParseTag_RejectsOtherFormats(string tag)
        => Assert.Null(UpdateFormat.ParseTag(tag));

    [Fact]
    public void ParseTag_ComparesNumerically()
        => Assert.True(UpdateFormat.ParseTag("v1.0.10") > UpdateFormat.ParseTag("v1.0.9"));

    [Fact]
    public void ParseSha256_ReadsHashBeforeFileName()
    {
        var hash = new string('a', 64);
        Assert.Equal(hash, UpdateFormat.ParseSha256($"{hash}  PasswordVault-win-x64.zip\r\n"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("abc  datei.zip")]
    [InlineData("zzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzz")]
    public void ParseSha256_RejectsInvalid(string content)
        => Assert.Throws<InvalidDataException>(() => UpdateFormat.ParseSha256(content));
}
