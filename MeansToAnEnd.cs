using System.Buffers;
using System.Buffers.Binary;
using protohackers.Transport;

namespace protohackers;

public class MeansToAnEnd : TcpServerBase
{
    protected override async Task ProcessConnection(Connection connection)
    {
        SortedSet<TimestampedPrice> prices = new SortedSet<TimestampedPrice>(new ByTimestamp());
        var responseBuffer = new byte[4];
        while (true)
        {
            var result = await connection.Input.ReadAsync();
            var buffer = result.Buffer;

            do
            {
                if (buffer.Length >= 9)
                {
                    var request = buffer.Slice(0, 9);
                    var response = ProcessRequest(request, prices);
                    if (response != null)
                    {
                        BinaryPrimitives.WriteInt32BigEndian(responseBuffer, response.Value);
                        await connection.Output.WriteAsync(responseBuffer);
                    }
                    buffer = buffer.Slice(buffer.GetPosition(1, request.End));
                }
            } while (buffer.Length >= 9);
            connection.Input.AdvanceTo(buffer.Start, buffer.End);

            if (result.IsCanceled ||
                result.IsCompleted)
            {
                break;
            }
        }

        await connection.Output.CompleteAsync();
    }

    private int? ProcessRequest(ReadOnlySequence<byte> request, SortedSet<TimestampedPrice> prices)
    {
        SequenceReader<byte> reader = new SequenceReader<byte>(request);
        reader.TryRead(out var q);
        Span<byte> numBytes = stackalloc byte[4];
        if (q == 'I')
        {
            reader.TryCopyTo(numBytes);
            int timestamp = BinaryPrimitives.ReadInt32BigEndian(numBytes);
            reader.TryCopyTo(numBytes);
            int price = BinaryPrimitives.ReadInt32BigEndian(numBytes);
            prices.Add(new TimestampedPrice(timestamp, price));
        }
        else if (q == 'Q')
        {
            reader.TryCopyTo(numBytes);
            int low = BinaryPrimitives.ReadInt32BigEndian(numBytes);
            reader.TryCopyTo(numBytes);
            int high = BinaryPrimitives.ReadInt32BigEndian(numBytes);
            var filtered = prices.GetViewBetween(
                new TimestampedPrice(low, 0),
                new TimestampedPrice(high, 0));
            int sum = 0;
            foreach (var p in filtered)
            {
                sum += p.Price;
            }

            return sum / filtered.Count;
        }

        return null;
    }
    private readonly record struct TimestampedPrice(int Timestamp, int Price);
    private class ByTimestamp : IComparer<TimestampedPrice>
    {
        public int Compare(TimestampedPrice x, TimestampedPrice y)
        {
            return x.Timestamp.CompareTo(y.Timestamp);
        }
    }
}