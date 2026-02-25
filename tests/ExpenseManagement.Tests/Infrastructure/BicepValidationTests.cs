using FluentAssertions;

namespace ExpenseManagement.Tests.Infrastructure;

/// <summary>
/// Validates the Bicep infrastructure templates without deploying to Azure.
/// Checks file existence, correct parameter usage (utcNow/newGuid must be
/// in <c>param</c> defaults, never in <c>var</c> declarations or resource
/// properties), and optionally compiles the templates with the Azure CLI.
/// </summary>
public class BicepValidationTests
{
    // Resolve the repo root relative to the test binary output folder.
    // When tests run from tests/ExpenseManagement.Tests/bin/Debug/net8.0/
    // the repo root is four levels up.
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
        // Fallback — walk up four levels from test binary
        return Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", ".."));
    }

    private string InfraPath(string relative) =>
        Path.Combine(RepoRoot, "deploy-infra", relative);

    // ─────────────────────────────────────────────────────────────────────────
    // File existence
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void BicepFiles_ExistInExpectedLocations()
    {
        var requiredFiles = new[]
        {
            InfraPath("main.bicep"),
            InfraPath("main.bicepparam"),
            InfraPath("modules/managed-identity.bicep"),
            InfraPath("modules/app-service.bicep"),
            InfraPath("modules/app-service-diagnostics.bicep"),
            InfraPath("modules/azure-sql.bicep"),
            InfraPath("modules/sql-diagnostics.bicep"),
            InfraPath("modules/monitoring.bicep"),
            InfraPath("modules/genai.bicep")
        };

        foreach (var file in requiredFiles)
        {
            File.Exists(file).Should().BeTrue(
                $"required Bicep file '{Path.GetRelativePath(RepoRoot, file)}' must exist");
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // utcNow() / newGuid() usage rules
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void MainBicep_HasTimestampAsParameter_NotVariable()
    {
        // timestamp must be declared as `param`, not `var`
        var content = File.ReadAllText(InfraPath("main.bicep"));

        // The param line: should find "param timestamp" on the same line as utcNow
        var lines = content.Split('\n');
        var paramTimestampLines = lines
            .Where(l => l.Contains("timestamp") && l.Contains("utcNow"))
            .ToList();

        paramTimestampLines.Should().NotBeEmpty(
            "main.bicep must declare 'param timestamp string = utcNow(...)' as a parameter default");

        // Every such line must start with "param" (after trimming), not "var"
        foreach (var line in paramTimestampLines)
        {
            line.TrimStart().Should().StartWith("param",
                because: "utcNow() is only valid as a parameter default value, never in a var");
        }
    }

    [Fact]
    public void BicepFiles_DoNotUseUtcNowInVariables()
    {
        // utcNow() is only valid as a default value in a `param` declaration.
        // Using it in a `var` declaration will cause an ARM deployment failure.
        var bicepFiles = Directory.GetFiles(InfraPath(""), "*.bicep", SearchOption.AllDirectories);

        foreach (var file in bicepFiles)
        {
            var lines = File.ReadAllLines(file);
            foreach (var line in lines)
            {
                var trimmed = line.TrimStart();

                // Skip comment lines
                if (trimmed.StartsWith("//"))
                    continue;

                // If the line contains utcNow() it must NOT start with "var"
                if (trimmed.Contains("utcNow("))
                {
                    trimmed.Should().StartWith("param",
                        because: $"utcNow() in '{Path.GetRelativePath(RepoRoot, file)}' must only appear in a 'param' default, not a 'var' declaration. Line: {line.Trim()}");
                }
            }
        }
    }

    [Fact]
    public void BicepFiles_DoNotUseNewGuidInResourceProperties()
    {
        // newGuid() is only valid as a default value in a `param` declaration.
        // It must not appear directly inside resource property blocks.
        var bicepFiles = Directory.GetFiles(InfraPath(""), "*.bicep", SearchOption.AllDirectories);

        foreach (var file in bicepFiles)
        {
            var lines = File.ReadAllLines(file);
            foreach (var line in lines)
            {
                var trimmed = line.TrimStart();

                // Skip comment lines
                if (trimmed.StartsWith("//"))
                    continue;

                // If the line contains newGuid() it must NOT be a var declaration
                if (trimmed.Contains("newGuid("))
                {
                    trimmed.Should().StartWith("param",
                        because: $"newGuid() in '{Path.GetRelativePath(RepoRoot, file)}' must only appear in a 'param' default. Line: {line.Trim()}");
                }
            }
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // main.bicepparam
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void MainBicepparam_ExistsAndUsesCorrectSyntax()
    {
        var paramFile = InfraPath("main.bicepparam");
        File.Exists(paramFile).Should().BeTrue("main.bicepparam must exist in deploy-infra/");

        var content = File.ReadAllText(paramFile);
        content.Should().Contain("using './main.bicep'",
            because: "main.bicepparam must reference main.bicep with the correct 'using' directive");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // SQL diagnostics — database level only
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void SqlDiagnostics_OnlyConfiguresAtDatabaseLevel()
    {
        // The sql-diagnostics module must scope to the database, not the server.
        // Server-level categories (e.g. SQLSecurityAuditEvents) are not supported
        // and will cause deployment failures.
        var diagFile = InfraPath("modules/sql-diagnostics.bicep");
        File.Exists(diagFile).Should().BeTrue("sql-diagnostics.bicep must exist");

        var content = File.ReadAllText(diagFile);

        // Must reference a SQL database resource type, not a bare server type
        content.Should().Contain("Microsoft.Sql/servers/databases",
            because: "SQL diagnostics must be scoped to the database, not the server");

        // Must NOT apply diagnostics directly to the SQL server resource
        content.Should().NotContain("Microsoft.Sql/servers'",
            because: "server-level SQL diagnostics are not supported and will cause failures");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Bicep compilation (requires Azure CLI — skipped if unavailable)
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void BicepFiles_CompileWithoutErrors()
    {
        // Skip if the Azure CLI is not installed — this is an optional check
        if (!IsAzureCliAvailable())
        {
            // Graceful skip — not an error
            return;
        }

        var bicepFiles = Directory.GetFiles(InfraPath(""), "*.bicep", SearchOption.AllDirectories);

        foreach (var file in bicepFiles)
        {
            var result = RunProcess("az", $"bicep build --file \"{file}\" --stdout");
            result.ExitCode.Should().Be(0,
                because: $"'{Path.GetRelativePath(RepoRoot, file)}' must compile without errors. Output: {result.StdErr}");
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Helpers
    // ─────────────────────────────────────────────────────────────────────────

    private static bool IsAzureCliAvailable()
    {
        try
        {
            var result = RunProcess("az", "--version");
            return result.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    private static (int ExitCode, string StdOut, string StdErr) RunProcess(string fileName, string arguments)
    {
        var psi = new System.Diagnostics.ProcessStartInfo
        {
            FileName               = fileName,
            Arguments              = arguments,
            RedirectStandardOutput = true,
            RedirectStandardError  = true,
            UseShellExecute        = false,
            CreateNoWindow         = true
        };

        using var process = System.Diagnostics.Process.Start(psi)!;
        var stdOut = process.StandardOutput.ReadToEnd();
        var stdErr = process.StandardError.ReadToEnd();
        process.WaitForExit();
        return (process.ExitCode, stdOut, stdErr);
    }
}
