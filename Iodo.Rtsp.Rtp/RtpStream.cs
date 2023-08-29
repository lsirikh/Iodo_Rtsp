using System;
using Iodo.Rtsp.MediaParsers;
using Iodo.Rtsp.Utils;

namespace Iodo.Rtsp.Rtp;

internal class RtpStream : ITransportStream, IRtpStatisticsProvider
{
	private readonly IRtpSequenceAssembler _rtpSequenceAssembler;

	private readonly IMediaPayloadParser _mediaPayloadParser;

	private readonly int _samplesFrequency;

	private ulong _samplesSum;

	private ushort _previousSeqNumber;

	private uint _previousTimestamp;

	private bool _isFirstPacket = true;

	public uint SyncSourceId { get; private set; }

	public ushort HighestSequenceNumberReceived { get; private set; }

	public int PacketsReceivedSinceLastReset { get; private set; }

	public int PacketsLostSinceLastReset { get; private set; }

	public uint CumulativePacketLost { get; private set; }

	public ushort SequenceCycles { get; private set; }

	public RtpStream(IMediaPayloadParser mediaPayloadParser, int samplesFrequency, IRtpSequenceAssembler rtpSequenceAssembler = null)
	{
		_mediaPayloadParser = mediaPayloadParser ?? throw new ArgumentNullException("mediaPayloadParser");
		_samplesFrequency = samplesFrequency;
		if (rtpSequenceAssembler != null)
		{
			_rtpSequenceAssembler = rtpSequenceAssembler;
			IRtpSequenceAssembler rtpSequenceAssembler2 = _rtpSequenceAssembler;
			rtpSequenceAssembler2.PacketPassed = (RefAction<RtpPacket>)Delegate.Combine(rtpSequenceAssembler2.PacketPassed, new RefAction<RtpPacket>(ProcessImmediately));
		}
	}

	public void Process(ArraySegment<byte> payloadSegment)
	{
		if (RtpPacket.TryParse(payloadSegment, out var rtpPacket))
		{
			if (_rtpSequenceAssembler != null)
			{
				_rtpSequenceAssembler.ProcessPacket(ref rtpPacket);
			}
			else
			{
				ProcessImmediately(ref rtpPacket);
			}
		}
	}

	private void ProcessImmediately(ref RtpPacket rtpPacket)
	{
		SyncSourceId = rtpPacket.SyncSourceId;
		if (!_isFirstPacket)
		{
			int num = (ushort)(rtpPacket.SeqNumber - _previousSeqNumber);
			if (num != 1)
			{
				int num2 = num - 1;
				if (num2 == -1)
				{
					num2 = 65535;
				}
				CumulativePacketLost += (uint)num2;
				if (CumulativePacketLost > 8388607)
				{
					CumulativePacketLost = 8388607u;
				}
				PacketsLostSinceLastReset += num2;
				_mediaPayloadParser.ResetState();
			}
			if (rtpPacket.SeqNumber < HighestSequenceNumberReceived)
			{
				ushort sequenceCycles = (ushort)(SequenceCycles + 1);
				SequenceCycles = sequenceCycles;
			}
			_samplesSum += rtpPacket.Timestamp - _previousTimestamp;
		}
		HighestSequenceNumberReceived = rtpPacket.SeqNumber;
		_isFirstPacket = false;
		int packetsReceivedSinceLastReset = PacketsReceivedSinceLastReset + 1;
		PacketsReceivedSinceLastReset = packetsReceivedSinceLastReset;
		_previousSeqNumber = rtpPacket.SeqNumber;
		_previousTimestamp = rtpPacket.Timestamp;
		if (rtpPacket.PayloadSegment.Count != 0)
		{
			TimeSpan timeOffset = ((_samplesFrequency != 0) ? new TimeSpan((long)(_samplesSum * 1000 / (uint)_samplesFrequency * 10000)) : TimeSpan.MinValue);
			_mediaPayloadParser.Parse(timeOffset, rtpPacket.PayloadSegment, rtpPacket.MarkerBit);
		}
	}

	public void ResetState()
	{
		PacketsLostSinceLastReset = 0;
		PacketsReceivedSinceLastReset = 0;
	}
}
