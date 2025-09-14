using System;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Drawing;
using System.Net;
using System.Collections.Generic;

namespace BasicSecsGemServer
{
    // SECS/GEM Message Definitions (Same as equipment side)
    public static class SecsGemMessages
    {
        // Dictionary for SECS message definitions
        public static readonly Dictionary<string, (string Description, bool AutoRespond)> MessageDefinitions =
            new Dictionary<string, (string, bool)>
            {
                // Stream 0 - System Messages
                ["S0F0"] = ("Link Test Request", false),
                ["S0F1"] = ("Link Test Response", false),

                // Stream 1 - Equipment Status
                ["S1F1"] = ("Are You There Request", false),
                ["S1F2"] = ("Are You There Response", false),
                ["S1F3"] = ("Selected Equipment Status Request", false),
                ["S1F4"] = ("Selected Equipment Status Response", false),
                ["S1F13"] = ("Establish Communications Request", false),
                ["S1F14"] = ("Establish Communications Acknowledge", false),

                // Stream 2 - Equipment Control and Diagnostics
                ["S2F17"] = ("Date and Time Request", false),
                ["S2F18"] = ("Date and Time Response", false),
                ["S2F21"] = ("Remote Command Send", false),
                ["S2F22"] = ("Remote Command Acknowledge", false),

                // Stream 5 - Exception Handling
                ["S5F1"] = ("Alarm Report Send", false),
                ["S5F2"] = ("Alarm Report Acknowledge", false),

                // Stream 6 - Data Collection
                ["S6F11"] = ("Event Report Send", false),
                ["S6F12"] = ("Event Report Acknowledge", false),

                // Stream 7 - Process Program Management
                ["S7F1"] = ("Process Program Load Inquire", false),
                ["S7F2"] = ("Process Program Load Grant", false),

                // Stream 10 - Terminal Services
                ["S10F1"] = ("Terminal Request", false),
                ["S10F2"] = ("Terminal Acknowledge", false)
            };

        // Host Events (different from equipment events)
        public static class Events
        {
            public const int HostStatusUpdate = 1001;
            public const int EquipmentConnected = 1002;
            public const int EquipmentDisconnected = 1003;
            public const int CommunicationEstablished = 1004;
            public const int PeriodicHeartbeat = 1005;
        }

        // Host Commands
        public static class Commands
        {
            public const int StartProcess = 2001;
            public const int StopProcess = 2002;
            public const int GetStatus = 2003;
            public const int Reset = 2004;
        }
    }

    public partial class MainForm : Form
    {
        private HsmsConnection _hsmsConnection;
        private SecsMessageProcessor _messageProcessor;
        private GemStateManager _stateManager;
        private LogManager _logManager;

        // UI Controls
        private GroupBox connectionGroupBox;
        private GroupBox statusGroupBox;
        private GroupBox messageGroupBox;
        private TextBox ipAddressTextBox;
        private TextBox portTextBox;
        private TextBox deviceIdTextBox;
        private Button connectButton;
        private Button disconnectButton;
        private Label connectionStatusLabel;
        private RadioButton onlineRadio;
        private RadioButton offlineRadio;
        private ListBox messageLogListBox;
        private Button sendTestButton;
        private RichTextBox logTextBox;

        public MainForm()
        {
            InitializeComponent();
            CreateControls();
            InitializeSecsGem();
            SetupEventHandlers();
        }

        private void CreateControls()
        {
            // Connection Group Box
            connectionGroupBox = new GroupBox
            {
                Text = "Connection Settings",
                Location = new Point(10, 10),
                Size = new Size(400, 150),
                Font = new Font("Segoe UI", 9F, FontStyle.Bold)
            };

            var ipLabel = new Label { Text = "IP Address:", Location = new Point(10, 30), Size = new Size(110, 23) };
            ipAddressTextBox = new TextBox { Location = new Point(120, 27), Size = new Size(120, 23), Text = "127.0.0.1" };

            var portLabel = new Label { Text = "Port:", Location = new Point(10, 60), Size = new Size(80, 23) };
            portTextBox = new TextBox { Location = new Point(120, 57), Size = new Size(120, 23), Text = "5000" };

            var deviceLabel = new Label { Text = "Device ID:", Location = new Point(10, 90), Size = new Size(80, 23) };
            deviceIdTextBox = new TextBox { Location = new Point(120, 87), Size = new Size(120, 23), Text = "0" };

            connectButton = new Button
            {
                Text = "Connect",
                Location = new Point(250, 30),
                Size = new Size(110, 35),
                BackColor = Color.LightGreen
            };

            disconnectButton = new Button
            {
                Text = "Disconnect",
                Location = new Point(250, 70),
                Size = new Size(120, 35),
                BackColor = Color.LightCoral,
                Enabled = false
            };

            connectionGroupBox.Controls.AddRange([
                ipLabel, ipAddressTextBox, portLabel, portTextBox,
                deviceLabel, deviceIdTextBox, connectButton, disconnectButton
            ]);

            // Status Group Box
            statusGroupBox = new GroupBox
            {
                Text = "GEM State Control",
                Location = new Point(10, 170),
                Size = new Size(400, 120),
                Font = new Font("Segoe UI", 9F, FontStyle.Bold)
            };

            onlineRadio = new RadioButton
            {
                Text = "Online",
                Location = new Point(20, 30),
                Size = new Size(100, 25)
            };

            offlineRadio = new RadioButton
            {
                Text = "Offline",
                Location = new Point(120, 30),
                Size = new Size(100, 25),
                Checked = true
            };

            connectionStatusLabel = new Label
            {
                Text = "Status: Offline",
                Location = new Point(20, 60),
                Size = new Size(200, 23),
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                ForeColor = Color.Red
            };

            statusGroupBox.Controls.AddRange(new Control[] {
                onlineRadio, offlineRadio, connectionStatusLabel
            });

            // Message Group Box
            messageGroupBox = new GroupBox
            {
                Text = "Messages & Events",
                Location = new Point(430, 10),
                Size = new Size(450, 280),
                Font = new Font("Segoe UI", 9F, FontStyle.Bold)
            };

            messageLogListBox = new ListBox
            {
                Location = new Point(20, 25),
                Size = new Size(400, 200),
                Font = new Font("Consolas", 8F)
            };

            sendTestButton = new Button
            {
                Text = "Send Test Message (S1F1)",
                Location = new Point(20, 240),
                Size = new Size(180, 30),
                Enabled = false
            };

            messageGroupBox.Controls.AddRange(new Control[] {
                messageLogListBox, sendTestButton
            });

            // Log Text Box
            var logLabel = new Label
            {
                Text = "System Log:",
                Location = new Point(10, 305),
                Size = new Size(100, 23),
                Font = new Font("Segoe UI", 9F, FontStyle.Bold)
            };

            logTextBox = new RichTextBox
            {
                Location = new Point(10, 330),
                Size = new Size(870, 320),
                Font = new Font("Consolas", 8F),
                ReadOnly = true,
                BackColor = Color.Black,
                ForeColor = Color.LimeGreen
            };

            this.Controls.AddRange(new Control[] {
                connectionGroupBox, statusGroupBox, messageGroupBox, logLabel, logTextBox
            });
        }

        private void SetupEventHandlers()
        {
            connectButton.Click += ConnectButton_Click;
            disconnectButton.Click += DisconnectButton_Click;
            sendTestButton.Click += SendTestButton_Click;
            onlineRadio.CheckedChanged += OnlineRadio_CheckedChanged;
            this.FormClosing += MainForm_FormClosing;
        }

        private void InitializeSecsGem()
        {
            _logManager = new LogManager();
            _stateManager = new GemStateManager();
            _messageProcessor = new SecsMessageProcessor();
            _hsmsConnection = new HsmsConnection();

            // Subscribe to events
            _hsmsConnection.ConnectionStateChanged += OnConnectionStateChanged;
            _hsmsConnection.MessageReceived += OnMessageReceived;
            _messageProcessor.MessageProcessed += OnMessageProcessed;
            _stateManager.StateChanged += OnGemStateChanged;
            _stateManager.OnEventTriggered += OnEventTriggered;

            LogMessage("SECS/GEM Host initialized using refactored message handling", LogType.General);
        }

        private async void ConnectButton_Click(object sender, EventArgs e)
        {
            try
            {
                var ipAddress = IPAddress.Parse(ipAddressTextBox.Text);
                var port = int.Parse(portTextBox.Text);
                var deviceId = int.Parse(deviceIdTextBox.Text);

                connectButton.Enabled = false;
                connectionStatusLabel.Text = "Status: Starting...";
                connectionStatusLabel.ForeColor = Color.Orange;

                bool connected = await _hsmsConnection.ConnectAsync(ipAddress, port, deviceId);

                if (connected)
                {
                    LogMessage($"SECS/GEM Host started on {ipAddress}:{port}, Device ID: {deviceId}", LogType.General);

                    connectButton.Enabled = false;
                    disconnectButton.Enabled = true;
                    sendTestButton.Enabled = true;

                    // Send initial event
                    SendHostEvent(SecsGemMessages.Events.EquipmentConnected, "Host connection established");

                    // Start periodic tasks
                    _ = Task.Run(SendPeriodicMessages);
                }
                else
                {
                    throw new Exception("Failed to establish connection");
                }
            }
            catch (Exception ex)
            {
                LogMessage($"Error starting host: {ex.Message}", LogType.General);
                connectButton.Enabled = true;
                connectionStatusLabel.Text = "Status: Error";
                connectionStatusLabel.ForeColor = Color.Red;
            }
        }

        private async void DisconnectButton_Click(object sender, EventArgs e)
        {
            try
            {
                if (_hsmsConnection != null)
                {
                    // Send disconnect event before closing
                    SendHostEvent(SecsGemMessages.Events.EquipmentDisconnected, "Host disconnecting");
                    await Task.Delay(500); // Allow message to send

                    await _hsmsConnection.DisconnectAsync();
                }

                connectButton.Enabled = true;
                disconnectButton.Enabled = false;
                sendTestButton.Enabled = false;

                connectionStatusLabel.Text = "Status: Offline";
                connectionStatusLabel.ForeColor = Color.Red;

                LogMessage("SECS/GEM Host stopped", LogType.General);
            }
            catch (Exception ex)
            {
                LogMessage($"Error stopping host: {ex.Message}", LogType.General);
            }
        }

        private void SendTestButton_Click(object sender, EventArgs e)
        {
            if (_hsmsConnection.IsConnected == true)
            {
                try
                {
                    var s1f1Message = SecsMessageProcessor.CreateS1F1Message();
                    _hsmsConnection.SendMessage(s1f1Message);

                    LogMessage("Sent S1F1 - Are You There", LogType.SML);
                    AddMessageToLog("TX: S1F1 - Are You There");
                }
                catch (Exception ex)
                {
                    LogMessage($"Error sending test message: {ex.Message}", LogType.General);
                }
            }
            else
            {
                LogMessage("Cannot send message - no equipment connected", LogType.General);
            }
        }

        private void OnlineRadio_CheckedChanged(object sender, EventArgs e)
        {
            if (onlineRadio.Checked)
            {
                _stateManager.GoOnline();
            }
            else
            {
                _stateManager.GoOffline();
            }
        }

        private void OnConnectionStateChanged(object sender, ConnectionStateEventArgs e)
        {
            this.Invoke((MethodInvoker)delegate
            {
                switch (e.State)
                {
                    case ConnectionState.Connected:
                        connectionStatusLabel.Text = "Status: Equipment Connected";
                        connectionStatusLabel.ForeColor = Color.Green;
                        LogMessage("Equipment connected", LogType.General);
                        break;
                    case ConnectionState.Connecting:
                        connectionStatusLabel.Text = "Status: Waiting for Equipment";
                        connectionStatusLabel.ForeColor = Color.Orange;
                        LogMessage("Waiting for equipment connection...", LogType.General);
                        break;
                    default:
                        connectionStatusLabel.Text = "Status: Offline";
                        connectionStatusLabel.ForeColor = Color.Red;
                        break;
                }
            });
        }

        private void OnMessageReceived(object sender, SecsMessageEventArgs e)
        {
            if (e.Message == null) return;

            var msg = e.Message;
            var messageKey = $"S{msg.Stream}F{msg.Function}";
            var messageText = $"{messageKey}{(msg.WBit ? "W" : "")}";

            this.Invoke((MethodInvoker)delegate
            {
                AddMessageToLog($"RX: {messageText}");
            });

            LogMessage($"Received: {messageText}", LogType.SML);

            try
            {
                HandleIncomingMessage(msg);
            }
            catch (Exception ex)
            {
                LogMessage($"Error handling message {messageText}: {ex.Message}", LogType.General);
            }
        }

        // Refactored message handling method using the message definitions
        private void HandleIncomingMessage(SecsMessage message)
        {
            var messageKey = $"S{message.Stream}F{message.Function}";

            // Check if message is defined
            if (!SecsGemMessages.MessageDefinitions.TryGetValue(messageKey, out var messageInfo))
            {
                LogMessage($"Unknown message received: {messageKey}", LogType.General);
                return;
            }

            LogMessage($"Processing: {messageInfo.Description}", LogType.SML);

            // Handle the message based on its type
            ProcessKnownMessage(message, messageKey, messageInfo);
        }

        private void ProcessKnownMessage(SecsMessage message, string messageKey, (string Description, bool AutoRespond) messageInfo)
        {
            try
            {
                switch (messageKey)
                {
                    case "S1F2":
                        HandleAreYouThereResponse(message);
                        break;

                    case "S1F14":
                        HandleEstablishCommunicationsAcknowledge(message);
                        break;

                    case "S2F18":
                        HandleDateTimeResponse(message);
                        break;

                    case "S5F1":
                        HandleAlarmReport(message);
                        break;

                    case "S6F11":
                        HandleEventReport(message);
                        break;

                    // Equipment responses to our requests
                    case "S0F1":
                        HandleLinkTestResponse(message);
                        break;

                    default:
                        LogMessage($"Message {messageKey} ({messageInfo.Description}) received but not specifically handled", LogType.General);
                        break;
                }
            }
            catch (Exception ex)
            {
                LogMessage($"Error handling {messageKey}: {ex.Message}", LogType.General);
            }
        }

        #region Message Handlers

        private void HandleAreYouThereResponse(SecsMessage message)
        {
            LogMessage("Equipment responded to Are You There request - Equipment is online", LogType.SML);
            AddMessageToLog("Equipment is online and responding");
        }

        private void HandleEstablishCommunicationsAcknowledge(SecsMessage message)
        {
            LogMessage("Communications established with equipment", LogType.SML);
            AddMessageToLog("Communications established");

            // Send event about successful communication
            SendHostEvent(SecsGemMessages.Events.CommunicationEstablished, "Communication link established with equipment");
        }

        private void HandleDateTimeResponse(SecsMessage message)
        {
            LogMessage("Received current date/time from equipment", LogType.SML);
            // Could parse the date/time data here if needed
        }

        private void HandleAlarmReport(SecsMessage message)
        {
            LogMessage("Received alarm report from equipment", LogType.SML);
            AddMessageToLog("Equipment alarm received");

            // Send acknowledge for alarm
            var s5f2 = _messageProcessor.CreateS5F2Message();
            s5f2.SystemBytes = message.SystemBytes;
            _hsmsConnection.SendMessage(s5f2);
            AddMessageToLog("TX: S5F2 - Alarm Acknowledge");
        }

        private void HandleEventReport(SecsMessage message)
        {
            LogMessage("Received event report from equipment", LogType.SML);
            AddMessageToLog("Equipment event received");

            // Send acknowledge for event
            var s6f12 = _messageProcessor.CreateS6F12Message();
            s6f12.SystemBytes = message.SystemBytes;
            _hsmsConnection.SendMessage(s6f12);
            AddMessageToLog("TX: S6F12 - Event Acknowledge");
        }

        private void HandleLinkTestResponse(SecsMessage message)
        {
            LogMessage("Equipment responded to link test - Connection is healthy", LogType.SML);
        }

        #endregion

        private async Task SendPeriodicMessages()
        {
            int messageCount = 0;

            while (_hsmsConnection?.IsConnected == true)
            {
                await Task.Delay(30000); // Every 30 seconds

                if (_hsmsConnection.IsConnected)
                {
                    messageCount++;
                    SendHostEvent(SecsGemMessages.Events.PeriodicHeartbeat, $"Periodic host heartbeat #{messageCount}");
                }
            }
        }

        private void SendHostEvent(uint eventId, string eventDescription)
        {
            try
            {
                var eventData = new Dictionary<string, object>
                {
                    ["EventId"] = eventId,
                    ["EventDescription"] = eventDescription,
                    ["Timestamp"] = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                    ["HostStatus"] = "ACTIVE",
                    ["HostVersion"] = "1.0.0"
                };

                var s6f11 = _messageProcessor.CreateS6F11Message(eventId, eventData);
                _hsmsConnection.SendMessage(s6f11);

                this.Invoke((MethodInvoker)delegate
                {
                    AddMessageToLog($"TX: S6F11 - {eventDescription}");
                });

                LogMessage($"Sent host event: {eventDescription}", LogType.SML);
            }
            catch (Exception ex)
            {
                LogMessage($"Error sending host event: {ex.Message}", LogType.General);
            }
        }

        private void OnMessageProcessed(object sender, MessageProcessedEventArgs e)
        {
            LogMessage($"Processed: {e.ProcessingResult}", LogType.General);
        }

        private void OnGemStateChanged(object sender, GemStateEventArgs e)
        {
            this.Invoke((MethodInvoker)delegate
            {
                if (e.NewState == GemState.Online)
                {
                    onlineRadio.Checked = true;
                    statusGroupBox.Text = "GEM State Control - ONLINE";
                }
                else
                {
                    offlineRadio.Checked = true;
                    statusGroupBox.Text = "GEM State Control - OFFLINE";
                }
            });

            LogMessage($"GEM State changed to: {e.NewState}", LogType.General);
        }

        private void OnEventTriggered(object sender, EventTriggeredEventArgs e)
        {
            LogMessage($"Event triggered: {e.EventId} - {e.Description}", LogType.General);
        }

        private void AddMessageToLog(string message)
        {
            if (messageLogListBox.InvokeRequired)
            {
                messageLogListBox.Invoke((MethodInvoker)delegate
                {
                    messageLogListBox.Items.Add($"{DateTime.Now:HH:mm:ss} - {message}");
                    if (messageLogListBox.Items.Count > 100)
                        messageLogListBox.Items.RemoveAt(0);
                    messageLogListBox.SelectedIndex = messageLogListBox.Items.Count - 1;
                });
            }
            else
            {
                messageLogListBox.Items.Add($"{DateTime.Now:HH:mm:ss} - {message}");
                if (messageLogListBox.Items.Count > 100)
                    messageLogListBox.Items.RemoveAt(0);
                messageLogListBox.SelectedIndex = messageLogListBox.Items.Count - 1;
            }
        }

        private void LogMessage(string message, LogType logType)
        {
            var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
            var logEntry = $"[{timestamp}] {message}";

            if (logTextBox.InvokeRequired)
            {
                logTextBox.Invoke((MethodInvoker)delegate
                {
                    logTextBox.AppendText(logEntry + Environment.NewLine);
                    logTextBox.ScrollToCaret();
                });
            }
            else
            {
                logTextBox.AppendText(logEntry + Environment.NewLine);
                logTextBox.ScrollToCaret();
            }

            //_logManager?.LogMessage(message, logType);
        }

        private async void MainForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (_hsmsConnection?.IsConnected == true)
            {
                await _hsmsConnection.DisconnectAsync();
            }
        }
    }
}