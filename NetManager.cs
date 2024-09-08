using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace LogicalServerUdp
{
    public class NetManager
    {
        private readonly CancellationTokenSource _tokenSource = new();
        private readonly ConcurrentDictionary<IPEndPoint, Client> _clients = [];
        private Task? _receiveTask;
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
        }

        public void Stop()
        {
            if (!_isRunning)
                return;

            Console.WriteLine("Server shutting down...");
            _isRunning = false;
            _tokenSource.Cancel();
            _receiveTask?.Wait();
            _server?.Close();
        }

        public void Send(Packet packet, IPEndPoint endpoint)
        {
            var data = packet.ToBytes();
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
                        var endpoint = result.RemoteEndPoint;

                        if (!_clients.TryGetValue(endpoint, out var client))
                        {
                            client = new Client(endpoint, this);
                            _clients.TryAdd(endpoint, client);
                        }

                        var packet = Packet.FromBytes(result.Buffer);
                        client.ProcessPacket(packet, cancellationToken);
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

        private void ProcessPacket(Client client, byte[] buffer, CancellationToken cancellationToken)
        {
            var packet = Packet.FromBytes(buffer);

            switch (packet.Type)
            {
                case PacketType.Connect:
                    break;
                case PacketType.Disconnect:
                    break;
                case PacketType.Ping:
                    break;
            }

            Console.WriteLine($"Received packet: {packet.Type}");
            Console.WriteLine($"Payload size: {packet.Payload.Length}");
        }
    }
}
