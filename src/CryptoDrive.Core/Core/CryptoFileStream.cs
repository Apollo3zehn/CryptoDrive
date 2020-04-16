//using System;
//using System.IO;

//namespace CryptoDrive.Core.Core
//{
//    public class CryptoFileStream : Stream
//    {
//        private Stream _inputStream;

//        public CryptoFileStream(Stream inputStream)
//        {
//            _inputStream = inputStream;
//        }

//        public override bool CanRead => true;

//        public override bool CanSeek => false;

//        public override bool CanWrite => false;

//        public override long Length => throw new NotImplementedException();

//        public override long Position { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

//        public override void Flush()
//        {
//            throw new NotImplementedException();
//        }

//        public override int Read(byte[] buffer, int offset, int count)
//        {
//            _inputStream.
//        }

//        public override long Seek(long offset, SeekOrigin origin)
//        {
//            throw new NotImplementedException();
//        }

//        public override void SetLength(long value)
//        {
//            throw new NotImplementedException();
//        }

//        public override void Write(byte[] buffer, int offset, int count)
//        {
//            throw new NotImplementedException();
//        }
//    }
//}
