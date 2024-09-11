using System.Net;
using LogicalServerUdp.Events;

namespace LogicalServerUdp
{
    public class Client : IDisposable
    {
        private readonly NetManager _netManager;
        private readonly Task _reliableTask;
        private readonly Task _sequencedTask;
        private readonly ReliableChannel _reliableChannel = new();
        private readonly SequencedChannel _sequencedChannel = new();
        private readonly CancellationTokenSource _tokenSource = new();
        private int _sequenceNumber = 0;

        private readonly byte[] _pingPacket = Packet.Create(PacketType.Ping).ToBytes();
        private readonly byte[] _pongPacket = Packet.Create(PacketType.Pong).ToBytes();

        public IPEndPoint EndPoint { get; }
        public DateTime LastPingTime { get; private set; } = DateTime.UtcNow;

        public Client(IPEndPoint endPoint, NetManager netManager)
        {
            EndPoint = endPoint;
            _netManager = netManager;

            var token = _tokenSource.Token;

            _reliableTask = Task.Run(() => _reliableChannel.ProcessPacketsAsync(packet =>
                _netManager.RaiseEvent(EventType.Receive, this, packet), SendAckAsync, token), token);

            _sequencedTask = Task.Run(() => _sequencedChannel.ProcessPacketsAsync(packet =>
                _netManager.RaiseEvent(EventType.Receive, this, packet), token), token);
        }

        internal async Task ProcessPacketAsync(byte[] buffer, CancellationToken cancellationToken)
        {
            var packet = Packet.FromBytes(buffer);

            switch (packet.Type)
            {
                case PacketType.Connect:

                    break;
                case PacketType.Disconnect:
                    _netManager.RaiseEvent(EventType.Disconnect, this, packet);
                    break;
                case PacketType.Reliable:
                    await _reliableChannel.EnqueuePacketAsync(packet, cancellationToken);
                    break;
                case PacketType.Sequenced:
                    await _sequencedChannel.EnqueuePacketAsync(packet, cancellationToken);
                    break;
                case PacketType.Unreliable:
                    _netManager.RaiseEvent(EventType.Receive, this, packet);
                    break;
                case PacketType.Ping:
                    LastPingTime = DateTime.UtcNow;
                    await _netManager.SendAsync(_pongPacket, EndPoint);
                    break;
            }
        }

        public void Send(byte[] data, DeliveryMethod deliveryMethod)
        {
            _ = SendCoreAsync(data, deliveryMethod);
        }

        public async Task SendAsync(byte[] data, DeliveryMethod deliveryMethod)
        {
            await SendCoreAsync(data, deliveryMethod);
        }

        private async Task SendCoreAsync(byte[] data, DeliveryMethod deliveryMethod)
        {
            var packet = deliveryMethod switch
            {
                DeliveryMethod.Reliable => Packet.Create(PacketType.Reliable, _sequenceNumber++, data),
                DeliveryMethod.Sequenced => Packet.Create(PacketType.Sequenced, _sequenceNumber++, data),
                DeliveryMethod.Unreliable => Packet.Create(PacketType.Unreliable, data),
                _ => Packet.Create(PacketType.Unreliable, data),
            };

            await _netManager.SendAsync(packet.ToBytes(), EndPoint);
        }

        private async Task SendAckAsync(int sequenceNumber)
        {
            Console.WriteLine("Sending acknowledgment");
            var ackPacket = Packet.CreateAck(sequenceNumber);
            await _netManager.SendAsync(ackPacket.ToBytes(), EndPoint);
        }

        public void Dispose()
        {
            _reliableChannel.Complete();
            _sequencedChannel.Complete();
            _reliableTask.Wait();
            _sequencedTask.Wait();
            _tokenSource.Cancel();

            GC.SuppressFinalize(this);
        }
    }
}
