using System.Text.RegularExpressions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace AIDevBuddy.Services;

/// <summary>
/// Manages on-disk project workspaces.
/// Default base path: ~/Documents/AIDevBuddy/Projects
/// Each project gets: &lt;base&gt;/&lt;slug&gt;-&lt;id&gt;/artifacts/
/// </summary>
public sealed class WorkspaceService : IWorkspaceService
{
    private readonly ILogger<WorkspaceService> _logger;
    private string _basePath;

    public WorkspaceService(IConfiguration configuration, ILogger<WorkspaceService> logger)
    {
        _logger = logger;

        var configured = configuration["Workspace:BasePath"];
        _basePath = string.IsNullOrWhiteSpace(configured)
            ? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "AIDevBuddy", "Projects")
            : Environment.ExpandEnvironmentVariables(configured);
    }

    public string WorkspaceBasePath => _basePath;

    public void SetWorkspaceBasePath(string path)
    {
        _basePath = path;
    }

    public string GetProjectFolder(string projectName, int projectId)
    {
        var slug = Slugify(projectName);
        return Path.Combine(_basePath, $"{slug}-{projectId}");
    }

    public async Task EnsureProjectFolderAsync(string projectName, int projectId)
    {
        var folder = GetProjectFolder(projectName, projectId);
        try
        {
            Directory.CreateDirectory(Path.Combine(folder, "artifacts"));
            _logger.LogInformation("Workspace folder ready: {Folder}", folder);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create workspace folder: {Folder}", folder);
        }
        await Task.CompletedTask;
    }

    public async Task WriteArtifactAsync(
        string projectName, int projectId, string artifactName, string content)
    {
        try
        {
            var folder = GetProjectFolder(projectName, projectId);
            var artifactsDir = Path.Combine(folder, "artifacts");
            Directory.CreateDirectory(artifactsDir);

            var fileName = $"{Slugify(artifactName)}.md";
            var filePath = Path.Combine(artifactsDir, fileName);

            await File.WriteAllTextAsync(filePath, content);
            _logger.LogInformation("Artifact written: {Path}", filePath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to write artifact '{Name}' for project {Id}",
                artifactName, projectId);
        }
    }

    private static string Slugify(string input)
    {
        var slug = input.ToLowerInvariant().Trim();
        slug = Regex.Replace(slug, @"[^a-z0-9\s\-]", "");
        slug = Regex.Replace(slug, @"\s+", "-");
        slug = Regex.Replace(slug, @"-{2,}", "-");
        return slug.Trim('-');
    }
}
