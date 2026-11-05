using Microsoft.VisualStudio.TestTools.UnitTesting;
using SAEA.FTP;
using SAEA.FTP.Model;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace SAEA.FTP.Tests
{
    [TestClass]
    public class FTPTransferTests
    {
        string _root;

        FTPServer _server;

        int _port;

        const string User = "tester";

        const string Password = "secret";

        [TestInitialize]
        public void Setup()
        {
            _root = Path.Combine(Path.GetTempPath(), "SAEA.FTP.Tests", Guid.NewGuid().ToString("N"));

            Directory.CreateDirectory(_root);

            StartServer(FTPOverwritePolicy.Overwrite, 0);
        }

        [TestCleanup]
        public void Cleanup()
        {
            try { _server?.Dispose(); } catch { }

            try { Directory.Delete(_root, true); } catch { }
        }

        void StartServer(FTPOverwritePolicy policy, int maxSessionsPerUser)
        {
            _port = GetFreePort();

            var config = new ServerConfig()
            {
                IP = "127.0.0.1",
                Port = (ushort)_port,
                BufferSize = 64 * 1024,
                MaxSessionsPerUser = maxSessionsPerUser,
                OverwritePolicy = policy,
                DataTimeout = TimeSpan.FromSeconds(30)
            };

            config.Users[User] = new FTPUser(User, Password, _root);

            _server = new FTPServer(config);

            _server.Start();
        }

        FTPClient CreateClient()
        {
            var client = new FTPClient(new ClientConfig()
            {
                IP = "127.0.0.1",
                Port = _port,
                UserName = User,
                Password = Password,
                BufferSize = 32 * 1024,
                ConnectTimeout = TimeSpan.FromSeconds(10),
                CommandTimeout = TimeSpan.FromSeconds(20),
                DataTimeout = TimeSpan.FromSeconds(30)
            });

            client.Connect();

            return client;
        }

        static int GetFreePort()
        {
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            var port = ((IPEndPoint)listener.LocalEndpoint).Port;
            listener.Stop();
            return port;
        }

        static byte[] CreatePayload(int size, int seed)
        {
            var data = new byte[size];
            var random = new Random(seed);
            random.NextBytes(data);
            return data;
        }

        static FTPTransferOptions FastParallelOptions()
        {
            return new FTPTransferOptions()
            {
                MaxSessions = 4,
                ParallelThresholdBytes = 256 * 1024,
                MinSegmentSize = 128 * 1024,
                BufferSize = 32 * 1024,
                MaxBufferSize = 128 * 1024,
                MaxRetries = 2,
                RetryDelay = TimeSpan.FromMilliseconds(50)
            };
        }

        [TestMethod]
        public void UploadAndDownload_Roundtrip()
        {
            var payload = CreatePayload(128 * 1024, 1);

            var local = Path.Combine(_root, "local.bin");

            File.WriteAllBytes(local, payload);

            using (var client = CreateClient())
            {
                var upload = client.Upload(local, "remote.bin", new FTPTransferOptions());

                Assert.AreEqual(FTPTransferStatus.Completed, upload.Status);

                var download = client.Download("remote.bin", Path.Combine(_root, "down.bin"), new FTPTransferOptions());

                Assert.AreEqual(FTPTransferStatus.Completed, download.Status);
                Assert.IsTrue(File.Exists(download.LocalPath));
                CollectionAssert.AreEqual(payload, File.ReadAllBytes(download.LocalPath));
            }
        }

        [TestMethod]
        public void Upload_ExistingRemote_SkipsByDefault()
        {
            var original = CreatePayload(4096, 2);

            File.WriteAllBytes(Path.Combine(_root, "dup.bin"), original);

            var local = Path.Combine(_root, "other.bin");

            File.WriteAllBytes(local, CreatePayload(8192, 3));

            using (var client = CreateClient())
            {
                var result = client.Upload(local, "dup.bin", new FTPTransferOptions());

                Assert.AreEqual(FTPTransferStatus.Skipped, result.Status);
                Assert.AreEqual(FTPConflictAction.Skip, result.ConflictAction);
                CollectionAssert.AreEqual(original, File.ReadAllBytes(Path.Combine(_root, "dup.bin")));
            }
        }

        [TestMethod]
        public void Upload_ExistingRemote_OverwriteViaHandler()
        {
            File.WriteAllBytes(Path.Combine(_root, "dup.bin"), CreatePayload(4096, 4));

            var payload = CreatePayload(8192, 5);

            var local = Path.Combine(_root, "other.bin");

            File.WriteAllBytes(local, payload);

            var options = new FTPTransferOptions()
            {
                ConflictHandler = ctx => new FTPConflictResult() { Action = FTPConflictAction.Overwrite }
            };

            using (var client = CreateClient())
            {
                var result = client.Upload(local, "dup.bin", options);

                Assert.AreEqual(FTPTransferStatus.Completed, result.Status);
                CollectionAssert.AreEqual(payload, File.ReadAllBytes(Path.Combine(_root, "dup.bin")));
            }
        }

        [TestMethod]
        public void Upload_ExistingRemote_RenameViaHandler()
        {
            File.WriteAllBytes(Path.Combine(_root, "dup.bin"), CreatePayload(1024, 6));

            var payload = CreatePayload(2048, 7);

            var local = Path.Combine(_root, "other.bin");

            File.WriteAllBytes(local, payload);

            var options = new FTPTransferOptions()
            {
                ConflictHandler = ctx => new FTPConflictResult() { Action = FTPConflictAction.Rename, NewName = "renamed.bin" }
            };

            using (var client = CreateClient())
            {
                var result = client.Upload(local, "dup.bin", options);

                Assert.AreEqual(FTPTransferStatus.Completed, result.Status);
                Assert.AreEqual("/renamed.bin", result.RemotePath);
                CollectionAssert.AreEqual(payload, File.ReadAllBytes(Path.Combine(_root, "renamed.bin")));
            }
        }

        [TestMethod]
        public void Download_ExistingLocal_SkipsByDefault_AndOverwriteHandlerWorks()
        {
            var payload = CreatePayload(8192, 8);

            File.WriteAllBytes(Path.Combine(_root, "remote.bin"), payload);

            var target = Path.Combine(_root, "target.bin");

            var existing = CreatePayload(1024, 9);

            File.WriteAllBytes(target, existing);

            using (var client = CreateClient())
            {
                var skipped = client.Download("remote.bin", target, new FTPTransferOptions());

                Assert.AreEqual(FTPTransferStatus.Skipped, skipped.Status);
                CollectionAssert.AreEqual(existing, File.ReadAllBytes(target));

                var options = new FTPTransferOptions()
                {
                    ConflictHandler = ctx => new FTPConflictResult() { Action = FTPConflictAction.Overwrite }
                };

                var overwritten = client.Download("remote.bin", target, options);

                Assert.AreEqual(FTPTransferStatus.Completed, overwritten.Status);
                CollectionAssert.AreEqual(payload, File.ReadAllBytes(target));
            }
        }

        [TestMethod]
        public void ParallelDownload_UsesMultipleSessions()
        {
            var payload = CreatePayload(2 * 1024 * 1024, 10);

            File.WriteAllBytes(Path.Combine(_root, "big.bin"), payload);

            using (var client = CreateClient())
            {
                var result = client.Download("big.bin", Path.Combine(_root, "big-down.bin"), FastParallelOptions());

                Assert.AreEqual(FTPTransferStatus.Completed, result.Status);
                Assert.IsTrue(result.SessionsUsed > 1, "expected parallel sessions, got " + result.SessionsUsed);
                CollectionAssert.AreEqual(payload, File.ReadAllBytes(result.LocalPath));
            }
        }

        [TestMethod]
        public void ParallelUpload_UsesMultipleSessions()
        {
            var payload = CreatePayload(2 * 1024 * 1024, 11);

            var local = Path.Combine(_root, "big-up.bin");

            File.WriteAllBytes(local, payload);

            using (var client = CreateClient())
            {
                var result = client.Upload(local, "big-remote.bin", FastParallelOptions());

                Assert.AreEqual(FTPTransferStatus.Completed, result.Status);
                Assert.IsTrue(result.SessionsUsed > 1, "expected parallel sessions, got " + result.SessionsUsed);
                CollectionAssert.AreEqual(payload, File.ReadAllBytes(Path.Combine(_root, "big-remote.bin")));
            }
        }

        [TestMethod]
        public void Download_ResumeAcrossCalls_UsesPartialFile()
        {
            var payload = CreatePayload(300 * 1024, 12);

            var remoteFile = Path.Combine(_root, "resume.bin");

            File.WriteAllBytes(remoteFile, payload);

            var target = Path.Combine(_root, "resume-down.bin");

            var temp = Path.Combine(_root, ".resume-down.bin.saea-part");

            var already = 100 * 1024;

            using (var fs = new FileStream(temp, FileMode.Create, FileAccess.Write))
            {
                fs.Write(payload, 0, already);
            }

            var ticks = File.GetLastWriteTimeUtc(remoteFile).ToString("yyyyMMddHHmmss");

            File.WriteAllText(temp + ".meta", "remoteSize=" + payload.Length + Environment.NewLine + "remoteTicks=" + ticks + Environment.NewLine);

            using (var client = CreateClient())
            {
                var options = new FTPTransferOptions() { EnableResumeAcrossCalls = true };

                var result = client.Download("resume.bin", target, options);

                Assert.AreEqual(FTPTransferStatus.Completed, result.Status);
                Assert.IsTrue(result.Resumed, "expected resume");
                CollectionAssert.AreEqual(payload, File.ReadAllBytes(target));
                Assert.IsFalse(File.Exists(temp + ".meta"));
            }
        }

        [TestMethod]
        public void ServerRejectPolicy_OverwriteUploadFails()
        {
            _server.Dispose();

            StartServer(FTPOverwritePolicy.Reject, 0);

            File.WriteAllBytes(Path.Combine(_root, "reject.bin"), CreatePayload(1024, 13));

            var local = Path.Combine(_root, "local-reject.bin");

            File.WriteAllBytes(local, CreatePayload(2048, 14));

            var options = new FTPTransferOptions()
            {
                ConflictHandler = ctx => new FTPConflictResult() { Action = FTPConflictAction.Overwrite }
            };

            using (var client = CreateClient())
            {
                var result = client.Upload(local, "reject.bin", options);

                Assert.AreEqual(FTPTransferStatus.Failed, result.Status);
                Assert.IsNotNull(result.Error);
            }
        }

        [TestMethod]
        public void ServerRenamePolicy_UploadReturnsRenamed()
        {
            _server.Dispose();

            StartServer(FTPOverwritePolicy.Rename, 0);

            File.WriteAllBytes(Path.Combine(_root, "rename.bin"), CreatePayload(1024, 15));

            var payload = CreatePayload(2048, 16);

            var local = Path.Combine(_root, "local-rename.bin");

            File.WriteAllBytes(local, payload);

            var options = new FTPTransferOptions()
            {
                ConflictHandler = ctx => new FTPConflictResult() { Action = FTPConflictAction.Overwrite }
            };

            using (var client = CreateClient())
            {
                var result = client.Upload(local, "rename.bin", options);

                Assert.AreEqual(FTPTransferStatus.Renamed, result.Status);
                Assert.AreNotEqual("/rename.bin", result.RemotePath);
                Assert.IsTrue(File.Exists(Path.Combine(_root, result.RemotePath.TrimStart('/'))));
            }
        }

        [TestMethod]
        public void MaxSessionsOne_UsesSingleSessionForLargeFiles()
        {
            var payload = CreatePayload(2 * 1024 * 1024, 18);

            var local = Path.Combine(_root, "single-up.bin");

            File.WriteAllBytes(local, payload);

            var options = FastParallelOptions();

            options.MaxSessions = 1;

            using (var client = CreateClient())
            {
                var upload = client.Upload(local, "single-remote.bin", options);

                Assert.AreEqual(FTPTransferStatus.Completed, upload.Status);
                Assert.AreEqual(1, upload.SessionsUsed);

                var download = client.Download("single-remote.bin", Path.Combine(_root, "single-down.bin"), options);

                Assert.AreEqual(FTPTransferStatus.Completed, download.Status);
                Assert.AreEqual(1, download.SessionsUsed);
                CollectionAssert.AreEqual(payload, File.ReadAllBytes(download.LocalPath));
            }
        }

        [TestMethod]
        public void Feat_AdvertisesExtensionsAndOverwritePolicy()
        {
            var lines = SendControlCommands("FEAT");

            var text = string.Join("\n", lines);

            StringAssert.Contains(text, "REST STREAM");
            StringAssert.Contains(text, "SIZE");
            StringAssert.Contains(text, "MDTM");
            StringAssert.Contains(text, "MLSD");
            StringAssert.Contains(text, "EPSV");
            StringAssert.Contains(text, "SAEA-OVERWRITE=Overwrite");
        }

        [TestMethod]
        public void Mlsd_UsesStandardFormat()
        {
            File.WriteAllBytes(Path.Combine(_root, "list.bin"), CreatePayload(1234, 17));

            var lines = SendControlCommands("MLSD");

            var entry = lines.FirstOrDefault(l => l.EndsWith("list.bin"));

            Assert.IsNotNull(entry, "MLSD entry not found");
            StringAssert.Contains(entry, "type=file;");
            StringAssert.Contains(entry, "size=1234;");
            StringAssert.Contains(entry, "modify=");
        }

        List<string> SendControlCommands(string command)
        {
            using (var tcp = new TcpClient())
            {
                tcp.Connect(IPAddress.Loopback, _port);

                using (var stream = tcp.GetStream())
                {
                    stream.ReadTimeout = 10000;

                    ReadLine(stream);
                    WriteLine(stream, "USER " + User);
                    ReadLine(stream);
                    WriteLine(stream, "PASS " + Password);
                    ReadLine(stream);
                    WriteLine(stream, "TYPE I");
                    ReadLine(stream);

                    if (command == "MLSD")
                    {
                        WriteLine(stream, "PASV");

                        var pasv = ReadLine(stream);

                        var start = pasv.IndexOf('(');
                        var end = pasv.IndexOf(')');
                        var parts = pasv.Substring(start + 1, end - start - 1).Split(',');

                        var port = int.Parse(parts[4]) * 256 + int.Parse(parts[5]);

                        WriteLine(stream, "MLSD");

                        var opening = ReadLine(stream);

                        Assert.IsTrue(opening.StartsWith("150"), opening);

                        var buffer = new List<byte>();

                        using (var data = new TcpClient())
                        {
                            data.Connect(IPAddress.Loopback, port);

                            using (var dataStream = data.GetStream())
                            {
                                var chunk = new byte[4096];
                                int count;

                                while ((count = dataStream.Read(chunk, 0, chunk.Length)) > 0)
                                {
                                    for (int i = 0; i < count; i++) buffer.Add(chunk[i]);
                                }
                            }
                        }

                        var closing = ReadLine(stream);

                        Assert.IsTrue(closing.StartsWith("226"), closing);

                        return Encoding.UTF8.GetString(buffer.ToArray())
                            .Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries)
                            .ToList();
                    }

                    WriteLine(stream, command);

                    var result = new List<string>();

                    while (true)
                    {
                        var line = ReadLine(stream);

                        result.Add(line);

                        if (line.StartsWith("211 ")) break;
                    }

                    return result;
                }
            }
        }

        static void WriteLine(NetworkStream stream, string line)
        {
            var data = Encoding.UTF8.GetBytes(line + "\r\n");
            stream.Write(data, 0, data.Length);
        }

        static string ReadLine(NetworkStream stream)
        {
            var bytes = new List<byte>();

            while (true)
            {
                var value = stream.ReadByte();

                if (value < 0) break;

                if (value == '\n') break;

                if (value != '\r') bytes.Add((byte)value);
            }

            return Encoding.UTF8.GetString(bytes.ToArray());
        }
    }
}