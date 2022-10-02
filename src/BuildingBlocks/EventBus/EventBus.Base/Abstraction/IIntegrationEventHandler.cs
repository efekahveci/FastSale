using EventBus.Base.Events;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EventBus.Base.Abstraction;

public interface IIntegrationEventHandler<TIntegrationEvent> where TIntegrationEvent:IntegrationEvent
{
    Task Handle(TIntegrationEvent integrationEvent);
}
