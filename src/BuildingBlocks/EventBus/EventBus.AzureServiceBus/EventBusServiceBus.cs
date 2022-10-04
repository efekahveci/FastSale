using EventBus.Base;
using EventBus.Base.Events;
using Microsoft.Azure.ServiceBus;
using Microsoft.Azure.ServiceBus.Management;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace EventBus.AzureServiceBus;

public class EventBusServiceBus : BaseEventBus
{
    private ITopicClient _topicClient;
    private ManagementClient _managementClient;
    private ILogger _logger;
    public EventBusServiceBus(EventBusConfig eventBusConfig, IServiceProvider serviceProvider) : base(eventBusConfig, serviceProvider)
    {
        _logger = serviceProvider.GetService(typeof(ILogger<EventBusServiceBus>)) as ILogger<EventBusServiceBus>;
        _managementClient = new ManagementClient(eventBusConfig.EventBusConnectionString);
        _topicClient = CreateTopicClient();
    }

    private ITopicClient CreateTopicClient()
    {

        if (_topicClient is null || _topicClient.IsClosedOrClosing)
            _topicClient = new TopicClient(_eventBusConfig.EventBusConnectionString, _eventBusConfig.DefaultTopicName, RetryPolicy.Default);

        _managementClient.TopicExistsAsync(_eventBusConfig.DefaultTopicName).GetAwaiter().GetResult();
        return _topicClient;
    }

    public override void Publish(IntegrationEvent integrationEvent)
    {

        var eventName = integrationEvent.GetType().Name;

        eventName = ProcessEventName(eventName);


        var message = new Message()
        {
            MessageId = Guid.NewGuid().ToString(),
            Body = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(integrationEvent)),
            Label = eventName
        };

        _topicClient.SendAsync(message).GetAwaiter().GetResult();
    }

    public override void Subscribe<T, TH>()
    {
        var eventName = typeof(T).Name;
        eventName = ProcessEventName(eventName);

        if (!_subscriptionManager.HasSubscriptionForEvent(eventName))
        {
           var subsClient = CreateSubsIfNotExist(eventName);

            RegisterSubsClientMessageHandler(subsClient);

        }

        _logger.LogInformation($"Subscribing to event {eventName}");

        _subscriptionManager.AddSubscription<T, TH>();
    }

    public override void UnSubscribe<T, TH>()
    {
        var eventName = typeof(T).Name;

        try
        {
            var subClient = CreateSubsClient(eventName);

            subClient.RemoveRuleAsync(eventName).GetAwaiter().GetResult();  
        }
        catch (MessagingEntityNotFoundException)
        {

            _logger.LogWarning("The messaging entity not found {eventName}", eventName);

            }

        _logger.LogInformation("Unsub from event {eventName}", eventName);

        _subscriptionManager.RemoveSubscription<T, TH>();


    }

    private void RegisterSubsClientMessageHandler(ISubscriptionClient client)
    {
        client.RegisterMessageHandler(
            async (message, token) =>
            {
                var eventName = $"{message.Label}";
                var mesData = Encoding.UTF8.GetString(message.Body);

                if (await ProcessEvent(ProcessEventName(eventName), mesData))

                    await client.CompleteAsync(message.SystemProperties.LockToken);

            }, new MessageHandlerOptions(ExceptionMessageHandler) { MaxConcurrentCalls = 10, AutoComplete = false }
            );
    }

    private Task ExceptionMessageHandler(ExceptionReceivedEventArgs arg)
    {
        var ex = arg.Exception;
        var context = arg.ExceptionReceivedContext;

        _logger.LogError(ex, "Error handling message: {ExceptionMessage} - Context {@ExceptionmContext} ", ex.Message, context);

        return Task.CompletedTask; 
    }

    private SubscriptionClient CreateSubsClient(string eventName)
    {
        return new SubscriptionClient(_eventBusConfig.EventBusConnectionString, _eventBusConfig.DefaultTopicName, GetSubName(eventName));
    }

    private ISubscriptionClient CreateSubsIfNotExist(string eventName)
    {
        var subClient = CreateSubsClient(eventName);

        var exist = _managementClient.SubscriptionExistsAsync(_eventBusConfig.DefaultTopicName, GetSubName(eventName)).GetAwaiter().GetResult();

        if (!exist)
        {
            _managementClient.CreateSubscriptionAsync(_eventBusConfig.DefaultTopicName, GetSubName(eventName)).GetAwaiter().GetResult();
            RemoveDefaultRule(subClient);
        }
        CreateRuleIfNotExist(ProcessEventName(eventName), subClient);

        return subClient;


    }

    private void CreateRuleIfNotExist(string eventName, ISubscriptionClient subscriptionClient)
    {
        bool ruleExist;

        try
        {
            var rule = _managementClient.GetRuleAsync(_eventBusConfig.DefaultTopicName, eventName, eventName).GetAwaiter().GetResult();
            ruleExist = rule != null;

        }
        catch (MessagingEntityNotFoundException)
        {

            ruleExist = false;
        }

        if (!ruleExist)
        {
            subscriptionClient.AddRuleAsync(new RuleDescription
            {
                Name = eventName,
                Filter = new CorrelationFilter { Label = eventName }
            }).GetAwaiter().GetResult();
        }
    }

    private void RemoveDefaultRule(SubscriptionClient subscriptionClient)
    {
        try
        {
            subscriptionClient.RemoveRuleAsync(RuleDescription.DefaultRuleName).GetAwaiter().GetResult();
        }
        catch (MessagingEntityNotFoundException)
        {
            _logger.LogWarning("The messaging entity {DefaultRuleName} Could not be found", RuleDescription.DefaultRuleName);
            throw;
        }
    }

    public override void Dispose()
    {
        base.Dispose();

        _topicClient.CloseAsync().GetAwaiter().GetResult();
        _managementClient.CloseAsync().GetAwaiter().GetResult();
        _topicClient = null;
        _managementClient = null;

    }

}
