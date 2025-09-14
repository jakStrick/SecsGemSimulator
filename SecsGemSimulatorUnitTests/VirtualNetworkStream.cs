// Additional test classes for more comprehensive coverage

using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Net;
using System.IO;
using SecsGemClient;
using BasicSecsGemServer;

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
    public class HsmsConnectionTests
    {
        [TestMethod]
        public void HsmsConnection_InitialState_IsDisconnected()
        {
            // Arrange & Act
            var connection = new SecsGemClient.HsmsConnection();

            // Assert
            Assert.IsFalse(connection.IsConnected);
            Assert.IsNull(connection.RemoteEndpoint);
            Assert.IsNull(connection.Port);
        }

        [TestMethod]
        public void HsmsConnection_Dispose_CleansUpResources()
        {
            // Arrange
            var connection = new SecsGemClient.HsmsConnection();
            // Use a real NetworkStream or null, since Dispose expects NetworkStream?
            // For unit test, passing null is safe and valid for cleanup
            // Act & Assert - Should not throw exception
            connection.Dispose(null);
        }
    }

    [TestClass]
    public class SecsMessageValidationTests
    {
        private SecsGemClient.SecsMessageProcessor _processor;

        [TestInitialize]
        public void Setup()
        {
            _processor = new SecsGemClient.SecsMessageProcessor();
        }

        [TestMethod]
        public void SecsMessage_WithNullData_HandlesGracefully()
        {
            // Arrange
            var message = new SecsGemClient.SecsMessage
            {
                Stream = 1,
                Function = 1,
                Data = null,
                RootItem = null
            };

            // Act
            var serialized = _processor.SerializeMessage(message);

            // Assert
            Assert.AreEqual(0, serialized.Length);
        }

        [TestMethod]
        public void SecsAsciiItem_WithNullString_HandlesGracefully()
        {
            // Act
            var item = new SecsGemClient.SecsAsciiItem(null);

            // Assert
            Assert.AreEqual("", item.Value);
            Assert.AreEqual(0, item.GetBytes().Length);
        }

        [TestMethod]
        public void SecsBinaryItem_WithNullArray_HandlesGracefully()
        {
            // Act
            var item = new SecsGemClient.SecsBinaryItem(null);

            // Assert
            Assert.IsNotNull(item.Value);
            Assert.AreEqual(0, item.Value.Length);
        }

        [TestMethod]
        public void SecsListItem_WithNullList_HandlesGracefully()
        {
            // Act
            var item = new SecsGemClient.SecsListItem(null);

            // Assert
            Assert.IsNotNull(item.Items);
            Assert.AreEqual(0, item.Items.Count);
        }
    }

    [TestClass]
    public class GemStateThreadSafetyTests
    {
        [TestMethod]
        public async Task GemStateManager_ConcurrentAccess_IsSafeThreadSafe()
        {
            // Arrange
            var stateManager = new SecsGemClient.GemStateManager();
            const int taskCount = 10;
            const int operationsPerTask = 100;

            // Act
            var tasks = new List<Task>();

            for (int i = 0; i < taskCount; i++)
            {
                int taskId = i;
                tasks.Add(Task.Run(() =>
                {
                    for (int j = 0; j < operationsPerTask; j++)
                    {
                        stateManager.UpdateStatusVariable($"Task{taskId}_Var{j}", $"Value{j}");
                        var value = stateManager.GetStatusVariable($"Task{taskId}_Var{j}");

                        if (j % 10 == 0)
                        {
                            stateManager.SetAlarm((uint)(taskId * 100 + j), $"Test alarm {taskId}_{j}");
                        }
                    }
                }));
            }

            await Task.WhenAll(tasks);

            // Assert - Should complete without exceptions
            Assert.IsTrue(stateManager.StatusVariables.Count > 0);
        }

        [TestMethod]
        public async Task GemStateManager_StateTransitions_HandleConcurrency()
        {
            // Arrange
            var stateManager = new SecsGemClient.GemStateManager();
            var stateChanges = new List<SecsGemClient.GemState>();
            var lockObject = new object();

            stateManager.StateChanged += (sender, args) =>
            {
                lock (lockObject)
                {
                    stateChanges.Add(args.NewState);
                }
            };

            // Act
            var tasks = new Task[]
            {
                Task.Run(() => stateManager.GoOnline()),
                Task.Run(() => stateManager.SetLocal()),
                Task.Run(() => stateManager.GoOffline()),
                Task.Run(() => stateManager.GoOnline()),
                Task.Run(() => stateManager.SetLocal())
            };

            await Task.WhenAll(tasks);

            // Assert
            Assert.IsTrue(stateChanges.Count > 0);
            // The final state should be one of the valid states
            Assert.IsTrue(stateManager.ControlState == SecsGemClient.GemState.Online ||
                         stateManager.ControlState == SecsGemClient.GemState.Local ||
                         stateManager.ControlState == SecsGemClient.GemState.Offline);
        }
    }

    [TestClass]
    public class SecsDataTypeEdgeCaseTests
    {
        [TestMethod]
        public void SecsF4Item_WithNaN_HandlesCorrectly()
        {
            // Arrange & Act
            var item = new SecsGemClient.SecsF4Item(float.NaN);

            // Assert
            Assert.IsTrue(float.IsNaN(item.Value));
            var bytes = item.GetBytes();
            Assert.AreEqual(4, bytes.Length);
        }

        [TestMethod]
        public void SecsF4Item_WithInfinity_HandlesCorrectly()
        {
            // Arrange & Act
            var positiveInfinity = new SecsGemClient.SecsF4Item(float.PositiveInfinity);
            var negativeInfinity = new SecsGemClient.SecsF4Item(float.NegativeInfinity);

            // Assert
            Assert.IsTrue(float.IsPositiveInfinity(positiveInfinity.Value));
            Assert.IsTrue(float.IsNegativeInfinity(negativeInfinity.Value));
        }

        [TestMethod]
        public void SecsU4Item_WithMaxValue_HandlesCorrectly()
        {
            // Arrange & Act
            var item = new SecsGemClient.SecsU4Item(uint.MaxValue);

            // Assert
            Assert.AreEqual(uint.MaxValue, item.Value);
            var bytes = item.GetBytes();
            Assert.AreEqual(4, bytes.Length);
        }

        [TestMethod]
        public void SecsAsciiItem_WithSpecialCharacters_HandlesCorrectly()
        {
            // Arrange
            const string specialChars = "Special chars: \r\n\t\0\x7F";

            // Act
            var item = new SecsGemClient.SecsAsciiItem(specialChars);

            // Assert
            Assert.AreEqual(specialChars, item.Value);
            var bytes = item.GetBytes();
            Assert.IsTrue(bytes.Length > 0);
        }

        [TestMethod]
        public void SecsBinaryItem_WithLargeArray_HandlesCorrectly()
        {
            // Arrange
            var largeArray = new byte[10000];
            for (int i = 0; i < largeArray.Length; i++)
            {
                largeArray[i] = (byte)(i % 256);
            }

            // Act
            var item = new SecsGemClient.SecsBinaryItem(largeArray);

            // Assert
            Assert.AreEqual(10000, item.Value.Length);
            CollectionAssert.AreEqual(largeArray, item.Value);
        }
    }

    [TestClass]
    public class MessageProcessorErrorHandlingTests
    {
        private SecsGemClient.SecsMessageProcessor _processor;

        [TestInitialize]
        public void Setup()
        {
            _processor = new SecsGemClient.SecsMessageProcessor();
        }

        [TestMethod]
        public void ParseMessage_WithCorruptedData_HandlesGracefully()
        {
            // Arrange
            var corruptedData = new byte[] { 0xFF, 0xFF, 0xFF, 0xFF };

            // Act
            var result = _processor.ParseMessage(corruptedData);

            // Assert
            Assert.IsNotNull(result);
            // Should not throw exception
        }

        [TestMethod]
        public void ParseMessage_WithEmptyArray_ReturnsValidMessage()
        {
            // Act
            var result = _processor.ParseMessage(new byte[0]);

            // Assert
            Assert.IsNotNull(result);
            Assert.IsNull(result.RootItem);
        }

        [TestMethod]
        public void SerializeMessage_WithComplexNestedStructure_WorksCorrectly()
        {
            // Arrange
            var nestedList = new SecsGemClient.SecsListItem(new List<SecsGemClient.SecsItem>
            {
                new SecsGemClient.SecsAsciiItem("Level1"),
                new SecsGemClient.SecsListItem(new List<SecsGemClient.SecsItem>
                {
                    new SecsGemClient.SecsAsciiItem("Level2"),
                    new SecsGemClient.SecsU4Item(42),
                    new SecsGemClient.SecsListItem(new List<SecsGemClient.SecsItem>
                    {
                        new SecsGemClient.SecsF4Item(3.14f),
                        new SecsGemClient.SecsBinaryItem(new byte[] { 0x01, 0x02, 0x03 })
                    })
                })
            });

            var message = new SecsGemClient.SecsMessage
            {
                Stream = 6,
                Function = 11,
                WBit = true,
                RootItem = nestedList
            };

            // Act
            var serialized = _processor.SerializeMessage(message);

            // Assert
            Assert.IsNotNull(serialized);
            Assert.IsTrue(serialized.Length > 0);
        }
    }

    [TestClass]
    public class AlarmManagementTests
    {
        private SecsGemClient.GemStateManager _stateManager;

        [TestInitialize]
        public void Setup()
        {
            _stateManager = new SecsGemClient.GemStateManager();
        }

        [TestMethod]
        public void SetAlarm_DuplicateAlarmId_DoesNotDuplicate()
        {
            // Arrange
            const uint alarmId = 100;
            const string alarmText1 = "First alarm";
            const string alarmText2 = "Second alarm";

            // Act
            _stateManager.SetAlarm(alarmId, alarmText1);
            _stateManager.SetAlarm(alarmId, alarmText2); // Same ID

            // Assert
            Assert.AreEqual(1, _stateManager.ActiveAlarms.Count);
            Assert.AreEqual(alarmText1, _stateManager.ActiveAlarms.First().AlarmText);
        }

        [TestMethod]
        public void ClearAlarm_NonExistentAlarm_DoesNotThrow()
        {
            // Act & Assert - Should not throw
            _stateManager.ClearAlarm(999);

            Assert.AreEqual(0, _stateManager.ActiveAlarms.Count);
        }

        [TestMethod]
        public void SetMultipleAlarms_DifferentSeverities_AllAreTracked()
        {
            // Act
            _stateManager.SetAlarm(101, "Info Alarm", SecsGemClient.AlarmSeverity.Info);
            _stateManager.SetAlarm(102, "Warning Alarm", SecsGemClient.AlarmSeverity.Warning);
            _stateManager.SetAlarm(103, "Error Alarm", SecsGemClient.AlarmSeverity.Error);
            _stateManager.SetAlarm(104, "Critical Alarm", SecsGemClient.AlarmSeverity.Critical);

            // Assert
            Assert.AreEqual(4, _stateManager.ActiveAlarms.Count);

            var severities = _stateManager.ActiveAlarms.Select(a => a.Severity).ToList();
            Assert.IsTrue(severities.Contains(SecsGemClient.AlarmSeverity.Info));
            Assert.IsTrue(severities.Contains(SecsGemClient.AlarmSeverity.Warning));
            Assert.IsTrue(severities.Contains(SecsGemClient.AlarmSeverity.Error));
            Assert.IsTrue(severities.Contains(SecsGemClient.AlarmSeverity.Critical));
        }
    }

    [TestClass]
    public class SubstrateTrackingTests
    {
        private SecsGemClient.GemStateManager _stateManager;

        [TestInitialize]
        public void Setup()
        {
            _stateManager = new SecsGemClient.GemStateManager();
        }

        [TestMethod]
        public void UpdateSubstrateLocation_MultipleMoves_TracksHistory()
        {
            // Arrange
            const string substrateId = "WAFER_001";

            // Act
            _stateManager.UpdateSubstrateLocation(substrateId, "LoadPort1");
            _stateManager.UpdateSubstrateLocation(substrateId, "ProcessChamber");
            _stateManager.UpdateSubstrateLocation(substrateId, "UnloadPort1");

            // Assert
            var history = _stateManager.GetStatusVariable("SubstHistory") as List<string>;
            Assert.IsNotNull(history);
            Assert.AreEqual(3, history.Count);
            Assert.IsTrue(history.Any(h => h.Contains("LoadPort1")));
            Assert.IsTrue(history.Any(h => h.Contains("ProcessChamber")));
            Assert.IsTrue(history.Any(h => h.Contains("UnloadPort1")));
        }

        [TestMethod]
        public void ProcessSubstrate_IncrementsCount_UpdatesHistory()
        {
            // Arrange
            const string substrateId = "WAFER_001";
            const string recipe = "TestRecipe";

            // Act
            _stateManager.ProcessSubstrate(substrateId, recipe);
            _stateManager.ProcessSubstrate("WAFER_002", recipe);

            // Assert
            var count = _stateManager.GetStatusVariable("SubstCount");
            Assert.AreEqual(2, count);

            var previousTask = _stateManager.GetStatusVariable("PreviousTaskName");
            Assert.AreEqual(recipe, previousTask);

            var taskType = _stateManager.GetStatusVariable("PreviousTaskType");
            Assert.AreEqual("PROCESS", taskType);
        }
    }

    [TestClass]
    public class EventManagementTests
    {
        private SecsGemClient.GemStateManager _stateManager;

        [TestInitialize]
        public void Setup()
        {
            _stateManager = new SecsGemClient.GemStateManager();
        }

        [TestMethod]
        public void TriggerEvent_ExistingEvent_UpdatesCountAndTimestamp()
        {
            // Arrange
            const uint eventId = 1; // Equipment Offline event
            var initialEvent = _stateManager.Events.Find(e => e.EventId == eventId);
            var initialCount = initialEvent?.TriggerCount ?? 0;

            // Act
            _stateManager.TriggerEvent(eventId, "Test trigger");

            // Assert
            var updatedEvent = _stateManager.Events.Find(e => e.EventId == eventId);
            Assert.IsNotNull(updatedEvent);
            Assert.AreEqual(initialCount + 1, updatedEvent.TriggerCount);
            Assert.IsTrue(updatedEvent.LastTriggered > DateTime.MinValue);
        }

        [TestMethod]
        public void TriggerEvent_DisabledEvent_DoesNotTrigger()
        {
            // Arrange
            const uint eventId = 1;
            var eventData = _stateManager.Events.Find(e => e.EventId == eventId);
            eventData.Enabled = false;
            var initialCount = eventData.TriggerCount;

            bool eventTriggered = false;
            _stateManager.OnEventTriggered += (sender, args) =>
            {
                if (args.EventId == eventId)
                    eventTriggered = true;
            };

            // Act
            _stateManager.TriggerEvent(eventId, "Test trigger");

            // Assert
            Assert.IsFalse(eventTriggered);
            Assert.AreEqual(initialCount, eventData.TriggerCount);
        }

        [TestMethod]
        public void TriggerEvent_NonExistentEvent_DoesNotThrow()
        {
            // Act & Assert - Should not throw
            _stateManager.TriggerEvent(9999, "Non-existent event");
        }
    }
}

// Performance tests
namespace SecsGemPerformanceTests
{
    [TestClass]
    public class PerformanceTests
    {
        [TestMethod]
        [Timeout(5000)] // 5 second timeout
        public void MessageProcessor_SerializeMany_PerformsWell()
        {
            // Arrange
            var processor = new SecsGemClient.SecsMessageProcessor();
            const int messageCount = 1000;

            // Act
            var start = DateTime.Now;
            for (int i = 0; i < messageCount; i++)
            {
                var message = processor.CreateS6F11Message((uint)i,
                    new Dictionary<string, object> { { "data", $"value{i}" } });
                var serialized = processor.SerializeMessage(message);
            }
            var elapsed = DateTime.Now - start;

            // Assert
            Assert.IsTrue(elapsed.TotalSeconds < 2.0,
                $"Serialization took too long: {elapsed.TotalSeconds} seconds");
        }

        [TestMethod]
        [Timeout(5000)]
        public void GemStateManager_ManyStatusUpdates_PerformsWell()
        {
            // Arrange
            var stateManager = new SecsGemClient.GemStateManager();
            const int updateCount = 10000;

            // Act
            var start = DateTime.Now;
            for (int i = 0; i < updateCount; i++)
            {
                stateManager.UpdateStatusVariable($"Var{i}", $"Value{i}");
            }
            var elapsed = DateTime.Now - start;

            // Assert
            Assert.IsTrue(elapsed.TotalSeconds < 1.0,
                $"Status updates took too long: {elapsed.TotalSeconds} seconds");
        }
    }
}