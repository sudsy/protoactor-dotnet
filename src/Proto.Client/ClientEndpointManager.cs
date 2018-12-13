using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Grpc.Core;
using Grpc.Core.Utils;
using Proto.Remote;

namespace Proto.Client
{
    public class ClientEndpointManager: ClientRemoting.ClientRemotingBase
    {
        private Dictionary<PID, PID> mappingTable = new Dictionary<PID, PID>();
        public override async Task ConnectClient(IAsyncStreamReader<MessageBatch> requestStream,
            IServerStreamWriter<MessageBatch> responseStream, ServerCallContext context)
        {
            //Read any messages we receive from the client
            await requestStream.ForEachAsync(async batch =>
            {
                Console.WriteLine("Got a message");
//                PID clientProxyActorPID = null;
//                //Check if we already have a proxy actor for this client's PID
//                if (mappingTable.ContainsKey(envelope.Sender))
//                {
//                    clientProxyActorPID = mappingTable[envelope.Sender];
//                }
//                else
//                {
//                    var props =
//                        Props.FromProducer(() => new ClientProxyActor(envelope.Sender, responseStream));
//                    clientProxyActorPID = RootContext.Empty.Spawn(props);
//                    
//                }

               
            });
        }
    }
}