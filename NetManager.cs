using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Threading.Channels;
using LogicalServerUdp.Events;
using MessagePack;

namespace LogicalServerUdp
{
    public class NetManager(IEventListener eventListener)
    {
        private readonly CancellationTokenSource _tokenSource = new();
        private readonly ConcurrentDictionary<IPEndPoint, Client> _clients = [];
        private readonly IEventListener _eventListener = eventListener;
        private readonly Channel<(byte[], IPEndPoint)> _sendChannel = Channel.CreateUnbounded<(byte[], IPEndPoint)>();
        private Task? _receiveTask;
        private Task? _sendTask;
        private UdpClient? _server;
        private bool _isRunning;

        public void Start(int port)
        {
            if (_isRunning)
                return;

            _isRunning = true;

            _server = new(port);

            var token = _tokenSource.Token;
            _receiveTask = Task.Run(() => ReceivePacketsAsync(token), token);
            _sendTask = Task.Run(() => ProcessOutgoingPacketsAsync(token), token);
        }

        public void Stop()
        {
            if (!_isRunning)
                return;

            Console.WriteLine("Server shutting down...");
            _isRunning = false;
            _tokenSource.Cancel();
            _receiveTask?.Wait();
            _sendTask?.Wait();
            _server?.Close();
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
                        }

                        await ProcessPacketAsync(client, result.Buffer, cancellationToken);
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
                        Console.WriteLine($"Error while processing packet: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error while receiving packets: {ex.Message}");
            }
            finally
            {
                Stop();
            }
        }

        private async Task ProcessPacketAsync(Client client, byte[] buffer, CancellationToken cancellationToken)
        {
            var packet = Packet.FromBytes(buffer);

            switch (packet.Type)
            {
                case PacketType.Connect:
                    break;
                case PacketType.Disconnect:
                    break;
                default:
                    await client.ProcessPacketAsync(packet, cancellationToken);
                    break;
            }
        }

        private async Task ProcessOutgoingPacketsAsync(CancellationToken cancellationToken)
        {
            try
            {
                await foreach (var (data, endPoint) in _sendChannel.Reader.ReadAllAsync(cancellationToken))
                {
                    await SendRawAsync(data, endPoint);
                }
            }
            catch (OperationCanceledException)
            {

            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in outgoing packet processing: {ex.Message}");
            }
        }

        internal void RaiseEvent(EventType type, Client client, Packet packet)
        {
            switch (type)
            {
                case EventType.Connect:
                    break;
                case EventType.Disconnect:
                    break;
                case EventType.ReceivePacket:
                    var reader = new MessagePackReader(packet.Payload);
                    _eventListener.OnPacketReceived(client, reader);
                    break;
            }
        }

        public async Task SendRawAsync(byte[] data, IPEndPoint endPoint)
        {
            if (_server is null)
                return;

            try
            {
                await _server.SendAsync(data, data.Length, endPoint);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error while sending packet to {endPoint}: {ex.Message}");
            }
        }

        public async Task SendAsync(byte[] data, IPEndPoint endpoint)
        {
            try
            {
                await _sendChannel.Writer.WriteAsync((data, endpoint), _tokenSource.Token);
            }
            catch (ChannelClosedException)
            {
                Console.WriteLine("Send channel is closed, unable to send packet.");
            }
        }

        public async Task SendToAll(byte[] data)
        {
            await SendToAll(data, []);
        }

        public async Task SendToAll(byte[] data, IReadOnlyList<IPEndPoint> excludedEndPoints)
        {
            foreach (var pair in _clients)
            {
                var endPoint = pair.Key;

                if (excludedEndPoints.Contains(endPoint))
                {
                    continue;
                }

                var client = pair.Value;
                await client.SendAsync(data);
            }
        }
    }
}
