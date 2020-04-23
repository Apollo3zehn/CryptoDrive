using System;
using System.IO;
using System.Security.Cryptography;

namespace CryptoDrive.Cryptography
{
    public class CryptoIVStream : Stream
    {
        private int _position;
        private byte[] _rgbIV;

        private CryptoStream _cryptoStream;

        public CryptoIVStream(CryptoStream cryptoStream, byte[] rgbIV, long cryptoLength)
        {
            // CanSeek = true is required for OneDrive`s large file upload process
            // Side effects with CanSeek = true:
            //      1. large file upload wants to seek (with SeekOrigin.Begin) 
            //      2. Stream.CopyTo() wants the stream length
            //      3. Stream.CopyTo() wants the stream position
            _rgbIV = rgbIV;
            _cryptoStream = cryptoStream;

            this.Length = cryptoLength;
        }

        public override bool CanRead => true;

        public override bool CanSeek => true;

        public override bool CanWrite => false;

        public override long Length { get; }

        public override long Position
        {
            get { return _position; }
            set { throw new NotImplementedException(); }
        }

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
            if (origin == SeekOrigin.Begin)
                return offset;
            else
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
