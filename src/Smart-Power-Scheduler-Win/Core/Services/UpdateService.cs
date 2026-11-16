using System.Diagnostics;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json.Nodes;
using SmartPowerScheduler.Config;

namespace SmartPowerScheduler.Core.Services;

public sealed class UpdateCheckResult
{
    public bool IsSuccess { get; set; }
    public bool IsUpdateAvailable { get; set; }
    public string LatestVersion { get; set; } = "";
    public string CurrentVersion { get; set; } = "";
    public string ReleaseUrl { get; set; } = "";
    public string ReleaseTitle { get; set; } = "";
    public string ErrorMessage { get; set; } = "";
}

public static class UpdateService
{
    private static readonly HttpClient _http = new()
    {
        Timeout = TimeSpan.FromSeconds(5)
    };

    static UpdateService()
    {
        _http.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("SmartPowerScheduler", "1.0"));
        _http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github.v3+json"));
    }

    public static async Task<UpdateCheckResult> CheckForUpdatesAsync()
    {
        string currentVerStr = AppConfig.Instance.App.Version;
        string endpoint = AppConfig.Instance.Defaults.UpdateApiEndpoint;
        if (string.IsNullOrWhiteSpace(endpoint))
        {
            string repoUrl = AppConfig.Instance.App.RepositoryUrl.TrimEnd('/');
            if (repoUrl.Contains("github.com/"))
            {
                string path = repoUrl.Substring(repoUrl.IndexOf("github.com/") + "github.com/".Length);
                endpoint = $"https://api.github.com/repos/{path}/releases/latest";
            }
        }

        try
        {
            using var response = await _http.GetAsync(endpoint).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                return new UpdateCheckResult
                {
                    IsSuccess = false,
                    CurrentVersion = currentVerStr,
                    ErrorMessage = response.StatusCode == System.Net.HttpStatusCode.NotFound
                        ? "No releases found on GitHub."
                        : $"GitHub API returned {(int)response.StatusCode}."
                };
            }

            string json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            var node = JsonNode.Parse(json);
            if (node == null)
            {
                return new UpdateCheckResult { IsSuccess = false, CurrentVersion = currentVerStr, ErrorMessage = "Invalid response from GitHub." };
            }

            string tagName = node["tag_name"]?.GetValue<string>() ?? "";
            string htmlUrl = node["html_url"]?.GetValue<string>() ?? AppConfig.Instance.App.RepositoryUrl + "/releases";
            string releaseTitle = node["name"]?.GetValue<string>() ?? tagName;

            string cleanLatest = tagName.Trim().TrimStart('v', 'V');
            string cleanCurrent = currentVerStr.Trim().TrimStart('v', 'V');

            bool isNewer = CompareVersions(cleanLatest, cleanCurrent) > 0;

            return new UpdateCheckResult
            {
                IsSuccess = true,
                IsUpdateAvailable = isNewer,
                LatestVersion = cleanLatest,
                CurrentVersion = cleanCurrent,
                ReleaseUrl = htmlUrl,
                ReleaseTitle = releaseTitle
            };
        }
        catch (Exception ex)
        {
            return new UpdateCheckResult
            {
                IsSuccess = false,
                CurrentVersion = currentVerStr,
                ErrorMessage = ex.Message
            };
        }
    }

    public static int CompareVersions(string verA, string verB)
    {
        if (Version.TryParse(NormalizeVersion(verA), out var vA) &&
            Version.TryParse(NormalizeVersion(verB), out var vB))
        {
            return vA.CompareTo(vB);
        }
        return string.Compare(verA, verB, StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeVersion(string ver)
    {
        var parts = ver.Split('.');
        if (parts.Length == 1) return $"{ver}.0";
        return ver;
    }

    public static void OpenReleaseUrl(string url)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(url))
                url = AppConfig.Instance.App.RepositoryUrl + "/releases";

            Process.Start(new ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true
            });
        }
        catch { }
    }
}
