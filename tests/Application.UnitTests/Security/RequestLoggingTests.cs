using EbayClone.Application.Common.Behaviours;
using EbayClone.Application.Common.Interfaces;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;

namespace EbayClone.Application.UnitTests.Security;

public class RequestLoggingTests
{
    public record SecretRequest(string Password, string Code, string AccountNumber);

    private sealed class CaptureLogger : ILogger<SecretRequest>
    {
        public List<string> Messages { get; } = [];
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel level) => true;
        public void Log<TState>(LogLevel level, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter) => Messages.Add(formatter(state, exception) + exception);
    }

    [Test]
    public async Task NormalSlowAndFailedRequests_DoNotLogPayloadOrExceptionContents()
    {
        var logger = new CaptureLogger();
        var request = new SecretRequest("private-password", "654321", "001234567890");
        var actor = SensitiveLogTests.Actor("42", "SuperAdmin");
        var identity = new Mock<IIdentityService>();
        await new LoggingBehaviour<SecretRequest>(logger, actor, identity.Object).Process(request, default);
        await new PerformanceBehaviour<SecretRequest, bool>(logger, actor, identity.Object).Handle(request,
            async ct => { await Task.Delay(550, ct); return true; }, default);
        Assert.ThrowsAsync<InvalidOperationException>(() => new UnhandledExceptionBehaviour<SecretRequest, bool>(logger)
            .Handle(request, ct => throw new InvalidOperationException("private-password 654321 001234567890"), default));
        Assert.That(logger.Messages, Has.Count.EqualTo(3));
        var output = string.Join("\n", logger.Messages);
        Assert.That(output, Does.Contain(nameof(SecretRequest)));
        foreach (var secret in new[] { request.Password, request.Code, request.AccountNumber })
            Assert.That(output, Does.Not.Contain(secret));
    }
}
