using System;
using System.Collections.Generic;
using Iodo.Rtsp.Utils;

namespace Iodo.Rtsp.Rtp;

internal class RtpSequenceAssembler : IRtpSequenceAssembler
{
	private readonly ChunksArray _chunksArray;

	private readonly int _maxCorrectionLength;

	private ushort _previousCorrectSeqNumber;

	private uint _previousTimestamp;

	private readonly List<RtpPacket> _bufferedRtpPackets;

	private readonly List<int> _rtpPacketIndexToChunkIndexMap = new List<int>();

	private readonly List<int> _removeList;

	private bool _isFirstPacket = true;

	public RefAction<RtpPacket> PacketPassed { get; set; }

	public RtpSequenceAssembler(int maxRtpPacketSize, int maxCorrectionLength)
	{
		if (maxRtpPacketSize <= 0)
		{
			throw new ArgumentOutOfRangeException("maxRtpPacketSize");
		}
		if (maxCorrectionLength < 1)
		{
			throw new ArgumentOutOfRangeException("maxCorrectionLength");
		}
		_maxCorrectionLength = maxCorrectionLength;
		_chunksArray = new ChunksArray(maxRtpPacketSize, maxCorrectionLength);
		_bufferedRtpPackets = new List<RtpPacket>(maxCorrectionLength);
		_removeList = new List<int>(maxCorrectionLength);
	}

	public void ProcessPacket(ref RtpPacket rtpPacket)
	{
		if (_isFirstPacket)
		{
			_previousCorrectSeqNumber = rtpPacket.SeqNumber;
			_previousTimestamp = rtpPacket.Timestamp;
			PacketPassed?.Invoke(ref rtpPacket);
			_isFirstPacket = false;
			return;
		}
		int num = (ushort)(rtpPacket.SeqNumber - _previousCorrectSeqNumber);
		if (num == 1)
		{
			_previousCorrectSeqNumber = rtpPacket.SeqNumber;
			_previousTimestamp = rtpPacket.Timestamp;
			PacketPassed?.Invoke(ref rtpPacket);
			if (_bufferedRtpPackets.Count != 0)
			{
				ushort nextSeqNumber = (ushort)(_previousCorrectSeqNumber + 1);
				ProcessBufferedPackets(nextSeqNumber);
			}
		}
		else if (_previousTimestamp != rtpPacket.Timestamp || num > _maxCorrectionLength)
		{
			while (_bufferedRtpPackets.Count != 0)
			{
				PassNearestBufferedPacket();
			}
			_previousCorrectSeqNumber = rtpPacket.SeqNumber;
			_previousTimestamp = rtpPacket.Timestamp;
			PacketPassed?.Invoke(ref rtpPacket);
		}
		else if (rtpPacket.SeqNumber != _previousCorrectSeqNumber)
		{
			_bufferedRtpPackets.Add(rtpPacket);
			int item = _chunksArray.Insert(rtpPacket.PayloadSegment);
			_rtpPacketIndexToChunkIndexMap.Add(item);
			if (_bufferedRtpPackets.Count == _maxCorrectionLength)
			{
				PassNearestBufferedPacket();
				ushort nextSeqNumber2 = (ushort)(_previousCorrectSeqNumber + 1);
				ProcessBufferedPackets(nextSeqNumber2);
			}
		}
	}

	private void PassNearestBufferedPacket()
	{
		int index = 0;
		int num = (ushort)(_bufferedRtpPackets[0].SeqNumber - _previousCorrectSeqNumber);
		for (int i = 1; i < _bufferedRtpPackets.Count; i++)
		{
			int num2 = (ushort)(_bufferedRtpPackets[i].SeqNumber - _previousCorrectSeqNumber);
			if (num2 < num)
			{
				index = i;
				num = num2;
			}
		}
		RtpPacket value = _bufferedRtpPackets[index];
		int index2 = _rtpPacketIndexToChunkIndexMap[index];
		value.PayloadSegment = _chunksArray[index2];
		_previousCorrectSeqNumber = value.SeqNumber;
		_previousTimestamp = value.Timestamp;
		PacketPassed?.Invoke(ref value);
		_bufferedRtpPackets.RemoveAt(index);
		_chunksArray.RemoveAt(index2);
		_rtpPacketIndexToChunkIndexMap.RemoveAt(index);
	}

	private void ProcessBufferedPackets(ushort nextSeqNumber)
	{
		bool flag;
		do
		{
			flag = false;
			for (int i = 0; i < _bufferedRtpPackets.Count; i++)
			{
				if (_bufferedRtpPackets[i].SeqNumber == nextSeqNumber)
				{
					RtpPacket value = _bufferedRtpPackets[i];
					int index = _rtpPacketIndexToChunkIndexMap[i];
					value.PayloadSegment = _chunksArray[index];
					_previousCorrectSeqNumber = value.SeqNumber;
					_previousTimestamp = value.Timestamp;
					PacketPassed?.Invoke(ref value);
					nextSeqNumber = (ushort)(nextSeqNumber + 1);
					flag = true;
					_removeList.Add(i);
				}
			}
		}
		while (flag);
		if (_removeList.Count == 0)
		{
			return;
		}
		if (_removeList.Count == _bufferedRtpPackets.Count)
		{
			_bufferedRtpPackets.Clear();
			_chunksArray.Clear();
			_rtpPacketIndexToChunkIndexMap.Clear();
		}
		else if (_removeList.Count == 1)
		{
			int index2 = _removeList[0];
			_bufferedRtpPackets.RemoveAt(index2);
			_chunksArray.RemoveAt(_rtpPacketIndexToChunkIndexMap[index2]);
			_rtpPacketIndexToChunkIndexMap.RemoveAt(index2);
		}
		else
		{
			_removeList.Sort();
			for (int num = _removeList.Count - 1; num > -1; num--)
			{
				int index3 = _removeList[num];
				_bufferedRtpPackets.RemoveAt(index3);
				_chunksArray.RemoveAt(_rtpPacketIndexToChunkIndexMap[index3]);
				_rtpPacketIndexToChunkIndexMap.RemoveAt(index3);
			}
		}
		_removeList.Clear();
	}
}
