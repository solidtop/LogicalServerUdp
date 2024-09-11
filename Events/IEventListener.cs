using MessagePack;

namespace LogicalServerUdp.Events
{
    public interface IEventListener
    {
        void OnClientConnected(Client client);
        void OnClientDisconnected(Client client, string? reason);
        void OnPacketReceived(Client client, MessagePackReader reader);
    }
}
