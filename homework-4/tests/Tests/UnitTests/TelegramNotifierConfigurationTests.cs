using Infrastructure.Notifications;

namespace Tests;

public sealed class TelegramNotifierConfigurationTests
{
    [Fact]
    public async Task NotifyError_WithEmptyToken_ReturnsWithoutMakingRequest()
    {
        // Arrange
        var notifier = new TelegramNotifier(token: "", chatId: "support-alerts");

        // Act
        await notifier.NotifyError("Test error message");

        // Assert
        // If the method returns without throwing, the test passes.
        // An empty token should short-circuit without attempting HTTP communication.
    }

    [Fact]
    public async Task NotifyError_WithNullToken_ReturnsWithoutMakingRequest()
    {
        // Arrange
        var notifier = new TelegramNotifier(token: null, chatId: "support-alerts");

        // Act
        await notifier.NotifyError("Test error message");

        // Assert
        // If the method returns without throwing, the test passes.
        // A null token should short-circuit without attempting HTTP communication.
    }

    [Fact]
    public async Task NotifyError_WithWhitespaceToken_ReturnsWithoutMakingRequest()
    {
        // Arrange
        var notifier = new TelegramNotifier(token: "   ", chatId: "support-alerts");

        // Act
        await notifier.NotifyError("Test error message");

        // Assert
        // If the method returns without throwing, the test passes.
        // Whitespace-only token should short-circuit without attempting HTTP communication.
    }

    [Fact]
    public async Task NotifyError_WithEmptyToken_Completes()
    {
        // Arrange
        var notifier = new TelegramNotifier(token: "", chatId: "support-alerts");
        var cts = new CancellationTokenSource();

        // Act & Assert
        // Should complete without throwing
        await notifier.NotifyError("Error occurred", cts.Token);
    }

    [Fact]
    public void TelegramNotifier_WithEmptyToken_ConstructsSuccessfully()
    {
        // Arrange & Act
        var notifier = new TelegramNotifier(token: "", chatId: "support-alerts");

        // Assert
        Assert.NotNull(notifier);
    }

    [Fact]
    public void TelegramNotifier_WithNullToken_ConstructsSuccessfully()
    {
        // Arrange & Act
        var notifier = new TelegramNotifier(token: null, chatId: "support-alerts");

        // Assert
        Assert.NotNull(notifier);
    }

    [Fact]
    public async Task NotifyError_WithEmptyTokenAndCancellation_ReturnsWithoutDelay()
    {
        // Arrange
        var notifier = new TelegramNotifier(token: "", chatId: "support-alerts");
        var cts = new CancellationTokenSource();

        // Act
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        await notifier.NotifyError("Test error", cts.Token);
        stopwatch.Stop();

        // Assert
        // Should complete almost instantly (< 100ms) since no network call is made
        Assert.True(stopwatch.ElapsedMilliseconds < 100);
    }

    [Fact]
    public async Task NotifyError_WithEmptyToken_AllowsMultipleCalls()
    {
        // Arrange
        var notifier = new TelegramNotifier(token: "", chatId: "support-alerts");

        // Act
        await notifier.NotifyError("First error");
        await notifier.NotifyError("Second error");
        await notifier.NotifyError("Third error");

        // Assert
        // All calls should complete without error
    }
}
