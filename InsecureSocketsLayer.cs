using System.Buffers;
using System.IO.Pipelines;
using System.Text;
using protohackers.Transport;

namespace protohackers;

public class InsecureSocketsLayer : TcpServerBase
{
    protected override async Task ProcessConnection(Connection connection)
    {
        CipherSpec? cipher = null;
        Pipe decrypted = new Pipe();

        while (true)
        {
            var result = await connection.Input.ReadAsync();
            if (result.IsCanceled)
            {
                break;
            }

            ReadOnlySequence<byte> buffer = result.Buffer;
            SequencePosition? position = null;

            if (cipher == null && buffer.Length > 0)
            {
                position = buffer.PositionOf((byte)0);
                if (position != null)
                {
                    var spec = buffer.Slice(0, buffer.GetPosition(1, position.Value)).ToArray();
                    if (IsNoOpCipher(spec) || spec.Length == 1)
                    {
                        Console.WriteLine(Convert.ToHexString(buffer.ToArray()));
                        break;
                    }

                    cipher = new CipherSpec(spec);
                    buffer = buffer.Slice(buffer.GetPosition(1, position.Value));
                }
            }

            if (cipher != null && buffer.Length > 0)
            {
                var data = buffer.ToArray();
                cipher.Decode(data);
                await decrypted.Writer.WriteAsync(data);

                var decryptedResult = await decrypted.Reader.ReadAsync();
                ReadOnlySequence<byte> decryptedBuffer = decryptedResult.Buffer;
                SequencePosition? decryptedPosition = null;
                do
                {
                    decryptedPosition = decryptedBuffer.PositionOf((byte)'\n');
                    if (decryptedPosition != null)
                    {
                        var response = ProcessRequest(decryptedBuffer.Slice(0, decryptedPosition.Value));
                        cipher.Encode(response);
                        await connection.Output.WriteAsync(response);
                        decryptedBuffer =
                            decryptedBuffer.Slice(decryptedBuffer.GetPosition(1, decryptedPosition.Value));
                    }
                } while (decryptedPosition != null);

                decrypted.Reader.AdvanceTo(decryptedBuffer.Start, decryptedBuffer.End);
            }

            if (cipher != null)
            {
                connection.Input.AdvanceTo(buffer.End);
            }
            else
            {
                connection.Input.AdvanceTo(buffer.Start, buffer.End);
            }


            if (result.IsCanceled ||
                result.IsCompleted)
            {
                break;
            }
        }

        await connection.Output.CompleteAsync();
    }

    private bool IsNoOpCipher(byte[] spec)
    {
        var cipher = new CipherSpec(spec);
        var data = new byte[32];
        Random.Shared.NextBytes(data);
        var data2 = data.ToArray();
        cipher.Encode(data);
        return data.SequenceEqual(data2);
    }

    private byte[] ProcessRequest(ReadOnlySequence<byte> request)
    {
        List<int> requestedCount = new();
        SequenceReader<byte> reader = new SequenceReader<byte>(request);
        do
        {
            reader.TryReadTo(out ReadOnlySpan<byte> seq, "x"u8, false);
            int count = 0;
            for (int i = 0; i < seq.Length; i++)
            {
                count *= 10;
                count += seq[i] - 48;
            }
            requestedCount.Add(count);
        } while (reader.TryAdvanceTo((byte)','));
        
        reader.Rewind(reader.Consumed);
        int maxCount = 0;
        int pos = 0;
        for (int i = 0; i < requestedCount.Count; i++)
        {
            if (requestedCount[i] > maxCount)
            {
                maxCount = requestedCount[i];
                pos = i;
            }
        }

        for (int i = 0; i < pos; i++)
        {
            reader.TryAdvanceTo((byte)',');
        }

        if (reader.TryReadTo(out ReadOnlySpan<byte> toy, 
                ","u8, false))
        {
            return toy.ToArray().Concat("\n"u8.ToArray()).ToArray();
        }
        return reader.UnreadSequence.ToArray().Concat("\n"u8.ToArray()).ToArray();
    }

    private class CipherSpec
    {
        private readonly List<CipherOperation> _spec = new();
        private int _encodePos;
        private int _decodePos;

        public CipherSpec(byte[] spec)
        {
            int specPos = 0;
            while (spec[specPos] != 0)
            {
                if (spec[specPos] == 2 ||
                    spec[specPos] == 4)
                {
                    _spec.Add(new CipherOperation(spec[specPos], spec[specPos + 1]));
                    specPos += 2;
                }
                else
                {
                    _spec.Add(new CipherOperation(spec[specPos]));
                    specPos++;
                }
            }
        }

        public void Encode(Span<byte> data)
        {
            foreach (var op in _spec)
            {
                if (op.Operation == 1)
                {
                    ReverseBits(data);
                }
                else if (op.Operation == 2)
                {
                    Xor(data, op.Operand);
                }
                else if (op.Operation == 3)
                {
                    XorPos(data);
                }
                else if (op.Operation == 4)
                {
                    Add(data, op.Operand);
                }
                else if (op.Operation == 5)
                {
                    AddPos(data);
                }
            }

            _encodePos += data.Length;
        }
        
        public void Decode(Span<byte> data)
        {
            for (int i = _spec.Count - 1; i >= 0; i--)
            {
                var op = _spec[i];
                if (op.Operation == 1)
                {
                    ReverseBits(data);
                }
                else if (op.Operation == 2)
                {
                    Xor(data, op.Operand);
                }
                else if (op.Operation == 3)
                {
                    XorPosDecode(data);
                }
                else if (op.Operation == 4)
                {
                    Subtract(data, op.Operand);
                }
                else if (op.Operation == 5)
                {
                    SubtractPos(data);
                }
            }

            _decodePos += data.Length;
        }

        private void ReverseBits(Span<byte> data)
        {
            for (int i = 0; i < data.Length; i++)
            {
                data[i] = BitReverseTable[data[i]];
            }
        }
        private void Xor(Span<byte> data, byte n)
        {
            for (int i = 0; i < data.Length; i++)
            {
                data[i] ^= n;
            }
        }
        private void XorPos(Span<byte> data)
        {
            for (int i = 0; i < data.Length; i++)
            {
                data[i] = (byte)(data[i]  ^ (_encodePos + i));
            }
        }
        private void XorPosDecode(Span<byte> data)
        {
            for (int i = 0; i < data.Length; i++)
            {
                data[i] = (byte)(data[i]  ^ (_decodePos + i));
            }
        }
        private void Add(Span<byte> data, byte n)
        {
            for (int i = 0; i < data.Length; i++)
            {
                data[i] += n;
            }
        }
        private void Subtract(Span<byte> data, byte n)
        {
            for (int i = 0; i < data.Length; i++)
            {
                data[i] -= n;
            }
        }
        private void AddPos(Span<byte> data)
        {
            for (int i = 0; i < data.Length; i++)
            {
                data[i] += (byte)(_encodePos + i);
            }
        }
        private void SubtractPos(Span<byte> data)
        {
            for (int i = 0; i < data.Length; i++)
            {
                data[i] -= (byte)(_decodePos + i);
            }
        }
        //https://stackoverflow.com/a/3590938/2015348
        private static readonly byte[] BitReverseTable =
        {
            0x00, 0x80, 0x40, 0xc0, 0x20, 0xa0, 0x60, 0xe0,
            0x10, 0x90, 0x50, 0xd0, 0x30, 0xb0, 0x70, 0xf0,
            0x08, 0x88, 0x48, 0xc8, 0x28, 0xa8, 0x68, 0xe8,
            0x18, 0x98, 0x58, 0xd8, 0x38, 0xb8, 0x78, 0xf8,
            0x04, 0x84, 0x44, 0xc4, 0x24, 0xa4, 0x64, 0xe4,
            0x14, 0x94, 0x54, 0xd4, 0x34, 0xb4, 0x74, 0xf4,
            0x0c, 0x8c, 0x4c, 0xcc, 0x2c, 0xac, 0x6c, 0xec,
            0x1c, 0x9c, 0x5c, 0xdc, 0x3c, 0xbc, 0x7c, 0xfc,
            0x02, 0x82, 0x42, 0xc2, 0x22, 0xa2, 0x62, 0xe2,
            0x12, 0x92, 0x52, 0xd2, 0x32, 0xb2, 0x72, 0xf2,
            0x0a, 0x8a, 0x4a, 0xca, 0x2a, 0xaa, 0x6a, 0xea,
            0x1a, 0x9a, 0x5a, 0xda, 0x3a, 0xba, 0x7a, 0xfa,
            0x06, 0x86, 0x46, 0xc6, 0x26, 0xa6, 0x66, 0xe6,
            0x16, 0x96, 0x56, 0xd6, 0x36, 0xb6, 0x76, 0xf6,
            0x0e, 0x8e, 0x4e, 0xce, 0x2e, 0xae, 0x6e, 0xee,
            0x1e, 0x9e, 0x5e, 0xde, 0x3e, 0xbe, 0x7e, 0xfe,
            0x01, 0x81, 0x41, 0xc1, 0x21, 0xa1, 0x61, 0xe1,
            0x11, 0x91, 0x51, 0xd1, 0x31, 0xb1, 0x71, 0xf1,
            0x09, 0x89, 0x49, 0xc9, 0x29, 0xa9, 0x69, 0xe9,
            0x19, 0x99, 0x59, 0xd9, 0x39, 0xb9, 0x79, 0xf9,
            0x05, 0x85, 0x45, 0xc5, 0x25, 0xa5, 0x65, 0xe5,
            0x15, 0x95, 0x55, 0xd5, 0x35, 0xb5, 0x75, 0xf5,
            0x0d, 0x8d, 0x4d, 0xcd, 0x2d, 0xad, 0x6d, 0xed,
            0x1d, 0x9d, 0x5d, 0xdd, 0x3d, 0xbd, 0x7d, 0xfd,
            0x03, 0x83, 0x43, 0xc3, 0x23, 0xa3, 0x63, 0xe3,
            0x13, 0x93, 0x53, 0xd3, 0x33, 0xb3, 0x73, 0xf3,
            0x0b, 0x8b, 0x4b, 0xcb, 0x2b, 0xab, 0x6b, 0xeb,
            0x1b, 0x9b, 0x5b, 0xdb, 0x3b, 0xbb, 0x7b, 0xfb,
            0x07, 0x87, 0x47, 0xc7, 0x27, 0xa7, 0x67, 0xe7,
            0x17, 0x97, 0x57, 0xd7, 0x37, 0xb7, 0x77, 0xf7,
            0x0f, 0x8f, 0x4f, 0xcf, 0x2f, 0xaf, 0x6f, 0xef,
            0x1f, 0x9f, 0x5f, 0xdf, 0x3f, 0xbf, 0x7f, 0xff
        };
    }

    private readonly record struct CipherOperation(byte Operation, byte Operand = 0);
}