using EventBus.Base;
using EventBus.Base.Events;
using Polly;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using RabbitMQ.Client.Exceptions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace EventBus.RabbitMQ;

public class EventBusRabbitMQ : BaseEventBus
{
    RabbitMQPersistanceConnection persistanceConnection;
    private readonly IConnectionFactory _connectionFactory;
    private readonly IModel _consumerChannel;

    public EventBusRabbitMQ(EventBusConfig eventBusConfig, IServiceProvider serviceProvider) : base(eventBusConfig, serviceProvider)
    {

        if(_eventBusConfig != null)
        {
            var connJson = JsonSerializer.Serialize(eventBusConfig.Connection, new JsonSerializerOptions()
            {
                ReferenceHandler = ReferenceHandler.IgnoreCycles

            });

            _connectionFactory = JsonSerializer.Deserialize<ConnectionFactory>(connJson);
        }
        else
            _connectionFactory = new ConnectionFactory();


        persistanceConnection = new RabbitMQPersistanceConnection(_connectionFactory, eventBusConfig.ConnectionRetryCount);

        _consumerChannel = CreateConsumerChannel();

        _subscriptionManager.OnEventRemoved += SubscriptionManager_OnEventRemoved;

    }

    private void SubscriptionManager_OnEventRemoved(object sender, string eventName)
    {
        eventName = ProcessEventName(eventName);

        if (!persistanceConnection.IsConnected)
        {
            persistanceConnection.TryConnect();
        }

        _consumerChannel.QueueUnbind(eventName, _eventBusConfig.DefaultTopicName, eventName);

        if (_subscriptionManager.IsEmpty)
            _consumerChannel.Close();
       
    }

    public override void Publish(IntegrationEvent integrationEvent)
    {
        if (!persistanceConnection.IsConnected)
        {
            persistanceConnection.TryConnect();
        }

        var policy = Policy.Handle<BrokerUnreachableException>().Or<SocketException>().WaitAndRetry(_eventBusConfig.ConnectionRetryCount,retryAttempt => TimeSpan.FromSeconds(Math.Pow(2,retryAttempt)),
            (ex, time) =>
            {

            });

        var eventName = integrationEvent.GetType().Name;

        eventName = ProcessEventName(eventName);

        _consumerChannel.ExchangeDeclare(_eventBusConfig.DefaultTopicName, "direct");

        var message = JsonSerializer.Serialize(integrationEvent);
        var body = Encoding.UTF8.GetBytes(message);

        policy.Execute(() =>
        {
            var properties = _consumerChannel.CreateBasicProperties();
            properties.DeliveryMode = 2;
            _consumerChannel.QueueDeclare(GetSubName(eventName), true, false, false, null);

            _consumerChannel.BasicPublish(_eventBusConfig.DefaultTopicName, eventName, true, properties, body);

        });

    }

    public override void Subscribe<T, TH>()
    {
        var eventName = typeof(T).Name;
        eventName = ProcessEventName(eventName);

        if (!_subscriptionManager.HasSubscriptionForEvent(eventName))
        {
            if (!persistanceConnection.IsConnected)
            {
                persistanceConnection.TryConnect();
            }

            _consumerChannel.QueueDeclare(GetSubName(eventName),
                true, false, false, null);
            _consumerChannel.QueueBind(GetSubName(eventName), _eventBusConfig.DefaultTopicName, eventName);
        }


        _subscriptionManager.AddSubscription<T, TH>();
        StartBasicConsumer(eventName);
    }

    public override void UnSubscribe<T, TH>()
    {
        _subscriptionManager.RemoveSubscription<T,TH>();    
    }

    private IModel CreateConsumerChannel()
    {
        if (!persistanceConnection.IsConnected)
            persistanceConnection.TryConnect(); 

        

        var channel = persistanceConnection.CreateModel();
        channel.ExchangeDeclare(exchange: _eventBusConfig.DefaultTopicName, type: "direct");

        return channel;
    }

    private void StartBasicConsumer(string eventName)
    {
        if (_consumerChannel != null)
        {
            var consumer = new EventingBasicConsumer(_consumerChannel);
            consumer.Received += Consumer_Received;

            _consumerChannel.BasicConsume(GetSubName(eventName),
                false, consumer);
        } 
    }

    private async void Consumer_Received(object sender, BasicDeliverEventArgs e)
    {
        var eventName = e.RoutingKey;
        eventName = ProcessEventName(eventName);
        var message = Encoding.UTF8.GetString(e.Body.Span);

        try
        {
            await ProcessEvent(eventName, message); 
        }
        catch (Exception)
        {

            throw;
        }
        _consumerChannel.BasicAck(e.DeliveryTag,false); 
    }
}
