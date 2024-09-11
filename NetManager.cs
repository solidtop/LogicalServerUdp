using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Threading.Channels;
using LogicalServerUdp.Events;
using MessagePack;
using Microsoft.Extensions.Logging;

namespace LogicalServerUdp
{
    public class NetManager(IEventListener eventListener) : IDisposable
    {
        private readonly CancellationTokenSource _tokenSource = new();
        private readonly ConcurrentDictionary<IPEndPoint, Client> _clients = [];
        private readonly IEventListener _eventListener = eventListener;
        private readonly Channel<(byte[], IPEndPoint)> _sendChannel = Channel.CreateUnbounded<(byte[], IPEndPoint)>();
        private UdpClient? _server;
        private bool _isRunning;

        private Task? _receiveTask;
        private Task? _sendTask;
        private Task? _timeoutTask;

        private readonly TimeSpan _timeout = TimeSpan.FromSeconds(30);
        private readonly TimeSpan _timeoutInterval = TimeSpan.FromSeconds(5);

        private readonly ILogger<NetManager> _logger = NetLoggerFactory.CreateLogger<NetManager>();

        public void Start(int port)
        {
            if (_isRunning)
                return;

            _logger.LogInformation("Server started on port {port}... Press enter to stop.", port);

            _isRunning = true;

            _server = new(port);

            var token = _tokenSource.Token;
            _receiveTask = Task.Run(() => ReceivePacketsAsync(token), token);
            _sendTask = Task.Run(() => ProcessOutgoingPacketsAsync(token), token);
            _timeoutTask = Task.Run(() => TimeoutClientsAsync(token), token);

            Console.ReadLine();

            Stop();
        }

        public void Stop()
        {
            if (!_isRunning)
                return;

            _logger.LogInformation("Server shutting down...");
            Dispose();
        }

        private async Task ReceivePacketsAsync(CancellationToken cancellationToken)
        {
            if (_server is null)
            {
                throw new NullReferenceException("UdpClient");
            }

            try
            {
                while (_isRunning && !cancellationToken.IsCancellationRequested)
                {
                    try
                    {
                        var result = await _server.ReceiveAsync(cancellationToken);
                        var endPoint = result.RemoteEndPoint;

                        if (!_clients.TryGetValue(endPoint, out var client))
                        {
                            client = new Client(endPoint, this);
                            _clients.TryAdd(endPoint, client);
                            _eventListener.OnClientConnected(client);
                        }

                        await client.ProcessPacketAsync(result.Buffer, cancellationToken);
                    }
                    catch (OperationCanceledException)
                    {

                    }
                    catch (ObjectDisposedException)
                    {
                        return;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error while processing packet.");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while receiving packets.");
            }
            finally
            {
                Stop();
            }
        }

        private async Task ProcessOutgoingPacketsAsync(CancellationToken cancellationToken)
        {
            try
            {
                await foreach (var (data, endPoint) in _sendChannel.Reader.ReadAllAsync(cancellationToken))
                {
                    await SendCoreAsync(data, endPoint);
                }
            }
            catch (OperationCanceledException)
            {

            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing outgoing packet.");
            }
        }

        internal void RaiseEvent(EventType type, Client client, Packet packet)
        {
            var reader = new MessagePackReader(packet.Payload);

            switch (type)
            {
                case EventType.Connect:
                    break;
                case EventType.Disconnect:
                    var reason = reader.ReadString();
                    DisconnectClient(client, reason);
                    break;
                case EventType.Receive:
                    _eventListener.OnPacketReceived(client, reader);
                    break;
            }
        }

        internal void RaiseReceiveEvent(Client client, Packet packet)
        {
            var reader = new MessagePackReader(packet.Payload);
            _eventListener.OnPacketReceived(client, reader);
        }

        internal void DisconnectClient(Client client, string? reason)
        {
            _logger.LogDebug("Disconnecting client {endPoint}", client.EndPoint);

            _eventListener.OnClientDisconnected(client, reason);

            _clients.TryRemove(client.EndPoint, out _);

            client.Dispose();
        }

        private async Task TimeoutClientsAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var now = DateTime.UtcNow;

                foreach (var client in _clients.Values)
                {
                    if (now - client.LastPingTime > _timeout)
                    {
                        _logger.LogDebug("Client {endPoint} time out.", client.EndPoint);
                        DisconnectClient(client, "Client timed out.");
                    }
                }

                await Task.Delay(_timeoutInterval, cancellationToken);
            }
        }

        public async Task SendAsync(byte[] data, IPEndPoint endPoint)
        {
            try
            {
                await _sendChannel.Writer.WriteAsync((data, endPoint), _tokenSource.Token);
            }
            catch (ChannelClosedException)
            {
                _logger.LogDebug("Send channel is closed, unable to send packet.");
            }
        }

        public void Send(byte[] data, IPEndPoint endPoint)
        {
            _ = SendAsync(data, endPoint);
        }

        public void SendToAll(byte[] data, DeliveryMethod deliveryMethod)
        {
            SendToAll(data, deliveryMethod, []);
        }

        public void SendToAll(byte[] data, DeliveryMethod deliveryMethod, IReadOnlyList<IPEndPoint> excludedEndPoints)
        {
            foreach (var pair in _clients)
            {
                var endPoint = pair.Key;

                if (excludedEndPoints.Contains(endPoint))
                {
                    continue;
                }

                var client = pair.Value;
                client.Send(data, deliveryMethod);
            }
        }

        public async Task SendToAllAsync(byte[] data, DeliveryMethod deliveryMethod, IReadOnlyList<IPEndPoint> excludedEndPoints)
        {
            var sendTasks = new List<Task>();

            foreach (var pair in _clients)
            {
                var endPoint = pair.Key;

                if (excludedEndPoints.Contains(endPoint))
                    continue;

                var client = pair.Value;
                sendTasks.Add(client.SendAsync(data, deliveryMethod));
            }

            await Task.WhenAll(sendTasks);
        }

        private async Task SendCoreAsync(byte[] data, IPEndPoint endPoint)
        {
            if (_server is null)
                return;

            try
            {
                await _server.SendAsync(data, data.Length, endPoint);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while sending packet to {endPoint}", endPoint);
            }
        }

        public void Dispose()
        {
            _isRunning = false;
            _tokenSource.Cancel();
            _receiveTask?.Wait();
            _sendTask?.Wait();
            _timeoutTask?.Dispose();
            _server?.Close();

            GC.SuppressFinalize(this);
        }
    }
}
