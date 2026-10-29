using System.ComponentModel.DataAnnotations;

namespace FSH.Framework.Jobs;

public sealed class HangfireOptions
{
    /// <summary>Username for the Hangfire dashboard.</summary>
    [MinLength(3)]
    public string UserName { get; set; } = "admin";

    /// <summary>
    /// Password for the Hangfire dashboard, from HANGFIRE_PASSWORD in a stamp. When it is
    /// empty the dashboard isn't mapped; the jobs still run. A short password is rejected
    /// at startup by <c>ValidateDataAnnotations().ValidateOnStart()</c>.
    /// </summary>
    [MinLength(12)]
    public string? Password { get; set; }

    public string Route { get; set; } = "/jobs";
}
