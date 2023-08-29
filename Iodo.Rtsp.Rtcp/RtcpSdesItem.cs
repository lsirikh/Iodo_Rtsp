using System.IO;

namespace Iodo.Rtsp.Rtcp;

internal abstract class RtcpSdesItem
{
	public abstract int SerializedLength { get; }

	public abstract void Serialize(Stream stream);
}
