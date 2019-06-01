using SAEA.Mongo.Bson.IO;
using SAEA.Mongo.Driver.Core.Misc;
using SAEA.Sockets.Interface;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SAEA.Mongo.Driver.Core.Core.Misc
{
    internal static class ClietnSocketExtension
    {
        public static void ReadBytes(this IClientSocket client, byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            Ensure.IsNotNull(client, nameof(client));
            Ensure.IsNotNull(buffer, nameof(buffer));
            Ensure.IsBetween(offset, 0, buffer.Length, nameof(offset));
            Ensure.IsBetween(count, 0, buffer.Length - offset, nameof(count));

            while (count > 0)
            {
                var bytesRead = client.ReceiveAsync(buffer, offset, count, cancellationToken).GetAwaiter().GetResult();
                if (bytesRead == 0)
                {
                    throw new EndOfStreamException();
                }
                offset += bytesRead;
                count -= bytesRead;
            }
        }

        public static void ReadBytes(this IClientSocket client, IByteBuffer buffer, int offset, int count, CancellationToken cancellationToken)
        {
            Ensure.IsNotNull(client, nameof(client));
            Ensure.IsNotNull(buffer, nameof(buffer));
            Ensure.IsBetween(offset, 0, buffer.Length, nameof(offset));
            Ensure.IsBetween(count, 0, buffer.Length - offset, nameof(count));

            while (count > 0)
            {
                var backingBytes = buffer.AccessBackingBytes(offset);
                var bytesToRead = Math.Min(count, backingBytes.Count);
                var bytesRead = client.ReceiveAsync(backingBytes.Array, backingBytes.Offset, bytesToRead, cancellationToken).GetAwaiter().GetResult();
                if (bytesRead == 0)
                {
                    throw new EndOfStreamException();
                }
                offset += bytesRead;
                count -= bytesRead;
            }
        }

        public static async Task ReadBytesAsync(this IClientSocket client, byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            Ensure.IsNotNull(client, nameof(client));
            Ensure.IsNotNull(buffer, nameof(buffer));
            Ensure.IsBetween(offset, 0, buffer.Length, nameof(offset));
            Ensure.IsBetween(count, 0, buffer.Length - offset, nameof(count));

            while (count > 0)
            {
                var bytesRead = await client.ReceiveAsync(buffer, offset, count, cancellationToken).ConfigureAwait(false);
                if (bytesRead == 0)
                {
                    throw new EndOfStreamException();
                }
                offset += bytesRead;
                count -= bytesRead;
            }
        }

        public static async Task ReadBytesAsync(this IClientSocket client, IByteBuffer buffer, int offset, int count, CancellationToken cancellationToken)
        {
            Ensure.IsNotNull(client, nameof(client));
            Ensure.IsNotNull(buffer, nameof(buffer));
            Ensure.IsBetween(offset, 0, buffer.Length, nameof(offset));
            Ensure.IsBetween(count, 0, buffer.Length - offset, nameof(count));

            while (count > 0)
            {
                var backingBytes = buffer.AccessBackingBytes(offset);
                var bytesToRead = Math.Min(count, backingBytes.Count);
                var bytesRead = await client.ReceiveAsync(backingBytes.Array, backingBytes.Offset, bytesToRead, cancellationToken).ConfigureAwait(false);
                if (bytesRead == 0)
                {
                    throw new EndOfStreamException();
                }
                offset += bytesRead;
                count -= bytesRead;
            }
        }

        public static void WriteBytes(this IClientSocket client, IByteBuffer buffer, int offset, int count, CancellationToken cancellationToken)
        {
            Ensure.IsNotNull(client, nameof(client));
            Ensure.IsNotNull(buffer, nameof(buffer));
            Ensure.IsBetween(offset, 0, buffer.Length, nameof(offset));
            Ensure.IsBetween(count, 0, buffer.Length - offset, nameof(count));

            while (count > 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var backingBytes = buffer.AccessBackingBytes(offset);
                var bytesToWrite = Math.Min(count, backingBytes.Count);
                client.SendAsync(backingBytes.Array, backingBytes.Offset, bytesToWrite, cancellationToken);
                offset += bytesToWrite;
                count -= bytesToWrite;
            }
        }

        public static async Task WriteBytesAsync(this IClientSocket client, IByteBuffer buffer, int offset, int count, CancellationToken cancellationToken)
        {
            Ensure.IsNotNull(client, nameof(client));
            Ensure.IsNotNull(buffer, nameof(buffer));
            Ensure.IsBetween(offset, 0, buffer.Length, nameof(offset));
            Ensure.IsBetween(count, 0, buffer.Length - offset, nameof(count));

            while (count > 0)
            {
                var backingBytes = buffer.AccessBackingBytes(offset);
                var bytesToWrite = Math.Min(count, backingBytes.Count);
                await client.SendAsync(backingBytes.Array, backingBytes.Offset, bytesToWrite, cancellationToken).ConfigureAwait(false);
                offset += bytesToWrite;
                count -= bytesToWrite;
            }
        }
    }
}
