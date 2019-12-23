using System;

namespace Proto
{
    public interface IProcessRegistry
    {
        string Address { get; set; }

        Process Get(PID pid);
        Process GetLocal(string id);
        string NextId();
        void RegisterHostResolver(Func<PID, Process> resolver);
        void Remove(PID pid);
        (PID pid, bool ok) TryAdd(string id, Process process);
    }
}