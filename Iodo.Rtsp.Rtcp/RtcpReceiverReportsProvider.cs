using System;
using System.Collections.Generic;
using Iodo.Rtsp.Rtp;

namespace Iodo.Rtsp.Rtcp;

internal class RtcpReceiverReportsProvider
{
	private readonly IRtpStatisticsProvider _rtpStatisticsProvider;

	private readonly IRtcpSenderStatisticsProvider _rtcpSenderStatisticsProvider;

	private readonly uint _senderSyncSourceId;

	private readonly string _machineName;

	public RtcpReceiverReportsProvider(IRtpStatisticsProvider rtpStatisticsProvider, IRtcpSenderStatisticsProvider rtcpSenderStatisticsProvider, uint senderSyncSourceId)
	{
		_rtpStatisticsProvider = rtpStatisticsProvider ?? throw new ArgumentNullException("rtpStatisticsProvider");
		_rtcpSenderStatisticsProvider = rtcpSenderStatisticsProvider ?? throw new ArgumentNullException("rtcpSenderStatisticsProvider");
		_senderSyncSourceId = senderSyncSourceId;
		_machineName = Environment.MachineName;
	}

	public IEnumerable<RtcpPacket> GetReportPackets()
	{
		yield return CreateReceiverReport();
		yield return CreateSdesReport();
	}

	private RtcpReceiverReportPacket CreateReceiverReport()
	{
		int fractionLost = ((_rtpStatisticsProvider.PacketsReceivedSinceLastReset != 0) ? (_rtpStatisticsProvider.PacketsLostSinceLastReset * 256 / _rtpStatisticsProvider.PacketsReceivedSinceLastReset) : 0);
		uint lastNtpTimeSenderReportReceived;
		uint delaySinceLastTimeSenderReportReceived;
		if (_rtcpSenderStatisticsProvider.LastTimeReportReceived == DateTime.MinValue)
		{
			lastNtpTimeSenderReportReceived = 0u;
			delaySinceLastTimeSenderReportReceived = 0u;
		}
		else
		{
			lastNtpTimeSenderReportReceived = (uint)((_rtcpSenderStatisticsProvider.LastNtpTimeReportReceived >> 16) & 0xFFFFFFFFu);
			delaySinceLastTimeSenderReportReceived = (uint)(DateTime.UtcNow - _rtcpSenderStatisticsProvider.LastTimeReportReceived).TotalSeconds * 65536;
		}
		uint extHighestSequenceNumberReceived = (uint)((_rtpStatisticsProvider.SequenceCycles << 16) | _rtpStatisticsProvider.HighestSequenceNumberReceived);
		RtcpReportBlock rtcpReportBlock = new RtcpReportBlock(_rtpStatisticsProvider.SyncSourceId, fractionLost, _rtpStatisticsProvider.CumulativePacketLost, extHighestSequenceNumberReceived, 0u, lastNtpTimeSenderReportReceived, delaySinceLastTimeSenderReportReceived);
		RtcpReceiverReportPacket result = new RtcpReceiverReportPacket(_senderSyncSourceId, new RtcpReportBlock[1] { rtcpReportBlock });
		_rtpStatisticsProvider.ResetState();
		return result;
	}

	private RtcpSdesReportPacket CreateSdesReport()
	{
		RtcpSdesNameItem[] items = new RtcpSdesNameItem[1]
		{
			new RtcpSdesNameItem(_machineName)
		};
		RtcpSdesChunk[] chunks = new RtcpSdesChunk[1]
		{
			new RtcpSdesChunk(_senderSyncSourceId, items)
		};
		return new RtcpSdesReportPacket(chunks);
	}
}
