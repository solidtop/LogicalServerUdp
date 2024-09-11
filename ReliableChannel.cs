using System.Threading.Channels;

namespace LogicalServerUdp
{
    internal class ReliableChannel
    {
        private readonly Channel<Packet> _channel = Channel.CreateUnbounded<Packet>();
        private readonly SortedDictionary<int, Packet> _packetBuffer = [];
        private readonly HashSet<int> _missingPackets = [];
        private int _expectedSequenceNumber = 0;

        public async Task EnqueuePacketAsync(Packet packet, CancellationToken cancellationToken)
        {
            await _channel.Writer.WriteAsync(packet, cancellationToken);
        }

        public async Task ProcessPacketsAsync(Action<Packet> onProcess, Func<int, Task> onAck, CancellationToken cancellationToken)
        {
            await foreach (var packet in _channel.Reader.ReadAllAsync(cancellationToken))
            {
                int sequenceNumber = packet.SequenceNumber;

                if (sequenceNumber == _expectedSequenceNumber)
                {
                    onProcess(packet);
                    await onAck(sequenceNumber);
                    _expectedSequenceNumber++;

                    ProcessBufferedPackets(onProcess, onAck);

                    _missingPackets.Remove(sequenceNumber);
                }
                else if (sequenceNumber > _expectedSequenceNumber)
                {
                    if (!_packetBuffer.ContainsKey(sequenceNumber))
                    {
                        _packetBuffer[sequenceNumber] = packet;

                        for (int i = _expectedSequenceNumber; i < sequenceNumber; i++)
                        {
                            _missingPackets.Add(i);
                        }
                    }
                }
                else
                {
                    Console.WriteLine("Duplicated packet");
                }
            }
        }

        private async void ProcessBufferedPackets(Action<Packet> onProcess, Func<int, Task> onAck)
        {
            while (_packetBuffer.ContainsKey(_expectedSequenceNumber))
            {
                var packet = _packetBuffer[_expectedSequenceNumber];
                _packetBuffer.Remove(_expectedSequenceNumber);

                onProcess(packet);
                await onAck(_expectedSequenceNumber);
                _expectedSequenceNumber++;
            }
        }

        public void Complete() => _channel.Writer.Complete();
    }
}
