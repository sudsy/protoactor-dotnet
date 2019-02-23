using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Grpc.Core;
using Grpc.Core.Testing;
using Grpc.Core.Utils;
using Moq;
using Proto.Remote;
using Proto.Remote.Tests;
using Proto.Remote.Tests.Messages;
using Xunit;

namespace Proto.Client.Tests
{

    public class ClientTests
    {

        [Fact, DisplayTestMethodName]
        public void TimeoutOnConnectFailure()
        {
                      
            Assert.ThrowsAsync<TimeoutException>(async () => { await Client.CreateAsync("127.0.0.1", 12222, new RemoteConfig(), 1000); });

        }

       

       
  
    }
}