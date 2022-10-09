using EventBus.Base;
using EventBus.Base.Abstraction;
using EventBus.Factory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace EventBus.UnitTest;

[TestClass]
public class EventBusTest
{

    private ServiceCollection services;

    public EventBusTest()
    {
        services = new ServiceCollection();
        services.AddLogging(configure => configure.AddConsole());
    }

    [TestMethod]
    public void SubscribeEventOnRabbitMQTest()
    {
        var sp = services.BuildServiceProvider();


        services.AddSingleton<IEventBus>(i =>
        {
            EventBusConfig config = new()
            {
                ConnectionRetryCount = 5,
                SubscriberClientAppName =
            };
            return EventBusFactory.Create(config,sp);
        });
    }
}