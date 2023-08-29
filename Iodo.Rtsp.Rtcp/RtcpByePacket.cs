using System;
using System.Collections.Generic;
using Iodo.Rtsp.Utils;

namespace Iodo.Rtsp.Rtcp;

internal class RtcpByePacket : RtcpPacket
{
	private readonly List<uint> _syncSourcesIds = new List<uint>();

	public IEnumerable<uint> SyncSourcesIds => _syncSourcesIds;

	protected override void FillFromByteSegment(ArraySegment<byte> byteSegment)
	{
		int num = byteSegment.Offset;
		for (int i = 0; i < base.SourceCount; i++)
		{
			uint item = BigEndianConverter.ReadUInt32(byteSegment.Array, num);
			num += 4;
			_syncSourcesIds.Add(item);
		}
	}
}
