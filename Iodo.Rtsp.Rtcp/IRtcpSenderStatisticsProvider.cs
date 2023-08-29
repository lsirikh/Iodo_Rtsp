using System;

namespace Iodo.Rtsp.Rtcp;

internal interface IRtcpSenderStatisticsProvider
{
	DateTime LastTimeReportReceived { get; }

	long LastNtpTimeReportReceived { get; }
}
