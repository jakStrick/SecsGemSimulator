using System;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Drawing;
using System.Net;

namespace BasicSecsGemServer
{
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

            // Subscribe to your existing events
            _hsmsConnection.ConnectionStateChanged += OnConnectionStateChanged;
            _hsmsConnection.MessageReceived += OnMessageReceived;
            _messageProcessor.MessageProcessed += OnMessageProcessed;
            _stateManager.StateChanged += OnGemStateChanged;
            _stateManager.OnEventTriggered += OnEventTriggered;

            LogMessage("SECS/GEM Host initialized using existing code structure", LogType.General);
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

                // Use your existing HsmsConnection
                bool connected = await _hsmsConnection.ConnectAsync(ipAddress, port, deviceId);

                if (connected)
                {
                    LogMessage($"SECS/GEM Host started on {ipAddress}:{port}, Device ID: {deviceId}", LogType.General);

                    connectButton.Enabled = false;
                    disconnectButton.Enabled = true;
                    sendTestButton.Enabled = true;

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
                    // Use your existing message processor
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
            var messageText = $"S{msg.Stream}F{msg.Function}";

            this.Invoke((MethodInvoker)delegate
            {
                AddMessageToLog($"RX: {messageText}");
            });

            LogMessage($"Received: {messageText}", LogType.SML);

            try
            {
                // Handle standard SECS messages using your existing processor
                HandleIncomingMessage(msg);
            }
            catch (Exception ex)
            {
                LogMessage($"Error handling message {messageText}: {ex.Message}", LogType.General);
            }
        }

        private void HandleIncomingMessage(SecsMessage message)
        {
            switch ($"S{message.Stream}F{message.Function}")
            {
                case "S0F1": // Are You There Request
                    var s0f1 = _messageProcessor.CreateS1F2Message();
                    s0f1.SystemBytes = message.SystemBytes; // Reply with same system bytes
                    _hsmsConnection.SendMessage(s0f1);
                    AddMessageToLog("TX: S0F1 - Online Data");
                    LogMessage("Sent S0F1 - Online Data", LogType.SML);
                    break;

                case "S1F1": // Are You There Request
                    var s1f2 = _messageProcessor.CreateS1F2Message();
                    s1f2.SystemBytes = message.SystemBytes; // Reply with same system bytes
                    _hsmsConnection.SendMessage(s1f2);
                    AddMessageToLog("TX: S1F2 - Online Data");
                    LogMessage("Sent S1F2 - Online Data", LogType.SML);
                    break;

                case "S1F13": // Establish Communications Request
                    var s1f14 = _messageProcessor.CreateS1F14Message();
                    s1f14.SystemBytes = message.SystemBytes;
                    _hsmsConnection.SendMessage(s1f14);
                    AddMessageToLog("TX: S1F14 - Establish Comm Ack");
                    LogMessage("Sent S1F14 - Establish Communications Acknowledge", LogType.SML);
                    break;

                case "S2F17": // Date/Time Request
                    var s2f18 = _messageProcessor.CreateS2F18Message();
                    s2f18.SystemBytes = message.SystemBytes;
                    _hsmsConnection.SendMessage(s2f18);
                    AddMessageToLog($"TX: S2F18 - Date/Time: {DateTime.Now}");
                    LogMessage($"Sent S2F18 - Date/Time: {DateTime.Now}", LogType.SML);
                    break;

                default:
                    LogMessage($"Unhandled message: S{message.Stream}F{message.Function}", LogType.General);
                    break;
            }
        }

        private async Task SendPeriodicMessages()
        {
            int messageCount = 0;

            while (_hsmsConnection?.IsConnected == true)
            {
                await Task.Delay(30000); // Every 30 seconds

                if (_hsmsConnection.IsConnected)
                {
                    messageCount++;
                    SendS6F11EventReport($"Periodic host message #{messageCount}");
                }
            }
        }

        private void SendS6F11EventReport(string eventText)
        {
            try
            {
                var eventData = new Dictionary<string, object>
                {
                    ["EventText"] = eventText,
                    ["Timestamp"] = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                    ["HostStatus"] = "ACTIVE"
                };

                var s6f11 = _messageProcessor.CreateS6F11Message(1001, eventData);
                _hsmsConnection.SendMessage(s6f11);

                this.Invoke((MethodInvoker)delegate
                {
                    AddMessageToLog($"TX: S6F11 - {eventText}");
                });

                LogMessage($"Sent event report: {eventText}", LogType.SML);
            }
            catch (Exception ex)
            {
                LogMessage($"Error sending event report: {ex.Message}", LogType.General);
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