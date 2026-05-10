using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using JulyCore;
using JulyCore.Core;
using JulyCore.Data.Save;
using JulyCore.Provider.Base;
using JulyCore.Provider.Save;
using LitJson;

namespace GameTemplate.Aot
{
    public class PlayerPrefsSaveProvider : ProviderBase, ISaveProvider
    {
        public override int Priority => Frameworkconst.PrioritySaveProvider;
        protected override LogChannel LogChannel => LogChannel.Save;

        private const string KeyPrefix = "Save_";

        private readonly Dictionary<string, ISaveData> _registered = new();
        private readonly HashSet<string> _dirty = new();

        public static string PrefKey(string key) => KeyPrefix + key;

        #region Registration

        public void Register(string key, ISaveData data)
        {
            if (string.IsNullOrEmpty(key)) throw new ArgumentNullException(nameof(key));
            if (data == null) throw new ArgumentNullException(nameof(data));
            _registered[key] = data;
        }

        public bool Unregister(string key)
        {
            if (string.IsNullOrEmpty(key)) return false;
            _dirty.Remove(key);
            return _registered.Remove(key);
        }

        public bool IsRegistered(string key)
        {
            if (string.IsNullOrEmpty(key)) return false;
            return _registered.ContainsKey(key);
        }

        public T GetRegisteredData<T>(string key) where T : class, ISaveData
        {
            if (string.IsNullOrEmpty(key)) return null;
            return _registered.TryGetValue(key, out var d) ? d as T : null;
        }

        public IEnumerable<string> GetAllRegisteredKeys()
        {
            return _registered.Keys;
        }

        #endregion

        #region Dirty Tracking

        public bool MarkDirty(string key)
        {
            if (string.IsNullOrEmpty(key)) return false;
            if (!_registered.ContainsKey(key)) return false;
            _dirty.Add(key);
            return true;
        }

        public bool IsDirty(string key)
        {
            if (string.IsNullOrEmpty(key)) return false;
            return _dirty.Contains(key);
        }

        public IEnumerable<string> GetDirtyKeys()
        {
            return _dirty;
        }

        public int DirtyCount => _dirty.Count;

        public void ClearDirty(string key)
        {
            if (string.IsNullOrEmpty(key)) return;
            _dirty.Remove(key);
        }

        public void ClearAllDirty()
        {
            _dirty.Clear();
        }

        #endregion

        #region Save & Load

        public UniTask<SaveResult> SaveAsync<T>(string key, T data, CancellationToken ct = default)
            where T : ISaveData
        {
            if (string.IsNullOrEmpty(key) || data == null)
                return UniTask.FromResult(SaveResult.CreateFailure(SaveFailureReason.InvalidData));

            try
            {
                var json = JsonMapper.ToJson(data);
                UnityEngine.PlayerPrefs.SetString(PrefKey(key), json);
                UnityEngine.PlayerPrefs.Save();
                return UniTask.FromResult(SaveResult.CreateSuccess());
            }
            catch (Exception ex)
            {
                GF.LogException(ex);
                return UniTask.FromResult(SaveResult.CreateFailure(SaveFailureReason.Unknown, ex.Message));
            }
        }

        public UniTask<T> LoadAsync<T>(string key, CancellationToken ct = default) where T : ISaveData
        {
            if (string.IsNullOrEmpty(key))
                return UniTask.FromResult(default(T));

            try
            {
                var prefKey = PrefKey(key);
                if (!UnityEngine.PlayerPrefs.HasKey(prefKey))
                    return UniTask.FromResult(default(T));

                var json = UnityEngine.PlayerPrefs.GetString(prefKey);
                if (string.IsNullOrEmpty(json))
                    return UniTask.FromResult(default(T));

                return UniTask.FromResult(JsonMapper.ToObject<T>(json));
            }
            catch (Exception ex)
            {
                GF.LogException(ex);
                return UniTask.FromResult(default(T));
            }
        }

        public async UniTask<T> LoadAndRegisterAsync<T>(string key, CancellationToken ct = default)
            where T : ISaveData, new()
        {
            var data = HasSave(key) ? await LoadAsync<T>(key, ct) : default;
            if (data == null) data = new T();
            Register(key, data);
            return data;
        }

        public async UniTask<Dictionary<string, T>> LoadAndRegisterBatchAsync<T>(
            string[] keys, CancellationToken ct = default) where T : ISaveData, new()
        {
            var results = new Dictionary<string, T>(keys.Length);
            foreach (var key in keys)
            {
                if (ct.IsCancellationRequested) break;
                results[key] = await LoadAndRegisterAsync<T>(key, ct);
            }

            return results;
        }

        public async UniTask<Dictionary<string, SaveResult>> SaveRegisteredAsync(
            IEnumerable<string> keys = null, CancellationToken ct = default)
        {
            var results = new Dictionary<string, SaveResult>();

            List<string> keysToSave;
            if (keys == null)
            {
                keysToSave = new List<string>(_dirty);
            }
            else
            {
                keysToSave = new List<string>();
                foreach (var k in keys)
                {
                    if (_dirty.Contains(k))
                        keysToSave.Add(k);
                }
            }

            foreach (var key in keysToSave)
            {
                if (ct.IsCancellationRequested)
                {
                    results[key] = SaveResult.CreateFailure(SaveFailureReason.Cancelled);
                    continue;
                }

                if (!_registered.TryGetValue(key, out var data)) continue;

                var result = await SaveAsync(key, data, ct);
                results[key] = result;
                if (result.Success) ClearDirty(key);
            }

            return results;
        }

        public async UniTask<Dictionary<string, SaveResult>> SaveBatchAsync<T>(
            Dictionary<string, T> dataMap, CancellationToken ct = default) where T : ISaveData
        {
            var results = new Dictionary<string, SaveResult>(dataMap.Count);
            foreach (var kvp in dataMap)
            {
                if (ct.IsCancellationRequested)
                {
                    results[kvp.Key] = SaveResult.CreateFailure(SaveFailureReason.Cancelled);
                    continue;
                }

                results[kvp.Key] = await SaveAsync(kvp.Key, kvp.Value, ct);
            }

            return results;
        }

        public async UniTask<Dictionary<string, T>> LoadBatchAsync<T>(
            string[] keys, CancellationToken ct = default) where T : ISaveData
        {
            var results = new Dictionary<string, T>(keys.Length);
            foreach (var key in keys)
            {
                if (ct.IsCancellationRequested) break;
                var data = await LoadAsync<T>(key, ct);
                if (data != null) results[key] = data;
            }

            return results;
        }

        #endregion

        #region File Operations

        public bool Delete(string key)
        {
            if (string.IsNullOrEmpty(key)) return false;
            Unregister(key);
            var prefKey = PrefKey(key);
            if (!UnityEngine.PlayerPrefs.HasKey(prefKey)) return false;
            UnityEngine.PlayerPrefs.DeleteKey(prefKey);
            UnityEngine.PlayerPrefs.Save();
            return true;
        }

        public bool HasSave(string key)
        {
            if (string.IsNullOrEmpty(key)) return false;
            return UnityEngine.PlayerPrefs.HasKey(PrefKey(key));
        }

        #endregion

        protected override void OnShutdown()
        {
            _registered.Clear();
            _dirty.Clear();
        }
    }
}
