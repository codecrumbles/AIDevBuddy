namespace AIDevBuddy.Services;

public interface IAutoLoopService
{
    /// <summary>Fires on the thread pool whenever a project loop produces output. Blazor components must InvokeAsync.</summary>
    event Action<int> ProjectUpdated;

    Task StartLoopAsync(int projectId);
    Task PauseLoopAsync(int projectId);
    Task ResumeLoopAsync(int projectId);
    Task StopLoopAsync(int projectId);

    bool IsRunning(int projectId);
    string GetLoopStatus(int projectId);
}
