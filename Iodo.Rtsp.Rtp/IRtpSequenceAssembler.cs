using Iodo.Rtsp.Utils;

namespace Iodo.Rtsp.Rtp;

internal interface IRtpSequenceAssembler
{
	RefAction<RtpPacket> PacketPassed { get; set; }

	void ProcessPacket(ref RtpPacket rtpPacket);
}
