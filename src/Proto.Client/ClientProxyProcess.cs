using Proto.Remote;

namespace Proto.Client
{
    public class ClientProxyProcess : Process
    {
        private readonly PID _pid;
        

        public ClientProxyProcess( PID pid)
        {
            _pid = pid;
        }

        protected override void SendUserMessage(PID _, object message) => Send(message);

        protected override void SendSystemMessage(PID _, object message) => Send(message);

        private void Send(object msg)
        {
           
            Client.SendMessage(_pid, msg, Serialization.DefaultSerializerId);
            
        }
    }
}