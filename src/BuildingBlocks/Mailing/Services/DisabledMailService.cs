using Microsoft.Extensions.Logging;

namespace FSH.Framework.Mailing.Services;

/// <summary>
/// Used when mail is off: no SMTP password (SMTP_PASSWORD in a stamp) and no SendGrid.
/// It logs each mail it doesn't send, so a flow that sends mail still completes.
/// </summary>
public sealed partial class DisabledMailService(ILogger<DisabledMailService> logger) : IMailService
{
    public Task SendAsync(MailRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        LogNotSent(logger, request.Subject, request.To.Count);
        return Task.CompletedTask;
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Mail is off (no SMTP password), so the mail '{Subject}' to {Count} recipient(s) was not sent.")]
    private static partial void LogNotSent(ILogger logger, string subject, int count);
}
