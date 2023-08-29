using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Iodo.Rtsp.Rtcp;

internal class RtcpSdesReportPacket : RtcpPacket, ISerializablePacket
{
	private static readonly byte[] PaddingBytes = new byte[3];

	private readonly int _paddingByteCount;

	public IReadOnlyList<RtcpSdesChunk> Chunks { get; }

	public RtcpSdesReportPacket(IReadOnlyList<RtcpSdesChunk> chunks)
	{
		Chunks = chunks;
		base.SourceCount = chunks.Count;
		base.PayloadType = 202;
		int num = chunks.Sum((RtcpSdesChunk chunk) => chunk.SerializedLength);
		int num2 = num % 4;
		if (num2 == 0)
		{
			base.PaddingFlag = false;
		}
		else
		{
			base.PaddingFlag = true;
			_paddingByteCount = 4 - num2;
		}
		base.DwordLength = (num + 3) / 4;
		base.Length = (base.DwordLength + 1) * 4;
	}

	protected override void FillFromByteSegment(ArraySegment<byte> byteSegment)
	{
	}

	public new void Serialize(Stream stream)
	{
		base.Serialize(stream);
		for (int i = 0; i < Chunks.Count; i++)
		{
			RtcpSdesChunk rtcpSdesChunk = Chunks[i];
			rtcpSdesChunk.Serialize(stream);
		}
		if (base.PaddingFlag)
		{
			stream.Write(PaddingBytes, 0, _paddingByteCount);
		}
	}
}
