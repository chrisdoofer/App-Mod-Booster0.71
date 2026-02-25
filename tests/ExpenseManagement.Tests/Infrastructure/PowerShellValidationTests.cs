using FluentAssertions;

namespace ExpenseManagement.Tests.Infrastructure;

/// <summary>
/// Validates the PowerShell deployment scripts and GitHub Actions workflow.
/// Checks that scripts follow the conventions documented in
/// <c>COMMON-ERRORS.md</c> and the shared Copilot instructions:
/// - hashtable splatting (not array splatting)
/// - $PSScriptRoot usage for portable paths
/// - no direct SQL piping to sqlcmd
/// - correct sqlcmd authentication-method quoting
/// - Azure Policy failure handling
/// - CI/CD detection
/// - OIDC authentication in GitHub Actions workflow
/// - hidden file inclusion for .deployment-context.json artifact
/// </summary>
public class PowerShellValidationTests
{
    // Resolve the repo root relative to the test binary output folder.
    private static readonly string RepoRoot = FindRepoRoot();

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "deploy-all.ps1")))
                return dir.FullName;
            dir = dir.Parent;
        }
        return Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", ".."));
    }

    private string ScriptPath(string relative) =>
        Path.Combine(RepoRoot, relative);

    // ─────────────────────────────────────────────────────────────────────────
    // File existence
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void DeployAllScript_Exists_InRepoRoot()
    {
        File.Exists(ScriptPath("deploy-all.ps1")).Should().BeTrue(
            "deploy-all.ps1 must exist at the repository root");
    }

    [Fact]
    public void DeployInfraScript_Exists()
    {
        File.Exists(ScriptPath("deploy-infra/deploy.ps1")).Should().BeTrue(
            "deploy-infra/deploy.ps1 must exist");
    }

    [Fact]
    public void DeployAppScript_Exists()
    {
        File.Exists(ScriptPath("deploy-app/deploy.ps1")).Should().BeTrue(
            "deploy-app/deploy.ps1 must exist");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Splatting conventions
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void DeployAllScript_UsesHashtableSplatting_NotArraySplatting()
    {
        // Array splatting (@("-ResourceGroup", $rg)) causes "positional parameter
        // cannot be found" errors. Scripts must use hashtable splatting (@{Key=Val}).
        var content = File.ReadAllText(ScriptPath("deploy-all.ps1"));

        // Must contain at least one hashtable splat
        content.Should().Contain("@{",
            because: "deploy-all.ps1 must use hashtable splatting when calling child scripts");

        // Must NOT contain array splatting patterns like @("-", "@(", etc.)
        // We check that no line passes @("-...") which is the classic array-splat mistake.
        var lines = content.Split('\n');
        foreach (var line in lines)
        {
            var trimmed = line.TrimStart();
            if (trimmed.StartsWith("#")) continue; // skip comment lines

            // Array splatting mistake: @("-ResourceGroup", ...) or @("-")
            trimmed.Should().NotMatchRegex(@"@\s*\(""-",
                because: $"array splatting with @(\"-...\") is forbidden. Use @{{Key = $Value}} instead. Line: {line.Trim()}");
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // $PSScriptRoot portability
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void DeployInfraScript_UsesPSScriptRoot()
    {
        var content = File.ReadAllText(ScriptPath("deploy-infra/deploy.ps1"));
        content.Should().Contain("$PSScriptRoot",
            because: "deploy-infra/deploy.ps1 must use $PSScriptRoot for portable path resolution");
    }

    [Fact]
    public void DeployAppScript_UsesPSScriptRoot()
    {
        var content = File.ReadAllText(ScriptPath("deploy-app/deploy.ps1"));
        content.Should().Contain("$PSScriptRoot",
            because: "deploy-app/deploy.ps1 must use $PSScriptRoot for portable path resolution");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // sqlcmd usage rules
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void DeployInfraScript_NeverPipesSqlToSqlcmd()
    {
        // Common mistake: Write-Output $sql | sqlcmd ...
        // Correct pattern: write to temp file, then sqlcmd -i $tempFile
        var content = File.ReadAllText(ScriptPath("deploy-infra/deploy.ps1"));

        var lines = content.Split('\n');
        foreach (var line in lines)
        {
            var trimmed = line.TrimStart();
            if (trimmed.StartsWith("#")) continue;

            // Detect "| sqlcmd" piping
            if (trimmed.Contains("| sqlcmd") || trimmed.Contains("|sqlcmd"))
            {
                false.Should().BeTrue(
                    because: $"SQL must never be piped to sqlcmd; write to a temp file and use -i flag instead. Line: {line.Trim()}");
            }
        }
    }

    [Fact]
    public void DeployInfraScript_UsesCorrectSqlcmdAuthMethod()
    {
        // The authentication-method flag must be quoted to avoid issues
        // with shell argument parsing on Linux/macOS runners.
        // Correct: "--authentication-method=ActiveDirectoryAzCli"
        // Wrong:   --authentication-method=ActiveDirectoryAzCli  (unquoted)
        var content = File.ReadAllText(ScriptPath("deploy-infra/deploy.ps1"));

        content.Should().Contain("\"--authentication-method=",
            because: "the sqlcmd --authentication-method flag must be passed as a quoted string " +
                     "to prevent argument parsing issues on Linux runners");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Azure Policy failure handling
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void DeployInfraScript_HandlesAzurePolicyFailures()
    {
        // Azure Policy assignments can cause transient "PolicyDeployment_" errors
        // during Bicep deployments. The script must filter these out.
        var content = File.ReadAllText(ScriptPath("deploy-infra/deploy.ps1"));

        content.Should().Contain("PolicyDeployment_",
            because: "deploy-infra/deploy.ps1 must filter out Azure Policy-related deployment " +
                     "errors to avoid false failures during infrastructure provisioning");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // CI/CD detection
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void DeployInfraScript_DetectsCI()
    {
        // The script must detect when running in CI/CD so it can adjust
        // authentication methods and skip interactive prompts.
        var content = File.ReadAllText(ScriptPath("deploy-infra/deploy.ps1"));

        content.Should().ContainAny(
            ["$env:GITHUB_ACTIONS", "$IsCI", "GITHUB_ACTIONS"],
            because: "deploy-infra/deploy.ps1 must detect CI/CD environment " +
                     "(e.g. GITHUB_ACTIONS env var) to switch to non-interactive auth");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // GitHub Actions workflow
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void GitHubActionsWorkflow_Exists()
    {
        var workflowsDir = Path.Combine(RepoRoot, ".github", "workflows");
        var workflowFiles = Directory.Exists(workflowsDir)
            ? Directory.GetFiles(workflowsDir, "*.yml", SearchOption.TopDirectoryOnly)
                .Concat(Directory.GetFiles(workflowsDir, "*.yaml", SearchOption.TopDirectoryOnly))
                .ToArray()
            : [];

        workflowFiles.Should().NotBeEmpty(
            "at least one GitHub Actions workflow file must exist in .github/workflows/");
    }

    [Fact]
    public void GitHubActionsWorkflow_UsesOidcAuth()
    {
        // OIDC requires id-token: write permission to be declared.
        var content = ReadMainWorkflow();
        content.Should().Contain("id-token: write",
            because: "the GitHub Actions workflow must declare id-token: write permission " +
                     "to enable OIDC (passwordless) Azure authentication");
    }

    [Fact]
    public void GitHubActionsWorkflow_MapsAzureClientId()
    {
        // vars.* GitHub variables are NOT automatically env vars.
        // The workflow must explicitly map AZURE_CLIENT_ID to the step's env block.
        var content = ReadMainWorkflow();
        content.Should().Contain("AZURE_CLIENT_ID",
            because: "the workflow must explicitly map AZURE_CLIENT_ID from vars.AZURE_CLIENT_ID " +
                     "in the step env block — GitHub vars.* are not automatic env vars");
    }

    [Fact]
    public void GitHubActionsWorkflow_IncludesHiddenFiles()
    {
        // actions/upload-artifact@v4 skips dotfiles by default.
        // The .deployment-context.json is a dotfile and must be explicitly included.
        var content = ReadMainWorkflow();
        content.Should().Contain("include-hidden-files: true",
            because: "upload-artifact@v4 skips dotfiles by default; " +
                     "include-hidden-files: true is required for .deployment-context.json");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Helpers
    // ─────────────────────────────────────────────────────────────────────────

    private string ReadMainWorkflow()
    {
        var workflowsDir = Path.Combine(RepoRoot, ".github", "workflows");
        if (!Directory.Exists(workflowsDir))
        {
            workflowsDir.Should().NotBeNull("the .github/workflows directory must exist");
            return string.Empty;
        }

        var workflowFiles = Directory.GetFiles(workflowsDir, "*.yml", SearchOption.TopDirectoryOnly)
            .Concat(Directory.GetFiles(workflowsDir, "*.yaml", SearchOption.TopDirectoryOnly))
            .ToArray();

        workflowFiles.Should().NotBeEmpty("at least one workflow file must exist");

        // Concatenate all workflow files — the test checks for patterns anywhere
        return string.Join("\n", workflowFiles.Select(File.ReadAllText));
    }
}
