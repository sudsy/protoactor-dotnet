using System;
using System.Reactive.Subjects;
using System.Threading;
using System.Threading.Tasks;
using Grpc.Core;
using Proto.Remote;

namespace Proto.Client
{
    public class Client
    {
        
        private IClientStreamWriter<ClientMessageBatch> _requestStream;
        private string _hostname;
        private int _port;

        public Client(string hostname, int port, RemoteConfig config)
        {
            _hostname = hostname;
            _port = port;
            Serialization.RegisterFileDescriptor(ProtosReflection.Descriptor);
            
            ProcessRegistry.Instance.RegisterHostResolver(pid => new ClientProxyProcess(this, pid));
            
            Channel channel = new Channel(hostname, port, config.ChannelCredentials, config.ChannelOptions);
            var client = new ClientRemoting.ClientRemotingClient(channel);
            
            
            var clientStreams = client.ConnectClient();

            _requestStream = clientStreams.RequestStream;

            //Setup listener for incoming stream
            Task.Factory.StartNew(async () =>
            {
                var responseStream = clientStreams.ResponseStream;
                while (await responseStream.MoveNext(CancellationToken.None)
                ) //Need to work out how this might be cancelled
                {
                    var messageBatch = responseStream.Current;
                    foreach (var envelope in messageBatch.Envelopes)
                    {
                        var target = messageBatch.TargetPids[envelope.Target];
                        var message = Serialization.Deserialize(messageBatch.TypeNames[envelope.TypeId], envelope.MessageData, envelope.SerializerId);
                        //todo: Need to convert the headers here
                        var localEnvelope = new Proto.MessageEnvelope(message, envelope.Sender, null);
                        
                        RootContext.Empty.Send(target, localEnvelope);
                    }
                   
                }
            });
            
            
        }

      

        public async Task<PID> SpawnProxyAsync(Props props)
        {
            //Get a local PID 
            var localPID = RootContext.Empty.Spawn(props);
            //Get a remote proxy PID
          
            
            var activator = new PID($"{_hostname}:{_port}", "proxy_activator");

            var createdMessage = await RootContext.Empty.RequestAsync<ProxyPidResponse>(activator, new ProxyPidRequest()
            {
                ClientPID = localPID
            });
            
            
            
            
            return createdMessage.ProxyPID;
        }
        
 

        public void SendMessage(PID target, object envelope, int serializerId)
        {
            //TODO: This really needs to be handled by an actor to make sure we don't write on different threads
            var (message, sender, header) = MessageEnvelope.Unwrap(envelope);
            _requestStream.WriteAsync(getClientMessageBatch(target, message, serializerId, sender, header));
        }
        
        
        static public ClientMessageBatch getClientMessageBatch(PID target, object message, int serializerId, PID sender = null, Proto.MessageHeader headers = null )
        {
            
            var typeName = Serialization.GetTypeName(message, serializerId);
            
            var batch = new ClientMessageBatch();
            
            batch.TypeNames.Add(typeName);
            
            
            if (target != null)
            {
                batch.TargetPids.Add(target); //TODO: We shouldn't really be sending a null target, this is really only fro the actor create message should this be a system message instead?
            }

            var targetId = target != null ? 0 : -1;
            
            batch.Envelopes.Add(new Remote.MessageEnvelope()
            {
                Target = targetId,
                TypeId = 0,
                SerializerId = serializerId,
                MessageData = Serialization.Serialize(message, serializerId),
                Sender = sender
//                MessageHeader = headers
            });
            return batch;
        }
    }
}