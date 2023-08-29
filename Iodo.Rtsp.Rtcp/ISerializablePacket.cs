using System.IO;

namespace Iodo.Rtsp.Rtcp;

internal interface ISerializablePacket
{
	void Serialize(Stream stream);
}
