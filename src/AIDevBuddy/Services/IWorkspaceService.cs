namespace AIDevBuddy.Services;

public interface IWorkspaceService
{
    /// <summary>Configured base path for all project workspaces.</summary>
    string WorkspaceBasePath { get; }

    /// <summary>Returns the full path for a project's workspace folder (does not create it).</summary>
    string GetProjectFolder(string projectName, int projectId);

    /// <summary>Creates the project workspace folder and sub-directories if they don't exist.</summary>
    Task EnsureProjectFolderAsync(string projectName, int projectId);

    /// <summary>Writes an artifact to &lt;project-folder&gt;/artifacts/&lt;name&gt;.md</summary>
    Task WriteArtifactAsync(string projectName, int projectId, string artifactName, string content);

    /// <summary>Updates the configured base workspace path and persists it.</summary>
    void SetWorkspaceBasePath(string path);
}
