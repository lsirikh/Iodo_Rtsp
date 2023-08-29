using System;
using System.Collections.Generic;
using System.IO;

namespace Iodo.Rtsp.Rtcp;

internal class RtcpReceiverReportPacket : RtcpPacket, ISerializablePacket
{
	public uint SyncSourceId { get; }

	public IReadOnlyList<RtcpReportBlock> Reports { get; }

	public RtcpReceiverReportPacket(uint syncSourceId, IReadOnlyList<RtcpReportBlock> reports)
	{
		SyncSourceId = syncSourceId;
		Reports = reports;
		base.PaddingFlag = false;
		base.SourceCount = reports.Count;
		base.PayloadType = 201;
		base.DwordLength = (4 + reports.Count * 24) / 4;
		base.Length = (base.DwordLength + 1) * 4;
	}

	protected override void FillFromByteSegment(ArraySegment<byte> byteSegment)
	{
	}

	public new void Serialize(Stream stream)
	{
		base.Serialize(stream);
		stream.WriteByte((byte)(SyncSourceId >> 24));
		stream.WriteByte((byte)(SyncSourceId >> 16));
		stream.WriteByte((byte)(SyncSourceId >> 8));
		stream.WriteByte((byte)SyncSourceId);
		foreach (RtcpReportBlock report in Reports)
		{
			stream.WriteByte((byte)(report.SyncSourceId >> 24));
			stream.WriteByte((byte)(report.SyncSourceId >> 16));
			stream.WriteByte((byte)(report.SyncSourceId >> 8));
			stream.WriteByte((byte)report.SyncSourceId);
			stream.WriteByte((byte)report.FractionLost);
			stream.WriteByte((byte)(report.CumulativePacketLost >> 16));
			stream.WriteByte((byte)(report.CumulativePacketLost >> 8));
			stream.WriteByte((byte)report.CumulativePacketLost);
			stream.WriteByte((byte)(report.ExtHighestSequenceNumberReceived >> 24));
			stream.WriteByte((byte)(report.ExtHighestSequenceNumberReceived >> 16));
			stream.WriteByte((byte)(report.ExtHighestSequenceNumberReceived >> 8));
			stream.WriteByte((byte)report.ExtHighestSequenceNumberReceived);
			stream.WriteByte((byte)(report.Jitter >> 24));
			stream.WriteByte((byte)(report.Jitter >> 16));
			stream.WriteByte((byte)(report.Jitter >> 8));
			stream.WriteByte((byte)report.Jitter);
			stream.WriteByte((byte)(report.LastNtpTimeSenderReportReceived >> 24));
			stream.WriteByte((byte)(report.LastNtpTimeSenderReportReceived >> 16));
			stream.WriteByte((byte)(report.LastNtpTimeSenderReportReceived >> 8));
			stream.WriteByte((byte)report.LastNtpTimeSenderReportReceived);
			stream.WriteByte((byte)(report.DelaySinceLastTimeSenderReportReceived >> 24));
			stream.WriteByte((byte)(report.DelaySinceLastTimeSenderReportReceived >> 16));
			stream.WriteByte((byte)(report.DelaySinceLastTimeSenderReportReceived >> 8));
			stream.WriteByte((byte)report.DelaySinceLastTimeSenderReportReceived);
		}
	}
}
