using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Parse.Internal.Utilities;

namespace Parse.ParseCommon.Internal.Storage.Portable
{
    /// <inheritdoc />
    /// <summary>
    /// Implements `IStorageController` for PCL targets, based off of PCLStorage.
    /// </summary>
    public class StorageController : IStorageController
    {
        private class StorageDictionary : IStorageDictionary<string, object>
        {
            private readonly object _mutex;
            private Dictionary<string, object> _dictionary;
            private FileStream file;

            public StorageDictionary(FileStream file)
            {
                this.file = file;

                _mutex = new object();
                _dictionary = new Dictionary<string, object>();
            }

            internal Task SaveAsync()
            {
                string json;
                lock (_mutex)
                {
                    json = Json.Encode(_dictionary);
                }

                return file.WriteAsync(Encoding.ASCII.GetBytes(json), 0, json.Length);
            }

            internal Task LoadAsync()
            {
                var filesize = (int) file.Length;
                var buffer = new byte[filesize];
                return file.ReadAsync(buffer, 0, filesize)
                    .ContinueWith(t =>
                    {
                        var text = Encoding.ASCII.GetString(buffer);
                        Dictionary<string, object> result = null;
                        try
                        {
                            result = Json.Parse(text) as Dictionary<string, object>;
                        }
                        catch (Exception)
                        {
                            // Do nothing, JSON error. Probaby was empty string.
                        }

                        lock (_mutex)
                        {
                            _dictionary = result ?? new Dictionary<string, object>();
                        }
                    });
            }

            internal void Update(IDictionary<string, object> contents)
            {
                lock (_mutex)
                {
                    _dictionary = contents.ToDictionary(p => p.Key, p => p.Value);
                }
            }

            public Task AddAsync(string key, object value)
            {
                lock (_mutex)
                {
                    _dictionary[key] = value;
                }

                return SaveAsync();
            }

            public Task RemoveAsync(string key)
            {
                lock (_mutex)
                {
                    _dictionary.Remove(key);
                }

                return SaveAsync();
            }

            public bool ContainsKey(string key)
            {
                lock (_mutex)
                {
                    return _dictionary.ContainsKey(key);
                }
            }

            public IEnumerable<string> Keys
            {
                get
                {
                    lock (_mutex)
                    {
                        return _dictionary.Keys;
                    }
                }
            }

            public bool TryGetValue(string key, out object value)
            {
                lock (_mutex)
                {
                    return _dictionary.TryGetValue(key, out value);
                }
            }

            public IEnumerable<object> Values
            {
                get
                {
                    lock (_mutex)
                    {
                        return _dictionary.Values;
                    }
                }
            }

            public object this[string key]
            {
                get
                {
                    lock (_mutex)
                    {
                        return _dictionary[key];
                    }
                }
            }

            public int Count
            {
                get
                {
                    lock (_mutex)
                    {
                        return _dictionary.Count;
                    }
                }
            }

            public IEnumerator<KeyValuePair<string, object>> GetEnumerator()
            {
                lock (_mutex)
                {
                    return _dictionary.GetEnumerator();
                }
            }

            System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
            {
                lock (_mutex)
                {
                    return _dictionary.GetEnumerator();
                }
            }
        }

        private const string ParseStorageFileName = "/ApplicationSettings";
        private readonly TaskQueue _taskQueue = new TaskQueue();
        private readonly Task<FileStream> _fileTask;
        private StorageDictionary _storageDictionary;

        private string ParseStorageFilePath { get; } =
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);

        public StorageController()
        {
            _fileTask = _taskQueue.Enqueue(
                t => t.ContinueWith(_ => File.OpenWrite(ParseStorageFilePath + ParseStorageFileName)),
                CancellationToken.None);
        }

        public StorageController(FileStream file)
        {
            _fileTask = Task.FromResult(file);
        }

        public Task<IStorageDictionary<string, object>> LoadAsync()
        {
            return _taskQueue.Enqueue(toAwait =>
            {
                return toAwait.ContinueWith(_ =>
                {
                    if (_storageDictionary != null)
                    {
                        return Task.FromResult<IStorageDictionary<string, object>>(_storageDictionary);
                    }

                    _storageDictionary = new StorageDictionary(_fileTask.Result);
                    return _storageDictionary.LoadAsync()
                        .OnSuccess(__ => _storageDictionary as IStorageDictionary<string, object>);
                }).Unwrap();
            }, CancellationToken.None);
        }

        public Task<IStorageDictionary<string, object>> SaveAsync(IDictionary<string, object> contents)
        {
            return _taskQueue.Enqueue(toAwait =>
            {
                return toAwait.ContinueWith(_ =>
                {
                    if (_storageDictionary == null)
                    {
                        _storageDictionary = new StorageDictionary(_fileTask.Result);
                    }

                    _storageDictionary.Update(contents);
                    return _storageDictionary.SaveAsync()
                        .OnSuccess(__ => _storageDictionary as IStorageDictionary<string, object>);
                }).Unwrap();
            }, CancellationToken.None);
        }
    }
}