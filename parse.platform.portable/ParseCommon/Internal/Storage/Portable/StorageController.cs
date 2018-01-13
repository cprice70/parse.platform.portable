using System;
using System.Threading.Tasks;
using System.Linq;
using System.Collections.Generic;
using System.Threading;
using System.IO;
using System.Text;

namespace Parse.Common.Internal
{
    /// <summary>
    /// Implements `IStorageController` for PCL targets, based off of PCLStorage.
    /// </summary>
    public class StorageController : IStorageController {
    private class StorageDictionary : IStorageDictionary<string, object> {
            private readonly object mutex;
            private Dictionary<string, object> dictionary;
            private FileStream file;

      public StorageDictionary(FileStream file) {
        this.file = file;

        mutex = new Object();
        dictionary = new Dictionary<string, object>();
      }

      internal Task SaveAsync() 
      {
          string json;
          lock (mutex) 
          {
            json = Json.Encode(dictionary);
          }
          return file.WriteAsync(Encoding.ASCII.GetBytes(json), 0, json.Length);
      }

      internal Task LoadAsync() 
            {
                int filesize = (int)file.Length;
                var buffer = new byte[filesize];
                return file.ReadAsync(buffer, 0, filesize)
                           .ContinueWith(t => {
                               string text = Encoding.ASCII.GetString(buffer);
          Dictionary<string, object> result = null;
          try {
            result = Json.Parse(text) as Dictionary<string, object>;
          } catch (Exception) {
            // Do nothing, JSON error. Probaby was empty string.
          }

          lock (mutex) {
            dictionary = result ?? new Dictionary<string, object>();
          }
        });
      }

      internal void Update(IDictionary<string, object> contents) {
        lock (mutex) {
          dictionary = contents.ToDictionary(p => p.Key, p => p.Value);
        }
      }

      public Task AddAsync(string key, object value) {
        lock (mutex) {
          dictionary[key] = value;
        }
        return SaveAsync();
      }

      public Task RemoveAsync(string key) {
        lock (mutex) {
          dictionary.Remove(key);
        }
        return SaveAsync();
      }

      public bool ContainsKey(string key) {
        lock (mutex) {
          return dictionary.ContainsKey(key);
        }
      }

      public IEnumerable<string> Keys {
        get { lock (mutex) { return dictionary.Keys; } }
      }

      public bool TryGetValue(string key, out object value) {
        lock (mutex) {
          return dictionary.TryGetValue(key, out value);
        }
      }

      public IEnumerable<object> Values {
        get { lock (mutex) { return dictionary.Values; } }
      }

      public object this[string key] {
        get { lock (mutex) { return dictionary[key]; } }
      }

      public int Count {
        get { lock (mutex) { return dictionary.Count; } }
      }

      public IEnumerator<KeyValuePair<string, object>> GetEnumerator() {
        lock (mutex) {
          return dictionary.GetEnumerator();
        }
      }

      System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() {
        lock (mutex) {
          return dictionary.GetEnumerator();
        }
      }
    }

    private const string ParseStorageFileName = "ApplicationSettings";
    private readonly TaskQueue taskQueue = new TaskQueue();
    private readonly Task<FileStream> fileTask;
    private StorageDictionary storageDictionary;

    public StorageController() {
      fileTask = taskQueue.Enqueue(t => t.ContinueWith(_ => {
                return File.OpenWrite(ParseStorageFileName);
      }), CancellationToken.None);
    }

    public StorageController(FileStream file) {
      this.fileTask = Task.FromResult(file);
    }

    public Task<IStorageDictionary<string, object>> LoadAsync() {
      return taskQueue.Enqueue(toAwait => {
        return toAwait.ContinueWith(_ => {
          if (storageDictionary != null) {
            return Task.FromResult<IStorageDictionary<string, object>>(storageDictionary);
          }

          storageDictionary = new StorageDictionary(fileTask.Result);
          return storageDictionary.LoadAsync().OnSuccess(__ => storageDictionary as IStorageDictionary<string, object>);
        }).Unwrap();
      }, CancellationToken.None);
    }

    public Task<IStorageDictionary<string, object>> SaveAsync(IDictionary<string, object> contents) {
      return taskQueue.Enqueue(toAwait => {
        return toAwait.ContinueWith(_ => {
          if (storageDictionary == null) {
            storageDictionary = new StorageDictionary(fileTask.Result);
          }

          storageDictionary.Update(contents);
          return storageDictionary.SaveAsync().OnSuccess(__ => storageDictionary as IStorageDictionary<string, object>);
        }).Unwrap();
      }, CancellationToken.None);
    }
  }
}
