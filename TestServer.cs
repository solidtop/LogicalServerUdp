using LogicalServerUdp.Events;
using MessagePack;

namespace LogicalServerUdp
{
    public class TestServer : IEventListener
    {
        private readonly NetManager _netManager;
        private readonly NetStatistics _statistics = new();

        public TestServer()
        {
            _netManager = new(this);
        }

        public void StartServer()
        {
            _netManager.Start(8000);
        }

        public void OnClientConnected(Client client)
        {
            Console.WriteLine($"Client {client.EndPoint} connected");
        }

        public void OnClientDisconnected(Client client, string? reason)
        {
            Console.WriteLine($"Client disconnected with reason: {reason}");
        }

        public void OnPacketReceived(Client client, MessagePackReader reader)
        {
            var magicNum = reader.ReadString();
            Console.WriteLine($"Received packet from {client.EndPoint}: {magicNum}");

            //var inputPacket = new InputPacket("Hello client! This is amazing");
            //var data = Serialize(inputPacket);

            //_netManager.SendToAll(data, DeliveryMethod.Reliable);
            var data = Serialize(69);
            client.Send(data, DeliveryMethod.Sequenced);
        }

        private byte[] Serialize(object packet)
        {
            return MessagePackSerializer.Serialize(packet);
        }
    }
}
