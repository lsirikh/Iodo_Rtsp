namespace Iodo.Rtsp.Codecs.Audio;

internal class PCMCodecInfo : AudioCodecInfo
{
	public int SampleRate { get; }

	public int BitsPerSample { get; }

	public int Channels { get; }

	public PCMCodecInfo(int sampleRate, int bitsPerSample, int channels)
	{
		SampleRate = sampleRate;
		BitsPerSample = bitsPerSample;
		Channels = channels;
	}
}
