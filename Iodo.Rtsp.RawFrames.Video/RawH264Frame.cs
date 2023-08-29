using System;

namespace Iodo.Rtsp.RawFrames.Video;

public abstract class RawH264Frame : RawVideoFrame
{
	public static readonly byte[] StartMarker = new byte[4] { 0, 0, 0, 1 };

	protected RawH264Frame(DateTime timestamp, ArraySegment<byte> frameSegment)
		: base(timestamp, frameSegment)
	{
	}
}
