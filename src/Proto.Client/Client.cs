using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel.Design;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Grpc.Core;
using Microsoft.Extensions.Logging;
using Proto.Remote;

namespace Proto.Client
{
    public class Client : IDisposable
    {
        private static readonly ILogger Logger = Log.CreateLogger<Client>();
        private static readonly List<Client> _activeClients = new List<Client>();
        private static readonly List<string> _allAddresses = new List<string>();
        private static readonly string _clientId = Guid.NewGuid().ToString();
       
        private static Channel _channel;
        private static ClientRemoting.ClientRemotingClient _client;
        private static AsyncDuplexStreamingCall<ClientMessageBatch, MessageBatch> _clientStreams;
        private static PID _endpointWriter;
        private static string _clientHostAddress;
        private static CancellationTokenSource _cancelListeningToken;
        
        private readonly TaskCompletionSource<string> _receivedClientAddressTCS;
        
        
        static Client()
        {
            Serialization.RegisterFileDescriptor(ProtosReflection.Descriptor);
        }
        
        
        public Client(string hostname, int port, RemoteConfig config, int connectionTimeoutMs = 10000)
        {
            _cancelListeningToken = new CancellationTokenSource();
            var connectionCancellationToken = new CancellationTokenSource(connectionTimeoutMs);
            
            if (_channel == null || _channel.State != ChannelState.Ready)
            {
                //THis hangs around even when there are no client rpcs
                _channel = new Channel(hostname, port, config.ChannelCredentials, config.ChannelOptions);
                _client = new ClientRemoting.ClientRemotingClient(_channel);
               
            }
            
            if (_activeClients.Count <= 0)
            {
                //This gets disposed when the last client is finished
                var connectionHeaders = new Metadata() {{"clientId", _clientId}};
                
                _clientStreams = _client.ConnectClient(connectionHeaders, null, connectionCancellationToken.Token);
               
                _receivedClientAddressTCS = new TaskCompletionSource<string>();
                connectionCancellationToken.Token.Register(() =>
                {
                    _receivedClientAddressTCS.TrySetCanceled();
                });
                
                
               

                _endpointWriter =
                    RootContext.Empty.Spawn(Props.FromProducer(() =>
                        new ClientEndpointWriter(_clientStreams.RequestStream)));
            
                ProcessRegistry.Instance.RegisterHostResolver(pid => new ClientHostProcess( pid));


                var listenerTask = Task.Factory.StartNew(IncomingStreamListener);
            
                //We need to wait until the clienthostaddress has been set
                //Use a cancellation token to time out on this if it doesn't return in time
                _receivedClientAddressTCS.Task.Wait();
            
               
               
            }
            
            // No need to wait for cancellation anymore 
            connectionCancellationToken.Dispose();

            //Count instances accessing this rpc so we can clean up at the end
            _activeClients.Add(this);
           

          
        }

        private async Task IncomingStreamListener()
        {
            try
            {
                var responseStream = _clientStreams.ResponseStream;
                while (await responseStream.MoveNext(_cancelListeningToken.Token)
                ) //Need to work out how this might be cancelled
                {
                    Logger.LogDebug("Received Message Batch");
                    var messageBatch = responseStream.Current;
                    foreach (var envelope in messageBatch.Envelopes)
                    {
                        var target = new PID(ProcessRegistry.Instance.Address,
                            messageBatch.TargetNames[envelope.Target]);

                        var message = Serialization.Deserialize(messageBatch.TypeNames[envelope.TypeId],
                            envelope.MessageData, envelope.SerializerId);

                        if (message is ClientConnectionStarted)
                        {
                            _clientHostAddress = envelope.Sender.Address;
                            _allAddresses.Add(_clientHostAddress);
                            ProcessRegistry.Instance.Address =
                                "client://" + envelope.Sender.Address + "/" + envelope.Sender.Id;
                            _receivedClientAddressTCS.SetResult(_clientHostAddress);
                            continue;
                        }


                        Logger.LogDebug(
                            $"Opened Envelope from {envelope.Sender} for {target} containing message {message}");
                        //todo: Need to convert the headers here
                        var localEnvelope = new Proto.MessageEnvelope(message, envelope.Sender, null);

                        RootContext.Empty.Send(target, localEnvelope);
                    }

                }
            }
            catch (Exception ex)
            {
                if (ex is RpcException rpcEx)
                {
                    if (rpcEx.Status.Equals(Status.DefaultCancelled) || _activeClients.Count <= 0)
                    {
                        return;
                    }
                }
                if (ex is InvalidOperationException)
                {
                    if (_activeClients.Count <= 0)
                    {
                        //Do nothing, this is an expected exception when we cancel the connection
                        
                        return;
                    }
                }

                Logger.LogCritical(ex, "Exception Thrown from inside stream listener task");
                throw ex;
            }

        }
       
    
 


        public Task<ActorPidResponse> SpawnNamedAsync(string address, string name, string kind, TimeSpan timeout)
        {
            return Remote.Remote.SpawnNamedAsync(address, name, kind, timeout);
        }

        public Task<ActorPidResponse> SpawnAsync(string address, string kind, TimeSpan timeout)
        {
            return Remote.Remote.SpawnAsync(address, kind, timeout);
        }

        public Task<string> GetClientHostAddress()
        {

            return Task.FromResult(_clientHostAddress);

        }

        public async Task<ActorPidResponse> SpawnOnClientHostAsync(string name, string kind, TimeSpan timeout)
        {
            var hostAddress = await GetClientHostAddress();
            return await SpawnNamedAsync(hostAddress, name, kind, timeout);

        }

        public async Task<ActorPidResponse> SpawnOnClientHostAsync(string kind, TimeSpan timeout)
        {
            var hostAddress = await GetClientHostAddress();
            return await SpawnAsync(hostAddress, kind, timeout);
        }
        
     
        public void Dispose()
        {

            //Keep track of clients accessing so we can clean up at the end
            _activeClients.Remove(this);
            
            if (_activeClients.Count <= 0)
            {
                _cancelListeningToken.Cancel();
                _endpointWriter.Stop();
                _clientStreams?.Dispose();
            }
            
            
        }
        
        
        public static void SendMessage(PID target, object envelope, int serializerId)
        {
           
            var (message, sender, header) = MessageEnvelope.Unwrap(envelope);
            
            if (_activeClients.Count <= 0)
            {
               
                    
                    Logger.LogWarning($"Message {message} for {target} could not be sent locally to {ProcessRegistry.Instance.Address} or delivered remotely - no active endpoints available should send to DeadLetter");
                    return;
               
               
            }
            
            
            var env = new RemoteDeliver(header, message, target, sender, serializerId);
            

            RootContext.Empty.Send(_endpointWriter, env);

        }
    
//        public static async Task Disconnect()
//        {
//            if (_channel == null)
//            {
//                return;
//            }
//            await _channel.ShutdownAsync();
//        }            

//    public static async Task Connect(string hostname, int port, RemoteConfig config, int connectionTimeoutMs = 10000)
//    {
//        /*if (_cancelListener != null && _cancelListener.IsCancellationRequested == false)
//        {
//            //We are already connected
//            Logger.LogDebug("Already Connected");
//            return;
//        }*/
//        if (_channel != null && _channel.State == ChannelState.Ready && _cancelListener != null)
//        {
//            if (!_cancelListener.IsCancellationRequested)
//            {
//                Logger.LogDebug("Already Connected");
//                return;
//            }
//
//        }
//
//
//        _cancelListener = new CancellationTokenSource();
//
//        _hostname = hostname;
//        _port = port;
//
//        var tcs = new TaskCompletionSource<bool>();
//
//
//        Logger.LogDebug("Connecting to client host");
//
//        ProcessRegistry.Instance.RegisterHostResolver(pid => new ClientHostProcess(pid));
//
//
//
//
//
//
//
//        _clientStreams = client.ConnectClient();
//
//
//
//
//
//
//        Logger.LogDebug("Connected to Client Host");
//
//        _endpointWriter =
//            RootContext.Empty.Spawn(Props.FromProducer(() =>
//                new ClientEndpointWriter(_clientStreams.RequestStream)));
//
//
//
//        //Setup listener for incoming stream
//        var streamListenerTask = Task.Factory.StartNew(async () =>
//            {
//               
//
//
//        await tcs.Task;
//
//
//    }

   





   
    }
}