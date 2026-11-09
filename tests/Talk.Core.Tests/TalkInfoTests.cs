using Xunit;

namespace Talk.Core.Tests;

/// <summary>
/// Prüft die festen Angaben in <see cref="TalkInfo"/>.
/// </summary>
public sealed class TalkInfoTests
{
    [Fact]
    public void Name_ist_Talk()
    {
        Assert.Equal("Talk", TalkInfo.Name);
    }

    [Fact]
    public void Version_ist_nicht_leer()
    {
        Assert.False(string.IsNullOrWhiteSpace(TalkInfo.Version));
    }
}
