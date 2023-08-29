using System;

namespace Iodo.Rtsp.RawFrames.Audio;

public class RawG711AFrame : RawG711Frame
{
	public RawG711AFrame(DateTime timestamp, ArraySegment<byte> frameSegment)
		: base(timestamp, frameSegment)
	{
	}
}
