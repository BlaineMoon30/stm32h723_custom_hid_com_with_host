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
            button2.Enabled = false; // 프로그램 시작 시 Button2 비활성화
        }

        private void button1_Click(object sender, EventArgs e)
        {
            //string targetVid = "0x0483"; // VID 입력 (예: "0483")
            //string targetPid = "0x5750"; // PID 입력 (예: "5750")
            string targetVid = textBox1.Text; // textbox1의 문자열 사용
            string targetPid = textBox2.Text; // textbox2의 문자열 사용

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

                // 데이터 수신을 위한 스레드 시작
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
                sendThread.Join(); // 스레드가 종료될 때까지 대기
            }
        }

        private void SendThreadMethod()
        {
            try
            {
                int offset = 0;
                while (sendActive && offset < fileBuffer.Length)
                {
                    byte[] dataToSend = new byte[65]; // 첫 바이트는 보고서 ID
                    dataToSend[0] = 0; // 보고서 ID 설정 (필요에 따라 변경)
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
                    Thread.Sleep(100); // 100ms 대기
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
                // 스레드가 강제 종료될 때 필요한 정리 작업
                sendActive = false;
            }
        }

        private void button3_Click(object sender, EventArgs e)
        {
            // 파일 브라우징 및 로드
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
                    label3.Text = "Loaded file path: " + filePath; // Label3에 파일 경로 표시
                    button2.Enabled = true; // Button2 활성화
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
                pollingThread.Abort(); // 스레드를 강제로 종료
                pollingThread.Join(); // 스레드가 종료될 때까지 대기
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
                    Thread.Sleep(100); // 적절한 대기 시간 설정
                }
            }
            catch (ThreadAbortException)
            {
                // 스레드가 강제 종료될 때 필요한 정리 작업
                pollingActive = false;
            }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            // 폼이 닫힐 때 안전하게 종료
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
