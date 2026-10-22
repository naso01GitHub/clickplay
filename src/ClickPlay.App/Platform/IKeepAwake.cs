namespace ClickPlay.App.Platform;

/// <summary>
/// Keeps the computer from going to sleep while Search runs. The display may still turn off.
/// </summary>
public interface IKeepAwake
{
    /// <summary>
    /// Starts blocking idle system sleep. Calling it again while active does nothing.
    /// </summary>
    /// <returns>True when sleep is now blocked; false when this platform cannot block it.</returns>
    /// <example>
    /// <code>
    /// keepAwake.Begin();
    /// </code>
    /// </example>
    bool Begin();

    /// <summary>
    /// Lets the computer sleep normally again. Calling it when not active does nothing.
    /// </summary>
    /// <returns>No return value.</returns>
    /// <example>
    /// <code>
    /// keepAwake.End();
    /// </code>
    /// </example>
    void End();
}
