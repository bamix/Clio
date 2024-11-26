using System;

namespace Clio
{
    public interface ICheckpointManager
    {
        void Register<T>(Func<T> save, Action<T> load, Action reset = null) where T: ICheckpointData;
        void Save();
        void Load();
        void Reset();
    }
}
