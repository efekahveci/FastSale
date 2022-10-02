using EventBus.Base.Abstraction;
using EventBus.Base.SubscriptionManager;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace EventBus.Base.Events;

public abstract class BaseEventBus : IEventBus
{
    public readonly IServiceProvider _serviceProvider;
    public readonly IEventBusSubscriptionManager _subscriptionManager;
    private EventBusConfig _eventBusConfig;

    public BaseEventBus(EventBusConfig eventBusConfig, IServiceProvider serviceProvider)
    {
        _eventBusConfig = eventBusConfig;
        _serviceProvider = serviceProvider;
        _subscriptionManager = new InMemoryEventBusSubscriptionManager(ProcessEventName);
    }

    public virtual string ProcessEventName(string eventName)
    {
        if (_eventBusConfig.DeleteEventPrefix)
            eventName = eventName.TrimStart(_eventBusConfig.EventNamePrefix.ToArray());
        if (_eventBusConfig.DeleteEventSuffix)
            eventName = eventName.TrimStart(_eventBusConfig.EventNameSuffix.ToArray());

        return eventName;
    }

    public virtual string GetSubName(string eventName) => $"{_eventBusConfig.SubscriberClientAppName}.{ProcessEventName(eventName)}";  
    
    public virtual void Dispose() => _eventBusConfig = null;


    public async Task<bool> ProcessEvent (string eventName, string message)
    {
        eventName=ProcessEventName(eventName);
        var processed = false;
        if (_subscriptionManager.HasSubscriptionForEvent(eventName))
        {
            var subs =_subscriptionManager.GetHandlersForEvent(eventName);

            using (var scope = _serviceProvider.CreateScope())
            {
                foreach (var sub in subs)
                {
                    var handler = _serviceProvider.GetService(sub.HandlerType); 
                    if(handler is null) continue;
                    var eventType = _subscriptionManager.GetEventTypeByName($"{_eventBusConfig.EventNamePrefix}{eventName}{_eventBusConfig.EventNameSuffix}");
                    var integrationEvent = JsonSerializer.Deserialize(message, eventType);


                    var concreteType = typeof(IIntegrationEventHandler<>).MakeGenericType(eventType);
                    await (Task) concreteType.GetMethod("Handle").Invoke(handler,new object[] { integrationEvent });

                }
            }
            processed = true;   
        }
        return processed;
    }

    public void Publish(IntegrationEvent integrationEvent)
    {
        throw new NotImplementedException();
    }

    public void Subscribe<T, TH>() where T : IntegrationEvent where TH : IIntegrationEventHandler<T>
    {
        throw new NotImplementedException();
    }

    public void UnSubscribe<T, TH>() where T : IntegrationEvent where TH : IIntegrationEventHandler<T>
    {
        throw new NotImplementedException();
    }
}
