using Moq;
using Trading.Application.Services.Trading;
using Trading.Domain.Entities;
using Trading.Domain.Events;

namespace Trading.Application.Tests.Services.Trading;
public class StrategyEventHandlerTests
{
    private readonly Mock<IStrategyTaskManager> _taskManagerMock;
    private readonly StrategyEventHandler _handler;
    private readonly Strategy _strategy;

    public StrategyEventHandlerTests()
    {
        _taskManagerMock = new Mock<IStrategyTaskManager>();
        _handler = new StrategyEventHandler(_taskManagerMock.Object);
        _strategy = new Strategy { Id = "test-id" };
    }

    [Fact]
    public async Task Handle_StrategyCreatedEvent_CallsAddAsync()
    {
        var evt = new StrategyCreatedEvent(_strategy);
        await _handler.Handle(evt, CancellationToken.None);

        _taskManagerMock.Verify(m => m.HandleCreatedAsync(_strategy, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_StrategyDeletedEvent_CallsRemoveAsync()
    {
        var evt = new StrategyDeletedEvent(_strategy);
        await _handler.Handle(evt, CancellationToken.None);

        _taskManagerMock.Verify(m => m.HandleDeletedAsync(_strategy, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_StrategyPausedEvent_CallsRemoveAsync()
    {
        var evt = new StrategyPausedEvent(_strategy);
        await _handler.Handle(evt, CancellationToken.None);

        _taskManagerMock.Verify(m => m.HandlePausedAsync(_strategy, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_StrategyResumedEvent_CallsAddAsync()
    {
        var evt = new StrategyResumedEvent(_strategy);
        await _handler.Handle(evt, CancellationToken.None);

        _taskManagerMock.Verify(m => m.HandleResumedAsync(_strategy, It.IsAny<CancellationToken>()), Times.Once);
    }
}
