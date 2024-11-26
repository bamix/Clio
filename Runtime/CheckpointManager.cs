using System;
using System.Collections.Generic;
using System.IO;
using Clio.Encryptors;
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

        private static readonly JsonSerializerSettings SerializerSettings = new()
        {
            TypeNameHandling = TypeNameHandling.Auto
        };

        public CheckpointManager(IDataEncryptor dataEncryptor = null, ILogger logger = null)
        {
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

        public void Save()
        {
            var checkpoint = new Checkpoint();
            foreach (var (type, saveAction) in this.saveActions)
            {
                try
                {
                    var checkpointData = saveAction();
                    checkpoint.Data.Add(checkpointData);
                }
                catch (Exception e)
                {
                    this.logger.LogException(e);
                    this.logger.LogError(null, $"Failed to save checkpoint for: {type.FullName}");
                }
            }

            var serializedObject = JsonConvert.SerializeObject(checkpoint, SerializerSettings);
            var destination = Application.persistentDataPath + "/checkpoint.dat";
            File.WriteAllText(destination, this.dataEncryptor.Encrypt(serializedObject));
        }

        public void Load()
        {
            var checkpoint = GetLatestCheckpoint();

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
                    this.logger.LogError(null, $"No handler found for {type.FullName}");
                }
            }
        }

        public void Reset()
        {
            ResetStateToDefault();
        }

        private Checkpoint GetLatestCheckpoint()
        {
            var destination = Application.persistentDataPath + "/checkpoint.dat";
            if (!File.Exists(destination))
            {
                this.logger.Log("No checkpoints found");
                ResetStateToDefault();
                return null;
            }

            try
            {
                var savedData = this.dataEncryptor.Decrypt(File.ReadAllText(destination));
                var checkpoint = JsonConvert.DeserializeObject<Checkpoint>(savedData, SerializerSettings);
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