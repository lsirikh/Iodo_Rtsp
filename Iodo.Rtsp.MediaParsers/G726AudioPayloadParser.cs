using System;
using Iodo.Rtsp.Codecs.Audio;
using Iodo.Rtsp.RawFrames.Audio;

namespace Iodo.Rtsp.MediaParsers;

internal class G726AudioPayloadParser : MediaPayloadParser
{
	private readonly int _bitsPerCodedSample;

	public G726AudioPayloadParser(G726CodecInfo g726CodecInfo)
	{
		if (g726CodecInfo == null)
		{
			throw new ArgumentNullException("g726CodecInfo");
		}
		_bitsPerCodedSample = GetBitsPerCodedSample(g726CodecInfo.Bitrate);
	}

	public override void Parse(TimeSpan timeOffset, ArraySegment<byte> byteSegment, bool markerBit)
	{
		DateTime frameTimestamp = GetFrameTimestamp(timeOffset);
		RawG726Frame e = new RawG726Frame(frameTimestamp, byteSegment, _bitsPerCodedSample);
		OnFrameGenerated(e);
	}

	public override void ResetState()
	{
	}

	private int GetBitsPerCodedSample(int bitrate)
	{
		return bitrate switch
		{
			16000 => 2, 
			24000 => 3, 
			32000 => 4, 
			40000 => 5, 
			_ => throw new ArgumentOutOfRangeException("bitrate"), 
		};
	}
}
