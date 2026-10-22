using System.Diagnostics;
using System.Runtime.InteropServices;

namespace ClickPlay.App.Platform;

/// <summary>
/// Blocks idle system sleep while Search runs, but lets the display turn off.
/// Windows: <c>SetThreadExecutionState(ES_CONTINUOUS | ES_SYSTEM_REQUIRED)</c> (no ES_DISPLAY_REQUIRED).
/// Mac: runs <c>caffeinate -i -w &lt;this process&gt;</c>, which ends by itself if ClickPlay exits.
/// </summary>
/// <remarks>
/// Lives in the App project, not ClickPlay.Core: .NET has no cross-platform API for this, and the
/// Windows call is Win32 interop, which AGENTS.md keeps out of shared code. Every OS call is guarded
/// by <see cref="OperatingSystem"/> checks. It cannot stop sleep the user asks for (Start → Sleep,
/// closing a laptop lid), only sleep after idle time.
/// Windows ties the request to the calling thread, so <see cref="Begin"/> and <see cref="End"/>
/// are called on the UI thread, which lives as long as the app.
/// </remarks>
public sealed class SystemKeepAwake : IKeepAwake, IDisposable
{
    private const uint EsContinuous = 0x80000000;
    private const uint EsSystemRequired = 0x00000001;

    private Process? _caffeinate;
    private bool _active;

    /// <summary>
    /// Blocks idle sleep on Windows or Mac.
    /// </summary>
    /// <returns>True when sleep is blocked; false on other platforms or when the OS call failed.</returns>
    /// <example>
    /// <code>
    /// using var keepAwake = new SystemKeepAwake();
    /// keepAwake.Begin();
    /// </code>
    /// </example>
    public bool Begin()
    {
        if (_active)
        {
            return true;
        }

        if (OperatingSystem.IsWindows())
        {
            _active = SetThreadExecutionState(EsContinuous | EsSystemRequired) != 0;
        }
        else if (OperatingSystem.IsMacOS())
        {
            _active = StartCaffeinate();
        }

        return _active;
    }

    /// <summary>
    /// Allows sleep again: clears the Windows request or stops <c>caffeinate</c>.
    /// </summary>
    /// <returns>No return value.</returns>
    /// <example>
    /// <code>
    /// keepAwake.End();
    /// </code>
    /// </example>
    public void End()
    {
        if (!_active)
        {
            return;
        }

        if (OperatingSystem.IsWindows())
        {
            SetThreadExecutionState(EsContinuous);
        }

        StopCaffeinate();
        _active = false;
    }

    /// <summary>
    /// Same as <see cref="End"/>; called when the app exits.
    /// </summary>
    /// <returns>No return value.</returns>
    /// <example>
    /// <code>
    /// keepAwake.Dispose();
    /// </code>
    /// </example>
    public void Dispose() => End();

    /// <summary>
    /// Starts <c>/usr/bin/caffeinate -i -w &lt;pid&gt;</c>: <c>-i</c> blocks idle system sleep only
    /// (the display may sleep), <c>-w</c> stops it when ClickPlay exits, even after a crash.
    /// </summary>
    /// <returns>True when caffeinate started.</returns>
    /// <example>
    /// <code>
    /// var ok = StartCaffeinate();
    /// </code>
    /// </example>
    private bool StartCaffeinate()
    {
        try
        {
            _caffeinate = Process.Start(new ProcessStartInfo
            {
                FileName = "/usr/bin/caffeinate",
                ArgumentList = { "-i", "-w", Environment.ProcessId.ToString(System.Globalization.CultureInfo.InvariantCulture) },
                UseShellExecute = false,
                CreateNoWindow = true
            });
            return _caffeinate is not null;
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            _caffeinate = null;
            return false;
        }
    }

    /// <summary>
    /// Stops the <c>caffeinate</c> process if one is running.
    /// </summary>
    /// <returns>No return value.</returns>
    /// <example>
    /// <code>
    /// StopCaffeinate();
    /// </code>
    /// </example>
    private void StopCaffeinate()
    {
        if (_caffeinate is null)
        {
            return;
        }

        try
        {
            if (!_caffeinate.HasExited)
            {
                _caffeinate.Kill();
            }
        }
        catch (InvalidOperationException)
        {
        }
        finally
        {
            _caffeinate.Dispose();
            _caffeinate = null;
        }
    }

    /// <summary>
    /// Win32: tells Windows the system is in use so it does not sleep after idle time.
    /// </summary>
    /// <param name="flags">ES_* flags; ES_CONTINUOUS keeps the state until changed.</param>
    /// <returns>The previous state, or 0 on failure.</returns>
    /// <example>
    /// <code>
    /// SetThreadExecutionState(0x80000000 | 0x00000001); // keep the system (not the display) awake
    /// </code>
    /// </example>
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern uint SetThreadExecutionState(uint flags);
}
