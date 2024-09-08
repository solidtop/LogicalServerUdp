using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace LogicalServerUdp
{
    public class SequencedChannel
    {
        private readonly Channel<Packet> _channel = Channel.CreateUnbounded<Packet>();
        private readonly SortedDictionary<int, Packet> _packetBuffer = [];
        private int _expectedSequenceNumber = 0;

        public async Task EnqueuePacketAsync(Packet packet, CancellationToken cancellationToken)
        {
            await _channel.Writer.WriteAsync(packet, cancellationToken);
        }

        public async Task ProcessPacketsAsync(Action<Packet> onProcess, CancellationToken cancellationToken)
        {
            await foreach (var packet in _channel.Reader.ReadAllAsync(cancellationToken))
            {
                int sequenceNumber = packet.SequenceNumber;

                if (sequenceNumber == _expectedSequenceNumber)
                {
                    onProcess(packet);
                    _expectedSequenceNumber++;

                    ProcessBufferedPackets(onProcess);
                }
                else if (sequenceNumber > _expectedSequenceNumber)
                {
                    if (!_packetBuffer.ContainsKey(sequenceNumber))
                    {
                        _packetBuffer[sequenceNumber] = packet;
                    }
                }
            }
        }

        private void ProcessBufferedPackets(Action<Packet> onProcess)
        {
            while (_packetBuffer.ContainsKey(_expectedSequenceNumber))
            {
                var packet = _packetBuffer[_expectedSequenceNumber];
                _packetBuffer.Remove(_expectedSequenceNumber);

                onProcess(packet);
                _expectedSequenceNumber++;
            }
        }

        public void Complete() => _channel.Writer.Complete();
    }
}
