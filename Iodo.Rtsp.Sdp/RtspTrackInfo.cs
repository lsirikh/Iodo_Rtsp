namespace Iodo.Rtsp.Sdp;

internal abstract class RtspTrackInfo
{
	public string TrackName { get; }

	protected RtspTrackInfo(string trackName)
	{
		TrackName = trackName;
	}
}
