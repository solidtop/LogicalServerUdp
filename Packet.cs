using System.Buffers;
using MessagePack;

namespace LogicalServerUdp
{
    public class Packet(PacketType type, int sequenceNumber, byte[] payload)
    {
        public PacketType Type { get; } = type;
        public int SequenceNumber { get; } = sequenceNumber;
        public byte[] Payload { get; } = payload;

        public byte[] ToBytes()
        {
            int headerSize = 5;
            int estimatedSize = headerSize + Payload.Length;

            var buffer = new ArrayBufferWriter<byte>(estimatedSize);
            var writer = new MessagePackWriter(buffer);

            writer.Write((byte)Type);

            switch (Type)
            {
                case PacketType.Reliable:
                case PacketType.Sequenced:
                    writer.Write(SequenceNumber);
                    break;
            }

            writer.Flush();

            if (Payload.Length > 0)
            {
                buffer.Write(Payload);
            }

            return buffer.WrittenSpan.ToArray();
        }

        public static Packet FromBytes(byte[] buffer)
        {
            var reader = new MessagePackReader(buffer);

            var type = (PacketType)reader.ReadByte();

            var sequenceNumber = 0;

            switch (type)
            {
                case PacketType.Ping:
                    return Create(type);
                case PacketType.Reliable:
                case PacketType.Sequenced:
                    sequenceNumber = reader.ReadInt32();
                    break;
            }

            int headerSize = (int)reader.Consumed;
            int payloadSize = buffer.Length - headerSize;

            if (payloadSize > 0)
            {
                ReadOnlySpan<byte> payload = buffer.AsSpan(headerSize, payloadSize);
                return new Packet(type, sequenceNumber, payload.ToArray());
            }

            return new Packet(type, sequenceNumber, []);
        }

        public static Packet Create(PacketType type, int sequenceNumber, byte[] payload)
        {
            return new Packet(type, sequenceNumber, payload);
        }

        public static Packet Create(PacketType type, byte[] payload)
        {
            return new Packet(type, 0, payload);
        }

        public static Packet Create(PacketType type)
        {
            return new Packet(type, 0, []);
        }

        public static Packet CreateAck(int sequenceNumber)
        {
            return Create(PacketType.Ack, sequenceNumber, []);
        }
    }
}
