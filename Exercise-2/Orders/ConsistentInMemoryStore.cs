﻿using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Newtonsoft.Json;
#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously

public class ConsistentInMemoryStore<T>
    where T : class, IEntity
{
    Dictionary<string, string> storage = new Dictionary<string, string>();
    Dictionary<string, int> versionInfo = new Dictionary<string, int>();

    JsonSerializerSettings jsonSerializerSettings = new JsonSerializerSettings
    {
        TypeNameHandling = TypeNameHandling.Auto
    };

    public async Task<T> Get(string id)
    {
        T entity = null;
        lock (storage)
        {
            if (storage.TryGetValue(id, out var serializedContainer))
            {
                entity = JsonConvert.DeserializeObject<T>(serializedContainer, jsonSerializerSettings);
                entity.VersionInfo = versionInfo[id];
            }
        }
        return entity;
    }

    public async Task Delete(T doc)
    {
        lock (storage)
        {
            if (versionInfo.TryGetValue(doc.Id, out var version))
            {
                if (doc.VersionInfo == null)
                {
                    throw new ConcurrencyException();
                }
                var expectedVersion = (int)doc.VersionInfo;
                if (version != expectedVersion)
                {
                    throw new ConcurrencyException();
                }

                storage.Remove(doc.Id);
                versionInfo.Remove(doc.Id);
            }
            else
            {
                throw new InvalidOperationException("Object does not exist");
            }
        }
    }

    public async Task Put(T doc)
    {
        lock (storage)
        {
            if (versionInfo.TryGetValue(doc.Id, out var version))
            {
                if (doc.VersionInfo == null)
                {
                    throw new ConcurrencyException();
                }
                var expectedVersion = (int)doc.VersionInfo;
                if (version != expectedVersion)
                {
                    throw new ConcurrencyException();
                }
                storage[doc.Id] = JsonConvert.SerializeObject(doc, jsonSerializerSettings);
                versionInfo[doc.Id] = version + 1;
            }
            else
            {
                storage[doc.Id] = JsonConvert.SerializeObject(doc, jsonSerializerSettings);
                versionInfo[doc.Id] = 0;
            }
            doc.VersionInfo = versionInfo[doc.Id];
        }
    }
}