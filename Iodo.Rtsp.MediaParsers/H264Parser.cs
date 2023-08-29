#define DEBUG
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Iodo.Rtsp.RawFrames;
using Iodo.Rtsp.RawFrames.Video;
using Iodo.Rtsp.Utils;

namespace Iodo.Rtsp.MediaParsers;

internal class H264Parser
{
	private enum FrameType
	{
		Unknown,
		IntraFrame,
		PredictionFrame
	}

	public static readonly ArraySegment<byte> StartMarkerSegment = new ArraySegment<byte>(RawH264Frame.StartMarker);

	private readonly Func<DateTime> _frameTimestampProvider;

	private readonly BitStreamReader _bitStreamReader = new BitStreamReader();

	private readonly Dictionary<int, byte[]> _spsMap = new Dictionary<int, byte[]>();

	private readonly Dictionary<int, byte[]> _ppsMap = new Dictionary<int, byte[]>();

	private bool _waitForIFrame = true;

	private byte[] _spsPpsBytes = new byte[0];

	private bool _updateSpsPpsBytes;

	private int _sliceType = -1;

	private readonly MemoryStream _frameStream;

	public Action<RawFrame> FrameGenerated;

	public H264Parser(Func<DateTime> frameTimestampProvider)
	{
		_frameTimestampProvider = frameTimestampProvider ?? throw new ArgumentNullException("frameTimestampProvider");
		_frameStream = new MemoryStream(8192);
	}

	public void Parse(ArraySegment<byte> byteSegment, bool generateFrame)
	{
		Debug.Assert(byteSegment.Array != null, "byteSegment.Array != null");
		if (ArrayUtils.StartsWith(byteSegment.Array, byteSegment.Offset, byteSegment.Count, RawH264Frame.StartMarker))
		{
			H264Slicer.Slice(byteSegment, SlicerOnNalUnitFound);
		}
		else
		{
			ProcessNalUnit(byteSegment, hasStartMarker: false, ref generateFrame);
		}
		if (generateFrame)
		{
			TryGenerateFrame();
		}
	}

	public void TryGenerateFrame()
	{
		if (_frameStream.Position != 0)
		{
			ArraySegment<byte> frameBytes = new ArraySegment<byte>(_frameStream.GetBuffer(), 0, (int)_frameStream.Position);
			_frameStream.Position = 0L;
			TryGenerateFrame(frameBytes);
		}
	}

	private void TryGenerateFrame(ArraySegment<byte> frameBytes)
	{
		if (_updateSpsPpsBytes)
		{
			UpdateSpsPpsBytes();
			_updateSpsPpsBytes = false;
		}
		if (_sliceType != -1 && _spsPpsBytes.Length != 0)
		{
			FrameType frameType = GetFrameType(_sliceType);
			_sliceType = -1;
			if (frameType == FrameType.PredictionFrame && !_waitForIFrame)
			{
				DateTime timestamp = _frameTimestampProvider();
				FrameGenerated?.Invoke(new RawH264PFrame(timestamp, frameBytes));
			}
			else if (frameType == FrameType.IntraFrame)
			{
				_waitForIFrame = false;
				ArraySegment<byte> spsPpsSegment = new ArraySegment<byte>(_spsPpsBytes);
				DateTime timestamp = _frameTimestampProvider();
				FrameGenerated?.Invoke(new RawH264IFrame(timestamp, frameBytes, spsPpsSegment));
			}
		}
	}

	public void ResetState()
	{
		_frameStream.Position = 0L;
		_sliceType = -1;
		_waitForIFrame = true;
	}

	private void SlicerOnNalUnitFound(ArraySegment<byte> byteSegment)
	{
		bool generateFrame = false;
		ProcessNalUnit(byteSegment, hasStartMarker: true, ref generateFrame);
	}

	private void ProcessNalUnit(ArraySegment<byte> byteSegment, bool hasStartMarker, ref bool generateFrame)
	{
		Debug.Assert(byteSegment.Array != null, "byteSegment.Array != null");
		int num = byteSegment.Offset;
		if (hasStartMarker)
		{
			num += RawH264Frame.StartMarker.Length;
		}
		int num2 = byteSegment.Array[num] & 0x1F;
		bool flag = ((byteSegment.Array[num] >> 5) & 3) == 0;
		if (num2 <= 0 || num2 >= 24)
		{
			throw new H264ParserException($"Invalid nal unit type: {num2}");
		}
		switch (num2)
		{
		case 7:
			ParseSps(byteSegment, hasStartMarker);
			return;
		case 8:
			ParsePps(byteSegment, hasStartMarker);
			return;
		}
		if (_sliceType == -1 && (num2 == 5 || num2 == 1))
		{
			_sliceType = GetSliceType(byteSegment, hasStartMarker);
		}
		if (flag || num2 == 6)
		{
			return;
		}
		if (generateFrame)
		{
			if (!hasStartMarker)
			{
				int offset = byteSegment.Offset;
				ArraySegment<byte> startMarkerSegment = StartMarkerSegment;
				if (offset < startMarkerSegment.Count)
				{
					goto IL_01b3;
				}
			}
			if (_frameStream.Position == 0)
			{
				if (!hasStartMarker)
				{
					int offset2 = byteSegment.Offset;
					ArraySegment<byte> startMarkerSegment = StartMarkerSegment;
					int num3 = offset2 - startMarkerSegment.Count;
					startMarkerSegment = StartMarkerSegment;
					byte[]? array = startMarkerSegment.Array;
					startMarkerSegment = StartMarkerSegment;
					int offset3 = startMarkerSegment.Offset;
					byte[]? array2 = byteSegment.Array;
					startMarkerSegment = StartMarkerSegment;
					Buffer.BlockCopy(array, offset3, array2, num3, startMarkerSegment.Count);
					byte[]? array3 = byteSegment.Array;
					int count = byteSegment.Count;
					startMarkerSegment = StartMarkerSegment;
					byteSegment = new ArraySegment<byte>(array3, num3, count + startMarkerSegment.Count);
				}
				generateFrame = false;
				TryGenerateFrame(byteSegment);
				return;
			}
		}
		goto IL_01b3;
		IL_01b3:
		if (!hasStartMarker)
		{
			MemoryStream frameStream = _frameStream;
			ArraySegment<byte> startMarkerSegment = StartMarkerSegment;
			byte[]? array4 = startMarkerSegment.Array;
			startMarkerSegment = StartMarkerSegment;
			int offset4 = startMarkerSegment.Offset;
			startMarkerSegment = StartMarkerSegment;
			frameStream.Write(array4, offset4, startMarkerSegment.Count);
		}
		_frameStream.Write(byteSegment.Array, byteSegment.Offset, byteSegment.Count);
	}

	private void ParseSps(ArraySegment<byte> byteSegment, bool hasStartMarker)
	{
		if (byteSegment.Count >= 5)
		{
			ProcessSpsOrPps(byteSegment, hasStartMarker, 4, _spsMap);
		}
	}

	private void ParsePps(ArraySegment<byte> byteSegment, bool hasStartMarker)
	{
		if (byteSegment.Count >= 2)
		{
			ProcessSpsOrPps(byteSegment, hasStartMarker, 1, _ppsMap);
		}
	}

	private void ProcessSpsOrPps(ArraySegment<byte> byteSegment, bool hasStartMarker, int offset, Dictionary<int, byte[]> idToBytesMap)
	{
		_bitStreamReader.ReInitialize(hasStartMarker ? byteSegment.SubSegment(RawH264Frame.StartMarker.Length + offset) : byteSegment.SubSegment(offset));
		int num = _bitStreamReader.ReadUe();
		if (num != -1)
		{
			if (hasStartMarker)
			{
				byteSegment = byteSegment.SubSegment(RawH264Frame.StartMarker.Length);
			}
			if (TryUpdateSpsOrPps(byteSegment, num, idToBytesMap))
			{
				_updateSpsPpsBytes = true;
			}
		}
	}

	private static bool TryUpdateSpsOrPps(ArraySegment<byte> byteSegment, int id, Dictionary<int, byte[]> idToBytesMap)
	{
		Debug.Assert(byteSegment.Array != null, "byteSegment.Array != null");
		if (!idToBytesMap.TryGetValue(id, out var value))
		{
			value = new byte[byteSegment.Count];
			Buffer.BlockCopy(byteSegment.Array, byteSegment.Offset, value, 0, byteSegment.Count);
			idToBytesMap.Add(id, value);
			return true;
		}
		if (!ArrayUtils.IsBytesEquals(value, 0, value.Length, byteSegment.Array, byteSegment.Offset, byteSegment.Count))
		{
			if (value.Length != byteSegment.Count)
			{
				value = new byte[byteSegment.Count];
			}
			Buffer.BlockCopy(byteSegment.Array, byteSegment.Offset, value, 0, byteSegment.Count);
			idToBytesMap[id] = value;
			return true;
		}
		return false;
	}

	private void UpdateSpsPpsBytes()
	{
		int num = _spsMap.Values.Sum((byte[] sps) => sps.Length) + _ppsMap.Values.Sum((byte[] pps) => pps.Length) + RawH264Frame.StartMarker.Length * (_spsMap.Count + _ppsMap.Count);
		if (_spsPpsBytes.Length != num)
		{
			_spsPpsBytes = new byte[num];
		}
		int num2 = 0;
		foreach (byte[] value in _spsMap.Values)
		{
			Buffer.BlockCopy(RawH264Frame.StartMarker, 0, _spsPpsBytes, num2, RawH264Frame.StartMarker.Length);
			num2 += RawH264Frame.StartMarker.Length;
			Buffer.BlockCopy(value, 0, _spsPpsBytes, num2, value.Length);
			num2 += value.Length;
		}
		foreach (byte[] value2 in _ppsMap.Values)
		{
			Buffer.BlockCopy(RawH264Frame.StartMarker, 0, _spsPpsBytes, num2, RawH264Frame.StartMarker.Length);
			num2 += RawH264Frame.StartMarker.Length;
			Buffer.BlockCopy(value2, 0, _spsPpsBytes, num2, value2.Length);
			num2 += value2.Length;
		}
	}

	private int GetSliceType(ArraySegment<byte> byteSegment, bool hasStartMarker)
	{
		int num = 1;
		if (hasStartMarker)
		{
			num += RawH264Frame.StartMarker.Length;
		}
		_bitStreamReader.ReInitialize(byteSegment.SubSegment(num));
		int num2 = _bitStreamReader.ReadUe();
		if (num2 == -1)
		{
			return num2;
		}
		return _bitStreamReader.ReadUe();
	}

	private static FrameType GetFrameType(int sliceType)
	{
		if (sliceType == 0 || sliceType == 5)
		{
			return FrameType.PredictionFrame;
		}
		if (sliceType == 2 || sliceType == 7)
		{
			return FrameType.IntraFrame;
		}
		return FrameType.Unknown;
	}
}
