using System.Net;
using LogicalServerUdp.Events;

namespace LogicalServerUdp
{
    public class Client
    {
        private readonly NetManager _netManager;
        private readonly SequencedChannel _sequencedChannel = new();
        private readonly CancellationTokenSource _tokenSource = new();
        private int _sequenceNumber = 0;

        private readonly byte[] _pongPacket = Packet.Create(PacketType.Pong).ToBytes();

        public IPEndPoint EndPoint { get; }

        public Client(IPEndPoint endPoint, NetManager netManager)
        {
            EndPoint = endPoint;
            _netManager = netManager;

            var token = _tokenSource.Token;

            Task.Run(() => _sequencedChannel.ProcessPacketsAsync(packet =>
                _netManager.RaiseEvent(EventType.ReceivePacket, this, packet), token), token);
        }

        internal async Task ProcessPacketAsync(Packet packet, CancellationToken cancellationToken)
        {
            switch (packet.Type)
            {
                case PacketType.Unreliable:
                    _netManager.RaiseEvent(EventType.ReceivePacket, this, packet);
                    break;
                case PacketType.Reliable:

                    break;
                case PacketType.Sequenced:
                    await _sequencedChannel.EnqueuePacketAsync(packet, cancellationToken);
                    break;
                case PacketType.Ping:
                    await _netManager.SendAsync(_pongPacket, EndPoint);
                    break;
            }
        }

        public async Task SendAsync(byte[] data, DeliveryMethod deliveryMethod)
        {
            var packet = new Packet(PacketType.Sequenced, _sequenceNumber++, data);
            await _netManager.SendAsync(packet.ToBytes(), EndPoint);
        }
    }
}
