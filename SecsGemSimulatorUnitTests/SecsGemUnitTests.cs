using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Net;
using SecsGemClient;
using BasicSecsGemServer;

namespace SecsGemTests
{
    [TestClass]
    public class GemStateManagerTests
    {
        private SecsGemClient.GemStateManager _clientStateManager;
        private BasicSecsGemServer.GemStateManager _serverStateManager;

        [TestInitialize]
        public void Setup()
        {
            _clientStateManager = new SecsGemClient.GemStateManager();
            _serverStateManager = new BasicSecsGemServer.GemStateManager();
        }

        [TestMethod]
        public void GemStateManager_InitialState_IsOffline()
        {
            // Arrange & Act - Constructor creates initial state

            // Assert
            Assert.AreEqual(SecsGemClient.GemState.Offline, _clientStateManager.ControlState);
            Assert.AreEqual(SecsGemClient.GemState.Offline, _clientStateManager.CommunicationState);
            Assert.AreEqual(SecsGemClient.ProcessingState.Idle, _clientStateManager.ProcessingState);
        }

        [TestMethod]
        public void GemStateManager_GoOnline_ChangesStateCorrectly()
        {
            // Arrange
            bool stateChangedEventFired = false;
            _clientStateManager.StateChanged += (sender, args) =>
            {
                stateChangedEventFired = true;
                Assert.AreEqual(SecsGemClient.GemState.Offline, args.PreviousState);
                Assert.AreEqual(SecsGemClient.GemState.Online, args.NewState);
            };

            // Act
            _clientStateManager.GoOnline();

            // Assert
            Assert.AreEqual(SecsGemClient.GemState.Online, _clientStateManager.ControlState);
            Assert.AreEqual(SecsGemClient.GemState.Online, _clientStateManager.CommunicationState);
            Assert.IsTrue(stateChangedEventFired);
        }

        [TestMethod]
        public void GemStateManager_GoOffline_ChangesStateCorrectly()
        {
            // Arrange
            _clientStateManager.GoOnline();
            bool stateChangedEventFired = false;
            _clientStateManager.StateChanged += (sender, args) =>
            {
                stateChangedEventFired = true;
                Assert.AreEqual(SecsGemClient.GemState.Online, args.PreviousState);
                Assert.AreEqual(SecsGemClient.GemState.Offline, args.NewState);
            };

            // Act
            _clientStateManager.GoOffline();

            // Assert
            Assert.AreEqual(SecsGemClient.GemState.Offline, _clientStateManager.ControlState);
            Assert.IsTrue(stateChangedEventFired);
        }

        [TestMethod]
        public void GemStateManager_SetLocal_ChangesStateCorrectly()
        {
            // Arrange
            bool stateChangedEventFired = false;
            _clientStateManager.StateChanged += (sender, args) =>
            {
                stateChangedEventFired = true;
                Assert.AreEqual(SecsGemClient.GemState.Offline, args.PreviousState);
                Assert.AreEqual(SecsGemClient.GemState.Local, args.NewState);
            };

            // Act
            _clientStateManager.SetLocal();

            // Assert
            Assert.AreEqual(SecsGemClient.GemState.Local, _clientStateManager.ControlState);
            Assert.IsTrue(stateChangedEventFired);
        }

        [TestMethod]
        public void GemStateManager_SetProcessingState_UpdatesCorrectly()
        {
            // Act
            _clientStateManager.SetProcessingState(SecsGemClient.ProcessingState.Processing);

            // Assert
            Assert.AreEqual(SecsGemClient.ProcessingState.Processing, _clientStateManager.ProcessingState);
            var eqState = _clientStateManager.GetStatusVariable("EqState");
            Assert.AreEqual("PROCESSING", eqState);
        }

        [TestMethod]
        public void GemStateManager_UpdateStatusVariable_WorksCorrectly()
        {
            // Arrange
            const string testKey = "TestVariable";
            const string testValue = "TestValue";

            // Act
            _clientStateManager.UpdateStatusVariable(testKey, testValue);

            // Assert
            var retrievedValue = _clientStateManager.GetStatusVariable(testKey);
            Assert.AreEqual(testValue, retrievedValue);
        }

        [TestMethod]
        public void GemStateManager_SetAlarm_AddsAlarmCorrectly()
        {
            // Arrange
            const uint alarmId = 101;
            const string alarmText = "Test Alarm";
            bool eventTriggered = false;

            _clientStateManager.OnEventTriggered += (sender, args) =>
            {
                eventTriggered = true;
                Assert.AreEqual(alarmId + 100, args.EventId);
            };

            // Act
            _clientStateManager.SetAlarm(alarmId, alarmText, SecsGemClient.AlarmSeverity.Warning);

            // Assert
            Assert.AreEqual(1, _clientStateManager.ActiveAlarms.Count);
            var alarm = _clientStateManager.ActiveAlarms.First();
            Assert.AreEqual(alarmId, alarm.AlarmId);
            Assert.AreEqual(alarmText, alarm.AlarmText);
            Assert.AreEqual(SecsGemClient.AlarmSeverity.Warning, alarm.Severity);
            Assert.IsTrue(alarm.IsSet);
            Assert.IsTrue(eventTriggered);
        }

        [TestMethod]
        public void GemStateManager_ClearAlarm_RemovesAlarmCorrectly()
        {
            // Arrange
            const uint alarmId = 101;
            const string alarmText = "Test Alarm";
            _clientStateManager.SetAlarm(alarmId, alarmText);

            bool eventTriggered = false;
            _clientStateManager.OnEventTriggered += (sender, args) =>
            {
                if (args.EventId == alarmId + 200) // Clear event
                    eventTriggered = true;
            };

            // Act
            _clientStateManager.ClearAlarm(alarmId);

            // Assert
            Assert.AreEqual(0, _clientStateManager.ActiveAlarms.Count);
            Assert.IsTrue(eventTriggered);
        }

        [TestMethod]
        public void GemStateManager_ProcessJobManagement_WorksCorrectly()
        {
            // Arrange
            const string processJobId = "TestJob001";
            const string recipe = "TestRecipe";
            var parameters = new Dictionary<string, object> { { "param1", "value1" } };

            // Act
            _clientStateManager.CreateProcessJob(processJobId, recipe, parameters);
            _clientStateManager.StartProcessJob(processJobId);

            // Assert
            Assert.AreEqual(processJobId, _clientStateManager.GetStatusVariable("ProcessJobId"));
            Assert.AreEqual(recipe, _clientStateManager.GetStatusVariable("PPID"));
            Assert.AreEqual(SecsGemClient.ProcessingState.Processing, _clientStateManager.ProcessingState);

            // Act - Complete job
            _clientStateManager.CompleteProcessJob(processJobId);

            // Assert
            Assert.AreEqual("", _clientStateManager.GetStatusVariable("ProcessJobId"));
            Assert.AreEqual(SecsGemClient.ProcessingState.Idle, _clientStateManager.ProcessingState);
        }
    }

    [TestClass]
    public class SecsMessageProcessorTests
    {
        private SecsGemClient.SecsMessageProcessor _processor;

        [TestInitialize]
        public void Setup()
        {
            _processor = new SecsGemClient.SecsMessageProcessor();
        }

        [TestMethod]
        public void CreateS1F1Message_CreatesCorrectMessage()
        {
            // Act
            var message = SecsGemClient.SecsMessageProcessor.CreateS1F1Message();

            // Assert
            Assert.AreEqual(1, message.Stream);
            Assert.AreEqual(1, message.Function);
            Assert.IsTrue(message.WBit);
            Assert.IsNull(message.RootItem);
        }

        [TestMethod]
        public void CreateS1F2Message_CreatesCorrectMessage()
        {
            // Act
            var message = _processor.CreateS1F2Message();

            // Assert
            Assert.AreEqual(1, message.Stream);
            Assert.AreEqual(2, message.Function);
            Assert.IsFalse(message.WBit);
            Assert.IsNotNull(message.RootItem);
            Assert.AreEqual(SecsGemClient.SecsFormat.List, message.RootItem.Format);
        }

        [TestMethod]
        public void CreateS1F14Message_CreatesCorrectMessage()
        {
            // Act
            var message = _processor.CreateS1F14Message();

            // Assert
            Assert.AreEqual(1, message.Stream);
            Assert.AreEqual(14, message.Function);
            Assert.IsFalse(message.WBit);
            Assert.IsNotNull(message.RootItem);

            var listItem = message.RootItem as SecsGemClient.SecsListItem;
            Assert.IsNotNull(listItem);
            Assert.AreEqual(2, listItem.Items.Count);
        }

        [TestMethod]
        public void CreateS2F18Message_CreatesCorrectMessage()
        {
            // Act
            var message = _processor.CreateS2F18Message();

            // Assert
            Assert.AreEqual(2, message.Stream);
            Assert.AreEqual(18, message.Function);
            Assert.IsFalse(message.WBit);
            Assert.IsNotNull(message.RootItem);

            var asciiItem = message.RootItem as SecsGemClient.SecsAsciiItem;
            Assert.IsNotNull(asciiItem);
            Assert.AreEqual(14, asciiItem.Value.Length); // yyyyMMddHHmmss format
        }

        [TestMethod]
        public void CreateS5F1Message_CreatesCorrectMessage()
        {
            // Arrange
            const uint alarmId = 101;
            const string alarmText = "Test Alarm";

            // Act
            var message = _processor.CreateS5F1Message(alarmId, alarmText);

            // Assert
            Assert.AreEqual(5, message.Stream);
            Assert.AreEqual(1, message.Function);
            Assert.IsTrue(message.WBit);
            Assert.IsNotNull(message.RootItem);

            var listItem = message.RootItem as SecsGemClient.SecsListItem;
            Assert.IsNotNull(listItem);
            Assert.AreEqual(3, listItem.Items.Count);
        }

        [TestMethod]
        public void CreateS6F11Message_CreatesCorrectMessage()
        {
            // Arrange
            const uint eventId = 1001;
            var reportData = new Dictionary<string, object>
            {
                { "EventText", "Test Event" },
                { "Value", 42 },
                { "FloatValue", 3.14f }
            };

            // Act
            var message = _processor.CreateS6F11Message(eventId, reportData);

            // Assert
            Assert.AreEqual(6, message.Stream);
            Assert.AreEqual(11, message.Function);
            Assert.IsTrue(message.WBit);
            Assert.IsNotNull(message.RootItem);

            var listItem = message.RootItem as SecsGemClient.SecsListItem;
            Assert.IsNotNull(listItem);
            Assert.AreEqual(2, listItem.Items.Count);
        }

        [TestMethod]
        public void SerializeMessage_ProducesValidOutput()
        {
            // Arrange
            var message = _processor.CreateS1F2Message();

            // Act
            var serializedData = _processor.SerializeMessage(message);

            // Assert
            Assert.IsNotNull(serializedData);
            Assert.IsTrue(serializedData.Length > 0);
        }

        [TestMethod]
        public void ParseMessage_HandlesEmptyData()
        {
            // Act
            var message = _processor.ParseMessage(null);

            // Assert
            Assert.IsNotNull(message);
            Assert.IsNull(message.RootItem);
        }
    }

    [TestClass]
    public class SecsItemTests
    {
        [TestMethod]
        public void SecsAsciiItem_CreatesCorrectly()
        {
            // Arrange
            const string testValue = "Hello World";

            // Act
            var item = new SecsGemClient.SecsAsciiItem(testValue);

            // Assert
            Assert.AreEqual(SecsGemClient.SecsFormat.ASCII, item.Format);
            Assert.AreEqual(testValue, item.Value);
            var bytes = item.GetBytes();
            Assert.AreEqual(testValue.Length, bytes.Length);
        }

        [TestMethod]
        public void SecsBinaryItem_CreatesCorrectly()
        {
            // Arrange
            var testData = new byte[] { 0x01, 0x02, 0x03, 0x04 };

            // Act
            var item = new SecsGemClient.SecsBinaryItem(testData);

            // Assert
            Assert.AreEqual(SecsGemClient.SecsFormat.Binary, item.Format);
            CollectionAssert.AreEqual(testData, item.Value);
            CollectionAssert.AreEqual(testData, item.GetBytes());
        }

        [TestMethod]
        public void SecsU4Item_CreatesCorrectly()
        {
            // Arrange
            const uint testValue = 12345;

            // Act
            var item = new SecsGemClient.SecsU4Item(testValue);

            // Assert
            Assert.AreEqual(SecsGemClient.SecsFormat.U4, item.Format);
            Assert.AreEqual(testValue, item.Value);

            var bytes = item.GetBytes();
            Assert.AreEqual(4, bytes.Length);
        }

        [TestMethod]
        public void SecsF4Item_CreatesCorrectly()
        {
            // Arrange
            const float testValue = 3.14159f;

            // Act
            var item = new SecsGemClient.SecsF4Item(testValue);

            // Assert
            Assert.AreEqual(SecsGemClient.SecsFormat.F4, item.Format);
            Assert.AreEqual(testValue, item.Value, 0.0001f);

            var bytes = item.GetBytes();
            Assert.AreEqual(4, bytes.Length);
        }

        [TestMethod]
        public void SecsListItem_CreatesCorrectly()
        {
            // Arrange
            var items = new List<SecsGemClient.SecsItem>
            {
                new SecsGemClient.SecsAsciiItem("Test"),
                new SecsGemClient.SecsU4Item(42)
            };

            // Act
            var listItem = new SecsGemClient.SecsListItem(items);

            // Assert
            Assert.AreEqual(SecsGemClient.SecsFormat.List, listItem.Format);
            Assert.AreEqual(2, listItem.Items.Count);
        }

        [TestMethod]
        public void SecsItem_ToString_ReturnsFormattedString()
        {
            // Arrange
            var asciiItem = new SecsGemClient.SecsAsciiItem("Test");
            var binaryItem = new SecsGemClient.SecsBinaryItem(new byte[] { 0x01, 0x02 });
            var u4Item = new SecsGemClient.SecsU4Item(123);

            // Act & Assert
            Assert.IsTrue(asciiItem.ToString().Contains("Test"));
            Assert.IsTrue(binaryItem.ToString().Contains("01 02"));
            Assert.IsTrue(u4Item.ToString().Contains("123"));
        }
    }

    [TestClass]
    public class HsmsMessageTests
    {
        [TestMethod]
        public void HsmsMessage_Properties_SetCorrectly()
        {
            // Arrange & Act
            var message = new SecsGemClient.HsmsMessage
            {
                SessionId = 1,
                HeaderByte2 = 0x81,
                HeaderByte3 = 0x01,
                PType = SecsGemClient.HsmsPType.DataMessage,
                SType = SecsGemClient.HsmsSType.DataMessage,
                SystemBytes = 12345,
                Data = new byte[] { 0x01, 0x02, 0x03 }
            };

            // Assert
            Assert.AreEqual(1, message.SessionId);
            Assert.AreEqual(0x81, message.HeaderByte2);
            Assert.AreEqual(0x01, message.HeaderByte3);
            Assert.AreEqual(SecsGemClient.HsmsPType.DataMessage, message.PType);
            Assert.AreEqual(SecsGemClient.HsmsSType.DataMessage, message.SType);
            Assert.AreEqual(12345u, message.SystemBytes);
            Assert.AreEqual(3, message.Data.Length);
        }

        [TestMethod]
        public void HsmsHeader_Properties_SetCorrectly()
        {
            // Arrange & Act
            var header = new SecsGemClient.HsmsHeader
            {
                Length = 13,
                SessionId = 1,
                HeaderByte2 = 0x81,
                HeaderByte3 = 0x01,
                PType = SecsGemClient.HsmsPType.DataMessage,
                SType = SecsGemClient.HsmsSType.DataMessage,
                SystemBytes = 12345
            };

            // Assert
            Assert.AreEqual(13u, header.Length);
            Assert.AreEqual(1, header.SessionId);
            Assert.AreEqual(0x81, header.HeaderByte2);
            Assert.AreEqual(0x01, header.HeaderByte3);
            Assert.AreEqual(SecsGemClient.HsmsPType.DataMessage, header.PType);
            Assert.AreEqual(SecsGemClient.HsmsSType.DataMessage, header.SType);
            Assert.AreEqual(12345u, header.SystemBytes);
        }
    }

    [TestClass]
    public class LogManagerTests
    {
        private SecsGemClient.LogManager _logManager;
        private string _testLogDirectory;

        [TestInitialize]
        public void Setup()
        {
            _testLogDirectory = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "SecsGemTestLogs");
            _logManager = new SecsGemClient.LogManager(_testLogDirectory);
        }

        [TestCleanup]
        public void Cleanup()
        {
            try
            {
                if (System.IO.Directory.Exists(_testLogDirectory))
                {
                    System.IO.Directory.Delete(_testLogDirectory, true);
                }
            }
            catch
            {
                // Ignore cleanup errors in tests
            }
        }

        [TestMethod]
        public void LogManager_LogMessage_CreatesLogFile()
        {
            // Arrange
            const string testMessage = "Test log message";

            // Act
            _logManager.LogMessage(testMessage, SecsGemClient.LogType.General);

            // Assert
            var logFiles = System.IO.Directory.GetFiles(_testLogDirectory, "General_*.log");
            Assert.AreEqual(1, logFiles.Length);

            var logContent = System.IO.File.ReadAllText(logFiles[0]);
            Assert.IsTrue(logContent.Contains(testMessage));
        }

        [TestMethod]
        public void LogManager_GetRecentLogEntries_ReturnsEntries()
        {
            // Arrange
            const string testMessage1 = "Test message 1";
            const string testMessage2 = "Test message 2";

            _logManager.LogMessage(testMessage1, SecsGemClient.LogType.General);
            _logManager.LogMessage(testMessage2, SecsGemClient.LogType.General);

            // Act
            var entries = _logManager.GetRecentLogEntries(SecsGemClient.LogType.General, 10);

            // Assert
            Assert.AreEqual(2, entries.Count);
            Assert.IsTrue(entries.Any(e => e.Contains(testMessage1)));
            Assert.IsTrue(entries.Any(e => e.Contains(testMessage2)));
        }

        [TestMethod]
        public void LogManager_DifferentLogTypes_CreateSeparateFiles()
        {
            // Act
            _logManager.LogMessage("SML Message", SecsGemClient.LogType.SML);
            _logManager.LogMessage("General Message", SecsGemClient.LogType.General);
            _logManager.LogMessage("EDA Message", SecsGemClient.LogType.EDA);

            // Assert
            var smlFiles = System.IO.Directory.GetFiles(_testLogDirectory, "SML_*.log");
            var generalFiles = System.IO.Directory.GetFiles(_testLogDirectory, "General_*.log");
            var edaFiles = System.IO.Directory.GetFiles(_testLogDirectory, "EDA_*.log");

            Assert.AreEqual(1, smlFiles.Length);
            Assert.AreEqual(1, generalFiles.Length);
            Assert.AreEqual(1, edaFiles.Length);
        }
    }

    [TestClass]
    public class SecsMessageStringRepresentationTests
    {
        [TestMethod]
        public void SecsMessage_ToString_ReturnsCorrectFormat()
        {
            // Arrange
            var message = new SecsGemClient.SecsMessage
            {
                Stream = 1,
                Function = 2,
                WBit = false,
                SystemBytes = 12345
            };

            // Act
            var result = message.ToString();

            // Assert
            Assert.AreEqual("S1F2 SysBytes: 0x00003039", result);
        }

        [TestMethod]
        public void SecsMessage_ToString_WithWBit_ReturnsCorrectFormat()
        {
            // Arrange
            var message = new SecsGemClient.SecsMessage
            {
                Stream = 5,
                Function = 1,
                WBit = true,
                SystemBytes = 0xABCD
            };

            // Act
            var result = message.ToString();

            // Assert
            Assert.AreEqual("S5F1W SysBytes: 0x0000ABCD", result);
        }

        [TestMethod]
        public void SecsMessage_GetDetailedString_IncludesDataStructure()
        {
            // Arrange
            var message = new SecsGemClient.SecsMessage
            {
                Stream = 1,
                Function = 2,
                WBit = false,
                SystemBytes = 12345,
                SessionId = 1,
                RootItem = new SecsGemClient.SecsAsciiItem("Test")
            };

            // Act
            var result = message.GetDetailedString();

            // Assert
            Assert.IsTrue(result.Contains("S1F2"));
            Assert.IsTrue(result.Contains("System: 0x00003039"));
            Assert.IsTrue(result.Contains("Session: 1"));
            Assert.IsTrue(result.Contains("Data Structure:"));
            Assert.IsTrue(result.Contains("Test"));
        }
    }

    [TestClass]
    public class EventArgumentsTests
    {
        [TestMethod]
        public void GemStateEventArgs_Constructor_SetsPropertiesCorrectly()
        {
            // Arrange & Act
            var args = new SecsGemClient.GemStateEventArgs(
                SecsGemClient.GemState.Offline,
                SecsGemClient.GemState.Online);

            // Assert
            Assert.AreEqual(SecsGemClient.GemState.Offline, args.PreviousState);
            Assert.AreEqual(SecsGemClient.GemState.Online, args.NewState);
        }

        [TestMethod]
        public void EventTriggeredEventArgs_Constructor_SetsPropertiesCorrectly()
        {
            // Arrange
            const uint eventId = 101;
            const string description = "Test Event";
            var beforeTime = DateTime.Now;

            // Act
            var args = new SecsGemClient.EventTriggeredEventArgs(eventId, description);
            var afterTime = DateTime.Now;

            // Assert
            Assert.AreEqual(eventId, args.EventId);
            Assert.AreEqual(description, args.Description);
            Assert.IsTrue(args.Timestamp >= beforeTime && args.Timestamp <= afterTime);
        }

        [TestMethod]
        public void ConnectionStateEventArgs_Constructor_SetsStateCorrectly()
        {
            // Arrange & Act
            var args = new SecsGemClient.ConnectionStateEventArgs(
                SecsGemClient.ConnectionState.Connected);

            // Assert
            Assert.AreEqual(SecsGemClient.ConnectionState.Connected, args.State);
        }

        [TestMethod]
        public void SecsMessageEventArgs_Constructor_SetsMessageCorrectly()
        {
            // Arrange
            var message = new SecsGemClient.SecsMessage
            {
                Stream = 1,
                Function = 1
            };

            // Act
            var args = new SecsGemClient.SecsMessageEventArgs(message);

            // Assert
            Assert.AreEqual(message, args.Message);
        }

        [TestMethod]
        public void MessageProcessedEventArgs_Constructor_SetsResultCorrectly()
        {
            // Arrange
            const string result = "Message processed successfully";

            // Act
            var args = new SecsGemClient.MessageProcessedEventArgs(result);

            // Assert
            Assert.AreEqual(result, args.ProcessingResult);
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
            var exception = new SecsGemClient.HsmsException(errorMessage);

            // Assert
            Assert.AreEqual(errorMessage, exception.Message);
        }

        [TestMethod]
        public void HsmsException_WithMessageAndInnerException_SetsPropertiesCorrectly()
        {
            // Arrange
            const string errorMessage = "Test HSMS error";
            var innerException = new InvalidOperationException("Inner error");

            // Act
            var exception = new SecsGemClient.HsmsException(errorMessage, innerException);

            // Assert
            Assert.AreEqual(errorMessage, exception.Message);
            Assert.AreEqual(innerException, exception.InnerException);
        }
    }
}

// Additional test class for integration-style tests
namespace SecsGemIntegrationTests
{
    [TestClass]
    public class MessageProcessingIntegrationTests
    {
        private SecsGemClient.SecsMessageProcessor _processor;

        [TestInitialize]
        public void Setup()
        {
            _processor = new SecsGemClient.SecsMessageProcessor();
        }

        [TestMethod]
        public void MessageProcessor_SerializeDeserializeRoundTrip_PreservesData()
        {
            // Arrange
            var originalMessage = _processor.CreateS1F14Message();
            originalMessage.SessionId = 42;
            originalMessage.SystemBytes = 0x12345678;

            // Act
            var serializedData = _processor.SerializeMessage(originalMessage);
            var deserializedMessage = _processor.ParseMessage(serializedData);

            // Assert
            Assert.IsNotNull(deserializedMessage.RootItem);
            Assert.AreEqual(originalMessage.RootItem.Format, deserializedMessage.RootItem.Format);
        }

        [TestMethod]
        public void GemStateManager_CompleteWorkflow_WorksCorrectly()
        {
            // Arrange
            var stateManager = new SecsGemClient.GemStateManager();
            var eventsTriggered = new List<uint>();

            stateManager.OnEventTriggered += (sender, args) =>
            {
                eventsTriggered.Add(args.EventId);
            };

            // Act - Simulate a complete workflow
            stateManager.SetLocal();           // Should trigger event 2
            stateManager.GoOnline();           // Should trigger event 3

            var processJobId = "TEST_001";
            var recipe = "TestRecipe";
            var parameters = new Dictionary<string, object> { { "param1", "value1" } };

            stateManager.CreateProcessJob(processJobId, recipe, parameters);  // Event 30
            stateManager.StartProcessJob(processJobId);                      // Event 33
            stateManager.CompleteProcessJob(processJobId);                   // Event 34

            stateManager.GoOffline();          // Should trigger event 1

            // Assert
            Assert.IsTrue(eventsTriggered.Contains(2));   // Local
            Assert.IsTrue(eventsTriggered.Contains(3));   // Online
            Assert.IsTrue(eventsTriggered.Contains(30));  // Job Created
            Assert.IsTrue(eventsTriggered.Contains(33));  // Job Executing
            Assert.IsTrue(eventsTriggered.Contains(34));  // Job Completed
            Assert.IsTrue(eventsTriggered.Contains(1));   // Offline

            Assert.AreEqual(SecsGemClient.GemState.Offline, stateManager.ControlState);
            Assert.AreEqual(SecsGemClient.ProcessingState.Idle, stateManager.ProcessingState);
        }
    }
}