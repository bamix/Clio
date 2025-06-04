using System;
using System.Collections.Generic;
using System.IO;
using Clio.Encryptors;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json;
using UnityEngine;

namespace Clio
{
    public class CheckpointManager : ICheckpointManager
    {
        private readonly ILogger logger;
        private readonly Dictionary<Type, Func<ICheckpointData>> saveActions = new();
        private readonly Dictionary<Type, Action<ICheckpointData>> loadActions = new();
        private readonly Dictionary<Type, Action> resetActions = new();

        private readonly IDataEncryptor dataEncryptor;
        private readonly string checkpointPath;

        private static readonly JsonSerializerSettings SerializerSettings = new()
        {
            TypeNameHandling = TypeNameHandling.Auto,
            Error = (_, args) =>
            {
                if (args.ErrorContext.Error is JsonSerializationException)
                {
                    args.ErrorContext.Handled = true;
                }
            }
        };

        public CheckpointManager(string checkpointPath, IDataEncryptor dataEncryptor = null, ILogger logger = null)
        {
            this.checkpointPath = checkpointPath;
            this.logger = logger ?? Debug.unityLogger;
            this.dataEncryptor = dataEncryptor ?? new EmptyDataEncryptor();
        }

        public void Register<T>(Func<T> save, Action<T> load, Action reset = null) where T: ICheckpointData
        {
            this.saveActions.Add(typeof(T), save as Func<ICheckpointData>);
            this.loadActions.Add(typeof(T), data => load((T)data));
            if (reset != null)
            {
                this.resetActions.Add(typeof(T), reset);
            }
        }

        public async UniTask SaveAsync()
        {
            var checkpoint = new Checkpoint();
            foreach (var (type, saveAction) in this.saveActions)
            {
                try
                {
                    var checkpointData = saveAction();
                    if (checkpointData != null)
                    {
                        checkpoint.Data.Add(checkpointData);
                    }
                }
                catch (Exception e)
                {
                    this.logger.LogException(e);
                    this.logger.LogError(null, $"Failed to save checkpoint for: {type.FullName}");
                }
            }

            var serializedObject = JsonConvert.SerializeObject(checkpoint, SerializerSettings);
            try
            {
                await File.WriteAllTextAsync(this.checkpointPath, this.dataEncryptor.Encrypt(serializedObject));
            }
            catch (Exception e)
            {
                this.logger.LogException(e);
                this.logger.LogError(null, "Failed to write checkpoint file");
            }
            this.logger.Log("Checkpoint saved successfully");
        }

        public async UniTask LoadAsync()
        {
            var checkpoint = await GetLatestCheckpoint();
            ParseCheckpoint(checkpoint);
        }

        public void Load()
        {
            var checkpoint = GetLatestCheckpoint(sync: true).GetAwaiter().GetResult();
            ParseCheckpoint(checkpoint);
        }

        public void Reset()
        {
            ResetStateToDefault();
        }

        private void ParseCheckpoint(Checkpoint checkpoint)
        {
            if (checkpoint == null)
            {
                return;
            }

            foreach (var checkpointData in checkpoint.Data)
            {
                var type = checkpointData.GetType();
                if (this.loadActions.TryGetValue(type, out var action))
                {
                    try
                    {
                        action(checkpointData);
                    }
                    catch (Exception e)
                    {
                        this.logger.LogException(e);
                        this.logger.LogError(null, $"Failed to load checkpoint for: {type.FullName}");
                    }
                }
                else
                {
                    this.logger.LogWarning(null, $"No handler found for {type.FullName}");
                }
            }
        }

        private async UniTask<Checkpoint> GetLatestCheckpoint(bool sync = false)
        {
            if (!File.Exists(this.checkpointPath))
            {
                this.logger.Log("No checkpoints found");
                ResetStateToDefault();
                return null;
            }

            try
            {
                var savedData = this.dataEncryptor.Decrypt(sync ? File.ReadAllText(this.checkpointPath) : await File.ReadAllTextAsync(this.checkpointPath));
                var checkpoint = JsonConvert.DeserializeObject<Checkpoint>(savedData, SerializerSettings);
                this.logger.Log("Checkpoint loaded");
                return checkpoint;
            }
            catch (Exception e)
            {
                this.logger.LogException(e);
                this.logger.LogError(null, "Corrupted checkpoint");
                ResetStateToDefault();
            }

            return null;
        }

        private void ResetStateToDefault()
        {
            foreach (var (type, action) in this.resetActions)
            {
                try
                {
                    action();
                }
                catch (Exception e)
                {
                    this.logger.LogException(e);
                    this.logger.LogError(null, $"Failed to reset checkpoint for: {type.FullName}");
                }
            }
        }

        private class Checkpoint
        {
            public List<ICheckpointData> Data { get; set; } = new ();
        }
    }
}