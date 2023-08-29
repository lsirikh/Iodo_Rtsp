#define DEBUG
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace Iodo.Rtsp.Utils;

internal class ChunksArray
{
	private readonly int _maxChunkSize;

	private readonly int _maxNumberOfChunks;

	private byte[] _chunksBytes = Array.Empty<byte>();

	private readonly List<int> _sizesList;

	private readonly Stack<int> _freeChunks;

	private int _chunksCount;

	public int Count => _chunksCount;

	public ArraySegment<byte> this[int index]
	{
		get
		{
			int offset = index * _maxChunkSize;
			return new ArraySegment<byte>(_chunksBytes, offset, _sizesList[index]);
		}
	}

	public ChunksArray(int maxChunkSize, int maxNumberOfChunks)
	{
		_maxChunkSize = maxChunkSize;
		_maxNumberOfChunks = maxNumberOfChunks;
		_sizesList = new List<int>(maxNumberOfChunks);
		_freeChunks = new Stack<int>(maxNumberOfChunks);
	}

	public int Insert(ArraySegment<byte> chunkSegment)
	{
		Debug.Assert(chunkSegment.Array != null, "chunkSegment.Array != null");
		if (chunkSegment.Count > _maxChunkSize)
		{
			throw new ArgumentException($"Chunk size is too large: {chunkSegment.Count}", "chunkSegment");
		}
		if (_chunksCount == _maxNumberOfChunks)
		{
			throw new InvalidOperationException("Number of chunks is reached the upper limit");
		}
		int num;
		int dstOffset;
		if (_freeChunks.Count != 0)
		{
			num = _freeChunks.Pop();
			dstOffset = num * _maxChunkSize;
			_sizesList[num] = chunkSegment.Count;
			_chunksCount++;
		}
		else
		{
			num = _chunksCount;
			int num2 = ++_chunksCount * _maxChunkSize;
			if (_chunksBytes.Length < num2)
			{
				Array.Resize(ref _chunksBytes, num2);
			}
			dstOffset = num2 - _maxChunkSize;
			_sizesList.Add(chunkSegment.Count);
		}
		Buffer.BlockCopy(chunkSegment.Array, chunkSegment.Offset, _chunksBytes, dstOffset, chunkSegment.Count);
		return num;
	}

	public void RemoveAt(int index)
	{
		_chunksCount--;
		_freeChunks.Push(index);
		_sizesList[index] = 0;
	}

	public void Clear()
	{
		_freeChunks.Clear();
		_sizesList.Clear();
		_chunksCount = 0;
	}
}
