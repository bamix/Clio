using System;
using Cysharp.Threading.Tasks;

namespace Clio
{
    public interface ICheckpointManager
    {
        void Register<T>(Func<T> save, Action<T> load, Action reset = null) where T: ICheckpointData;
        UniTask SaveAsync();
        UniTask LoadAsync();
        void Load();
        void Reset();
    }
}