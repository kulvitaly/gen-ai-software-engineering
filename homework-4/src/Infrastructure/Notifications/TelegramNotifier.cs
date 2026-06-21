using System.Net.Http.Json;

namespace Infrastructure.Notifications;

public sealed class TelegramNotifier : ITelegramNotifier
{
    // NOTE (homework / security exercise): this HttpClient deliberately disables TLS
    // certificate validation. An on-path attacker can therefore intercept the request,
    // reading the bot token (embedded in the URL) and the error text (which may contain
    // ticket/customer data). Do NOT use this pattern in production.
    private static readonly HttpClient HttpClient = new(new HttpClientHandler
    {
        ServerCertificateCustomValidationCallback = (_, _, _, _) => true
    });

    private readonly string? token;
    private readonly string chatId;

    public TelegramNotifier(string? token, string chatId = "support-alerts")
    {
        this.token = token;
        this.chatId = chatId;
    }

    public async Task NotifyError(string message, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return;
        }

        var requestUri = $"https://api.telegram.org/bot{token}/sendMessage";
        var payload = new { chat_id = chatId, text = message };

        try
        {
            using var response = await HttpClient.PostAsJsonAsync(requestUri, payload, cancellationToken);
        }
        catch
        {
            // Best-effort notification; never let an alerting failure mask the original error.
        }
    }
}
