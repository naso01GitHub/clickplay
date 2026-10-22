using ClickPlay.App.Platform;

namespace ClickPlay.Tests.Fakes;

/// <summary>
/// Test keep-awake: records calls instead of touching the OS power settings.
/// </summary>
public sealed class FakeKeepAwake : IKeepAwake
{
    /// <summary>True between <see cref="Begin"/> and <see cref="End"/>.</summary>
    public bool IsActive { get; private set; }

    /// <summary>Number of <see cref="Begin"/> calls.</summary>
    public int BeginCount { get; private set; }

    /// <summary>Number of <see cref="End"/> calls.</summary>
    public int EndCount { get; private set; }

    /// <summary>
    /// Marks sleep as blocked.
    /// </summary>
    /// <returns>Always true.</returns>
    /// <example>
    /// <code>
    /// var keep = new FakeKeepAwake();
    /// keep.Begin();
    /// Assert.True(keep.IsActive);
    /// </code>
    /// </example>
    public bool Begin()
    {
        BeginCount++;
        IsActive = true;
        return true;
    }

    /// <summary>
    /// Marks sleep as allowed again.
    /// </summary>
    /// <returns>No return value.</returns>
    /// <example>
    /// <code>
    /// keep.End();
    /// Assert.False(keep.IsActive);
    /// </code>
    /// </example>
    public void End()
    {
        EndCount++;
        IsActive = false;
    }
}
