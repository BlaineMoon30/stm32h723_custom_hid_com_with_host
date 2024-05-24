namespace stm32h723_hid_dfu_v0_2
{
    using System;
    using System.IO;
    using System.Linq;
    using System.Text;
    using System.Threading;
    using System.Windows.Forms;
    using HidLibrary;
    using static System.Windows.Forms.VisualStyles.VisualStyleElement;

    public partial class Form1 : Form
    {
        private HidDevice connectedDevice;
        private Thread pollingThread;
        private bool pollingActive;
        private byte[] fileBuffer;
        private Thread sendThread;
        private bool sendActive;

        public Form1()
        {
            InitializeComponent();
            button2.Enabled = false; // ���α׷� ���� �� Button2 ��Ȱ��ȭ
        }

        private void button1_Click(object sender, EventArgs e)
        {
            //string targetVid = "0x0483"; // VID �Է� (��: "0483")
            //string targetPid = "0x5750"; // PID �Է� (��: "5750")
            string targetVid = textBox1.Text; // textbox1�� ���ڿ� ���
            string targetPid = textBox2.Text; // textbox2�� ���ڿ� ���

            connectedDevice = GetHidDevice(targetVid, targetPid);
            if (connectedDevice != null)
            {
                richTextBox1.AppendText("HID Device found: " + connectedDevice.Description + Environment.NewLine);
                ConnectToHidDevice(connectedDevice);
            }
            else
            {
                richTextBox1.AppendText("HID Device not found." + Environment.NewLine);
            }
        }

        private HidDevice GetHidDevice(string vid, string pid)
        {
            try
            {
                int vendorId = Convert.ToInt32(vid, 16);
                int productId = Convert.ToInt32(pid, 16);

                var devices = HidDevices.Enumerate().ToList();
                foreach (var device in devices)
                {
                    if (device.Attributes.VendorId == vendorId && device.Attributes.ProductId == productId)
                    {
                        return device;
                    }
                }
            }
            catch (FormatException)
            {
                richTextBox1.AppendText("Invalid VID or PID format. Please enter hexadecimal values." + Environment.NewLine);
            }
            return null;
        }

        private void ConnectToHidDevice(HidDevice device)
        {
            device.OpenDevice();
            if (device.IsConnected)
            {
                richTextBox1.AppendText("HID Device connected successfully." + Environment.NewLine);
                DisplayHidDeviceDescriptor(device);

                // ������ ������ ���� ������ ����
                StartPolling();
            }
            else
            {
                richTextBox1.AppendText("Failed to connect to HID Device." + Environment.NewLine);
            }
        }

        private void DisplayHidDeviceDescriptor(HidDevice device)
        {
            richTextBox1.AppendText("Vendor ID: " + device.Attributes.VendorId.ToString("X4") + Environment.NewLine);
            richTextBox1.AppendText("Product ID: " + device.Attributes.ProductId.ToString("X4") + Environment.NewLine);
            richTextBox1.AppendText("Version Number: " + device.Attributes.Version + Environment.NewLine);

            if (device.ReadManufacturer(out byte[] manufacturerData))
            {
                richTextBox1.AppendText("Manufacturer: " + Encoding.Default.GetString(manufacturerData) + Environment.NewLine);
            }
            else
            {
                richTextBox1.AppendText("Failed to read Manufacturer." + Environment.NewLine);
            }

            if (device.ReadProduct(out byte[] productData))
            {
                richTextBox1.AppendText("Product: " + Encoding.Default.GetString(productData) + Environment.NewLine);
            }
            else
            {
                richTextBox1.AppendText("Failed to read Product." + Environment.NewLine);
            }

            if (device.ReadSerialNumber(out byte[] serialNumberData))
            {
                richTextBox1.AppendText("Serial Number: " + Encoding.Default.GetString(serialNumberData) + Environment.NewLine);
            }
            else
            {
                richTextBox1.AppendText("Failed to read Serial Number." + Environment.NewLine);
            }
        }

        private void button2_Click(object sender, EventArgs e)
        {
            if (connectedDevice != null && connectedDevice.IsConnected && fileBuffer != null)
            {
                StartSending();
            }
            else
            {
                richTextBox1.AppendText("No HID device connected or file not loaded." + Environment.NewLine);
            }
        }

        private void StartSending()
        {
            if (sendThread == null || !sendThread.IsAlive)
            {
                sendActive = true;
                sendThread = new Thread(SendThreadMethod);
                sendThread.Start();
            }
        }

        private void StopSending()
        {
            if (sendThread != null && sendThread.IsAlive)
            {
                sendActive = false;
                sendThread.Join(); // �����尡 ����� ������ ���
            }
        }

        private void SendThreadMethod()
        {
            try
            {
                int offset = 0;
                while (sendActive && offset < fileBuffer.Length)
                {
                    byte[] dataToSend = new byte[65]; // ù ����Ʈ�� ���� ID
                    dataToSend[0] = 0; // ���� ID ���� (�ʿ信 ���� ����)
                    int bytesToSend = Math.Min(64, fileBuffer.Length - offset);
                    Array.Copy(fileBuffer, offset, dataToSend, 1, bytesToSend);

                    bool success = connectedDevice.Write(dataToSend);

                    if (success)
                    {
                        Invoke((MethodInvoker)delegate
                        {
                            richTextBox1.AppendText("Data sent: " + BitConverter.ToString(dataToSend, 1, bytesToSend) + Environment.NewLine);
                            richTextBox1.SelectionStart = richTextBox1.Text.Length;
                            richTextBox1.ScrollToCaret();
                        });
                    }
                    else
                    {
                        Invoke((MethodInvoker)delegate
                        {
                            richTextBox1.AppendText("Failed to send data at offset: " + offset + Environment.NewLine);
                            richTextBox1.SelectionStart = richTextBox1.Text.Length;
                            richTextBox1.ScrollToCaret();
                        });
                        break;
                    }

                    offset += bytesToSend;
                    Thread.Sleep(100); // 100ms ���
                }

                Invoke((MethodInvoker)delegate
                {
                    if (offset >= fileBuffer.Length)
                    {
                        richTextBox1.AppendText("All data sent successfully." + Environment.NewLine);
                        richTextBox1.SelectionStart = richTextBox1.Text.Length;
                        richTextBox1.ScrollToCaret();
                    }
                });
            }
            catch (ThreadAbortException)
            {
                // �����尡 ���� ����� �� �ʿ��� ���� �۾�
                sendActive = false;
            }
        }

        private void button3_Click(object sender, EventArgs e)
        {
            // ���� ����¡ �� �ε�
            OpenFileDialog openFileDialog = new OpenFileDialog
            {
                Filter = "Binary files (*.bin)|*.bin|All files (*.*)|*.*",
                Title = "Select a BIN file"
            };

            if (openFileDialog.ShowDialog() == DialogResult.OK)
            {
                string filePath = openFileDialog.FileName;
                try
                {
                    fileBuffer = File.ReadAllBytes(filePath);
                    richTextBox1.AppendText("File loaded successfully. Size: " + fileBuffer.Length + " bytes" + Environment.NewLine);
                    label3.Text = "Loaded file path: " + filePath; // Label3�� ���� ��� ǥ��
                    button2.Enabled = true; // Button2 Ȱ��ȭ
                }
                catch (Exception ex)
                {
                    richTextBox1.AppendText("Failed to load file: " + ex.Message + Environment.NewLine);
                }
            }
        }

        private void StartPolling()
        {
            if (pollingThread == null || !pollingThread.IsAlive)
            {
                pollingActive = true;
                pollingThread = new Thread(PollingThreadMethod);
                pollingThread.Start();
            }
        }

        private void StopPolling()
        {
            if (pollingThread != null && pollingThread.IsAlive)
            {
                pollingActive = false;
                pollingThread.Abort(); // �����带 ������ ����
                pollingThread.Join(); // �����尡 ����� ������ ���
            }
        }

        private void PollingThreadMethod()
        {
            try
            {
                while (pollingActive)
                {
                    if (connectedDevice != null && connectedDevice.IsConnected)
                    {
                        var report = connectedDevice.ReadReport();
                        if (report.Data.Length > 0)
                        {
                            StringBuilder sb = new StringBuilder();
                            foreach (var b in report.Data)
                            {
                                sb.Append(b.ToString("X2") + " ");
                            }
                            Invoke((MethodInvoker)delegate
                            {
                                richTextBox2.AppendText("Received Data: " + sb.ToString() + Environment.NewLine);
                                richTextBox2.SelectionStart = richTextBox2.Text.Length;
                                richTextBox2.ScrollToCaret();
                            });
                        }
                    }
                    Thread.Sleep(100); // ������ ��� �ð� ����
                }
            }
            catch (ThreadAbortException)
            {
                // �����尡 ���� ����� �� �ʿ��� ���� �۾�
                pollingActive = false;
            }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            // ���� ���� �� �����ϰ� ����
            StopPolling();
            StopSending();
            if (connectedDevice != null && connectedDevice.IsConnected)
            {
                connectedDevice.CloseDevice();
                connectedDevice.Dispose();
            }
            base.OnFormClosing(e);
        }
    }
}
