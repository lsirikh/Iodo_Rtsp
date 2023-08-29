#define DEBUG
using System;
using System.Diagnostics;
using System.IO;
using Iodo.Rtsp.Codecs.Video;
using Iodo.Rtsp.RawFrames.Video;
using Iodo.Rtsp.Utils;

namespace Iodo.Rtsp.MediaParsers;

internal class H264VideoPayloadParser : MediaPayloadParser
{
	private enum PackModeType
	{
		STAP_A = 24,
		STAP_B,
		MTAP16,
		MTAP24,
		FU_A,
		FU_B
	}

	private const int DecodingOrderNumberFieldSize = 2;

	private const int DondFieldSize = 1;

	private readonly H264Parser _h264Parser;

	private readonly MemoryStream _nalStream;

	private bool _waitForStartFu = true;

	private TimeSpan _timeOffset = TimeSpan.MinValue;

	public H264VideoPayloadParser(H264CodecInfo codecInfo)
	{
		if (codecInfo == null)
		{
			throw new ArgumentNullException("codecInfo");
		}
		if (codecInfo.SpsPpsBytes == null)
		{
			throw new ArgumentException("SpsPpsBytes is null", "codecInfo");
		}
		_h264Parser = new H264Parser(() => GetFrameTimestamp(_timeOffset))
		{
			FrameGenerated = OnFrameGenerated
		};
		if (codecInfo.SpsPpsBytes.Length != 0)
		{
			_h264Parser.Parse(new ArraySegment<byte>(codecInfo.SpsPpsBytes), generateFrame: false);
		}
		_nalStream = new MemoryStream(8192);
	}

	public override void Parse(TimeSpan timeOffset, ArraySegment<byte> byteSegment, bool markerBit)
	{
		Debug.Assert(byteSegment.Array != null, "byteSegment.Array != null");
		if (!markerBit && timeOffset != _timeOffset)
		{
			_h264Parser.TryGenerateFrame();
		}
		_timeOffset = timeOffset;
		switch ((PackModeType)(byteSegment.Array[byteSegment.Offset] & 0x1F))
		{
		case PackModeType.FU_A:
			ParseFU(byteSegment, 0, markerBit);
			break;
		case PackModeType.FU_B:
			ParseFU(byteSegment, 2, markerBit);
			break;
		case PackModeType.STAP_A:
			ParseSTAP(byteSegment, 0, markerBit);
			break;
		case PackModeType.STAP_B:
			ParseSTAP(byteSegment, 2, markerBit);
			break;
		case PackModeType.MTAP16:
			ParseMTAP(byteSegment, 2, markerBit);
			break;
		case PackModeType.MTAP24:
			ParseMTAP(byteSegment, 3, markerBit);
			break;
		default:
			_h264Parser.Parse(byteSegment, markerBit);
			break;
		}
	}

	public override void ResetState()
	{
		_nalStream.Position = 0L;
		_h264Parser.ResetState();
		_waitForStartFu = true;
	}

	private void ParseFU(ArraySegment<byte> byteSegment, int donFieldSize, bool markerBit)
	{
		Debug.Assert(byteSegment.Array != null, "byteSegment.Array != null");
		int num = byteSegment.Offset + 1;
		int num2 = byteSegment.Array[num];
		bool flag = (num2 & 0x80) != 0;
		bool flag2 = (num2 & 0x40) != 0;
		if (flag)
		{
			int num3 = (num2 & 0x1F) | (byteSegment.Array[byteSegment.Offset] & 0xE0);
			num += donFieldSize;
			byteSegment.Array[num] = (byte)num3;
			ArraySegment<byte> byteSegment2 = new ArraySegment<byte>(byteSegment.Array, num, byteSegment.Offset + byteSegment.Count - num);
			if (!ArrayUtils.StartsWith(byteSegment2.Array, byteSegment2.Offset, byteSegment2.Count, RawH264Frame.StartMarker))
			{
				MemoryStream nalStream = _nalStream;
				ArraySegment<byte> startMarkerSegment = H264Parser.StartMarkerSegment;
				byte[]? array = startMarkerSegment.Array;
				startMarkerSegment = H264Parser.StartMarkerSegment;
				int offset = startMarkerSegment.Offset;
				startMarkerSegment = H264Parser.StartMarkerSegment;
				nalStream.Write(array, offset, startMarkerSegment.Count);
			}
			_nalStream.Write(byteSegment2.Array, byteSegment2.Offset, byteSegment2.Count);
			if (flag2)
			{
				_h264Parser.Parse(byteSegment2, markerBit);
				_waitForStartFu = true;
			}
			else
			{
				_waitForStartFu = false;
			}
		}
		else if (!_waitForStartFu)
		{
			num += donFieldSize + 1;
			_nalStream.Write(byteSegment.Array, num, byteSegment.Offset + byteSegment.Count - num);
			if (flag2)
			{
				ArraySegment<byte> byteSegment3 = new ArraySegment<byte>(_nalStream.GetBuffer(), 0, (int)_nalStream.Position);
				_nalStream.Position = 0L;
				_h264Parser.Parse(byteSegment3, markerBit);
				_waitForStartFu = true;
			}
		}
	}

	private void ParseSTAP(ArraySegment<byte> byteSegment, int donFieldSize, bool markerBit)
	{
		Debug.Assert(byteSegment.Array != null, "byteSegment.Array != null");
		int num = byteSegment.Offset + 1 + donFieldSize;
		int num2 = byteSegment.Offset + byteSegment.Count;
		while (num < num2)
		{
			int num3 = BigEndianConverter.ReadUInt16(byteSegment.Array, num);
			num += 2;
			ArraySegment<byte> byteSegment2 = new ArraySegment<byte>(byteSegment.Array, num, num3);
			num += num3;
			_h264Parser.Parse(byteSegment2, markerBit && num >= num2);
		}
	}

	private void ParseMTAP(ArraySegment<byte> byteSegment, int tsOffsetFieldSize, bool markerBit)
	{
		Debug.Assert(byteSegment.Array != null, "byteSegment.Array != null");
		int offset = byteSegment.Offset;
		int num = byteSegment.Offset + byteSegment.Count;
		offset += 3;
		while (offset < num)
		{
			int num2 = BigEndianConverter.ReadUInt16(byteSegment.Array, offset);
			offset += 3 + tsOffsetFieldSize;
			ArraySegment<byte> byteSegment2 = new ArraySegment<byte>(byteSegment.Array, offset, num2);
			offset += num2;
			_h264Parser.Parse(byteSegment2, markerBit && offset >= num);
		}
	}
}
