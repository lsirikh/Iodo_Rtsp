using System;
using System.Collections.Generic;
using System.Threading;

namespace Iodo.Rtsp.Rtcp;

internal class RtcpStream : ITransportStream, IRtcpSenderStatisticsProvider
{
	private long _lastNtpTimeReportReceived;

	private long _lastTimeReportReceivedTicks = DateTime.MinValue.Ticks;

	public DateTime LastTimeReportReceived => new DateTime(Interlocked.Read(ref _lastTimeReportReceivedTicks));

	public long LastNtpTimeReportReceived => Interlocked.Read(ref _lastNtpTimeReportReceived);

	public event EventHandler SessionShutdown;

	public void Process(ArraySegment<byte> payloadSegment)
	{
		IEnumerable<RtcpPacket> enumerable = RtcpPacket.Parse(payloadSegment);
		foreach (RtcpPacket item in enumerable)
		{
			RtcpPacket rtcpPacket = item;
			RtcpPacket rtcpPacket2 = rtcpPacket;
			if (!(rtcpPacket2 is RtcpSenderReportPacket rtcpSenderReportPacket))
			{
				if (rtcpPacket2 is RtcpByePacket)
				{
					this.SessionShutdown?.Invoke(this, EventArgs.Empty);
				}
			}
			else
			{
				Interlocked.Exchange(ref _lastNtpTimeReportReceived, rtcpSenderReportPacket.NtpTimestamp);
				Interlocked.Exchange(ref _lastTimeReportReceivedTicks, DateTime.UtcNow.Ticks);
			}
		}
	}
}
