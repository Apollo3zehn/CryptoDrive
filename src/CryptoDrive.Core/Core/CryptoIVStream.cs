using System;
using System.IO;
using System.Security.Cryptography;

namespace CryptoDrive.Core
{
    public class CryptoIVStream : Stream
    {
        private int _position;
        private byte[] _rgbIV;

        private CryptoStream _cryptoStream;

        public CryptoIVStream(CryptoStream cryptoStream, byte[] rgbIV)
        {
            _rgbIV = rgbIV;
            _cryptoStream = cryptoStream;
        }

        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => throw new NotImplementedException();

        public override long Position { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

        public override void Flush()
        {
            throw new NotImplementedException();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            if (_position < _rgbIV.Length)
            {
                var readCount = Math.Min(_rgbIV.Length - _position, count);

                Array.Copy(_rgbIV, _position, buffer, offset, readCount);
                _position += readCount;

                return readCount;
            }
            else
            {
                return _cryptoStream.Read(buffer, offset, count);
            }
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotImplementedException();
        }

        public override void SetLength(long value)
        {
            throw new NotImplementedException();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new NotImplementedException();
        }
    }
}
