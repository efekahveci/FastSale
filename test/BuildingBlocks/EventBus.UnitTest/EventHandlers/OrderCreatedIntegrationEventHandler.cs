using EventBus.Base.Abstraction;
using EventBus.UnitTest.Events;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EventBus.UnitTest.EventHandlers;

public class OrderCreatedIntegrationEventHandler : IIntegrationEventHandler<OrderCreatedIntegrationsEvent>
{
    public Task Handle(OrderCreatedIntegrationsEvent integrationEvent)
    {
        return Task.CompletedTask;  
    }
}
