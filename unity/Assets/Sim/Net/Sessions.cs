using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace Rats.Sim.Net
{
    /// <summary>
    /// Host-authoritative networking, step 1: direct connection by address (LAN or Tailscale).
    /// The host runs the only real sim; the client sends inputs and shows the host's snapshots.
    /// Matchmaking later replaces how the two sides find each other, not this protocol.
    /// </summary>
    public static class NetProtocol
    {
        public const int Version = 7; // 2: double snapshots, 3: lock-on timers, 4: drafting, 5: per-player offers, 6: match flow, 7: fire/downed
        public const int DefaultPort = 7777;

        public static byte[] Encode(InputFrame frame)
        {
            using var ms = new System.IO.MemoryStream();
            using (var w = new System.IO.BinaryWriter(ms)) frame.Write(w);
            return ms.ToArray();
        }

        public static InputFrame DecodeInput(byte[] payload) =>
            InputFrame.Read(new System.IO.BinaryReader(new System.IO.MemoryStream(payload)));
    }

    /// <summary>
    /// The remote player's input as seen by the host sim. Movement is the latest frame received;
    /// button presses accumulate until a sim tick reads them, so none are lost when several
    /// client frames arrive between host ticks.
    /// </summary>
    public sealed class RemoteInput : IInputSource
    {
        InputFrame latest;
        bool melee, dash, ranged, toggleLock;
        byte draftPick;

        public int FramesReceived { get; private set; }

        public void Push(InputFrame f)
        {
            FramesReceived++;
            latest = f;
            melee |= f.Melee;
            dash |= f.Dash;
            ranged |= f.Ranged;
            toggleLock |= f.ToggleLock;
            if (f.DraftPick != 0) draftPick = f.DraftPick;
        }

        public InputFrame Read(int tick)
        {
            var f = latest;
            f.Melee = melee; f.Dash = dash; f.Ranged = ranged; f.ToggleLock = toggleLock; f.DraftPick = draftPick;
            melee = dash = ranged = toggleLock = false;
            draftPick = 0;
            return f;
        }

        public void Clear()
        {
            latest = default;
            melee = dash = ranged = toggleLock = false;
            draftPick = 0;
        }
    }

    /// <summary>Listens for one opponent. Poll once per frame.</summary>
    public sealed class HostSession : IDisposable
    {
        readonly TcpListener listener;
        const int RejectLingerPolls = 300; // ~5 s of frames for a turned-away client to read why
        readonly List<Message> inbox = new List<Message>();
        readonly List<(Connection conn, int pollsLeft)> rejected = new List<(Connection, int)>();
        Connection client;
        bool greeted;

        public readonly RemoteInput RemoteInput = new RemoteInput();
        public int Port => ((IPEndPoint)listener.LocalEndpoint).Port;
        public bool HasPlayer => client != null && client.IsOpen && greeted;
        public string Status { get; private set; }

        public HostSession(int port)
        {
            listener = new TcpListener(IPAddress.Any, port);
            listener.Start();
            Status = $"Hosting on port {Port}, waiting for a player";
        }

        public enum Event { None, PlayerJoined, PlayerLeft }

        public Event Poll()
        {
            while (listener.Pending())
            {
                var incoming = new Connection(listener.AcceptTcpClient());
                if (client == null) client = incoming;
                else Reject(incoming, "game is full");
            }
            DrainRejected();
            if (client == null) return Event.None;

            var result = Event.None;
            inbox.Clear();
            client.Receive(inbox);
            foreach (var m in inbox)
            {
                if (m.Type == MessageType.Hello && !greeted)
                {
                    int version = m.Payload.Length >= 4 ? BitConverter.ToInt32(m.Payload, 0) : -1;
                    if (version != NetProtocol.Version)
                    {
                        Reject(client, $"version mismatch: host {NetProtocol.Version}, you {version}. Pull the latest code.");
                        client = null;
                        return Event.None;
                    }
                    greeted = true;
                    Status = "Player connected";
                    result = Event.PlayerJoined;
                }
                else if (m.Type == MessageType.Input && greeted)
                {
                    RemoteInput.Push(NetProtocol.DecodeInput(m.Payload));
                }
            }

            if (!client.IsOpen)
            {
                bool wasPlaying = greeted;
                Status = $"Player left ({client.CloseReason}); waiting for a player";
                client.Dispose();
                client = null;
                greeted = false;
                RemoteInput.Clear();
                if (wasPlaying) result = Event.PlayerLeft;
            }
            return result;
        }

        void Reject(Connection conn, string reason)
        {
            conn.Send(MessageType.Reject, Encoding.UTF8.GetBytes(reason));
            conn.ShutdownSend();
            rejected.Add((conn, RejectLingerPolls));
        }

        void DrainRejected()
        {
            for (int i = rejected.Count - 1; i >= 0; i--)
            {
                var (conn, pollsLeft) = rejected[i];
                inbox.Clear();
                conn.Receive(inbox); // discard whatever they send; notices when they hang up
                if (!conn.IsOpen || pollsLeft <= 1) { conn.Dispose(); rejected.RemoveAt(i); }
                else rejected[i] = (conn, pollsLeft - 1);
            }
        }

        public void SendWelcome(string tunablesText) => client?.Send(MessageType.Welcome, Encoding.UTF8.GetBytes(tunablesText));
        public void SendRestart(string tunablesText) => client?.Send(MessageType.Restart, Encoding.UTF8.GetBytes(tunablesText));
        public void SendSnapshot(byte[] snapshot) { if (HasPlayer) client.Send(MessageType.Snapshot, snapshot); }

        public void Dispose()
        {
            client?.Dispose();
            foreach (var (conn, _) in rejected) conn.Dispose();
            listener.Stop();
        }
    }

    /// <summary>Connects to a host by address. Poll once per frame.</summary>
    public sealed class ClientSession : IDisposable
    {
        readonly TcpClient tcp = new TcpClient();
        readonly Task connecting;
        readonly List<Message> inbox = new List<Message>();
        Connection host;
        byte[] latestSnapshot;

        public string Status { get; private set; }
        public bool Failed { get; private set; }
        public bool Connected => host != null && host.IsOpen;
        /// <summary>Host's tunables for the round in progress. Set by Welcome/Restart.</summary>
        public string RoundTunables { get; private set; }

        public ClientSession(string address, int port)
        {
            Status = $"Connecting to {address}:{port}...";
            connecting = tcp.ConnectAsync(address, port);
        }

        public enum Event { None, RoundStarted, Lost }

        public Event Poll()
        {
            if (Failed) return Event.None;
            if (host == null)
            {
                if (!connecting.IsCompleted) return Event.None;
                if (connecting.IsFaulted || !tcp.Connected)
                {
                    Failed = true;
                    Status = "Could not connect: " + (connecting.Exception?.GetBaseException().Message ?? "unknown error");
                    return Event.Lost;
                }
                host = new Connection(tcp);
                host.Send(MessageType.Hello, BitConverter.GetBytes(NetProtocol.Version));
                Status = "Connected, waiting for the host";
            }

            var result = Event.None;
            inbox.Clear();
            host.Receive(inbox);
            foreach (var m in inbox)
            {
                switch (m.Type)
                {
                    case MessageType.Welcome:
                    case MessageType.Restart:
                        RoundTunables = Encoding.UTF8.GetString(m.Payload);
                        latestSnapshot = null;
                        Status = "In match";
                        result = Event.RoundStarted;
                        break;
                    case MessageType.Snapshot:
                        latestSnapshot = m.Payload;
                        break;
                    case MessageType.Reject:
                        Status = "Rejected: " + Encoding.UTF8.GetString(m.Payload);
                        break;
                }
            }

            if (!host.IsOpen)
            {
                Failed = true;
                if (!Status.StartsWith("Rejected")) Status = $"Disconnected ({host.CloseReason})";
                return Event.Lost;
            }
            return result;
        }

        public void SendInput(InputFrame frame) => host?.Send(MessageType.Input, NetProtocol.Encode(frame));

        /// <summary>Newest snapshot since the last call, if any. Older ones are simply skipped.</summary>
        public bool TakeSnapshot(out byte[] snapshot)
        {
            snapshot = latestSnapshot;
            latestSnapshot = null;
            return snapshot != null;
        }

        public void Dispose()
        {
            if (host != null) host.Dispose();
            else tcp.Close();
        }
    }
}
