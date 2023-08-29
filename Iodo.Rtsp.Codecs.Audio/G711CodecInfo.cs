namespace Iodo.Rtsp.Codecs.Audio;

internal abstract class G711CodecInfo : AudioCodecInfo
{
	public int SampleRate { get; set; } = 8000;


	public int Channels { get; set; } = 1;

}
