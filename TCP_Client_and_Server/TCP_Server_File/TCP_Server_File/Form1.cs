using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Windows.Forms;

namespace TCP_Server_File
{
    public partial class Form1 : Form
    {
        protected internal string filePath;

        static Thread listenThread; 
        static TcpListener tcpListener; 

        List<ClientObject> clients = new List<ClientObject>();

        public RichTextBox console;
        public OpenFileDialog OFD;

        private delegate void UpdateStatusCallback(string StatusMessage);
        private delegate void UpdateButtons(bool start, bool stop, bool change, bool open, bool s);

        private bool isEnd = false;
        private object locker = 0;

        private List<Thread> clientsThreads = new List<Thread>();

        #region Конструктор
        public Form1()
        {
            InitializeComponent();

            label3.Text = Directory.GetCurrentDirectory();
            filePath = label3.Text;

            stopButton.Enabled = false;
            send.Enabled = false;

            console = richTextBox1;
            OFD = openFileDialog1;
        }
        #endregion

        #region Метод изменения доступа кнопок (ChangeButtons)
        private void ChangeButtons(bool start, bool stop, bool change, bool open, bool s)
        {
            startButton.Enabled = start;
            stopButton.Enabled = stop;
            changeButton.Enabled = change;
            openButton.Enabled = open;
            send.Enabled = s;
        }
        #endregion

        #region Для класса ClientObject вызов инвока (ConsoleMessage)
        protected internal void ConsoleMessage(string s)
        {
            if (!isEnd)
                this.Invoke(new UpdateStatusCallback(this.UpdateStatus), new object[] { s });
        }
        #endregion

        #region Главный цикл сервера, ожидание подключений (Listen)
        private void Listen()
        {
            try
            {
                IPAddress localAddr = IPAddress.Parse(textBox2.Text);
                tcpListener = new TcpListener(localAddr, Convert.ToInt32(textBox1.Text));
                tcpListener.Start();
                this.Invoke(new UpdateStatusCallback(this.UpdateStatus), new object[] { "Сервер запущен. Ожидание подключений...\n" });
               
                int i = 0;

                while (true)
                {
                    TcpClient tcpClient = tcpListener.AcceptTcpClient();

                    ClientObject clientObject = new ClientObject(tcpClient, this);
                    clientsThreads.Add(new Thread(new ThreadStart(clientObject.Process)));
                    clientsThreads[i].Start();
                    i++;
                }
            }
            catch (Exception ex)
            {
                if (!isEnd)
                {
                    this.Invoke(new UpdateStatusCallback(this.UpdateStatus), new object[] { ex.Message + "\n" });
                    Disconnect();

                    this.Invoke(new UpdateButtons(this.ChangeButtons), new object[] { true, false, true, openButton.Enabled, send.Enabled });
                }
            }
            finally
            {
                Disconnect();
            }
        }
        #endregion

        #region Для класса ClientObject, для добавления в List clients (AddConnection)
        protected internal void AddConnection(ClientObject clientObject)
        {
            clients.Add(clientObject);
            this.Invoke(new UpdateStatusCallback(this.ComboBoxAdd), new object[] { clientObject.Id });

            this.Invoke(new UpdateButtons(this.ChangeButtons), new object[] { startButton.Enabled, stopButton.Enabled, changeButton.Enabled, openButton.Enabled, true });
        }
        #endregion

        #region Добавление в ComboBox (ComboBoxAdd)
        private void ComboBoxAdd(string id)
        {
            comboBox1.Items.Add(id);
        }
        #endregion

        #region Удаление из ComboBox (ComboBoxRemove)
        private void ComboBoxRemove(string id)
        {
            comboBox1.Items.Remove(id);
        }
        #endregion

        #region Для класса ClientObject, удаление клиента (RemoveConnection)
        protected internal void RemoveConnection(string id)
        {
            // получаем по id закрытое подключение
            ClientObject client = clients.FirstOrDefault(c => c.Id == id);
            // и удаляем его из списка подключений
            if (client != null)
                clients.Remove(client);
            // и удаляем из comboBox
            if (client != null)
                this.Invoke(new UpdateStatusCallback(this.ComboBoxRemove), new object[] { client.Id });

            if (clients.Count == 0 && !isEnd)
            {
                this.Invoke(new UpdateButtons(this.ChangeButtons), new object[] { startButton.Enabled, stopButton.Enabled, false, openButton.Enabled, send.Enabled });
                clientsThreads.Clear();
            }
        }
        #endregion

        #region Вывод в консоль (UpdateStatus)
        private void UpdateStatus(string StatusMessage)
        {
            richTextBox1.Text += StatusMessage;
        }
        #endregion

        #region Отключение сервера (Disconnect)
        protected internal void Disconnect()
        {
            lock (locker)
            {
                if (tcpListener != null)
                    tcpListener.Stop();

                for (int i = 0; i < clients.Count; i++)
                {
                    clients[i].Close();
                }

                foreach (var c in clients)
                {
                    this.Invoke(new UpdateStatusCallback(this.ComboBoxRemove), new object[] { c.Id });
                }

                foreach (var ct in clientsThreads)
                {
                    if (ct.IsAlive && ct != null)
                        ct.Abort();
                }

                clients.Clear();
                clientsThreads.Clear();
            }
        }
        #endregion

        #region Кнопка Старт сервера
        private void button1_Click(object sender, EventArgs e)
        {
            try
            {
                listenThread = new Thread(Listen);
                listenThread.Start(); 
            }
            catch (Exception ex)
            {
                Disconnect();
                this.Invoke(new UpdateStatusCallback(this.UpdateStatus), new object[] { "Exception: " + ex.Message + "\n" });
            }

            this.Invoke(new UpdateButtons(this.ChangeButtons), new object[] { false, true, false, openButton.Enabled, send.Enabled });
        }
        #endregion

        #region Кнопка Стоп сервера
        private void button2_Click(object sender, EventArgs e)
        {
            Disconnect();

            this.Invoke(new UpdateStatusCallback(this.UpdateStatus), new object[] { "Сервер остановлен.\n" });
            this.Invoke(new UpdateButtons(this.ChangeButtons), new object[] { true, false, true, openButton.Enabled, send.Enabled });
        }
        #endregion

        #region Кнопка Изменить путь сохранения файлов
        private void button3_Click(object sender, EventArgs e)
        {
            if (folderBrowserDialog1.ShowDialog() == DialogResult.OK)
            {
                label3.Text = folderBrowserDialog1.SelectedPath;
                filePath = folderBrowserDialog1.SelectedPath;

                this.Invoke(new UpdateButtons(this.ChangeButtons), new object[] { true, stopButton.Enabled, changeButton.Enabled, true, send.Enabled });
            }
        }
        #endregion

        #region Кнопка Открыть папку сохранения файлов
        private void button4_Click(object sender, EventArgs e)
        {
            Process.Start(filePath);
        }
        #endregion

        #region Кнопка Отправки файла
        private void send_Click(object sender, EventArgs e)
        {
            if (comboBox1.SelectedItem != null)
            {
                string tempId = (string)comboBox1.SelectedItem;
                ClientObject client = clients.FirstOrDefault(c => c.Id == tempId);
                if (client != null)
                {
                    if (openFileDialog1.ShowDialog() == DialogResult.OK)
                    {
                        Thread sendThread = new Thread(new ThreadStart(client.SendFile));
                        sendThread.Start();
                    }
                }
            }
            else
                this.Invoke(new UpdateStatusCallback(this.UpdateStatus), new object[] { "Выберите клиента для отправки файла.\n" });
        }
        #endregion

        #region Закрытие формы
        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            isEnd = true;
            Disconnect();

            if (listenThread != null && listenThread.IsAlive)
                listenThread.Abort();

            if (tcpListener != null)
                tcpListener.Stop();
        }
        #endregion
    }
}
