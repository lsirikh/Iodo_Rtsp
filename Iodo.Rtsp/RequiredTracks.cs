using System;

namespace Iodo.Rtsp;

[Flags]
public enum RequiredTracks
{
	Video = 1,
	Audio = 2,
	All = 3
}
