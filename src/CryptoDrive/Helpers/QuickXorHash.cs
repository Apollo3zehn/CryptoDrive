using System;
using System.Security.Cryptography;

namespace CryptoDrive.Helpers
{
    public class QuickXorHash : HashAlgorithm
    {
        private const int BitsInLastCell = 32;
        private const byte Shift = 11;
        private const byte WidthInBits = 160;

        private ulong[] _data;
        private long _lengthSoFar;
        private int _shiftSoFar;

        public QuickXorHash()
        {
            this.Initialize();
        }

        protected override void HashCore(byte[] array, int ibStart, int cbSize)
        {
            unchecked
            {
                int currentShift = this._shiftSoFar;

                // The bitvector where we'll start xoring
                int vectorArrayIndex = currentShift / 64;

                // The position within the bit vector at which we begin xoring
                int vectorOffset = currentShift % 64;
                int iterations = Math.Min(cbSize, QuickXorHash.WidthInBits);

                for (int i = 0; i < iterations; i++)
                {
                    bool isLastCell = vectorArrayIndex == _data.Length - 1;
                    int bitsInVectorCell = isLastCell ? QuickXorHash.BitsInLastCell : 64;

                    // There's at least 2 bitvectors before we reach the end of the array
                    if (vectorOffset <= bitsInVectorCell - 8)
                    {
                        for (int j = ibStart + i; j < cbSize + ibStart; j += QuickXorHash.WidthInBits)
                        {
                            this._data[vectorArrayIndex] ^= (ulong)array[j] << vectorOffset;
                        }
                    }
                    else
                    {
                        int index1 = vectorArrayIndex;
                        int index2 = isLastCell ? 0 : (vectorArrayIndex + 1);
                        byte low = (byte)(bitsInVectorCell - vectorOffset);

                        byte xoredByte = 0;

                        for (int j = ibStart + i; j < cbSize + ibStart; j += QuickXorHash.WidthInBits)
                        {
                            xoredByte ^= array[j];
                        }

                        this._data[index1] ^= (ulong)xoredByte << vectorOffset;
                        this._data[index2] ^= (ulong)xoredByte >> low;
                    }

                    vectorOffset += QuickXorHash.Shift;

                    while (vectorOffset >= bitsInVectorCell)
                    {
                        vectorArrayIndex = isLastCell ? 0 : vectorArrayIndex + 1;
                        vectorOffset -= bitsInVectorCell;
                    }
                }

                // Update the starting position in a circular shift pattern
                this._shiftSoFar = (this._shiftSoFar + QuickXorHash.Shift * (cbSize % QuickXorHash.WidthInBits)) % QuickXorHash.WidthInBits;
            }

            this._lengthSoFar += cbSize;
        }

        protected override byte[] HashFinal()
        {
            // Create a byte array big enough to hold all our data
            byte[] rgb = new byte[(QuickXorHash.WidthInBits - 1) / 8 + 1];

            // Block copy all our bitvectors to this byte array
            for (int i = 0; i < _data.Length - 1; i++)
            {
                Buffer.BlockCopy(BitConverter.GetBytes(this._data[i]), 0, rgb, i * 8, 8);
            }

            Buffer.BlockCopy(BitConverter.GetBytes(this._data[this._data.Length - 1]), 0,
                            rgb, (this._data.Length - 1) * 8,
                            rgb.Length - (this._data.Length - 1) * 8);

            // XOR the file length with the least significant bits
            // Note that GetBytes is architecture-dependent, so care should
            // be taken with porting. The expected value is 8-bytes in length in little-endian format
            var lengthBytes = BitConverter.GetBytes(this._lengthSoFar);

            for (int i = 0; i < lengthBytes.Length; i++)
            {
                rgb[QuickXorHash.WidthInBits / 8 - lengthBytes.Length + i] ^= lengthBytes[i];
            }

            return rgb;
        }

        public override sealed void Initialize()
        {
            this._data = new ulong[(QuickXorHash.WidthInBits - 1) / 64 + 1];
            this._shiftSoFar = 0;
            this._lengthSoFar = 0;
        }

        public override int HashSize
        {
            get
            {
                return QuickXorHash.WidthInBits;
            }
        }
    }
}
