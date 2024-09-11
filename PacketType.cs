namespace LogicalServerUdp
{
    public enum PacketType : byte
    {
        Connect,
        Disconnect,
        Ack,
        Ping,
        Pong,
        Reliable,
        Sequenced,
        Unreliable,
    }
}
