using System;
using System.Diagnostics;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace TCP_Client_File
{
    public partial class Form1 : Form
    {
        private TcpClient client;
        private NetworkStream stream = null;
        private FileStream file;
        private FileStream fileGet;

        private Thread thrSend;
        private Thread thrDownload;

        private string filePath;
        private static int PercentProgress;

        private delegate void UpdateStatusCallback(string StatusMessage);
        private delegate void UpdateProgressBar(Int64 BytesRead, Int64 TotalBytes, bool firstPB);
        private delegate void dis(object sender, EventArgs e);

        private bool isEnd = false;

        #region Конструктор
        public Form1()
        {
            InitializeComponent();

            pathLabel.Text = Directory.GetCurrentDirectory();
            filePath = pathLabel.Text;

            send.Enabled = false;
            disconnect.Enabled = false;
        }
        #endregion

        #region Консоль
        private void UpdateStatus(string StatusMessage)
        {
            richTextBox1.Text += StatusMessage;
        }
        #endregion

        #region Прием файла
        private void StartReceiving()
        {
            try
            {
                while (true)
                {
                    try
                    {
                        ProcessReceiving();
                    }
                    catch (Exception e)
                    {
                        this.Invoke(new UpdateStatusCallback(this.UpdateStatus), new object[] { e.Message + "\n" });
                        this.Invoke(new UpdateProgressBar(this.UpdateProgress), new object[] { 0, 100, true });
                        this.Invoke(new dis(this.disconnect_Click), new object[] { e, new EventArgs() });
                        break;
                    }
                }
            }
            catch (Exception e)
            {
                if (!isEnd)
                {
                    this.Invoke(new UpdateStatusCallback(this.UpdateStatus), new object[] { e.Message + "\n" });
                    this.Invoke(new UpdateProgressBar(this.UpdateProgress), new object[] { 0, 100, true });
                }
            }
            finally
            {
                if (stream != null)
                    stream.Close();
                if (fileGet != null)
                    fileGet.Close();
                if (client != null)
                    client.Close();
            }
        }

        // Функция приема файла
        private void ProcessReceiving()
        {
            int bytesSize = 0;

            // Получение имя файла
            byte[] ByteFileName = new byte[2048];
            bytesSize = stream.Read(ByteFileName, 0, 2048);
            string FileName = Encoding.UTF8.GetString(ByteFileName, 0, bytesSize);

            // Создание файла
            fileGet = new FileStream(pathLabel.Text + "\\" + FileName, FileMode.Create, FileAccess.Write, FileShare.ReadWrite);

            // Получение размера файла
            byte[] ByteFileSize = new byte[2048];
            bytesSize = stream.Read(ByteFileSize, 0, 2048);
            string temp = Encoding.UTF8.GetString(ByteFileSize, 0, bytesSize);
            long FileSize = Convert.ToInt64(temp);

            this.Invoke(new UpdateStatusCallback(this.UpdateStatus), new object[] { "Получение файла: " + FileName + " (" + FileSize + " байт)\n" });

            // Получение самого файла
            byte[] downBuffer = new byte[2048];
            int totalBytes = 0;

            DateTime timer = DateTime.Now;

            while (stream.CanRead && (totalBytes != FileSize))
            {
                bytesSize = stream.Read(downBuffer, 0, downBuffer.Length);
                fileGet.Write(downBuffer, 0, bytesSize);
                this.Invoke(new UpdateProgressBar(this.UpdateProgress), new object[] { fileGet.Length, FileSize, true });
                totalBytes += bytesSize;

                if (bytesSize == 0)
                {
                    DateTime d = DateTime.Now;

                    if (d.Subtract(timer).TotalSeconds > 5)
                    {
                        throw new Exception("Разрыв соединения");
                    }
                }
                else
                {
                    timer = DateTime.Now;
                }
            }     

            this.Invoke(new UpdateStatusCallback(this.UpdateStatus), new object[] { "Файл " + FileName + " принят.\n" });

            this.Invoke(new UpdateProgressBar(this.UpdateProgress), new object[] { 0, FileSize, true });

            fileGet.Close();
        }
        #endregion

        #region Отправка файла
        private void StartSending()
        {
            try
            {
                file = new FileStream(openFile.FileName, FileMode.Open, FileAccess.Read);

                BinaryReader binFile = new BinaryReader(file);
                FileInfo fInfo = new FileInfo(openFile.FileName);

                // Имя файла
                string FileName = fInfo.Name;
                byte[] ByteFileName = new byte[2048];
                ByteFileName = Encoding.UTF8.GetBytes(FileName.ToCharArray());
                stream.Write(ByteFileName, 0, ByteFileName.Length);

                // Размер файла
                long FileSize = fInfo.Length;
                string FileSizeString = FileSize.ToString();
                byte[] ByteFileSize = new byte[2048];
                ByteFileSize = Encoding.UTF8.GetBytes(FileSizeString.ToCharArray());
                stream.Write(ByteFileSize, 0, FileSizeString.Length);

                this.Invoke(new UpdateStatusCallback(this.UpdateStatus), new object[] { "Sending the file " + FileName + " (" + FileSize + " bytes)\n" });

                Thread.Sleep(1000);

                int bytesSize = 0, totalBytes = 0;
                byte[] downBuffer = new byte[2048];

                // Цикл передачи файла
                while ((bytesSize = file.Read(downBuffer, 0, downBuffer.Length)) > 0)
                {
                    stream.Write(downBuffer, 0, bytesSize);
                    totalBytes += bytesSize;
                    this.Invoke(new UpdateProgressBar(this.UpdateProgress), new object[] { totalBytes, FileSize, false });
                }

                file.Close();

                this.Invoke(new UpdateStatusCallback(this.UpdateStatus), new object[] { "Файл отправлен.\n" });

                this.Invoke(new UpdateProgressBar(this.UpdateProgress), new object[] { 0, FileSize, false });
            }
            catch (SocketException ex)
            {
                this.Invoke(new UpdateStatusCallback(this.UpdateStatus), new object[] { "SocketException: " + ex + "\n" });
                this.Invoke(new UpdateProgressBar(this.UpdateProgress), new object[] { 0, 100, false });
            }
            catch (Exception ex)
            {
                if (!isEnd)
                {
                    this.Invoke(new UpdateStatusCallback(this.UpdateStatus), new object[] { "Exception: " + ex.Message + "\n" });
                    this.Invoke(new UpdateProgressBar(this.UpdateProgress), new object[] { 0, 100, false });
                }
            }
            finally
            {
                if (file != null)
                    file.Close();
            }
        }

        // Send file button
        private void send_Click(object sender, EventArgs e)
        {
            if (client.Connected == false)
            {
                this.Invoke(new UpdateStatusCallback(this.UpdateStatus), new object[] { "Отстутствует подключение к серверу.\n" });
            }
            else
            {
                if (openFile.ShowDialog() == DialogResult.OK)
                {
                    thrSend = new Thread(StartSending);
                    thrSend.Start();
                }
            }
        }
        #endregion

        #region Кнопки
        // Кнопка disconnect
        private void disconnect_Click(object sender, EventArgs e)
        {
            if (stream != null)
                stream.Close();
            if (client != null)
                client.Close();
            if (file != null)
                file.Close();
            if (fileGet != null)
                fileGet.Close();

            connect.Enabled = true;
            disconnect.Enabled = false;
            send.Enabled = false;

            this.Invoke(new UpdateStatusCallback(this.UpdateStatus), new object[] { "Клиент отключен.\n" });
        }

        // Connect to server
        private void connect_Click(object sender, EventArgs e)
        {
            try
            {
                client = new TcpClient();
                client.Connect(textBox1.Text, Convert.ToInt32(textBox2.Text));
                this.Invoke(new UpdateStatusCallback(this.UpdateStatus), new object[] { "Успешное подключение к серверу.\n" });

                send.Enabled = true;
                disconnect.Enabled = true;
                connect.Enabled = false;
                changeButton.Enabled = false;

                stream = client.GetStream();

                thrDownload = new Thread(StartReceiving);
                thrDownload.Start();
            }
            catch (SocketException ex)
            {
                this.Invoke(new UpdateStatusCallback(this.UpdateStatus), new object[] { "SocketException: " + ex + "\n" });
            }
            catch (Exception ex)
            {
                this.Invoke(new UpdateStatusCallback(this.UpdateStatus), new object[] { "Exception: " + ex.Message + "\n" });
            }
        }

        // Изменить путь сохранения файлов
        private void changeButton_Click(object sender, EventArgs e)
        {
            if (folderBrowserDialog1.ShowDialog() == DialogResult.OK)
            {
                pathLabel.Text = folderBrowserDialog1.SelectedPath;
                filePath = folderBrowserDialog1.SelectedPath;

                openButton.Enabled = true;
                connect.Enabled = true;
            }
        }

        // Открыть текущий путь сохранения файлов
        private void openButton_Click(object sender, EventArgs e)
        {
            Process.Start(filePath);
        }
        #endregion

        #region progressBar
        // Progress
        private void UpdateProgress(Int64 BytesRead, Int64 TotalBytes, bool firstPB)
        {
            if (TotalBytes > 0)
            {
                PercentProgress = Convert.ToInt32((BytesRead * 100) / TotalBytes);
                if (firstPB)
                    progressBar1.Value = PercentProgress;
                else
                    progressBar2.Value = PercentProgress;
            }
        }
        #endregion

        #region Закрытие формы
        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            isEnd = true;

            if (thrSend != null && thrSend.IsAlive)
                thrSend.Abort();
            if (thrDownload != null && thrDownload.IsAlive)
                thrDownload.Abort();
            if (client != null)
                client.Close();
            if (stream != null)
                stream = null;
            if (file != null)
                file.Close();
            if (fileGet != null)
                fileGet.Close();                  
        }
        #endregion
    }
}
