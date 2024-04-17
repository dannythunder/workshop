﻿using Newtonsoft.Json;

public abstract class Entity
{
    [JsonProperty("id")] public string Id { get; set; }
    public string Customer { get; set; } //Partition key
}