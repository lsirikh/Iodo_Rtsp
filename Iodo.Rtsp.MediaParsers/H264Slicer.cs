#define DEBUG
using System;
using System.Diagnostics;
using Iodo.Rtsp.RawFrames.Video;
using Iodo.Rtsp.Utils;

namespace Iodo.Rtsp.MediaParsers;

internal static class H264Slicer
{
	public static void Slice(ArraySegment<byte> byteSegment, Action<ArraySegment<byte>> nalUnitHandler)
	{
		Debug.Assert(byteSegment.Array != null, "byteSegment.Array != null");
		Debug.Assert(ArrayUtils.StartsWith(byteSegment.Array, byteSegment.Offset, byteSegment.Count, RawH264Frame.StartMarker));
		int num = byteSegment.Offset + byteSegment.Count;
		int num2 = ArrayUtils.IndexOfBytes(byteSegment.Array, RawH264Frame.StartMarker, byteSegment.Offset, byteSegment.Count);
		if (num2 == -1)
		{
			nalUnitHandler?.Invoke(byteSegment);
		}
		int num3;
		while (true)
		{
			num3 = num - num2;
			if (num3 == RawH264Frame.StartMarker.Length)
			{
				return;
			}
			int num4 = byteSegment.Array[num2 + RawH264Frame.StartMarker.Length] & 0x1F;
			if (num4 == 5 || num4 == 1)
			{
				nalUnitHandler?.Invoke(new ArraySegment<byte>(byteSegment.Array, num2, num3));
				return;
			}
			int num5 = ArrayUtils.IndexOfBytes(byteSegment.Array, RawH264Frame.StartMarker, num2 + RawH264Frame.StartMarker.Length, num3 - RawH264Frame.StartMarker.Length);
			if (num5 <= 0)
			{
				break;
			}
			int num6 = num5 - num2;
			if (num6 != RawH264Frame.StartMarker.Length)
			{
				nalUnitHandler?.Invoke(new ArraySegment<byte>(byteSegment.Array, num2, num6));
			}
			num2 = num5;
		}
		nalUnitHandler?.Invoke(new ArraySegment<byte>(byteSegment.Array, num2, num3));
	}
}
