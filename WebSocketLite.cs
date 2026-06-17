using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Frtal.LorebookReader {

    /// <summary>
    /// Minimální WebSocket klient (RFC 6455) nad TcpClient + SslStream.
    /// Existuje proto, že ClientWebSocket na .NET Frameworku zakazuje
    /// nastavit User-Agent a další hlavičky, které Edge TTS endpoint
    /// vyžaduje. Podporuje jen to, co Edge TTS potřebuje:
    /// odeslání textových rámců a příjem text/binary rámců.
    /// </summary>
    public sealed class WebSocketLite : IDisposable {

        public enum FrameType { Text, Binary, Closed }

        private TcpClient _tcp;
        private SslStream _ssl;
        private readonly RandomNumberGenerator _rng = RandomNumberGenerator.Create();

        public async Task ConnectAsync(string host, string pathAndQuery,
                                       IDictionary<string, string> headers,
                                       CancellationToken ct) {
            _tcp = new TcpClient();
            using (ct.Register(() => { try { _tcp.Close(); } catch { } }))
                await _tcp.ConnectAsync(host, 443).ConfigureAwait(false);
            ct.ThrowIfCancellationRequested();

            _ssl = new SslStream(_tcp.GetStream(), false);
            await _ssl.AuthenticateAsClientAsync(
                host, null,
                System.Security.Authentication.SslProtocols.Tls12,
                false).ConfigureAwait(false);

            byte[] keyBytes = new byte[16];
            _rng.GetBytes(keyBytes);
            string wsKey = Convert.ToBase64String(keyBytes);

            var sb = new StringBuilder();
            sb.Append($"GET {pathAndQuery} HTTP/1.1\r\n");
            sb.Append($"Host: {host}\r\n");
            sb.Append("Connection: Upgrade\r\n");
            sb.Append("Upgrade: websocket\r\n");
            sb.Append("Sec-WebSocket-Version: 13\r\n");
            sb.Append($"Sec-WebSocket-Key: {wsKey}\r\n");
            foreach (var kv in headers)
                sb.Append($"{kv.Key}: {kv.Value}\r\n");
            sb.Append("\r\n");

            byte[] req = Encoding.ASCII.GetBytes(sb.ToString());
            await _ssl.WriteAsync(req, 0, req.Length, ct).ConfigureAwait(false);

            string response = await ReadHandshakeResponseAsync(ct)
                .ConfigureAwait(false);
            string statusLine = response.Split('\r')[0];
            if (!statusLine.Contains(" 101"))
                throw new IOException(
                    $"WebSocket handshake rejected: {statusLine.Trim()}");
        }

        private async Task<string> ReadHandshakeResponseAsync(CancellationToken ct) {
            var buf = new MemoryStream();
            var one = new byte[1];
            // číst do prázdného řádku (\r\n\r\n)
            while (true) {
                int n = await _ssl.ReadAsync(one, 0, 1, ct).ConfigureAwait(false);
                if (n == 0) throw new IOException("Connection closed during handshake.");
                buf.WriteByte(one[0]);
                if (buf.Length >= 4) {
                    byte[] a = buf.GetBuffer();
                    long L = buf.Length;
                    if (a[L - 4] == '\r' && a[L - 3] == '\n'
                        && a[L - 2] == '\r' && a[L - 1] == '\n')
                        break;
                }
                if (buf.Length > 64 * 1024)
                    throw new IOException("Handshake response too large.");
            }
            return Encoding.ASCII.GetString(buf.ToArray());
        }

        public Task SendTextAsync(string message, CancellationToken ct) =>
            SendFrameAsync(0x1, Encoding.UTF8.GetBytes(message), ct);

        private async Task SendFrameAsync(byte opcode, byte[] payload,
                                          CancellationToken ct) {
            var header = new MemoryStream();
            header.WriteByte((byte)(0x80 | opcode));           // FIN + opcode

            if (payload.Length < 126) {
                header.WriteByte((byte)(0x80 | payload.Length)); // masked + len
            } else if (payload.Length <= ushort.MaxValue) {
                header.WriteByte(0x80 | 126);
                header.WriteByte((byte)(payload.Length >> 8));
                header.WriteByte((byte)(payload.Length & 0xFF));
            } else {
                header.WriteByte(0x80 | 127);
                ulong len = (ulong)payload.Length;
                for (int i = 7; i >= 0; i--)
                    header.WriteByte((byte)(len >> (8 * i)));
            }

            byte[] mask = new byte[4];
            _rng.GetBytes(mask);
            header.Write(mask, 0, 4);

            byte[] masked = new byte[payload.Length];
            for (int i = 0; i < payload.Length; i++)
                masked[i] = (byte)(payload[i] ^ mask[i % 4]);

            byte[] head = header.ToArray();
            await _ssl.WriteAsync(head, 0, head.Length, ct).ConfigureAwait(false);
            await _ssl.WriteAsync(masked, 0, masked.Length, ct).ConfigureAwait(false);
            await _ssl.FlushAsync(ct).ConfigureAwait(false);
        }

        /// <summary>Přijme jednu kompletní zprávu (skládá fragmenty,
        /// odpovídá na ping). Closed = server ukončil spojení.</summary>
        public async Task<(FrameType Type, byte[] Data)> ReceiveAsync(
                CancellationToken ct) {
            var message = new MemoryStream();
            int messageOpcode = -1;

            while (true) {
                byte b0 = await ReadByteAsync(ct).ConfigureAwait(false);
                byte b1 = await ReadByteAsync(ct).ConfigureAwait(false);
                bool fin = (b0 & 0x80) != 0;
                int opcode = b0 & 0x0F;
                bool maskedByServer = (b1 & 0x80) != 0;   // nemělo by nastat
                long len = b1 & 0x7F;

                if (len == 126) {
                    len = (await ReadByteAsync(ct).ConfigureAwait(false) << 8)
                        | await ReadByteAsync(ct).ConfigureAwait(false);
                } else if (len == 127) {
                    len = 0;
                    for (int i = 0; i < 8; i++)
                        len = (len << 8)
                            | await ReadByteAsync(ct).ConfigureAwait(false);
                }

                byte[] maskKey = null;
                if (maskedByServer) {
                    maskKey = await ReadExactAsync(4, ct).ConfigureAwait(false);
                }

                byte[] payload = await ReadExactAsync((int)len, ct)
                    .ConfigureAwait(false);
                if (maskKey != null)
                    for (int i = 0; i < payload.Length; i++)
                        payload[i] ^= maskKey[i % 4];

                switch (opcode) {
                    case 0x8:                                  // close
                        return (FrameType.Closed, payload);
                    case 0x9:                                  // ping -> pong
                        await SendFrameAsync(0xA, payload, ct)
                            .ConfigureAwait(false);
                        continue;
                    case 0xA:                                  // pong
                        continue;
                    case 0x1:
                    case 0x2:
                        messageOpcode = opcode;
                        message.Write(payload, 0, payload.Length);
                        break;
                    case 0x0:                                  // continuation
                        message.Write(payload, 0, payload.Length);
                        break;
                }

                if (fin && messageOpcode != -1) {
                    return (messageOpcode == 0x1 ? FrameType.Text
                                                 : FrameType.Binary,
                            message.ToArray());
                }
            }
        }

        private async Task<byte> ReadByteAsync(CancellationToken ct) {
            byte[] one = await ReadExactAsync(1, ct).ConfigureAwait(false);
            return one[0];
        }

        private async Task<byte[]> ReadExactAsync(int count, CancellationToken ct) {
            byte[] buf = new byte[count];
            int read = 0;
            using (ct.Register(() => { try { _tcp.Close(); } catch { } })) {
                while (read < count) {
                    int n = await _ssl.ReadAsync(buf, read, count - read, ct)
                        .ConfigureAwait(false);
                    if (n == 0)
                        throw new IOException("Connection closed by server.");
                    read += n;
                }
            }
            return buf;
        }

        public void Dispose() {
            try { _ssl?.Dispose(); } catch { }
            try { _tcp?.Close(); } catch { }
            _rng.Dispose();
        }
    }
}
