using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;

namespace Rats.Sim.Net
{
    public enum MessageType : byte
    {
        Hello = 1,     // client -> host: protocol version
        Welcome = 2,   // host -> client: tunables text, a round is starting
        Input = 3,     // client -> host: one InputFrame
        Snapshot = 4,  // host -> client: Simulation.SaveSnapshot bytes
        Restart = 5,   // host -> client: tunables text, a new round is starting
        Reject = 6,    // host -> client: reason text, then the host closes
    }

    public readonly struct Message
    {
        public readonly MessageType Type;
        public readonly byte[] Payload;
        public Message(MessageType type, byte[] payload) { Type = type; Payload = payload; }
    }

    /// <summary>
    /// Splits a byte stream into messages: [int32 length][byte type][payload], length = 1 + payload.
    /// Separate from the socket so partial reads can be tested directly.
    /// </summary>
    public sealed class FrameReader
    {
        const int MaxFrame = 1 << 20;
        byte[] buffer = new byte[4096];
        int count;

        public void Append(byte[] data, int offset, int length)
        {
            if (count + length > buffer.Length) Array.Resize(ref buffer, Math.Max(buffer.Length * 2, count + length));
            Buffer.BlockCopy(data, offset, buffer, count, length);
            count += length;
        }

        public bool TryRead(out Message message)
        {
            message = default;
            if (count < 4) return false;
            int length = BinaryPrimitives.ReadInt32LittleEndian(buffer);
            if (length < 1 || length > MaxFrame) throw new InvalidDataException($"bad frame length {length}");
            if (count < 4 + length) return false;
            var payload = new byte[length - 1];
            Buffer.BlockCopy(buffer, 5, payload, 0, payload.Length);
            message = new Message((MessageType)buffer[4], payload);
            count -= 4 + length;
            Buffer.BlockCopy(buffer, 4 + length, buffer, 0, count);
            return true;
        }

        public static byte[] Encode(MessageType type, byte[] payload)
        {
            var frame = new byte[5 + payload.Length];
            BinaryPrimitives.WriteInt32LittleEndian(frame, payload.Length + 1);
            frame[4] = (byte)type;
            Buffer.BlockCopy(payload, 0, frame, 5, payload.Length);
            return frame;
        }
    }

    /// <summary>
    /// One TCP connection, polled from the game loop (no threads). Any socket error closes it
    /// and records why; callers check IsOpen.
    /// </summary>
    public sealed class Connection : IDisposable
    {
        readonly TcpClient tcp;
        readonly NetworkStream stream;
        readonly FrameReader reader = new FrameReader();
        readonly byte[] readBuffer = new byte[8192];

        public bool IsOpen { get; private set; } = true;
        public string CloseReason { get; private set; }

        public Connection(TcpClient tcp)
        {
            this.tcp = tcp;
            tcp.NoDelay = true; // small messages every tick; don't batch them
            stream = tcp.GetStream();
        }

        public void Send(MessageType type, byte[] payload)
        {
            if (!IsOpen) return;
            try
            {
                var frame = FrameReader.Encode(type, payload);
                stream.Write(frame, 0, frame.Length);
            }
            catch (Exception e) when (e is IOException || e is SocketException || e is ObjectDisposedException)
            {
                Close(e.Message);
            }
        }

        /// <summary>Reads whatever has arrived and appends complete messages.</summary>
        public void Receive(List<Message> into)
        {
            if (!IsOpen) return;
            try
            {
                while (stream.DataAvailable)
                {
                    int n = stream.Read(readBuffer, 0, readBuffer.Length);
                    if (n <= 0) { Close("closed by the other side"); return; }
                    reader.Append(readBuffer, 0, n);
                }
                // Readable with nothing to read means the other side hung up.
                if (tcp.Client.Poll(0, SelectMode.SelectRead) && tcp.Client.Available == 0)
                    Close("closed by the other side");
                while (reader.TryRead(out var message)) into.Add(message);
            }
            catch (Exception e) when (e is IOException || e is SocketException || e is ObjectDisposedException || e is InvalidDataException)
            {
                Close(e.Message);
            }
        }

        /// <summary>
        /// Graceful hang-up: no more sends, but the socket stays open so the other side still gets
        /// what we already sent. Closing immediately can reset the connection and drop it.
        /// </summary>
        public void ShutdownSend()
        {
            try { tcp.Client.Shutdown(SocketShutdown.Send); }
            catch (Exception e) when (e is SocketException || e is ObjectDisposedException) { Close(e.Message); }
        }

        public void Close(string reason)
        {
            if (!IsOpen) return;
            IsOpen = false;
            CloseReason = reason;
            tcp.Close();
        }

        public void Dispose() => Close("closed");
    }
}
