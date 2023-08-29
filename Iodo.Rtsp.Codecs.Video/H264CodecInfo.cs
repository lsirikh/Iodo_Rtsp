using System;

namespace Iodo.Rtsp.Codecs.Video;

internal class H264CodecInfo : VideoCodecInfo
{
	public byte[] SpsPpsBytes { get; set; } = Array.Empty<byte>();

}
