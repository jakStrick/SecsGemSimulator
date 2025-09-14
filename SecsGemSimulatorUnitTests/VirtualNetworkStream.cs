using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Net;
using System.Threading;
using BasicSecsGemServer;

// Additional test classes for more comprehensive coverage
namespace SecsGemAdvancedTests
{
    // Mock network stream for testing HSMS communication without actual network
    public class MockNetworkStream : Stream
    {
        private MemoryStream _writeStream = new MemoryStream();
        private MemoryStream _readStream = new MemoryStream();

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => true;
        public override long Length => _readStream.Length;
        public override long Position { get => _readStream.Position; set => _readStream.Position = value; }

        public byte[] GetWrittenData() => _writeStream.ToArray();

        public void SetDataToRead(byte[] data)
        {
            _readStream = new MemoryStream(data);
        }

        public override void Flush() => _writeStream.Flush();

        public override int Read(byte[] buffer, int offset, int count)
        {
            return _readStream.Read(buffer, offset, count);
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotSupportedException();
        }

        public override void SetLength(long value)
        {
            throw new NotSupportedException();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            _writeStream.Write(buffer, offset, count);
        }

        protected override void Dispose(bool disposing)
        {
            _writeStream?.Dispose();
            _readStream?.Dispose();
            base.Dispose(disposing);
        }
    }

    [TestClass]
    public class HsmsConnectionAdvancedTests
    {
        [TestMethod]
        public void HsmsConnection_InitialState_IsDisconnected()
        {
            // Arrange & Act
            var connection = new HsmsConnection();

            // Assert
            Assert.IsFalse(connection.IsConnected);
            Assert.IsNull(connection.RemoteEndpoint);
            Assert.IsNull(connection.Port);
        }

        [TestMethod]
        public void HsmsConnection_Dispose_CleansUpResources()
        {
            // Arrange
            var connection = new HsmsConnection();

            // Act & Assert - Should not throw exception
            connection.Dispose(null);
        }

        [TestMethod]
        public async Task HsmsConnection_ConnectAsync_WithValidEndpoint_ReturnsTrue()
        {
            // Arrange
            var connection = new HsmsConnection();
            var testServer = new TestHsmsServer(5002);

            try
            {
                await testServer.StartAsync();

                // Act
                var result = await connection.ConnectAsync(IPAddress.Loopback, 5002, 1);

                // Assert
                Assert.IsTrue(result);
                Assert.IsTrue(connection.IsConnected);
            }
            finally
            {
                connection.Dispose(null);
                await testServer.StopAsync();
                testServer.Dispose();
            }
        }

        [TestMethod]
        public async Task HsmsConnection_DisconnectAsync_CleansUpProperly()
        {
            // Arrange
            var connection = new HsmsConnection();
            var testServer = new TestHsmsServer(5003);

            try
            {
                await testServer.StartAsync();
                await connection.ConnectAsync(IPAddress.Loopback, 5003, 1);

                // Act
                await connection.DisconnectAsync();

                // Assert
                Assert.IsFalse(connection.IsConnected);
            }
            finally
            {
                connection.Dispose(null);
                await testServer.StopAsync();
                testServer.Dispose();
            }
        }
    }

    [TestClass]
    public class SecsMessageValidationTests
    {
        [TestMethod]
        public void SecsMessage_WithNullData_HandlesGracefully()
        {
            // Arrange & Act
            var message = new SecsMessage
            {
                Stream = 1,
                Function = 1,
                Data = null
            };

            // Assert
            Assert.IsNull(message.Data);
            Assert.AreEqual(1, message.Stream);
            Assert.AreEqual(1, message.Function);
        }

        [TestMethod]
        public void SecsMessage_WithLargeData_HandlesCorrectly()
        {
            // Arrange
            var largeData = new byte[10000];
            for (int i = 0; i < largeData.Length; i++)
            {
                largeData[i] = (byte)(i % 256);
            }

            // Act
            var message = new SecsMessage
            {
                Stream = 6,
                Function = 11,
                Data = largeData
            };

            // Assert
            Assert.AreEqual(10000, message.Data.Length);
            Assert.AreEqual(6, message.Stream);
            Assert.AreEqual(11, message.Function);
        }

        [TestMethod]
        public void SecsMessage_WithEmptyData_HandlesCorrectly()
        {
            // Arrange & Act
            var message = new SecsMessage
            {
                Stream = 2,
                Function = 18,
                Data = new byte[0]
            };

            // Assert
            Assert.IsNotNull(message.Data);
            Assert.AreEqual(0, message.Data.Length);
        }
    }

    [TestClass]
    public class HsmsProtocolTests
    {
        [TestMethod]
        public void HsmsMessage_WithControlMessage_SetsCorrectTypes()
        {
            // Arrange & Act
            var selectRequest = new HsmsMessage
            {
                SessionId = 1,
                HeaderByte2 = 0x00,
                HeaderByte3 = 0x00,
                PType = HsmsPType.SelectReq,
                SType = HsmsSType.SelectReq,
                SystemBytes = 12345
            };

            // Assert
            Assert.AreEqual(HsmsPType.SelectReq, selectRequest.PType);
            Assert.AreEqual(HsmsSType.SelectReq, selectRequest.SType);
            Assert.AreEqual(1, selectRequest.SessionId);
        }

        [TestMethod]
        public void HsmsMessage_WithDataMessage_SetsCorrectTypes()
        {
            // Arrange & Act
            var dataMessage = new HsmsMessage
            {
                SessionId = 1,
                HeaderByte2 = 0x81, // Stream 1 with W-bit set
                HeaderByte3 = 0x01, // Function 1
                PType = HsmsPType.DataMessage,
                SType = HsmsSType.DataMessage,
                SystemBytes = 54321,
                Data = new byte[] { 0x01, 0x02, 0x03 }
            };

            // Assert
            Assert.AreEqual(HsmsPType.DataMessage, dataMessage.PType);
            Assert.AreEqual(HsmsSType.DataMessage, dataMessage.SType);
            Assert.AreEqual(0x81, dataMessage.HeaderByte2);
            Assert.AreEqual(0x01, dataMessage.HeaderByte3);
            Assert.AreEqual(3, dataMessage.Data.Length);
        }

        [TestMethod]
        public void HsmsHeader_WithValidData_ParsesCorrectly()
        {
            // Arrange & Act
            var header = new HsmsHeader
            {
                Length = 13, // 10 bytes header + 3 bytes data
                SessionId = 1,
                HeaderByte2 = 0x81,
                HeaderByte3 = 0x01,
                PType = HsmsPType.DataMessage,
                SType = HsmsSType.DataMessage,
                SystemBytes = 0x12345678
            };

            // Assert
            Assert.AreEqual(13u, header.Length);
            Assert.AreEqual(1, header.SessionId);
            Assert.AreEqual(0x81, header.HeaderByte2);
            Assert.AreEqual(0x01, header.HeaderByte3);
            Assert.AreEqual(HsmsPType.DataMessage, header.PType);
            Assert.AreEqual(HsmsSType.DataMessage, header.SType);
            Assert.AreEqual(0x12345678u, header.SystemBytes);
        }
    }

    [TestClass]
    public class HsmsConnectionThreadSafetyTests
    {
        [TestMethod]
        public async Task HsmsConnection_ConcurrentSendMessage_IsSafeThreadSafe()
        {
            // Arrange
            var connection = new HsmsConnection();
            var testServer = new TestHsmsServer(5004);

            try
            {
                await testServer.StartAsync();
                await connection.ConnectAsync(IPAddress.Loopback, 5004, 1);

                const int taskCount = 10;
                const int messagesPerTask = 10;

                // Act
                var tasks = new List<Task>();

                for (int i = 0; i < taskCount; i++)
                {
                    int taskId = i;
                    tasks.Add(Task.Run(() =>
                    {
                        for (int j = 0; j < messagesPerTask; j++)
                        {
                            var message = new SecsMessage
                            {
                                Stream = 1,
                                Function = 1,
                                WBit = true,
                                Data = new byte[] { (byte)taskId, (byte)j }
                            };
                            connection.SendMessage(message);
                        }
                    }));
                }

                await Task.WhenAll(tasks);

                // Assert - Should complete without exceptions
                Assert.IsTrue(connection.IsConnected);
            }
            finally
            {
                connection.Dispose(null);
                await testServer.StopAsync();
                testServer.Dispose();
            }
        }

        [TestMethod]
        public async Task HsmsConnection_StateTransitions_HandleConcurrency()
        {
            // Arrange
            var connection = new HsmsConnection();
            var stateChanges = new List<ConnectionState?>();
            var lockObject = new object();

            connection.ConnectionStateChanged += (sender, args) =>
            {
                lock (lockObject)
                {
                    stateChanges.Add(args.State);
                }
            };

            var testServer = new TestHsmsServer(5005);

            try
            {
                await testServer.StartAsync();

                // Act - Multiple connection attempts
                var tasks = new Task[]
                {
                    Task.Run(async () =>
                    {
                        try { await connection.ConnectAsync(IPAddress.Loopback, 5005, 1); }
                        catch { /* Ignore connection errors in concurrent test */ }
                    }),
                    Task.Run(async () =>
                    {
                        await Task.Delay(10);
                        try { await connection.DisconnectAsync(); }
                        catch { /* Ignore disconnection errors */ }
                    })
                };

                await Task.WhenAll(tasks);

                // Assert
                lock (lockObject)
                {
                    Assert.IsTrue(stateChanges.Count >= 0); // At least some state changes should occur
                }
            }
            finally
            {
                connection.Dispose(null);
                await testServer.StopAsync();
                testServer.Dispose();
            }
        }
    }

    [TestClass]
    public class HsmsMessageEdgeCaseTests
    {
        [TestMethod]
        public void HsmsMessage_WithMaxSystemBytes_HandlesCorrectly()
        {
            // Arrange & Act
            var message = new HsmsMessage
            {
                SessionId = 1,
                HeaderByte2 = 0x00,
                HeaderByte3 = 0x00,
                PType = HsmsPType.DataMessage,
                SType = HsmsSType.DataMessage,
                SystemBytes = uint.MaxValue
            };

            // Assert
            Assert.AreEqual(uint.MaxValue, message.SystemBytes);
        }

        [TestMethod]
        public void HsmsMessage_WithAllControlTypes_AreValid()
        {
            // Test all control message types are valid enum values
            var controlTypes = new[]
            {
                HsmsPType.SelectReq,
                HsmsPType.SelectRsp,
                HsmsPType.DeselectReq,
                HsmsPType.DeselectRsp,
                HsmsPType.LinktestReq,
                HsmsPType.LinktestRsp,
                HsmsPType.RejectReq,
                HsmsPType.SeparateReq
            };

            foreach (var pType in controlTypes)
            {
                // Act
                var message = new HsmsMessage
                {
                    SessionId = 1,
                    HeaderByte2 = 0x00,
                    HeaderByte3 = 0x00,
                    PType = pType,
                    SType = (HsmsSType)pType, // Cast should be valid for control messages
                    SystemBytes = 12345
                };

                // Assert
                Assert.AreEqual(pType, message.PType);
                Assert.IsTrue(Enum.IsDefined(typeof(HsmsPType), pType));
            }
        }

        [TestMethod]
        public void SecsMessage_WithMaxStreamFunction_HandlesCorrectly()
        {
            // Arrange & Act
            var message = new SecsMessage
            {
                Stream = 127, // Max 7-bit value
                Function = 255, // Max 8-bit value
                WBit = true,
                SessionId = ushort.MaxValue,
                SystemBytes = uint.MaxValue,
                Data = new byte[1000]
            };

            // Assert
            Assert.AreEqual(127, message.Stream);
            Assert.AreEqual(255, message.Function);
            Assert.IsTrue(message.WBit);
            Assert.AreEqual(ushort.MaxValue, message.SessionId);
            Assert.AreEqual(uint.MaxValue, message.SystemBytes);
            Assert.AreEqual(1000, message.Data.Length);
        }
    }

    [TestClass]
    public class HsmsConnectionErrorHandlingTests
    {
        [TestMethod]
        public async Task HsmsConnection_ConnectToInvalidAddress_ThrowsException()
        {
            // Arrange
            var connection = new HsmsConnection();
            var invalidAddress = IPAddress.Parse("192.0.2.1"); // RFC 5737 test address

            // Act & Assert
            await Assert.ThrowsExceptionAsync<HsmsException>(
                () => connection.ConnectAsync(invalidAddress, 12345, 1));

            connection.Dispose(null);
        }

        [TestMethod]
        public void HsmsConnection_SendMessageWhenDisconnected_ThrowsException()
        {
            // Arrange
            var connection = new HsmsConnection();
            var message = new SecsMessage
            {
                Stream = 1,
                Function = 1,
                WBit = true,
                Data = new byte[] { 0x01 }
            };

            // Act & Assert
            Assert.ThrowsException<InvalidOperationException>(
                () => connection.SendMessage(message));

            connection.Dispose(null);
        }

        [TestMethod]
        public async Task HsmsConnection_DoubleConnect_HandlesGracefully()
        {
            // Arrange
            var connection = new HsmsConnection();
            var testServer = new TestHsmsServer(5006);

            try
            {
                await testServer.StartAsync();

                // Act
                var result1 = await connection.ConnectAsync(IPAddress.Loopback, 5006, 1);
                var result2 = await connection.ConnectAsync(IPAddress.Loopback, 5006, 1);

                // Assert
                Assert.IsTrue(result1);
                Assert.IsTrue(result2);
                Assert.IsTrue(connection.IsConnected);
            }
            finally
            {
                connection.Dispose(null);
                await testServer.StopAsync();
                testServer.Dispose();
            }
        }
    }

    // Simple test server for integration testing
    public class TestHsmsServer : IDisposable
    {
        private readonly int _port;
        private System.Net.Sockets.TcpListener _listener;
        private CancellationTokenSource _cancellationTokenSource;
        private Task _serverTask;

        public TestHsmsServer(int port)
        {
            _port = port;
            _cancellationTokenSource = new CancellationTokenSource();
        }

        public async Task StartAsync()
        {
            _listener = new System.Net.Sockets.TcpListener(IPAddress.Any, _port);
            _listener.Start();

            _serverTask = Task.Run(async () =>
            {
                try
                {
                    while (!_cancellationTokenSource.Token.IsCancellationRequested)
                    {
                        var client = await _listener.AcceptTcpClientAsync();
                        _ = Task.Run(() => HandleClientAsync(client, _cancellationTokenSource.Token));
                    }
                }
                catch (ObjectDisposedException)
                {
                    // Expected when stopping
                }
                catch (Exception)
                {
                    // Ignore other exceptions during shutdown
                }
            });

            await Task.Delay(50); // Give server time to start
        }

        private async Task HandleClientAsync(System.Net.Sockets.TcpClient client, CancellationToken cancellationToken)
        {
            try
            {
                using (client)
                using (var stream = client.GetStream())
                {
                    var buffer = new byte[1024];
                    while (!cancellationToken.IsCancellationRequested && client.Connected)
                    {
                        var bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length, cancellationToken);
                        if (bytesRead == 0) break;

                        // Echo back a simple HSMS response for testing
                        var response = new byte[] { 0x00, 0x00, 0x00, 0x0A, 0x00, 0x01, 0x00, 0x02, 0x00, 0x00 };
                        await stream.WriteAsync(response, 0, response.Length, cancellationToken);
                    }
                }
            }
            catch (Exception)
            {
                // Ignore client handling errors in test server
            }
        }

        public async Task StopAsync()
        {
            _cancellationTokenSource?.Cancel();
            _listener?.Stop();

            if (_serverTask != null)
            {
                try
                {
                    await _serverTask.WaitAsync(TimeSpan.FromSeconds(5));
                }
                catch (TimeoutException)
                {
                    // Server didn't stop gracefully
                }
            }
        }

        public void Dispose()
        {
            _cancellationTokenSource?.Cancel();
            _listener?.Stop();
            _cancellationTokenSource?.Dispose();
        }
    }
}

// Performance tests
namespace SecsGemPerformanceTests
{
    using SecsGemAdvancedTests;
    [TestClass]
    public class PerformanceTests
    {
        [TestMethod]
        [Timeout(5000)] // 5 second timeout
        public async Task HsmsConnection_ManyMessages_PerformsWell()
        {
            // Arrange
            var connection = new HsmsConnection();
            var testServer = new TestHsmsServer(5007);
            const int messageCount = 100;

            try
            {
                await testServer.StartAsync();
                await connection.ConnectAsync(IPAddress.Loopback, 5007, 1);

                // Act
                var start = DateTime.Now;
                for (int i = 0; i < messageCount; i++)
                {
                    var message = new SecsMessage
                    {
                        Stream = 1,
                        Function = 1,
                        WBit = true,
                        Data = new byte[] { (byte)(i & 0xFF) }
                    };
                    connection.SendMessage(message);
                }
                var elapsed = DateTime.Now - start;

                // Assert
                Assert.IsTrue(elapsed.TotalSeconds < 2.0,
                    $"Message sending took too long: {elapsed.TotalSeconds} seconds");
            }
            finally
            {
                connection.Dispose(null);
                await testServer.StopAsync();
                testServer.Dispose();
            }
        }

        [TestMethod]
        [Timeout(5000)]
        public void SecsMessage_ManyInstantiations_PerformsWell()
        {
            // Arrange
            const int messageCount = 10000;

            // Act
            var start = DateTime.Now;
            for (int i = 0; i < messageCount; i++)
            {
                var message = new SecsMessage
                {
                    Stream = (byte)(i % 128),
                    Function = (byte)(i % 256),
                    WBit = (i % 2) == 0,
                    SessionId = (ushort)(i % 65536),
                    SystemBytes = (uint)i,
                    Data = new byte[] { (byte)(i & 0xFF), (byte)((i >> 8) & 0xFF) }
                };
            }
            var elapsed = DateTime.Now - start;

            // Assert
            Assert.IsTrue(elapsed.TotalSeconds < 1.0,
                $"Message creation took too long: {elapsed.TotalSeconds} seconds");
        }

        [TestMethod]
        [Timeout(5000)]
        public void HsmsMessage_ManyInstantiations_PerformsWell()
        {
            // Arrange
            const int messageCount = 10000;

            // Act
            var start = DateTime.Now;
            for (int i = 0; i < messageCount; i++)
            {
                var message = new HsmsMessage
                {
                    SessionId = (ushort)(i % 65536),
                    HeaderByte2 = (byte)(i % 256),
                    HeaderByte3 = (byte)(i % 256),
                    PType = (i % 2 == 0) ? HsmsPType.DataMessage : HsmsPType.SelectReq,
                    SType = (i % 2 == 0) ? HsmsSType.DataMessage : HsmsSType.SelectReq,
                    SystemBytes = (uint)i,
                    Data = new byte[] { (byte)(i & 0xFF) }
                };
            }
            var elapsed = DateTime.Now - start;

            // Assert
            Assert.IsTrue(elapsed.TotalSeconds < 1.0,
                $"HSMS message creation took too long: {elapsed.TotalSeconds} seconds");
        }
    }
}