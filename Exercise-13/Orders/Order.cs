﻿using System.Collections.Generic;

public class Order : IEntity
{
    public object VersionInfo { get; set; }
    public string Id { get; set; }
    public List<OrderLine> Lines { get; set; } = new List<OrderLine>();
    public Dictionary<string, OutboxState> OutboxState { get; set; } = new Dictionary<string, OutboxState>();
}