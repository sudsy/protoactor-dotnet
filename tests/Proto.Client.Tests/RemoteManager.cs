using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;

using Proto.Remote;
using Xunit;

namespace Proto.Client.Tests
{
    public class RemoteManager : IDisposable
    {
        private static string DefaultNodeAddress = "127.0.0.1:12000";
        
        private static string RemoteNodeAddress = "127.0.0.1:12001";
        public Dictionary<string, System.Diagnostics.Process> Nodes = new Dictionary<string, System.Diagnostics.Process>();

        public (string Address, System.Diagnostics.Process Process) DefaultNode => (DefaultNodeAddress, Nodes[DefaultNodeAddress]);
        public (string Address, System.Diagnostics.Process Process) RemoteNode => (RemoteNodeAddress, Nodes[RemoteNodeAddress]);

        public RemoteManager()
        {
            Serialization.RegisterFileDescriptor(Proto.Remote.Tests.Messages.ProtosReflection.Descriptor);
            ProvisionNode("127.0.0.1", 12000, true);
            ProvisionNode("127.0.0.1", 12001);
            
            
           
            
            Thread.Sleep(6000);
        }

        public void Dispose()
        {
            foreach (var (_, process) in Nodes)
            {
                if (process != null && !process.HasExited)
                    process.Kill();
            }
        }

        public (string Address, System.Diagnostics.Process Process) ProvisionNode(string host = "127.0.0.1", int port = 12000, bool clientHost = false)
        {
            var address = $"{host}:{port}";
            var buildConfig = "Debug";
#if RELEASE
            buildConfig = "Release";
#endif
            var nodeAppPath = $@"Proto.Remote.Tests.Node/bin/{buildConfig}/netcoreapp2.0/Proto.Remote.Tests.Node.dll";
            var testsDirectory = Directory.GetParent(Directory.GetCurrentDirectory()).Parent.Parent.Parent;
            var nodeDllPath = $@"{testsDirectory.FullName}/{nodeAppPath}";
            
            if (!File.Exists(nodeDllPath))
            {
                throw new FileNotFoundException(nodeDllPath);
            }

            var clientHostArgument = clientHost ? "--clienthost" : "";
            var process = new System.Diagnostics.Process
            {
                StartInfo =
                {
                    Arguments = $"{nodeDllPath} --host {host} --port {port} {clientHostArgument}",
                    CreateNoWindow = false,
                    UseShellExecute = false,
                    FileName = "dotnet"
                }
            };
            
            process.Start();
            Nodes.Add(address, process);
            
          
            Thread.Sleep(TimeSpan.FromSeconds(3));

            return (address, process);
        }
    }

    [CollectionDefinition("RemoteTests")]
    public class RemoteCollection : ICollectionFixture<RemoteManager>
    {
    }
}
