using CatCommander.Utils;
using Xunit;

namespace CatCommander.Tests.Utils;

public class QuickFilterTests
{
    [Theory]
    [InlineData("", "anything", true)]
    [InlineData("   ", "anything", true)]
    [InlineData("aa bb", "aaccbb", true)]
    [InlineData("aa bb", "aacc", false)]
    [InlineData("aa bb", "bbcc", false)]
    [InlineData("AA", "aaccbb", true)]
    [InlineData("zz", "aaccbb", false)]
    [InlineData("bb aa", "aaccbb", true)] // token order doesn't matter
    public void Matches_AndsSpaceSeparatedTokensCaseInsensitively(string filterText, string name, bool expected)
    {
        Assert.Equal(expected, QuickFilter.Matches(filterText, name));
    }
}
