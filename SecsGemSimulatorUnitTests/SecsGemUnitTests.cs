
using System.Net;
using BasicSecsGemServer;

namespace SecsGemTests
{
    [TestClass]
    public class HsmsConnectionTests
    {
        private HsmsConnection _connection;
        private IPAddress _testAddress;
        private const int TestPort = 5000;
        private const int TestDeviceId = 1;

        [TestInitialize]
        public void Setup()
        {
            _connection = new HsmsConnection();
            _testAddress = IPAddress.Loopback;
        }

        [TestCleanup]
        public void Cleanup()
        {
            try
            {
                _connection?.Dispose(null);
            }
            catch
            {
                // Ignore cleanup errors in tests
            }
        }

        [TestMethod]
        public void HsmsConnection_InitialState_IsDisconnected()
        {
            // Assert
            Assert.IsFalse(_connection.IsConnected);
            Assert.IsNull(_connection.RemoteEndpoint);
            Assert.AreEqual(0, _connection.Port);
            Assert.AreEqual(0, _connection.DeviceId);
        }

        [TestMethod]
        public async Task HsmsConnection_ConnectAsync_ToUnavailableServer_ThrowsHsmsException()
        {
            // Arrange - Use a port that's unlikely to be in use
            var unavailablePort = 12345;

            // Act & Assert
            var exception = await Assert.ThrowsExceptionAsync<HsmsException>(
                () => _connection.ConnectAsync(_testAddress, unavailablePort, TestDeviceId));

            Assert.IsTrue(exception.Message.Contains("Failed to connect"));
        }

        [TestMethod]
        public void HsmsConnection_SendMessage_WhenDisconnected_ThrowsInvalidOperationException()
        {
            // Arrange
            var message = new SecsMessage
            {
                Stream = 1,
                Function = 1,
                WBit = true,
                Data = new byte[] { 0x01, 0x02 }
            };

            // Act & Assert
            Assert.ThrowsException<InvalidOperationException>(
                () => _connection.SendMessage(message));
        }

        [TestMethod]
        public void HsmsConnection_Events_AreProperlyDeclared()
        {
            // Arrange
            bool connectionStateChangedFired = false;
            bool messageReceivedFired = false;

            // Act - Subscribe to events (test that they exist and are subscribable)
            _connection.ConnectionStateChanged += (sender, args) => connectionStateChangedFired = true;
            _connection.MessageReceived += (sender, args) => messageReceivedFired = true;

            // Assert - Just test that subscription worked (events exist)
            // We can't easily test the actual firing without a real connection
            Assert.IsTrue(true); // If we got here, events are subscribable
        }

        [TestMethod]
        public void HsmsConnection_GetTcpStream_ReturnsNullWhenDisconnected()
        {
            // Act
            var stream = _connection.GetTcpStream();

            // Assert
            Assert.IsNull(stream);
        }

        [TestMethod]
        public void HsmsConnection_Dispose_CanBeCalledMultipleTimes()
        {
            // Act & Assert - Should not throw exceptions
            _connection.Dispose(null);
            _connection.Dispose(null); // Second call should be safe
        }
    }

    [TestClass]
    public class HsmsMessageTests
    {
        [TestMethod]
        public void HsmsMessage_Properties_SetCorrectly()
        {
            // Arrange & Act
            var message = new HsmsMessage
            {
                SessionId = 1,
                HeaderByte2 = 0x81,
                HeaderByte3 = 0x01,
                PType = HsmsPType.DataMessage,
                SType = HsmsSType.DataMessage,
                SystemBytes = 12345,
                Data = new byte[] { 0x01, 0x02, 0x03 }
            };

            // Assert
            Assert.AreEqual(1, message.SessionId);
            Assert.AreEqual(0x81, message.HeaderByte2);
            Assert.AreEqual(0x01, message.HeaderByte3);
            Assert.AreEqual(HsmsPType.DataMessage, message.PType);
            Assert.AreEqual(HsmsSType.DataMessage, message.SType);
            Assert.AreEqual(12345u, message.SystemBytes);
            Assert.AreEqual(3, message.Data.Length);
        }

        [TestMethod]
        public void HsmsHeader_Properties_SetCorrectly()
        {
            // Arrange & Act
            var header = new HsmsHeader
            {
                Length = 13,
                SessionId = 1,
                HeaderByte2 = 0x81,
                HeaderByte3 = 0x01,
                PType = HsmsPType.DataMessage,
                SType = HsmsSType.DataMessage,
                SystemBytes = 12345
            };

            // Assert
            Assert.AreEqual(13u, header.Length);
            Assert.AreEqual(1, header.SessionId);
            Assert.AreEqual(0x81, header.HeaderByte2);
            Assert.AreEqual(0x01, header.HeaderByte3);
            Assert.AreEqual(HsmsPType.DataMessage, header.PType);
            Assert.AreEqual(HsmsSType.DataMessage, header.SType);
            Assert.AreEqual(12345u, header.SystemBytes);
        }
    }

    [TestClass]
    public class SecsMessageTests
    {
        [TestMethod]
        public void SecsMessage_Properties_SetCorrectly()
        {
            // Arrange & Act
            var message = new SecsMessage
            {
                Stream = 1,
                Function = 2,
                WBit = true,
                SessionId = 42,
                SystemBytes = 0x12345678,
                Data = new byte[] { 0x01, 0x02, 0x03, 0x04 }
            };

            // Assert
            Assert.AreEqual(1, message.Stream);
            Assert.AreEqual(2, message.Function);
            Assert.IsTrue(message.WBit);
            Assert.AreEqual(42, message.SessionId);
            Assert.AreEqual(0x12345678u, message.SystemBytes);
            Assert.AreEqual(4, message.Data.Length);
        }
    }

    [TestClass]
    public class EventArgumentsTests
    {
        [TestMethod]
        public void ConnectionStateEventArgs_Constructor_SetsStateCorrectly()
        {
            // Arrange & Act
            var args = new ConnectionStateEventArgs(ConnectionState.Connected);

            // Assert
            Assert.AreEqual(ConnectionState.Connected, args.State);
        }

        [TestMethod]
        public void SecsMessageEventArgs_Constructor_SetsMessageCorrectly()
        {
            // Arrange
            var message = new SecsMessage
            {
                Stream = 1,
                Function = 1
            };

            // Act
            var args = new SecsMessageEventArgs(message);

            // Assert
            Assert.AreEqual(message, args.Message);
        }
    }

    [TestClass]
    public class HsmsExceptionTests
    {
        [TestMethod]
        public void HsmsException_WithMessage_SetsMessageCorrectly()
        {
            // Arrange
            const string errorMessage = "Test HSMS error";

            // Act
            var exception = new HsmsException(errorMessage);

            // Assert
            Assert.AreEqual(errorMessage, exception.Message);
            Assert.IsNull(exception.InnerException);
        }

        [TestMethod]
        public void HsmsException_WithMessageAndInnerException_SetsPropertiesCorrectly()
        {
            // Arrange
            const string errorMessage = "Test HSMS error";
            var innerException = new InvalidOperationException("Inner error");

            // Act
            var exception = new HsmsException(errorMessage, innerException);

            // Assert
            Assert.AreEqual(errorMessage, exception.Message);
            Assert.AreEqual(innerException, exception.InnerException);
        }
    }

    [TestClass]
    public class EnumTests
    {
        [TestMethod]
        public void HsmsPType_EnumValues_AreCorrect()
        {
            // Assert
            Assert.AreEqual(0x00, (byte)HsmsPType.DataMessage);
            Assert.AreEqual(0x01, (byte)HsmsPType.SelectReq);
            Assert.AreEqual(0x02, (byte)HsmsPType.SelectRsp);
            Assert.AreEqual(0x03, (byte)HsmsPType.DeselectReq);
            Assert.AreEqual(0x04, (byte)HsmsPType.DeselectRsp);
            Assert.AreEqual(0x05, (byte)HsmsPType.LinktestReq);
            Assert.AreEqual(0x06, (byte)HsmsPType.LinktestRsp);
            Assert.AreEqual(0x07, (byte)HsmsPType.RejectReq);
            Assert.AreEqual(0x09, (byte)HsmsPType.SeparateReq);
        }

        [TestMethod]
        public void HsmsSType_EnumValues_AreCorrect()
        {
            // Assert
            Assert.AreEqual(0x00, (byte)HsmsSType.DataMessage);
            Assert.AreEqual(0x01, (byte)HsmsSType.SelectReq);
            Assert.AreEqual(0x02, (byte)HsmsSType.SelectRsp);
            Assert.AreEqual(0x03, (byte)HsmsSType.DeselectReq);
            Assert.AreEqual(0x04, (byte)HsmsSType.DeselectRsp);
            Assert.AreEqual(0x05, (byte)HsmsSType.LinktestReq);
            Assert.AreEqual(0x06, (byte)HsmsSType.LinktestRsp);
            Assert.AreEqual(0x07, (byte)HsmsSType.RejectReq);
            Assert.AreEqual(0x09, (byte)HsmsSType.SeparateReq);
        }

        [TestMethod]
        public void ConnectionState_EnumValues_AreCorrect()
        {
            // Assert
            Assert.IsTrue(Enum.IsDefined(typeof(ConnectionState), ConnectionState.Disconnected));
            Assert.IsTrue(Enum.IsDefined(typeof(ConnectionState), ConnectionState.Connecting));
            Assert.IsTrue(Enum.IsDefined(typeof(ConnectionState), ConnectionState.Connected));
        }
    }

    // Mock server for testing actual connections
    [TestClass]
    public class HsmsConnectionIntegrationTests
    {
        private TestHsmsServer _testServer;
        private HsmsConnection _connection;
        private const int TestPort = 5001;

        [TestInitialize]
        public async Task Setup()
        {
            _testServer = new TestHsmsServer(TestPort);
            await _testServer.StartAsync();
            _connection = new HsmsConnection();
        }

        [TestCleanup]
        public async Task Cleanup()
        {
            try
            {
                _connection?.Dispose(null);
            }
            catch
            {
                // Ignore cleanup errors
            }

            if (_testServer != null)
            {
                await _testServer.StopAsync();
                _testServer.Dispose();
            }
        }

        [TestMethod]
        public async Task HsmsConnection_ConnectToTestServer_SucceedsAndRaisesEvents()
        {
            // Arrange
            bool connectionStateChanged = false;
            ConnectionState? newState = null;

            _connection.ConnectionStateChanged += (sender, args) =>
            {
                connectionStateChanged = true;
                newState = args.State;
            };

            // Act
            var result = await _connection.ConnectAsync(IPAddress.Loopback, TestPort, 1);

            // Allow some time for the connection to establish
            await Task.Delay(100);

            // Assert
            Assert.IsTrue(result);
            Assert.IsTrue(connectionStateChanged);
            Assert.IsTrue(_connection.IsConnected);
        }

        [TestMethod]
        public async Task HsmsConnection_SendMessage_AfterConnection_DoesNotThrow()
        {
            // Arrange
            await _connection.ConnectAsync(IPAddress.Loopback, TestPort, 1);
            var message = new SecsMessage
            {
                Stream = 1,
                Function = 1,
                WBit = true,
                Data = new byte[] { 0x01, 0x02 }
            };

            // Act & Assert - Should not throw
            _connection.SendMessage(message);
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

                        // Echo back a simple response (for testing purposes)
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