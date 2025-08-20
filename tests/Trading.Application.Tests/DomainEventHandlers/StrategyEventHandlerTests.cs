using Moq;
using Trading.Application.DomainEventHandlers;
using Trading.Application.Services.Trading;
using Trading.Domain.Entities;
using Trading.Domain.Events;

namespace Trading.Application.Tests.DomainEventHandlers;

public class StrategyEventHandlerTests
{
    private readonly Mock<IStrategyTaskManager> _mockTaskManager;
    private readonly StrategyEventHandler _handler;
    private readonly Strategy _strategy;

    public StrategyEventHandlerTests()
    {
        _mockTaskManager = new Mock<IStrategyTaskManager>();
        _handler = new StrategyEventHandler(_mockTaskManager.Object);
        _strategy = new Strategy { Id = "test-id" };
    }

    [Fact]
    public async Task Handle_StrategyCreatedEvent_CallsAddAsync()
    {
        var evt = new StrategyCreatedEvent(_strategy);
        await _handler.Handle(evt, CancellationToken.None);

        _mockTaskManager.Verify(m => m.StartAsync(_strategy, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_StrategyDeletedEvent_CallsRemoveAsync()
    {
        var evt = new StrategyDeletedEvent(_strategy);
        await _handler.Handle(evt, CancellationToken.None);

        _mockTaskManager.Verify(m => m.StopAsync(_strategy, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_StrategyPausedEvent_CallsRemoveAsync()
    {
        var evt = new StrategyPausedEvent(_strategy);
        await _handler.Handle(evt, CancellationToken.None);

        _mockTaskManager.Verify(m => m.PauseAsync(_strategy, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_StrategyResumedEvent_CallsAddAsync()
    {
        var evt = new StrategyResumedEvent(_strategy);
        await _handler.Handle(evt, CancellationToken.None);

        _mockTaskManager.Verify(m => m.ResumeAsync(_strategy, It.IsAny<CancellationToken>()), Times.Once);
    }
}
