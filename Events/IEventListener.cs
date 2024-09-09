using MessagePack;

namespace LogicalServerUdp.Events
{
    public interface IEventListener
    {
        void OnPacketReceived(Client client, MessagePackReader reader);
    }
}
