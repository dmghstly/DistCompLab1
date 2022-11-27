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
using System.Threading;
using PipesServer;
using Newtonsoft.Json;
using System.Xml.Linq;
using System.Threading.Tasks;

namespace Pipes
{
    public partial class frmMain : Form
    {
        private readonly MessagesDBContext _dbContext;

        private Int32 UserPipeHandle;
        private Int32 MessagePipeHandle;                                                       // дескриптор канала
        private string MessagePipeName = "\\\\.\\pipe\\MessagePipe";    // имя канала, Dns.GetHostName() - метод, возвращающий имя машины, на которой запущено приложение
        private string UserPipeName = "\\\\.\\pipe\\UserPipe";

        private Thread t1;
        private Thread t2; 
        
        private bool _mainContinue = true;                                                  // флаг, указывающий продолжается ли работа с каналом

        // конструктор формы
        public frmMain()
        {
            InitializeComponent();

            _dbContext = new MessagesDBContext();

            // создание именованного канала
            MessagePipeHandle = DIS.Import.CreateNamedPipe(MessagePipeName, 
                DIS.Types.PIPE_ACCESS_DUPLEX, 
                DIS.Types.PIPE_TYPE_BYTE | DIS.Types.PIPE_WAIT, 
                1, 0, 1024, 
                DIS.Types.NMPWAIT_WAIT_FOREVER, 
                (uint)0);

            UserPipeHandle = DIS.Import.CreateNamedPipe(UserPipeName, 
                DIS.Types.PIPE_ACCESS_DUPLEX,
                DIS.Types.PIPE_TYPE_BYTE | DIS.Types.PIPE_WAIT,
                1, 0, 1024,
                DIS.Types.NMPWAIT_WAIT_FOREVER,
                (uint)0);

            // вывод имени канала в заголовок формы, чтобы можно было его использовать для ввода имени в форме клиента, запущенного на другом вычислительном узле
            this.Text += "Server part";
            
            // создание потока, отвечающего за работу с каналом
            t1 = new Thread(ReceiveMessage);
            t1.Start();

            t2 = new Thread(RecieveUserName);
            t2.Start();
        }

        private Guid GetClientId(string name)
        {
            var entry = _dbContext.Users.Where(u => u.UserName == name).FirstOrDefault();

            return entry.Id;
        }

        private void CreateClient(string name)
        {
            if (!_dbContext.Users.Where(u => u.UserName == name).Any())
            {
                var clientId = Guid.NewGuid();

                _dbContext.Users.Add(new Client
                {
                    Id = Guid.NewGuid(),
                    UserName = name
                });
                _dbContext.SaveChanges();
            }
        }

        private void AddMessage(string userName, JsonMessage message)
        {
            _dbContext.ClientMessages.Add(new ClientMessage
            {
                MessageContent = message.Message,
                Time = message.Time,
                UserName = userName
            });

            _dbContext.SaveChanges();
        }

        private string GetAllMessages()
        {
            string result = "";
            var entries = _dbContext.ClientMessages.ToList();

            foreach (var entry in entries)
            {
                result += $"\n {entry.UserName} >> {entry.MessageContent} >> {entry.Time}";
            }

            return result;
        }

        private void SendOldMessages(string name)
        {
            uint BytesWritten = 0;  // количество реально записанных в канал байт

            var result = GetAllMessages();

            byte[] buff = Encoding.Unicode.GetBytes(result);    // выполняем преобразование сообщения (вместе с идентификатором машины) в последовательность байт

            var RetrieveMessagesPipeHandle = DIS.Import.CreateFile("\\\\.\\pipe\\AllMessagesPipe" + name, DIS.Types.EFileAccess.GenericWrite, DIS.Types.EFileShare.Read, 0, DIS.Types.ECreationDisposition.OpenExisting, 0, 0);
            DIS.Import.WriteFile(RetrieveMessagesPipeHandle, buff, Convert.ToUInt32(buff.Length), ref BytesWritten, 0);         // выполняем запись последовательности байт в канал

            DIS.Import.CloseHandle(RetrieveMessagesPipeHandle);
        }

        private List<NewMessage> FormNewMessageList(JsonMessage message)
        {
            List<NewMessage> messages = new List<NewMessage>();
            var users = _dbContext.Users.ToList();

            foreach (var user in users)
            {
                messages.Add(new NewMessage
                {
                    Message = $"\n {message.ClientName} >> {message.Message} >> {message.Time}",
                    Reciever = user.UserName
                });
            }

            return messages;
        }

        private void SendNewMessages(NewMessage newMessage)
        {
            uint BytesWritten = 0;  // количество реально записанных в канал байт

            byte[] buff = Encoding.Unicode.GetBytes(newMessage.Message);    // выполняем преобразование сообщения (вместе с идентификатором машины) в последовательность байт

            var SendNewMessagePipeHandle = DIS.Import.CreateFile("\\\\.\\pipe\\RecieverPipe" + newMessage.Reciever, DIS.Types.EFileAccess.GenericWrite, DIS.Types.EFileShare.Read, 0, DIS.Types.ECreationDisposition.OpenExisting, 0, 0);
            DIS.Import.WriteFile(SendNewMessagePipeHandle, buff, Convert.ToUInt32(buff.Length), ref BytesWritten, 0);         // выполняем запись последовательности байт в канал

            DIS.Import.CloseHandle(SendNewMessagePipeHandle);
        }

        private void RecieveUserName()
        {
            uint realBytesReaded = 0;

            // входим в бесконечный цикл работы с каналом
            while (_mainContinue)
            {
                if (DIS.Import.ConnectNamedPipe(UserPipeHandle, 0))
                {
                    byte[] buff = new byte[1024];                                           // буфер прочитанных из канала байтов
                    DIS.Import.FlushFileBuffers(UserPipeHandle);                                // "принудительная" запись данных, расположенные в буфере операционной системы, в файл именованного канала
                    DIS.Import.ReadFile(UserPipeHandle, buff, 1024, ref realBytesReaded, 0);    // считываем последовательность байтов из канала в буфер buff
                    var msg = Encoding.Unicode.GetString(buff);

                    CreateClient(msg);

                    SendOldMessages(msg);
                    
                    DIS.Import.DisconnectNamedPipe(UserPipeHandle);                             // отключаемся от канала клиента 
                    Thread.Sleep(500);
                }
            }
        }

        private void ReceiveMessage()
        {
            string msg = "";            // прочитанное сообщение
            uint realBytesReaded = 0;   // количество реально прочитанных из канала байтов

            // входим в бесконечный цикл работы с каналом
            while (_mainContinue)
            {
                if (DIS.Import.ConnectNamedPipe(MessagePipeHandle, 0))
                {
                    byte[] buff = new byte[1024];                                           // буфер прочитанных из канала байтов
                    DIS.Import.FlushFileBuffers(MessagePipeHandle);                                // "принудительная" запись данных, расположенные в буфере операционной системы, в файл именованного канала
                    DIS.Import.ReadFile(MessagePipeHandle, buff, 1024, ref realBytesReaded, 0);    // считываем последовательность байтов из канала в буфер buff
                    msg = Encoding.Unicode.GetString(buff);                                 // выполняем преобразование байтов в последовательность символов

                    JsonMessage message = JsonConvert.DeserializeObject<JsonMessage>(msg);

                    rtbMessages.Invoke((MethodInvoker)delegate
                    {
                        // выводим полученное сообщение на форму
                        if (message != null)
                            rtbMessages.Text += $"\n {message.ClientName} >> {message.Message} >> {message.Time}";             
                    });

                    var clientId = GetClientId(message.ClientName);
                    AddMessage(message.ClientName, message);

                    var messages = FormNewMessageList(message);
                    Parallel.ForEach(messages, SendNewMessages);

                    DIS.Import.DisconnectNamedPipe(MessagePipeHandle);                             // отключаемся от канала клиента 
                    Thread.Sleep(500);                                                      // приостанавливаем работу потока перед тем, как приcтупить к обслуживанию очередного клиента
                }
            }
        }

        private void frmMain_FormClosing(object sender, FormClosingEventArgs e)
        {
            _mainContinue = false;      // сообщаем, что работа с каналами завершена

            if (t1 != null)
                t1.Abort();          // завершаем поток

            if (t2 != null)
                t2.Abort();
            
            //if (MessagePipeHandle != -1)
            //    DIS.Import.CloseHandle(MessagePipeHandle);     // закрываем дескриптор канала

            //if (UserPipeHandle != -1)
            //    DIS.Import.CloseHandle(UserPipeHandle);
        }
    }
}