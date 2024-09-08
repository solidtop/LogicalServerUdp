using System;
using System.Net;
using System.Threading;

namespace LogicalServerUdp
{
    public class Client(IPEndPoint endPoint, NetManager netManager)
    {
        private readonly NetManager _netManager = netManager;
        private readonly SequencedChannel _sequencedChannel = new();

        public IPEndPoint EndPoint { get; } = endPoint;

        internal void ProcessPacket(Packet packet, CancellationToken cancellationToken)
        {
            switch (packet.Type)
            {
                case PacketType.Unreliable:

                    break;
                case PacketType.Reliable:

                    break;
                case PacketType.Sequenced:
                    _ = _sequencedChannel.EnqueuePacketAsync(packet, cancellationToken);
                    break;
                case PacketType.Ping:
                    var pongPacket = Packet.Create(PacketType.Pong);
                    _netManager.Send(pongPacket, EndPoint);
                    break;
            }

            Console.WriteLine($"Received packet: {packet.Type}");
            Console.WriteLine($"Payload size: {packet.Payload.Length}");
        }
    }
}
