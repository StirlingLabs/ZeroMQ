using System;
using System.Buffers;
using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using JetBrains.Annotations;

namespace ZeroMQ
{
    public delegate ZapResult ZapCallbackNull(string version, string sequence, string domain, string address, string? identity);

    public delegate ZapResult ZapCallbackPlain(string version, string sequence, string domain, string address, string? identity,
        string? userName, string? password);

    public delegate ZapResult ZapCallbackCurve(string version, string sequence, string domain, string address, string? identity,
        Span<byte> pubKey);

    public delegate ZapResult ZapCallbackGssapi(string version, string sequence, string domain, string address, string? identity,
        byte[] principal);

    [PublicAPI]
    public class ZapClient : IDisposable
    {
        public ZapCallbackNull? AuthenticateNull { get; set; }

        public ZapCallbackPlain? AuthenticatePlain { get; set; }

        public ZapCallbackCurve? AuthenticateCurve { get; set; }

        public ZapCallbackGssapi? AuthenticateGssapi { get; set; }

        private readonly CancellationTokenSource _ctsThread = new();
        private readonly Thread _thread;

        public bool SecurityCritical = true;
        public ZapClient()
        {
            using var readySignal = new SemaphoreSlim(0, 1);
            _thread = new(AuthLoop) { IsBackground = false, Priority = ThreadPriority.AboveNormal };
            _thread.Start((this, readySignal));
            readySignal.Wait();
        }

        private void AuthLoop(object? o)
        {
            var (client, ready) = ((ZapClient, SemaphoreSlim))o!;

            using var socket = new ZSocket(ZSocketType.REP)
            {
                Affinity = 1,
                Backlog = 1,
                SendHighWatermark = 100,
                ReceiveHighWatermark = 100,
                SendBufferSize = 100 * 512,
                ReceiveBufferSize = 100 * 512,
                Immediate = true
            };

            socket.Bind("inproc://zeromq.zap.01");

            ready.Release();

            Span<byte> pubKey = stackalloc byte[32];
            Unsafe.InitBlock(ref pubKey.GetPinnableReference(), 0, 32);

            void ReadNextFrame(ref ZFrame frame)
            {
                Debug.Assert(frame.GetOption(ZFrameOption.MORE) != 0);
                for (;;)
                {
                    frame = socket.ReceiveFrame(out var error)!;

                    if (error == ZError.EAGAIN)
                    {
                        Thread.Yield();
                        continue;
                    }

                    // ReSharper disable once ConditionIsAlwaysTrueOrFalse
                    Debug.Assert(frame is not null);

                    if (error is null) break;

                    var msg = "ZAP missing expected message frame.";

                    if (SecurityCritical)
                        Environment.FailFast(msg);
                    else
                        throw new NotImplementedException(msg);
                }
            }

            void ReadRemainingFrames(ref ZFrame frame)
            {
                while (frame.GetOption(ZFrameOption.MORE) != 0)
                    ReadNextFrame(ref frame);
            }

            void DispatchFrame(ZFrame frame, bool last)
            {
                int i;
                for (i = 1; i <= 3; ++i)
                {
                    socket.SendFrame(frame, last ? ZSocketFlags.None : ZSocketFlags.More, out var zError);
                    if (zError != ZError.EAGAIN)
                        break;
                    Thread.Yield();
                }
                if (i <= 3) return;

                if (SecurityCritical)
                    Environment.FailFast("Can't answer ZAP authentication request.");
                else
                    throw new InvalidOperationException();
            }

            void DispatchStringFrame(string msg, bool last = false)
            {
                using var reply = ZFrame.Create(msg);
                DispatchFrame(reply, last);
            }

            void DispatchDataFrame(ReadOnlyMemory<byte> msg, bool last = false)
            {
                using var reply = ZFrame.Create(msg);
                DispatchFrame(reply, last);
            }

            do
            {

                var frame = socket.ReceiveFrame(out var error);

                if (error == ZError.EAGAIN)
                {
                    Thread.Yield();
                    continue;
                }

                Debug.Assert(frame is not null);

                var version = frame.ReadString();

                if (version != "1.0")
                {
                    var msg = $"ZAP Version {version} required, only 1.0 implemented.";

                    if (SecurityCritical)
                        Environment.FailFast(msg);
                    else
                        throw new NotImplementedException(msg);
                }

                ReadNextFrame(ref frame);

                var sequence = frame.ReadString();
                ReadNextFrame(ref frame);

                var domain = frame.ReadString();
                ReadNextFrame(ref frame);

                var address = frame.ReadString();
                ReadNextFrame(ref frame);

                var identity = frame.ReadString();
                ReadNextFrame(ref frame);

                var mechanism = frame.ReadString();

                //Debug.Assert(version is not null);
                Debug.Assert(sequence is not null);
                Debug.Assert(domain is not null);
                Debug.Assert(address is not null);
                Debug.Assert(identity is not null);
                Debug.Assert(mechanism is not null);

                ZapResult result;

                switch (mechanism)
                {
                    case "NULL": {
                        try
                        {
                            result = client.AuthenticateNull?.Invoke(version, sequence, domain, address, identity)
                                ?? 405;
                        }
#if NET5_0_OR_GREATER
                        catch (HttpRequestException ex)
                        {
                            var sc = (int)(ex.StatusCode ?? HttpStatusCode.InternalServerError);
                            result = sc is >= 300 and <= 599 ? sc : 500;
                        }
#endif
                        catch
                        {
                            result = 500;
                        }
                        break;
                    }
                    case "PLAIN": {
                        ReadNextFrame(ref frame);
                        var userNameStr = frame.ReadString();
                        ReadNextFrame(ref frame);
                        var passwordStr = frame.ReadString();
                        Debug.Assert(userNameStr is not null);
                        Debug.Assert(passwordStr is not null);
                        try
                        {
                            result = client.AuthenticatePlain?.Invoke(version, sequence, domain, address, identity, userNameStr,
                                passwordStr) ?? 405;
                        }
#if NET5_0_OR_GREATER
                        catch (HttpRequestException ex)
                        {
                            var sc = (int)(ex.StatusCode ?? HttpStatusCode.InternalServerError);
                            result = sc is >= 300 and <= 599 ? sc : 500;
                        }
#endif
                        catch
                        {
                            result = 500;
                        }
                        break;
                    }
                    case "CURVE": {
                        // ReSharper disable once RedundantAssignment
                        ReadNextFrame(ref frame);
                        var size = frame.Read(pubKey);
                        Debug.Assert(size == 32);
                        try
                        {
                            result = client.AuthenticateCurve?.Invoke(version, sequence, domain, address, identity,
                                pubKey) ?? 405;
                        }
#if NET5_0_OR_GREATER
                        catch (HttpRequestException ex)
                        {
                            var sc = (int)(ex.StatusCode ?? HttpStatusCode.InternalServerError);
                            result = sc is >= 300 and <= 599 ? sc : 500;
                        }
#endif
                        catch
                        {
                            result = 500;
                        }
                        Unsafe.InitBlock(ref pubKey.GetPinnableReference(), 0, 32);
                        break;
                    }
                    case "GSSAPI": {
                        ReadNextFrame(ref frame);
                        var size = frame.GetAvailableBytes();
                        var principal = ArrayPool<byte>.Shared.Rent((int)size);
                        frame.Read(principal);
                        try
                        {
                            result = client.AuthenticateGssapi?.Invoke(version, sequence, domain, address, identity,
                                principal) ?? 405;
                        }
#if NET5_0_OR_GREATER
                        catch (HttpRequestException ex)
                        {
                            var sc = (int)(ex.StatusCode ?? HttpStatusCode.InternalServerError);
                            result = sc is >= 300 and <= 599 ? sc : 500;
                        }
#endif
                        catch
                        {
                            result = 500;
                        }
#if NET5_0_OR_GREATER
                        Unsafe.InitBlock(ref MemoryMarshal.GetArrayDataReference(principal), 0, (uint)size);
#else
                        Unsafe.InitBlock(ref principal[0], 0, (uint)size);
#endif
                        ArrayPool<byte>.Shared.Return(principal);
                        break;
                    }
                    default: throw new NotSupportedException();
                }

                ReadRemainingFrames(ref frame);

                using var f = ZFrame.Create();
                DispatchStringFrame("1.0");
                DispatchStringFrame(sequence!);

                // "fix" the status code to the supported values of
                // "only 200, 300, 400 and 500" per zap_client.cpp
                var statusCode = result.StatusCode / 100 * 100;
                var statusCodeStr = statusCode.ToString();
                DispatchStringFrame(statusCodeStr);

                DispatchStringFrame(result.StatusText);

#if NET5_0_OR_GREATER || NETCOREAPP3_1_OR_GREATER
                var id = result.Identity.TrimEnd((byte)0);
#else
                var id = result.Identity;
                while (id.Span.Slice(-1)[0] == 0)
                    id = id.Slice(0, -1);
#endif

                Debug.Assert(statusCode == 200 ? !id.IsEmpty : id.IsEmpty);
                DispatchDataFrame(id);
                var md = result.Metadata.Encode();
                DispatchDataFrame(md, true);

#if NET5_0_OR_GREATER
                Debug.WriteLine(
                    $"ZAP {System.Text.Encoding.UTF8.GetString(result.Identity.Span)} on {domain}, {statusCode}: {result.StatusText}");
#endif

            } while (!client._ctsThread.IsCancellationRequested);
        }

        public void Dispose()
        {
            _ctsThread.Cancel();
            _thread.Join();
            _ctsThread.Dispose();
        }
    }
}
