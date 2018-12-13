using System;

namespace Proto.Client
{
    public class Client
    {
        public void Start()
        {
            ProcessRegistry.Instance.Address = "client:" + Guid.NewGuid().ToString();
        }

        public static void SendMessage(PID remoteActor, MessageEnvelope envelope, int i)
        {
            throw new NotImplementedException();
        }
    }
}