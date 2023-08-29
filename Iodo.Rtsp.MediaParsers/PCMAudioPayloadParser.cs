using System;
using Iodo.Rtsp.Codecs.Audio;
using Iodo.Rtsp.RawFrames.Audio;

namespace Iodo.Rtsp.MediaParsers;

internal class PCMAudioPayloadParser : MediaPayloadParser
{
	private readonly PCMCodecInfo _pcmCodecInfo;

	public PCMAudioPayloadParser(PCMCodecInfo pcmCodecInfo)
	{
		_pcmCodecInfo = pcmCodecInfo ?? throw new ArgumentNullException("pcmCodecInfo");
	}

	public override void Parse(TimeSpan timeOffset, ArraySegment<byte> byteSegment, bool markerBit)
	{
		DateTime frameTimestamp = GetFrameTimestamp(timeOffset);
		RawPCMFrame e = new RawPCMFrame(frameTimestamp, byteSegment, _pcmCodecInfo.SampleRate, _pcmCodecInfo.BitsPerSample, _pcmCodecInfo.Channels);
		OnFrameGenerated(e);
	}

	public override void ResetState()
	{
	}
}
