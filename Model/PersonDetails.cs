using System;

namespace MiniMappingway.Model;

public class PersonDetails
{
    public string Name { get; }

    public ulong Id { get; }

    public string SourceName { get; }

    public int Index { get; }

    public PersonDetails(string name, ulong id, string sourceName, int index)
    {
        Name = name;
        Id = id;
        SourceName = sourceName;
        Index = index;
    }
}
