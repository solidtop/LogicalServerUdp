using LogicalServerUdp.Events;
using MessagePack;

namespace LogicalServerUdp
{
    public class TestServer : IEventListener
    {
        private readonly NetManager _netManager;

        public TestServer()
        {
            _netManager = new(this);
        }

        public void StartServer()
        {
            _netManager.Start(8000);

            Console.WriteLine("Server started... Press enter to stop");

            Console.ReadLine();

            _netManager.Stop();
        }

        public void OnPacketReceived(Client client, MessagePackReader reader)
        {
            var magicNum = reader.ReadUInt16();
            Console.WriteLine($"Received packet from {client.EndPoint}: {magicNum}");

            //var inputPacket = new InputPacket("Hello client! This is amazing");
            //var data = Serialize(inputPacket);
            ////_ = client.SendAsync(data);

            //_ = _netManager.SendToAll(data);
        }

        private byte[] Serialize(object packet)
        {
            return MessagePackSerializer.Serialize(packet);
        }
    }
}
