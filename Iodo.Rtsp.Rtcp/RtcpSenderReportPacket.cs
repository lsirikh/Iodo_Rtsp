using System;
using Iodo.Rtsp.Utils;

namespace Iodo.Rtsp.Rtcp;

internal class RtcpSenderReportPacket : RtcpPacket
{
	public uint SyncSourceId { get; private set; }

	public long NtpTimestamp { get; private set; }

	protected override void FillFromByteSegment(ArraySegment<byte> byteSegment)
	{
		SyncSourceId = BigEndianConverter.ReadUInt32(byteSegment.Array, byteSegment.Offset);
		uint num = BigEndianConverter.ReadUInt32(byteSegment.Array, byteSegment.Offset + 4);
		uint num2 = BigEndianConverter.ReadUInt32(byteSegment.Array, byteSegment.Offset + 8);
		NtpTimestamp = (long)(((ulong)num << 32) | num2);
	}
}
