using System;
using System.IO;
using System.Text;

namespace Iodo.Rtsp.Rtcp;

internal class RtcpSdesNameItem : RtcpSdesItem
{
	public string DomainName { get; }

	public override int SerializedLength => 2 + GetDomainLength() + 1;

	public RtcpSdesNameItem(string domainName)
	{
		DomainName = domainName ?? throw new ArgumentNullException("domainName");
	}

	public override void Serialize(Stream stream)
	{
		int domainLength = GetDomainLength();
		byte[] bytes = Encoding.ASCII.GetBytes(DomainName);
		stream.WriteByte(1);
		stream.WriteByte((byte)(domainLength + 1));
		stream.Write(bytes, 0, domainLength);
		stream.WriteByte(0);
	}

	private int GetDomainLength()
	{
		return Math.Min(Encoding.ASCII.GetByteCount(DomainName), 254);
	}
}
