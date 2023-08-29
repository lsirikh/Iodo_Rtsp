using System;
using Iodo.Rtsp.Codecs;
using Iodo.Rtsp.Codecs.Audio;
using Iodo.Rtsp.Codecs.Video;
using Iodo.Rtsp.RawFrames;

namespace Iodo.Rtsp.MediaParsers;

internal abstract class MediaPayloadParser : IMediaPayloadParser
{
	private DateTime _baseTime = DateTime.MinValue;

	public Action<RawFrame> FrameGenerated { get; set; }

	public abstract void Parse(TimeSpan timeOffset, ArraySegment<byte> byteSegment, bool markerBit);

	public abstract void ResetState();

	protected DateTime GetFrameTimestamp(TimeSpan timeOffset)
	{
		if (timeOffset == TimeSpan.MinValue)
		{
			return DateTime.UtcNow;
		}
		if (_baseTime == DateTime.MinValue)
		{
			_baseTime = DateTime.UtcNow;
		}
		return _baseTime + timeOffset;
	}

	protected virtual void OnFrameGenerated(RawFrame e)
	{
		FrameGenerated?.Invoke(e);
	}

	public static IMediaPayloadParser CreateFrom(CodecInfo codecInfo)
	{
		if (!(codecInfo is H264CodecInfo codecInfo2))
		{
			if (!(codecInfo is MJPEGCodecInfo))
			{
				if (!(codecInfo is AACCodecInfo codecInfo3))
				{
					if (!(codecInfo is G711CodecInfo g711CodecInfo))
					{
						if (!(codecInfo is G726CodecInfo g726CodecInfo))
						{
							if (codecInfo is PCMCodecInfo pcmCodecInfo)
							{
								return new PCMAudioPayloadParser(pcmCodecInfo);
							}
							throw new ArgumentOutOfRangeException("codecInfo", "Unsupported codec: " + codecInfo.GetType().Name);
						}
						return new G726AudioPayloadParser(g726CodecInfo);
					}
					return new G711AudioPayloadParser(g711CodecInfo);
				}
				return new AACAudioPayloadParser(codecInfo3);
			}
			return new MJPEGVideoPayloadParser();
		}
		return new H264VideoPayloadParser(codecInfo2);
	}
}
