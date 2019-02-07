using System;
using System.Collections.Generic;
using System.Data;
using System.Threading;
using System.Threading.Tasks;
using Grpc.Core;
using Grpc.Core.Testing;
using Grpc.Core.Utils;
using Microsoft.Extensions.Logging;
using Moq;
using Proto.Remote;
using Proto.Remote.Tests;
using Proto.Remote.Tests.Messages;
using Xunit;
using Xunit.Abstractions;

namespace Proto.Client.Tests
{

    
    [Collection("RemoteTests"), Trait("Category", "Remote")]
    public class ClientIntegrationTests
    {
        
       
        private readonly RemoteManager _remoteManager;
        

        public ClientIntegrationTests(RemoteManager remoteManager, ITestOutputHelper testOutputHelper)
        {
           
            _remoteManager = remoteManager;
           
        }

        
        
        [Fact, DisplayTestMethodName]
        public async void CanSendJsonAndReceiveToClient()
        {
            
            await Client.Connect("127.0.0.1", 12000, new RemoteConfig());
            var clientHostActor = new PID(_remoteManager.DefaultNode.Address, "EchoActorInstance");
            var ct = new CancellationTokenSource(30000);
            var tcs = new TaskCompletionSource<bool>();
            ct.Token.Register(() =>
            {
                tcs.TrySetCanceled();
            });
            
            //This needs to spawn with a connection ID
            var localActor = RootContext.Empty.Spawn(Props.FromFunc(ctx =>
            {
                if (ctx.Message is Pong)
                {
                    tcs.SetResult(true);
                    ctx.Self.Stop();
                }

                return Actor.Done;
            }));

            
            var json = new JsonMessage("remote_test_messages.Ping", "{ \"message\":\"Hello\"}");
            var envelope = new Proto.MessageEnvelope(json, localActor, Proto.MessageHeader.Empty);
            
            Client.SendMessage(clientHostActor, envelope, 1);
            await tcs.Task;
        }

        [Fact, DisplayTestMethodName]
        public async void CanRequestAsyncToRemoteFromClientActor()
        {
            
            await Client.Connect("127.0.0.1", 12000, new RemoteConfig());
            var remoteActor = new PID(_remoteManager.RemoteNode.Address, "EchoActorInstance");
            
            var ct = new CancellationTokenSource(30000);
            var tcs = new TaskCompletionSource<bool>();
            ct.Token.Register(() =>
            {
                tcs.TrySetCanceled();
            });
            
            //This needs to spawn with a connection ID
            var proxyActor = RootContext.Empty.Spawn(Props.FromFunc(async ctx =>
            {
                if (ctx.Message is Started)
                {
                   
                    var result = await ctx.RequestAsync<Pong>(remoteActor, new Ping()
                    {
                        Message = "Hello to remote from inside"
                    });
                    tcs.SetResult(true);
                    ctx.Self.Stop();
                }
                
                
            }));
            
           
            await tcs.Task;
        }

        [Fact, DisplayTestMethodName]
        public async void CanGetProxyActorID()
        {
            await Client.Connect("127.0.0.1", 12000, new RemoteConfig());
            var localPID = RootContext.Empty.Spawn((Props.FromFunc(ctx => Actor.Done)));
            var proxyPID = await Client.GetProxyPID(localPID);
        }
        
        
        [Fact, DisplayTestMethodName]
        public async void CanSpawnRemoteActor()
        {
            await Client.Connect("127.0.0.1", 12000, new RemoteConfig());
            var remoteActorName = Guid.NewGuid().ToString();
            var remoteActorResp = await Client.SpawnNamedAsync(_remoteManager.DefaultNode.Address, remoteActorName, "EchoActor", TimeSpan.FromSeconds(5));
            var remoteActor = remoteActorResp.Pid;
            var pong = await RootContext.Empty.RequestAsync<Pong>(remoteActor, new Ping{Message="Hello"}, TimeSpan.FromMilliseconds(5000));
            Assert.Equal($"{_remoteManager.DefaultNode.Address} Hello", pong.Message);
        }
        
        [Fact, DisplayTestMethodName]
        public async void CanGetClientHostAddress()
        {
            await Client.Connect("127.0.0.1", 12000, new RemoteConfig());
            var address = await Client.GetClientHostAddress();
            Assert.Equal("127.0.0.1:12000", address);
        }
        
        [Fact, DisplayTestMethodName]
        public async void CanSpawnClientHostActor()
        {
            await Client.Connect("127.0.0.1", 12000, new RemoteConfig());
            var remoteActorName = Guid.NewGuid().ToString();
            var remoteActorResp = await Client.SpawnOnClientHostAsync( remoteActorName, "EchoActor", TimeSpan.FromSeconds(5));
            var remoteActor = remoteActorResp.Pid;
            var pong = await RootContext.Empty.RequestAsync<Pong>(remoteActor, new Ping{Message="Hello"}, TimeSpan.FromMilliseconds(5000));
            Assert.Equal($"{_remoteManager.DefaultNode.Address} Hello", pong.Message);
        }
    }
}