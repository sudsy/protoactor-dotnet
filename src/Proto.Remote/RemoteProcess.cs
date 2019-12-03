using Microsoft.Extensions.Logging;
// -----------------------------------------------------------------------
//   <copyright file="RemoteProcess.cs" company="Asynkron HB">
//       Copyright (C) 2015-2018 Asynkron HB All rights reserved
//   </copyright>
// -----------------------------------------------------------------------

namespace Proto.Remote
{
    public class RemoteProcess : Process
    {
        private static readonly ILogger Logger = Log.CreateLogger(typeof(RemoteProcess).FullName);
        private readonly PID _pid;

        public RemoteProcess(PID pid)
        {
            _pid = pid;
        }

        protected override void SendUserMessage(PID _, object message) => Send(message);

        protected override void SendSystemMessage(PID _, object message) => Send(message);

        private void Send(object msg)
        {
            Logger.LogDebug($"Sending {msg.GetType()} message to remote {_pid}");
            if (msg is Watch w)
            {
                var rw = new RemoteWatch(w.Watcher, _pid);
                EndpointManager.RemoteWatch(rw);
            }
            else if (msg is Unwatch uw)
            {
                var ruw = new RemoteUnwatch(uw.Watcher, _pid);
                EndpointManager.RemoteUnwatch(ruw);
            }
            else
            {
                Remote.SendMessage(_pid, msg,-1);
            }
        }
    }
}