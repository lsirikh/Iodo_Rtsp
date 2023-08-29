using System;

namespace Iodo.Rtsp.RawFrames.Video;

public class RawJpegFrame : RawVideoFrame
{
	public static readonly byte[] StartMarkerBytes = new byte[2] { 255, 216 };

	public static readonly byte[] EndMarkerBytes = new byte[2] { 255, 217 };

	public RawJpegFrame(DateTime timestamp, ArraySegment<byte> frameSegment)
		: base(timestamp, frameSegment)
	{
	}
}
