using ExpenseManagement.Services;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;

namespace ExpenseManagement.Tests.Unit;

/// <summary>
/// Unit tests for <see cref="ChatService"/>.
/// These tests verify configuration detection and graceful fallback behaviour
/// when Azure OpenAI is not deployed or not configured.
/// </summary>
public class ChatServiceTests
{
    private readonly Mock<ILogger<ChatService>> _mockChatLogger;
    private readonly Mock<ILogger<ExpenseService>> _mockExpenseLogger;

    public ChatServiceTests()
    {
        _mockChatLogger    = new Mock<ILogger<ChatService>>();
        _mockExpenseLogger = new Mock<ILogger<ExpenseService>>();
    }

    // Helper — builds a ChatService from an in-memory config dictionary.
    private ChatService BuildChatService(Dictionary<string, string?> configValues)
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(configValues)
            .Build();

        // ExpenseService requires a logger — inject a mock
        var expenseService = new ExpenseService(config, _mockExpenseLogger.Object);

        return new ChatService(config, _mockChatLogger.Object, expenseService);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // IsConfigured property
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void ChatService_IsConfigured_ReturnsFalse_WhenEndpointMissing()
    {
        // Arrange — no GenAI settings at all
        var service = BuildChatService(new Dictionary<string, string?>());

        // Act / Assert
        service.IsConfigured.Should().BeFalse(
            "IsConfigured must be false when GenAISettings:OpenAIEndpoint is absent");
    }

    [Fact]
    public void ChatService_IsConfigured_ReturnsFalse_WhenEndpointIsEmpty()
    {
        // Arrange — endpoint key present but empty
        var service = BuildChatService(new Dictionary<string, string?>
        {
            ["GenAISettings:OpenAIEndpoint"] = ""
        });

        // Act / Assert
        service.IsConfigured.Should().BeFalse(
            "IsConfigured must be false when the endpoint value is an empty string");
    }

    [Fact]
    public void ChatService_IsConfigured_ReturnsTrue_WhenEndpointSet()
    {
        // Arrange — endpoint configured with a real-looking URL
        var service = BuildChatService(new Dictionary<string, string?>
        {
            ["GenAISettings:OpenAIEndpoint"]  = "https://myopenai.openai.azure.com/",
            ["GenAISettings:OpenAIModelName"] = "gpt-4o"
        });

        // Act / Assert
        service.IsConfigured.Should().BeTrue(
            "IsConfigured must be true when a non-empty endpoint URL is provided");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // SendMessageAsync — not-configured fallback
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task ChatService_SendMessage_ReturnsNotConfiguredMessage_WhenNotConfigured()
    {
        // Arrange — no endpoint → IsConfigured == false
        var service = BuildChatService(new Dictionary<string, string?>());

        // Act
        var response = await service.SendMessageAsync("How many expenses do I have?");

        // Assert — must return a friendly message, not throw
        response.Should().NotBeNullOrWhiteSpace(
            "the service should always return a string, never throw");
        response.Should().Contain("not available",
            because: "the fallback message must inform the user that AI chat is not yet enabled");
    }

    [Fact]
    public async Task ChatService_SendMessage_WithHistory_ReturnsNotConfiguredMessage_WhenNotConfigured()
    {
        // Arrange — no endpoint, with a non-null history list
        var service = BuildChatService(new Dictionary<string, string?>());
        var history = new List<ChatMessageDto>
        {
            new() { Role = "user",      Content = "Hello" },
            new() { Role = "assistant", Content = "Hi there!" }
        };

        // Act
        var response = await service.SendMessageAsync("Tell me my expenses", history);

        // Assert
        response.Should().NotBeNullOrWhiteSpace();
        response.Should().Contain("not available");
    }
}
