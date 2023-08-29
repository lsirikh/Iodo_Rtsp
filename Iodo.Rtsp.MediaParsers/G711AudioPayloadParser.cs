using System;
using Iodo.Rtsp.Codecs.Audio;
using Iodo.Rtsp.RawFrames.Audio;

namespace Iodo.Rtsp.MediaParsers;

internal class G711AudioPayloadParser : MediaPayloadParser
{
	private readonly G711CodecInfo _g711CodecInfo;

	public G711AudioPayloadParser(G711CodecInfo g711CodecInfo)
	{
		_g711CodecInfo = g711CodecInfo ?? throw new ArgumentNullException("g711CodecInfo");
	}

	public override void Parse(TimeSpan timeOffset, ArraySegment<byte> byteSegment, bool markerBit)
	{
		G711UCodecInfo g711UCodecInfo = _g711CodecInfo as G711UCodecInfo;
		DateTime frameTimestamp = GetFrameTimestamp(timeOffset);
		RawG711Frame rawG711Frame = ((g711UCodecInfo == null) ? ((RawG711Frame)new RawG711AFrame(frameTimestamp, byteSegment)) : ((RawG711Frame)new RawG711UFrame(frameTimestamp, byteSegment)));
		rawG711Frame.SampleRate = _g711CodecInfo.SampleRate;
		rawG711Frame.Channels = _g711CodecInfo.Channels;
		OnFrameGenerated(rawG711Frame);
	}

	public override void ResetState()
	{
	}
}
