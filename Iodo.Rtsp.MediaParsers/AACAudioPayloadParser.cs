#define DEBUG
using System;
using System.Diagnostics;
using Iodo.Rtsp.Codecs.Audio;
using Iodo.Rtsp.RawFrames.Audio;
using Iodo.Rtsp.Utils;

namespace Iodo.Rtsp.MediaParsers;

internal class AACAudioPayloadParser : MediaPayloadParser
{
	private readonly AACCodecInfo _codecInfo;

	private readonly BitStreamReader _bitStreamReader = new BitStreamReader();

	public AACAudioPayloadParser(AACCodecInfo codecInfo)
	{
		_codecInfo = codecInfo ?? throw new ArgumentNullException("codecInfo");
	}

	public override void Parse(TimeSpan timeOffset, ArraySegment<byte> byteSegment, bool markerBit)
	{
		int num = BigEndianConverter.ReadUInt16(byteSegment.Array, byteSegment.Offset);
		int num2 = (num + 7) / 8;
		int num3 = _codecInfo.SizeLength + _codecInfo.IndexLength;
		int num4 = num - num3;
		if (num4 < 0 || num3 <= 0)
		{
			return;
		}
		int num5 = 1 + num4 / (_codecInfo.SizeLength + _codecInfo.IndexDeltaLength);
		_bitStreamReader.ReInitialize(byteSegment.SubSegment(2));
		int num6 = byteSegment.Offset + 2 + num2;
		for (int i = 0; i < num5; i++)
		{
			int num7 = _bitStreamReader.ReadBits(_codecInfo.SizeLength);
			if (i == 0)
			{
				_bitStreamReader.ReadBits(_codecInfo.IndexLength);
			}
			else if (_codecInfo.IndexDeltaLength != 0)
			{
				_bitStreamReader.ReadBits(_codecInfo.IndexDeltaLength);
			}
			Debug.Assert(byteSegment.Array != null, "byteSegment.Array != null");
			ArraySegment<byte> frameBytes = new ArraySegment<byte>(byteSegment.Array, num6, num7);
			DateTime frameTimestamp = GetFrameTimestamp(timeOffset);
			RawAACFrame e = new RawAACFrame(frameTimestamp, frameBytes, new ArraySegment<byte>(_codecInfo.ConfigBytes));
			OnFrameGenerated(e);
			num6 += num7;
		}
	}

	public override void ResetState()
	{
	}
}
