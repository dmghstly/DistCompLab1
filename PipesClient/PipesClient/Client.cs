using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Net;
using System.Net.Sockets;
using System.IO;
using PipesClient;
using Newtonsoft.Json;
using System.Threading;

namespace Pipes
{
    public partial class frmMain : Form
    {
        private Int32 MessagePipeHandle;   // дескриптор канала
        private readonly string MessagePipeName = "\\\\.\\pipe\\MessagePipe";
        private Int32 UserPipeHandle;   // дескриптор канала
        private readonly string UserPipeName = "\\\\.\\pipe\\UserPipe";

        private Int32 RecieverPipeHandel;   // дескриптор канала
        private string RecieverPipeName = "";
        private Int32 AllMessagesPipeHandle;   // дескриптор канала
        private string AllMessagesPipeName = "";

        private bool _retrieveMessagesContinue = true;
        private bool _messageRecieverContinue = true;

        private Thread t1;
        private Thread t2;

        // конструктор формы
        public frmMain()
        {
            InitializeComponent();
            this.Text += "Client on computer: " + Dns.GetHostName();   // выводим имя текущей машины в заголовок формы
            userName.Text = "Some Name";

            rtbClient.Enabled = false;
            tbMessage.Enabled = false;
            btnSend.Enabled = false;
        }

        public void InitializePipes(string name)
        {
            RecieverPipeName = "\\\\.\\pipe\\RecieverPipe" + name;
            AllMessagesPipeName = "\\\\.\\pipe\\AllMessagesPipe" + name;

            RecieverPipeHandel = DIS.Import.CreateNamedPipe(RecieverPipeName,
                DIS.Types.PIPE_ACCESS_DUPLEX,
                DIS.Types.PIPE_TYPE_BYTE | DIS.Types.PIPE_WAIT,
                1, 0, 1024,
                DIS.Types.NMPWAIT_WAIT_FOREVER,
                (uint)0);

            AllMessagesPipeHandle = DIS.Import.CreateNamedPipe(AllMessagesPipeName,
                DIS.Types.PIPE_ACCESS_DUPLEX,
                DIS.Types.PIPE_TYPE_BYTE | DIS.Types.PIPE_WAIT,
                1, 0, 1024,
                DIS.Types.NMPWAIT_WAIT_FOREVER,
                (uint)0);
        }

        private void InitializeUserName(string name)
        {
            userName.Enabled = false;
            accept_Btn.Enabled = false;

            uint BytesWritten = 0;  // количество реально записанных в канал байт

            byte[] buff = Encoding.Unicode.GetBytes(name);    // выполняем преобразование сообщения (вместе с идентификатором машины) в последовательность байт

            UserPipeHandle = DIS.Import.CreateFile(UserPipeName, DIS.Types.EFileAccess.GenericWrite, DIS.Types.EFileShare.Read, 0, DIS.Types.ECreationDisposition.OpenExisting, 0, 0);
            DIS.Import.WriteFile(UserPipeHandle, buff, Convert.ToUInt32(buff.Length), ref BytesWritten, 0);         // выполняем запись последовательности байт в канал

            DIS.Import.CloseHandle(UserPipeHandle);

            InitializePipes(name);

            rtbClient.Enabled = true;
            tbMessage.Enabled = true;
            btnSend.Enabled = true;

            t1 = new Thread(RetrieveAllMessages);
            t1.Start();

            t2 = new Thread(RecieveMessage);
            t2.Start();
        }

        private void RecieveMessage()
        {
            uint realBytesReaded = 0;

            // входим в бесконечный цикл работы с каналом
            while (_messageRecieverContinue)
            {
                if (DIS.Import.ConnectNamedPipe(RecieverPipeHandel, 0))
                {
                    byte[] buff = new byte[1024];                                           // буфер прочитанных из канала байтов
                    DIS.Import.FlushFileBuffers(RecieverPipeHandel);                                // "принудительная" запись данных, расположенные в буфере операционной системы, в файл именованного канала
                    DIS.Import.ReadFile(RecieverPipeHandel, buff, 1024, ref realBytesReaded, 0);    // считываем последовательность байтов из канала в буфер buff
                    var msg = Encoding.Unicode.GetString(buff);

                    rtbClient.Invoke((MethodInvoker)delegate
                    {
                        // выводим полученное сообщение на форму
                        if (msg != "")
                            rtbClient.Text += msg;
                    });

                    DIS.Import.DisconnectNamedPipe(RecieverPipeHandel);                             // отключаемся от канала клиента 
                }
            }
        }

        private void RetrieveAllMessages()
        {
            uint realBytesReaded = 0;

            // входим в бесконечный цикл работы с каналом
            while (_retrieveMessagesContinue)
            {
                if (DIS.Import.ConnectNamedPipe(AllMessagesPipeHandle, 0))
                {
                    byte[] buff = new byte[1024];                                           // буфер прочитанных из канала байтов
                    DIS.Import.FlushFileBuffers(AllMessagesPipeHandle);                                // "принудительная" запись данных, расположенные в буфере операционной системы, в файл именованного канала
                    DIS.Import.ReadFile(AllMessagesPipeHandle, buff, 1024, ref realBytesReaded, 0);    // считываем последовательность байтов из канала в буфер buff
                    var msg = Encoding.Unicode.GetString(buff);

                    rtbClient.Invoke((MethodInvoker)delegate
                    {
                        // выводим полученное сообщение на форму
                        if (msg != "")
                            rtbClient.Text += msg;
                    });

                    DIS.Import.DisconnectNamedPipe(AllMessagesPipeHandle);                             // отключаемся от канала клиента 
                    
                    _retrieveMessagesContinue = false;
                }
            }
        }

        private void btnSend_Click(object sender, EventArgs e)
        {
            uint BytesWritten = 0;  // количество реально записанных в канал байт
            var time = DateTime.Now.ToString("HH:mm:ss");

            JsonMessage message = new JsonMessage
            {
                ClientName = userName.Text,
                Message = tbMessage.Text,
                Time = time
            };

            var jsonData = JsonConvert.SerializeObject(message);

            byte[] buff = Encoding.Unicode.GetBytes(jsonData);    // выполняем преобразование сообщения (вместе с идентификатором машины) в последовательность байт

            MessagePipeHandle = DIS.Import.CreateFile(MessagePipeName, DIS.Types.EFileAccess.GenericWrite, DIS.Types.EFileShare.Read, 0, DIS.Types.ECreationDisposition.OpenExisting, 0, 0);
            DIS.Import.WriteFile(MessagePipeHandle, buff, Convert.ToUInt32(buff.Length), ref BytesWritten, 0);         // выполняем запись последовательности байт в канал

            DIS.Import.CloseHandle(MessagePipeHandle);                                                                 // закрываем дескриптор канала
        }

        private void accept_Btn_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(userName.Text))
            {
                string user = "User:" + Guid.NewGuid().ToString();
                userName.Text = user;
                InitializeUserName(user);
            }
            else
            {
                InitializeUserName(userName.Text);
            }
        }

        private void frmMain_FormClosing(object sender, FormClosingEventArgs e)
        {
            _messageRecieverContinue = false;

            if (t1 != null)
                t1.Abort();          // завершаем поток

            if (t2 != null)
                t2.Abort();


            //if (RecieverPipeHandel != -1)
            //    DIS.Import.CloseHandle(RecieverPipeHandel);     // закрываем дескриптор канала

            //if (AllMessagesPipeHandle != -1)
            //    DIS.Import.CloseHandle(AllMessagesPipeHandle);
        }
    }
}
