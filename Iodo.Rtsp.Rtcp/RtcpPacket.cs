#define DEBUG
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using Iodo.Rtsp.Utils;

namespace Iodo.Rtsp.Rtcp;

internal abstract class RtcpPacket
{
	public int ProtocolVersion { get; protected set; } = 2;


	public bool PaddingFlag { get; protected set; }

	public int SourceCount { get; protected set; }

	public int PayloadType { get; protected set; }

	public int DwordLength { get; protected set; }

	public int Length { get; protected set; }

	protected abstract void FillFromByteSegment(ArraySegment<byte> byteSegment);

	protected void Serialize(Stream stream)
	{
		int num = (PaddingFlag ? 1 : 0);
		stream.WriteByte((byte)((uint)((ProtocolVersion << 6) | (num << 5)) | ((uint)SourceCount & 0x1Fu)));
		stream.WriteByte((byte)PayloadType);
		stream.WriteByte((byte)(DwordLength >> 8));
		stream.WriteByte((byte)DwordLength);
	}

	public static IEnumerable<RtcpPacket> Parse(ArraySegment<byte> byteSegment)
	{
		Debug.Assert(byteSegment.Array != null, "byteSegment.Array != null");
		int offset2 = byteSegment.Offset;
		int totalLength = byteSegment.Count;
		while (totalLength > 0)
		{
			int value = byteSegment.Array[offset2++];
			int version = value >> 6;
			int padding = (value >> 5) & 1;
			int sourceCount = value & 0x1F;
			int payloadType = byteSegment.Array[offset2++];
			int dwordLength = BigEndianConverter.ReadUInt16(byteSegment.Array, offset2);
			offset2 += 2;
			int payloadLength = dwordLength * 4;
			if (payloadLength > totalLength - 4)
			{
				throw new ArgumentException("Invalid RTCP packet size. It seems that data segment contains bad data", "byteSegment");
			}
			RtcpPacket packet;
			switch (payloadType)
			{
			case 200:
				packet = new RtcpSenderReportPacket();
				break;
			case 203:
				packet = new RtcpByePacket();
				break;
			default:
				offset2 += payloadLength;
				totalLength -= 4 + payloadLength;
				continue;
			}
			packet.ProtocolVersion = version;
			packet.PaddingFlag = padding != 0;
			packet.SourceCount = sourceCount;
			packet.PayloadType = payloadType;
			packet.DwordLength = dwordLength;
			packet.Length = (dwordLength + 1) * 4;
			ArraySegment<byte> segment = new ArraySegment<byte>(byteSegment.Array, offset2, payloadLength);
			packet.FillFromByteSegment(segment);
			yield return packet;
			offset2 += payloadLength;
			totalLength -= 4 + payloadLength;
		}
	}
}
