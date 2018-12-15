using System;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Reactive.Threading.Tasks;
using System.Threading;
using System.Threading.Tasks;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Proto.Remote;

namespace Proto.Client
{
    public class ClientContext : ISpawnContext, ISenderContext
    {
        private IClientStreamWriter<ClientMessageBatch> _requestStream;
        private Subject<Object> _responseStreamSubject;

        public ClientContext(string hostname, int port, RemoteConfig config)
        {
         
            Serialization.RegisterFileDescriptor(ProtosReflection.Descriptor);
            Channel channel = new Channel(hostname, port, config.ChannelCredentials, config.ChannelOptions);
            var client = new ClientRemoting.ClientRemotingClient(channel);
            var clientStreams = client.ConnectClient();

            _requestStream = clientStreams.RequestStream;

            _responseStreamSubject = new Subject<Object>();

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
                        
                        _responseStreamSubject.OnNext(message); //Perhaps there's a better way to do this than rx and the listener probably a local actor
                        
                        RootContext.Empty.Send(target, localEnvelope);
                    }
                   
                }
            });
            
            

        }
        
        public PID Spawn(Props props)
        {
            //Get a local PID 
            var localPID = RootContext.Empty.Spawn(props);
            //Get a remote proxy PID
            Send(null, new CreateClientProxyActor()
            {
                ClientPID = localPID
            });

            //Wait for a response
            var createdMessage =  (ClientProxyActorCreated)_responseStreamSubject
                .Where((responseMessage) => responseMessage is ClientProxyActorCreated created && created.ClientPID.Equals(localPID))
                .Take(1)
                .Timeout(TimeSpan.FromSeconds(30))
                .Wait();

            //Map messages
            
            
            //Make sure we shut down the listener if either the remote or local actor shuts down
            
            
            
            return createdMessage.ProxyPID;
        }

        public PID SpawnNamed(Props props, string name)
        {
            throw new NotImplementedException();
        }

        public PID SpawnPrefix(Props props, string prefix)
        {
            throw new NotImplementedException();
        }

        public void SendEnvelope(PID target, MessageEnvelope envelope, int serializerID)
        {
            var (message, sender, header) = MessageEnvelope.Unwrap(envelope);
            _requestStream.WriteAsync(getClientMessageBatch(target, message, serializerID, sender, header));
        }
        
        public void Send(PID target, object message)
        {
           
            
            //Don't know how to get the sender ID from here but doesn't matter right now

            //TODO: This really needs to be handled by an actor to make sure we don't write on different threads
            _requestStream.WriteAsync(getClientMessageBatch(target, message, Serialization.DefaultSerializerId));
        }

        
        
        public void Request(PID target, object message)
        {
            throw new NotImplementedException();
        }

        public Task<T> RequestAsync<T>(PID target, object message, TimeSpan timeout)
        {
            throw new NotImplementedException();
        }

        public Task<T> RequestAsync<T>(PID target, object message, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public Task<T> RequestAsync<T>(PID target, object message)
        {
            throw new NotImplementedException();
        }

        public MessageHeader Headers { get; }
        public object Message { get; }
        
        
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