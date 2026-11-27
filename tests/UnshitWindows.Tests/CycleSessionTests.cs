using UnshitWindows.Switching;

namespace UnshitWindows.Tests;

public class CycleSessionTests
{
    private static readonly nint[] Windows = [10, 20, 30];

    [Fact]
    public void Forward_StartsAtSecondWindowAndWraps()
    {
        var session = new CycleSession(Windows);
        Assert.Equal(20, session.Step(forward: true));
        Assert.Equal(30, session.Step(forward: true));
        Assert.Equal(10, session.Step(forward: true));
        Assert.Equal(20, session.Step(forward: true));
    }

    [Fact]
    public void Backward_StartsAtLastWindow()
    {
        var session = new CycleSession(Windows);
        Assert.Equal(30, session.Step(forward: false));
        Assert.Equal(20, session.Step(forward: false));
        Assert.Equal(10, session.Step(forward: false));
    }

    [Fact]
    public void MixingDirections_WalksBack()
    {
        var session = new CycleSession(Windows);
        session.Step(forward: true);
        session.Step(forward: true);
        Assert.Equal(20, session.Step(forward: false));
    }

    [Fact]
    public void TwoWindows_Toggle()
    {
        var session = new CycleSession([1, 2]);
        Assert.Equal(2, session.Step(forward: true));
        Assert.Equal(1, session.Step(forward: true));
    }

    [Fact]
    public void SkipsWindowsThatAreGone()
    {
        var session = new CycleSession(Windows);
        Assert.Equal(30, session.Step(forward: true, isAlive: h => h != 20));
    }

    [Fact]
    public void ReturnsZeroWhenNoOtherWindowIsAlive()
    {
        var session = new CycleSession(Windows);
        Assert.Equal(0, session.Step(forward: true, isAlive: h => h == 10));
    }

    [Fact]
    public void RequiresAtLeastOneWindow() =>
        Assert.Throws<ArgumentException>(() => new CycleSession([]));
}
