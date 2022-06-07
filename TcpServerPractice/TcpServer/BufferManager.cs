using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TcpServerTest
{
    class BufferManager
    {
        int _bufferSize = 0;
        int _readPos = 0;
        int _writePos = 0;

        int _maxPacketSize = 0;
        byte[] _packetData;
        byte[] _packetDataTemp;

        public bool Init(int size, int maxPacketSize)
        {
            if (size < (maxPacketSize * 2) || size < 1 ||  maxPacketSize < 1)
            {
                return false;
            }

            _bufferSize = size;
            _packetData = new byte[size];
            _packetDataTemp = new byte[size];
            _maxPacketSize = maxPacketSize;

            return true;
        }

        public int Size()
        {
            return _writePos - _readPos;
        }

        public void Clear()
        {
            _readPos = 0;
            _writePos = 0;
        }

        public bool Write(byte[] data, int pos, int size)
        {
            if (data == null || (data.Length < (pos + size)))
            {
                return false;
            }

            var remainBufferSize = _bufferSize - _writePos;

            if (remainBufferSize < size)
            {
                return false;
            }

            Buffer.BlockCopy(data, pos, _packetData, _writePos, size);
            _writePos += size;

            if (NextFree() == false)
            {
                BufferRelocate();
            }
            return true;
        }

        public ArraySegment<byte> Read()
        {
            var enableReadSize = _writePos - _readPos;
            return Read(enableReadSize);
        }

        public ArraySegment<byte> Read(int readSize, bool delete = true)
        {
            var enableReadSize = _writePos - _readPos;
            
            if (enableReadSize < readSize)
            {
                return new ArraySegment<byte>();
            }

            var completePacketData = new ArraySegment<byte>(_packetData, _readPos, readSize);
            if(delete)
            {
                _readPos += readSize;
            }
            return completePacketData;
        }

        bool NextFree()
        {
            var enableWriteSize = _bufferSize - _writePos;

            if (enableWriteSize < _maxPacketSize)
            {
                return false;
            }

            return true;
        }

        void BufferRelocate()
        {
            var enableReadSize = _writePos - _readPos;

            Buffer.BlockCopy(_packetData, _readPos, _packetDataTemp, 0, enableReadSize);
            Buffer.BlockCopy(_packetDataTemp, 0, _packetData, 0, enableReadSize);

            _readPos = 0;
            _writePos = enableReadSize;
        }
    }
}
