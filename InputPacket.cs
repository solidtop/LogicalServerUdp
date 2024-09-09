using MessagePack;

namespace LogicalServerUdp
{
    [MessagePackObject]
    public class InputPacket(string message)
    {
        [Key(0)]
        public string Message { get; } = message;
    }
}
