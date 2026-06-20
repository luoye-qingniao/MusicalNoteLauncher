using System;
using System.Collections.Generic;
using System.IO;

namespace PCL.Account
{
    /// <summary>
    /// 账户持久化设置，映射 AccountManager 中的 Settings 调用
    /// 数据存储在 accounts.json 中
    /// </summary>
    public static class Settings
    {
        private static readonly string FilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "accounts.json");
        private static Dictionary<string, object> _store;
        private static readonly object _lock = new object();

        static Settings()
        {
            Load();
        }

        private static void Load()
        {
            lock (_lock)
            {
                try
                {
                    if (File.Exists(FilePath))
                    {
                        string json = File.ReadAllText(FilePath);
                        _store = Newtonsoft.Json.JsonConvert.DeserializeObject<Dictionary<string, object>>(json) ?? new Dictionary<string, object>();
                    }
                    else
                    {
                        _store = new Dictionary<string, object>();
                    }
                }
                catch
                {
                    _store = new Dictionary<string, object>();
                }
            }
        }

        public static void Save()
        {
            lock (_lock)
            {
                try
                {
                    string json = Newtonsoft.Json.JsonConvert.SerializeObject(_store, Newtonsoft.Json.Formatting.Indented);
                    string dir = Path.GetDirectoryName(FilePath);
                    if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                        Directory.CreateDirectory(dir);
                    File.WriteAllText(FilePath, json);
                }
                catch { }
            }
        }

        public static T Get<T>(string key, object instance = null)
        {
            lock (_lock)
            {
                if (_store.TryGetValue(GetFullKey(key, instance), out object value))
                {
                    try
                    {
                        if (typeof(T) == typeof(long) && value is long lv) return (T)(object)lv;
                        if (typeof(T) == typeof(bool) && value is bool bv) return (T)(object)bv;
                        if (typeof(T) == typeof(string) && value is string sv) return (T)(object)sv;
                        if (typeof(T) == typeof(int) && value is long l) return (T)(object)(int)l;
                        if (value is Newtonsoft.Json.Linq.JToken jt)
                            return jt.ToObject<T>();
                        return (T)Convert.ChangeType(value, typeof(T));
                    }
                    catch
                    {
                        return default(T);
                    }
                }
                return default(T);
            }
        }

        public static void Set<T>(string key, T value)
        {
            lock (_lock)
            {
                _store[GetFullKey(key, null)] = value;
                Save();
            }
        }

        public static void Set<T>(string key, T value, object instance)
        {
            lock (_lock)
            {
                _store[GetFullKey(key, instance)] = value;
                Save();
            }
        }

        private static string GetFullKey(string key, object instance)
        {
            if (instance != null)
                return instance + "::" + key;
            return key;
        }
    }
}
