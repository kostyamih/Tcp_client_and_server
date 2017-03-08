using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace TCP_Server_File
{
    public class ClientObject
    {
        protected internal string Id { get; private set; }
        protected internal NetworkStream Stream { get; private set; }
        TcpClient client;
        Form1 form;
        Stream fileSend;

        int count;
        TimeSpan ts;
        long fs;
        private delegate void UpdateStatusProgress(string StatusMessage, ref Label label, ref ProgressBar pb, int a, Form form);
        int countSend;
        TimeSpan tsSend;
        long fsSend;

        #region Конструктор
        public ClientObject(TcpClient tcpClient, Form1 f)
        {
            Id = Guid.NewGuid().ToString();
            client = tcpClient;
            form = f;
            form.AddConnection(this);
            Stream = client.GetStream();

            form.ConsoleMessage("Клиент " + Id + " присоеденен.\n");
        }
        #endregion

        #region Прием файла
        public void Process()
        {
            Stream file = null;
            try
            {       
                while (true)
                {
                    try
                    {       
                        GetFile(ref file);
                    }
                    catch
                    {                 
                        break;
                    }
                    finally
                    {
                        if (file != null)
                            file.Close();
                    }
                }
            }
            catch (Exception e)
            {
                form.ConsoleMessage(e.Message + "\n");
            }
            finally
            {
                form.RemoveConnection(this.Id);
                Close();
                if (file != null)
                    file.Close();
            }
        }

        // Получение файла
        private void GetFile(ref Stream file)
        {
            int bytesSize = 0;

            // Получение имя файла
            byte[] ByteFileName = new byte[2048];
            bytesSize = Stream.Read(ByteFileName, 0, 2048);
            string FileName = Encoding.UTF8.GetString(ByteFileName, 0, bytesSize);

            // Создание файла
            file = new FileStream(form.filePath + "\\" + FileName, FileMode.Create, FileAccess.Write, FileShare.ReadWrite);

            // Получение размера файла
            byte[] ByteFileSize = new byte[2048];
            bytesSize = Stream.Read(ByteFileSize, 0, 2048);
            string temp = Encoding.UTF8.GetString(ByteFileSize, 0, bytesSize);
            long FileSize = Convert.ToInt64(temp);

            form.ConsoleMessage("Получение файла: " + FileName + " (" + FileSize + " байт)\n");  

            // Получение самого файла
            byte[] downBuffer = new byte[2048];
            int totalBytes = 0;

            Stopwatch stopWatch = new Stopwatch();
            count = 0;
            ts = new TimeSpan();
            fs = FileSize;

            Thread formThread = new Thread(() => FormProgress(FileSize, FileName));
            formThread.Start();

            DateTime timer = DateTime.Now;

            while (Stream.CanRead && (totalBytes < FileSize))
            {
                stopWatch.Start();
                bytesSize = Stream.Read(downBuffer, 0, downBuffer.Length);
                count++;
                if (count == 1000)
                {
                    stopWatch.Stop();
                    ts = stopWatch.Elapsed;
                    count = 0;
                    fs -= 2048 * 1000;
                }

                file.Write(downBuffer, 0, bytesSize);
                totalBytes += bytesSize;      
                
                if (bytesSize == 0)
                {
                    DateTime d = DateTime.Now;

                    if (d.Subtract(timer).TotalSeconds > 5)
                    {
                        if (formThread.IsAlive)
                            formThread.Abort();
                        throw new Exception("Разрыв соединения");
                    }
                }  
                else
                {
                    timer = DateTime.Now;
                }
            }

            fs = 0;

            if (totalBytes >= FileSize)
                form.ConsoleMessage("Файл " + FileName + " принят.\n");         
        }
        #endregion

        #region Формы получения/отправки файла
        private void FormProgress(long FileSize, string FileName)
        {
            Form newForm = new Form();
            newForm.Size = new Size(400, 100);
            newForm.FormBorderStyle = FormBorderStyle.FixedDialog;
            newForm.ControlBox = false;

            Label newLabel = new Label();
            newLabel.Location = new Point(0, 0);
            newLabel.Text = "";
            newForm.Controls.Add(newLabel);
            newLabel.Visible = false;

            newForm.Text = "Прием: " + FileName;

            ProgressBar pb = new ProgressBar();
            pb.Location = new Point(10, 30);
            pb.Size = new Size(360,20);
            newForm.Controls.Add(pb);

            Thread formThr = new Thread(() => FormDataProgress(FileSize, ref newLabel, ref newForm, ref pb, ref newForm));
            formThr.Start();
            newForm.ShowDialog();
        }

        private void FormProgress2(long FileSize, string FileName)
        {
            Form newForm = new Form();
            newForm.Size = new Size(400, 100);
            newForm.FormBorderStyle = FormBorderStyle.FixedDialog;
            newForm.ControlBox = false;

            Label newLabel = new Label();
            newLabel.Location = new Point(0, 0);
            newLabel.Text = "";
            newForm.Controls.Add(newLabel);
            newLabel.Visible = false;

            newForm.Text = "Send: " + FileName;

            ProgressBar pb = new ProgressBar();
            pb.Location = new Point(10, 30);
            pb.Size = new Size(360, 20);
            newForm.Controls.Add(pb);

            Thread formThr = new Thread(() => FormDataProgress2(FileSize, ref newLabel, ref newForm, ref pb, ref newForm));
            formThr.Start();
            newForm.ShowDialog();
        }

        private void LabelProgress(string s, ref Label l, ref ProgressBar pb, int a, Form form)
        {
            l.Text = s;
            pb.Value = a;
            if (a == 100 && s == "End")
            {
                form.Hide();
            }
        }

        private void FormDataProgress(long FileSize, ref Label l, ref Form f, ref ProgressBar pb, ref Form form)
        {
            bool flag = false;

            while (fs > 0)
            {
                if (count == 1000 && !flag)
                {
                    if (Convert.ToInt32(ts.TotalMilliseconds) != 0)
                        try
                        {
                            f.Invoke(new UpdateStatusProgress(this.LabelProgress), new object[] { Math.Round((fs * ts.TotalMilliseconds / (2048 * 100 * 100) / 1000)).ToString() + "sec. ", l, pb, Convert.ToInt32(((FileSize - fs) * 100) / FileSize), form });
                        }
                        catch(Exception e)
                        {
                            flag = true;
                        }
                }
            }
            if (!flag)
                f.Invoke(new UpdateStatusProgress(this.LabelProgress), new object[] { "End", l, pb, 100, form });
        }

        private void FormDataProgress2(long FileSize, ref Label l, ref Form f, ref ProgressBar pb, ref Form form)
        {
            bool flag = false;

            while (fsSend > 0)
            {
                if (countSend == 1000 && !flag)
                {
                    if (Convert.ToInt32(tsSend.TotalMilliseconds) != 0)
                        try
                        { 
                            f.Invoke(new UpdateStatusProgress(this.LabelProgress), new object[] { Math.Round((fsSend * tsSend.TotalMilliseconds / (2048 * 100 * 100) / 1000)).ToString() + "sec. ", l, pb, Convert.ToInt32(((FileSize - fsSend) * 100) / FileSize), form });
                        }
                        catch (Exception e)
                        {
                            flag = true;
                        }
                }
            }
            if (!flag)
                f.Invoke(new UpdateStatusProgress(this.LabelProgress), new object[] { "End", l, pb, 100, form });
        }
        #endregion

        #region Отправка файла
        public void SendFile()
        {
            Thread formThread = null;

            try
            {
                fileSend = new FileStream(form.OFD.FileName, FileMode.Open, FileAccess.Read);

                BinaryReader binFile = new BinaryReader(fileSend);
                FileInfo fInfo = new FileInfo(form.OFD.FileName);

                // Имя файла
                string FileName = fInfo.Name;
                byte[] ByteFileName = new byte[2048];
                ByteFileName = System.Text.Encoding.UTF8.GetBytes(FileName.ToCharArray());
                Stream.Write(ByteFileName, 0, ByteFileName.Length);

                // Размер файла
                long FileSize = fInfo.Length;
                string FileSizeString = FileSize.ToString();
                byte[] ByteFileSize = new byte[2048];
                ByteFileSize = Encoding.UTF8.GetBytes(FileSizeString.ToCharArray());
                Stream.Write(ByteFileSize, 0, FileSizeString.Length);

                form.ConsoleMessage("Sending the file " + FileName + " (" + FileSize + " bytes)\n");

                Thread.Sleep(1000);

                formThread = new Thread(() => FormProgress2(FileSize, FileName));
                formThread.Start();

                int bs = 0;
                byte[] downBuf = new byte[2048];

                Stopwatch stopWatch = new Stopwatch();
                countSend = 0;
                tsSend = new TimeSpan();
                fsSend = FileSize;
                int totalBytes = 0;

                // Цикл передачи файла
                while ((bs = fileSend.Read(downBuf, 0, downBuf.Length)) > 0)
                {
                    stopWatch.Start();
                    Stream.Write(downBuf, 0, bs);
                    countSend++;
                    if (countSend == 1000)
                    {
                        stopWatch.Stop();
                        tsSend = stopWatch.Elapsed;
                        countSend = 0;
                        fsSend -= 2048 * 1000;
                    }

                    totalBytes += bs;
                }

                form.ConsoleMessage("Файл отправлен.\n");
                fsSend = 0;

                fileSend.Close();
            }
            catch (Exception ex)
            {
                form.ConsoleMessage(ex + "\n");
                if (formThread.IsAlive)
                    formThread.Abort();
            }
            finally
            {
                if (fileSend != null)
                    fileSend.Close();               
            }
        }
        #endregion

        #region Закрытие подключения
        protected internal void Close()
        {
            if (Stream != null)
                Stream.Close();
            if (client != null)
                client.Close();

            form.ConsoleMessage("Клиент " + Id + " отключен.\n");
        }
        #endregion
    }
}
