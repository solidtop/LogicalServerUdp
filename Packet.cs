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
            writer.Write(SequenceNumber);

            if (Payload.Length > 0)
            {
                writer.Write(Payload);
            }

            writer.Flush();
            return buffer.WrittenSpan.ToArray();
        }

        public static Packet FromBytes(byte[] buffer)
        {
            var reader = new MessagePackReader(buffer);

            var type = (PacketType)reader.ReadByte();
            var sequenceNumber = reader.ReadInt32();

            var headerSize = reader.Consumed;
            var payloadSize = buffer.Length - headerSize;

            if (payloadSize > 0)
            {
                byte[] payload = new byte[payloadSize];
                Array.Copy(buffer, headerSize, payload, 0, payloadSize);
                return new Packet(type, sequenceNumber, payload);
            }

            return new Packet(type, sequenceNumber, []);
        }

        public static Packet Create(PacketType type)
        {
            return new Packet(type, 0, []);
        }
    }
}
