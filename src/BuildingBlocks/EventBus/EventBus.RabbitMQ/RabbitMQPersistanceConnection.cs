using Polly;
using RabbitMQ.Client;
using RabbitMQ.Client.Exceptions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace EventBus.RabbitMQ;

public class RabbitMQPersistanceConnection : IDisposable
{
    private IConnection _connection;
    private IConnectionFactory _connectionFactory;
    private readonly int tryCount;
    private object lock_object = new object();
    private bool _disposed;
    public bool IsConnected => _connection != null && _connection.IsOpen;

    public RabbitMQPersistanceConnection(IConnectionFactory connectionFactory,int tryCount=5)
    {
        _connectionFactory = connectionFactory;
        this.tryCount = tryCount;
    }

    public IModel CreateModel()
    {
        return _connection.CreateModel();
    }
    public bool TryConnect()
    {
        lock (lock_object)
        {
            var policy = Policy.Handle<SocketException>().Or<BrokerUnreachableException>().WaitAndRetry(tryCount, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)), (ex, time) =>
            {

            }


            );
            policy.Execute(() =>
            {

                _connection = _connectionFactory.CreateConnection();

            });

            if (IsConnected)
            {
                _connection.ConnectionShutdown += ConnectionConnectionShutdown;
                _connection.CallbackException += ConnectionCallbackException;
                _connection.ConnectionBlocked += ConnectionConnectionBlocked;

                return true;

            }
            return false;   
        }
    }

    private void ConnectionConnectionBlocked(object sender, global::RabbitMQ.Client.Events.ConnectionBlockedEventArgs e)
    {
        if(_disposed) return;
        TryConnect();

    }

    private void ConnectionCallbackException(object sender, global::RabbitMQ.Client.Events.CallbackExceptionEventArgs e)
    {
        if (_disposed) return;

        TryConnect();

    }

    private void ConnectionConnectionShutdown(object sender, ShutdownEventArgs e)
    {
        if (_disposed) return;

        TryConnect();
    }

    public void Dispose()
    {
        _disposed = true;   
        _connection.Dispose();
    }
}
