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
        private static readonly ILogger _logger = Log.CreateLogger<Client>();
        private static PID _clientEndpointManager;
        private static Tuple<string, int> _endpointConfig;
        
        
        
        private PID _clientHostPID;
        private bool _disposed;
        private static bool _connectionFailed;
        
        


        static Client()
        {
            Serialization.RegisterFileDescriptor(ProtosReflection.Descriptor);
            
        }

        public static async Task<Client> CreateAsync(string hostname, int port, RemoteConfig config, int connectionTimeoutMs = 10000)
        {
            _logger.LogDebug("CreateAsync Called");
            var endpointConfig = Tuple.Create(hostname, port);
            if (_endpointConfig != null & !endpointConfig.Equals(_endpointConfig) & _clientEndpointManager != null)
            {
                throw new InvalidOperationException("Can't connect to multiple client hosts");
            }

         
            if (_clientEndpointManager == null)
             {
                
                 _logger.LogDebug("No endpoint manager available - creating new one");
     
                 //Exponential Backoff for client connection
                 var backoffStrategy =
                     new ExponentialBackoffWithAction(() =>
                     {
                         
                         _clientEndpointManager = null;
                         _connectionFailed = true;
                     }, TimeSpan.FromMilliseconds(250), 10, TimeSpan.FromSeconds(30));
                 _clientEndpointManager =
                     RootContext.Empty.SpawnPrefix(Props.FromProducer(() => new ClientEndpointManager(hostname, port, config, connectionTimeoutMs)).WithChildSupervisorStrategy(backoffStrategy), "clientEndpointManager");
 
                 
             }
            

              
            try
            {
                await RootContext.Empty.RequestAsync<int>(_clientEndpointManager, new AcquireClientEndpointReference(),
                    TimeSpan.FromMilliseconds(connectionTimeoutMs)).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogWarning("Failed to acquire endpoint reference prior to timeout resetting endpoint manager");
                _clientEndpointManager?.Stop();
                
                _clientEndpointManager = null;
                
                _connectionFailed = true;
                
                throw;
            }
            
            _connectionFailed = false;
                    
            _endpointConfig = endpointConfig;
            
            return new Client();    
            
            
            
            
        }

        private Client()
        {
            
        }

      
 
        public Task<ActorPidResponse> SpawnNamedAsync(string address, string name, string kind, TimeSpan timeout)
        {
            checkIfDisposed();
            return Remote.Remote.SpawnNamedAsync(address, name, kind, timeout);
        }
        
     
        public Task<ActorPidResponse> SpawnAsync(string address, string kind, TimeSpan timeout)
        {
            checkIfDisposed();
            return Remote.Remote.SpawnAsync(address, kind, timeout);
        }

        public async Task<PID> GetClientHostPID(TimeSpan timeout)
        {
            checkIfDisposed();
            if (_clientHostPID != null)
            {
                return _clientHostPID;
            }
            _clientHostPID = await RootContext.Empty.RequestAsync<PID>(_clientEndpointManager, "getclienthostpid", timeout);
            return _clientHostPID;


        }

       

        public async Task<ActorPidResponse> SpawnOnClientHostAsync(string name, string kind, TimeSpan timeout)
        {
            checkIfDisposed();
            var hostPID = await GetClientHostPID(timeout);
            return await SpawnNamedAsync(hostPID.Address, name, kind, timeout);

        }

        public async Task<ActorPidResponse> SpawnOnClientHostAsync(string kind, TimeSpan timeout)
        {
            checkIfDisposed();
            var hostPID = await GetClientHostPID(timeout);
            return await SpawnAsync(hostPID.Address, kind, timeout);
        }
        
     
        public void Dispose()
        {
            if (!_disposed)
            {
                RootContext.Empty.Send(_clientEndpointManager, new ReleaseClientEndpointReference());
                _disposed = true;
            }
            
            
        }
        
        private void checkIfDisposed()
        {
       
            if (_disposed) throw new InvalidOperationException("Client is disposed");
        }
        
        
        public static void SendMessage(PID target, object envelope, int serializerId)
        {

            if (_connectionFailed)
            {
                _logger.LogWarning("Did not send message Connection to client host failed after several retries");
                return;
            }
            
            var (message, sender, header) = MessageEnvelope.Unwrap(envelope);
            
            
            var env = new RemoteDeliver(header, message, target, sender, serializerId);
            

            RootContext.Empty.Send(_clientEndpointManager, env);

        }
    





   
    }

    public class ExponentialBackoffWithAction : ISupervisorStrategy
    {
        private static readonly ILogger Logger = Log.CreateLogger<ExponentialBackoffWithAction>();
        private Action _actionOnRestartFail;
        private TimeSpan _initialBackoff;
        private DateTime _firstFail = DateTime.MinValue;
        private readonly Random _random = new Random();
        private int _maxNrOfRetries;
        private TimeSpan? _withinTimeSpan;

        public ExponentialBackoffWithAction(Action actionOnRestartFail,  TimeSpan initialBackoff, int maxNrOfRetries, TimeSpan? withinTimeSpan)
        {
            _actionOnRestartFail = actionOnRestartFail;
            _initialBackoff = initialBackoff;
            _maxNrOfRetries = maxNrOfRetries;
            _withinTimeSpan = withinTimeSpan;

        }

        public new void HandleFailure(ISupervisor supervisor, PID child, RestartStatistics rs, Exception reason,
            object message)
        {
            if (ShouldStop(rs))
            {
                Logger.LogWarning($"Stopping {child.ToShortString()} after retries expired Reason { reason}");
                _actionOnRestartFail();
                supervisor.StopChildren(supervisor.Children.ToArray());
            }
            else
            {
                var backoff = rs.FailureCount * ToNanoseconds(_initialBackoff);
                var noise = _random.Next(500);
                var duration = TimeSpan.FromMilliseconds(ToMilliseconds(backoff + noise));
                Task.Delay(duration).ContinueWith(t =>
                {
                    Logger.LogWarning($"Restarting {child.ToShortString()} after {duration} Reason {reason}");
                    supervisor.RestartChildren(reason, child);
                });
            }
        }
        
        private long ToNanoseconds(TimeSpan timeSpan)
        {
            return Convert.ToInt64(timeSpan.TotalMilliseconds * 1000000);
        }

        private long ToMilliseconds(long nanoseconds)
        {
            return nanoseconds / 1000000;
        }
        
        private bool ShouldStop(RestartStatistics rs)
        {
            if (_maxNrOfRetries == 0)
            {
                return true;
            }
            rs.Fail();
            
            if (rs.NumberOfFailures(_withinTimeSpan) > _maxNrOfRetries)
            {
                rs.Reset();
                return true;
            }
            
            return false;
        }
    }
}