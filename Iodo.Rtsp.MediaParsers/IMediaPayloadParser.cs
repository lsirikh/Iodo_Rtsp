using System;
using Iodo.Rtsp.RawFrames;

namespace Iodo.Rtsp.MediaParsers;

internal interface IMediaPayloadParser
{
	Action<RawFrame> FrameGenerated { get; set; }

	void Parse(TimeSpan timeOffset, ArraySegment<byte> byteSegment, bool markerBit);

	void ResetState();
}
