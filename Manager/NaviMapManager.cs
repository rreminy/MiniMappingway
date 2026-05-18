using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Component.GUI;
using Dalamud.Bindings.ImGui;
using Lumina.Excel;
using Lumina.Excel.Sheets;
using MiniMappingway.Model;
using MiniMappingway.Service;
using MiniMappingway.Utility;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace MiniMappingway.Manager;

public unsafe sealed class NaviMapManager : IDisposable
{

    public readonly ConcurrentDictionary<string, ConcurrentDictionary<int, PersonDetails>> PersonDict = new();

    public readonly ConcurrentDictionary<string, SourceData> SourceDataDict = new();

    public int X;

    public int Y;

    public float NaviScale;

    public float ZoneScale;

    public float Rotation;

    public bool Visible;

    public float Zoom;

    public short OffsetX;
    public short OffsetY;

    public bool Loading;

    public bool DebugMode = false;

    public bool IsLocked;

    public bool InCombat { get; set; }

    private AtkUnitBase* NaviMapPointer => (AtkUnitBase*)ServiceManager.GameGui.GetAddonByName("_NaviMap").Address;

    private readonly ExcelSheet<Map>? _maps;

    public readonly ConcurrentDictionary<int, Queue<CircleData>> CircleData = new();

    public NaviMapManager()
    {
        ServiceManager.GameInteropProvider.InitializeFromAttributes(this);
        this._maps = ServiceManager.DataManager.GetExcelSheet<Map>();
        this.UpdateNaviMap();
        this.UpdateMap();
    }

    public bool AddOrUpdateSource(string sourceName, uint colour)
    {
        this.SourceDataDict[sourceName] = ServiceManager.Configuration.SourceConfigs.GetValueOrDefault(sourceName) ?? new SourceData(colour);
        this.PersonDict.GetOrAdd(sourceName, _ => new ConcurrentDictionary<int, PersonDetails>()).Clear();
        return true;
    }

    public bool AddOrUpdateSource(string sourceName, Vector4 colour)
    {
        if (ServiceManager.Configuration.SourceConfigs.TryGetValue(sourceName, out var source))
        {
            SourceDataDict[sourceName] = source;
            PersonDict.GetOrAdd(sourceName, _ => new ConcurrentDictionary<int, PersonDetails>()).Clear();
        }
        else
        {
            var uintColor = ImGui.ColorConvertFloat4ToU32(colour);
            var sourceData = new SourceData(uintColor);
            switch (sourceName)
            {
                case FinderService.EveryoneKey:
                    sourceData.Priority = 0;
                    sourceData.Enabled = false;
                    break;
                case FinderService.FcMembersKey:
                    sourceData.Priority = 1;
                    break;
                case FinderService.FriendKey:
                    sourceData.Priority = 2;
                    break;
                default:
                    sourceData.Priority = GetNextFreePriority();
                    break;
            }

            ServiceManager.Configuration.SourceConfigs.TryAdd(sourceName, sourceData);
            SourceDataDict[sourceName] = sourceData;
            PersonDict.GetOrAdd(sourceName, _ => new ConcurrentDictionary<int, PersonDetails>()).Clear();
        }
        return true;
    }

    public bool UpdateNaviMap()
    {
        if (this.NaviMapPointer is null || this.NaviMapPointer->UldManager.LoadedState is not AtkLoadState.Loaded) return false;
        try
        {
            //There's probably a better way of doing this but I don't know it for now
            this.IsLocked = ((AtkComponentCheckBox*)NaviMapPointer->GetNodeById(4)->GetComponent())->IsChecked;

            this.Rotation = NaviMapPointer->GetNodeById(8)->Rotation;
            this.Zoom = NaviMapPointer->GetNodeById(18)->GetComponent()->GetImageNodeById(6)->ScaleX;
        }
        catch
        {
            // ignored
        }

        this.X = NaviMapPointer->X;
        this.Y = NaviMapPointer->Y;
        this.NaviScale = NaviMapPointer->Scale;
        this.Visible = NaviMapPointer->IsVisible && NaviMapPointer->VisibilityFlags == 0 && !ServiceManager.GameGui.GameUiHidden;

        // Multi-monitor viewport offset fix - may or may not work
        // https://github.com/GemPlugins/MiniMappingway/issues/2
        if (ServiceManager.Configuration.MultiMonitorFix)
        {
            var viewport = ImGui.GetWindowViewport();
            if (!viewport.IsNull)
            {
                this.X -= (int)viewport.Pos.X;
                this.Y -= (int)viewport.Pos.Y;
            }
        }

        return true;
    }

    public bool CheckIfLoading()
    {
        var locationTitle = (AtkUnitBase*)ServiceManager.GameGui.GetAddonByName("_LocationTitle").Address;
        var fadeMiddle = (AtkUnitBase*)ServiceManager.GameGui.GetAddonByName("FadeMiddle").Address;
        return this.Loading =
            locationTitle->IsVisible ||
            fadeMiddle->IsVisible;
    }

    public void UpdateMap()
    {
        var maps = this._maps;
        if (maps is null || !maps.TryGetRow(this.GetMapId(), out var map)) return;

        if (map.SizeFactor is not 0)
        {
            this.ZoneScale = (float)map.SizeFactor / 100;
        }
        else
        {
            this.ZoneScale = 1;
        }
        this.OffsetX = map.OffsetX;
        this.OffsetY = map.OffsetY;
    }

    private uint GetMapId()
    {
        return AgentMap.Instance()->CurrentMapId;
    }

    public bool ClearPersonBag(string sourceName)
    {
        if (!this.PersonDict.TryGetValue(sourceName, out var dict)) return false;
        dict.Clear();
        return true;
    }
    public bool OverwriteWholeBag(string sourceName, List<PersonDetails> list)
    {
        var success = true;

        if (!this.PersonDict.TryGetValue(sourceName, out var dict)) return false;
        dict.Clear();

        foreach (var person in list)
        {
            var personIndex = MarkerUtility.GetObjIndexById(person.Id);
            if (personIndex is null) continue;
            if (!dict.TryAdd((int)personIndex, person)) success = false;
        }
        return success;
    }

    public bool AddToBag(PersonDetails details)
    {
        if (!this.PersonDict.TryGetValue(details.SourceName, out var dict)) return false;
        var personIndex = MarkerUtility.GetObjIndexById(details.Id);
        if (personIndex is null) return false;
        return dict.TryAdd((int)personIndex, details);
    }

    public bool RemoveFromBag(ulong id, string sourceName)
    {
        if (!this.PersonDict.TryGetValue(sourceName, out var dict)) return false;
        var entry = dict.First(x => x.Value.Id == id);
        return dict.TryRemove(entry);

    }

    public bool RemoveFromBag(string name, string sourceName)
    {
        if (!this.PersonDict.TryGetValue(sourceName, out var dict)) return false;
        var entry = dict.First(x => x.Value.Name == name);
        return dict.TryRemove(entry);
    }

    public bool RemoveSourceAndPeople(string sourceName)
    {
        var successPerson = ClearPersonBag(sourceName);
        var successSource = SourceDataDict.TryRemove(sourceName, out _);
        return successPerson && successSource;
    }

    public void Dispose()
    {
        this.PersonDict.Clear();
        this.SourceDataDict.Clear();
    }

    public int GetNextFreePriority()
    {
        for (var i = 0; i < 99; i++)
        {
            if (SourceDataDict.Values.All(x => x.Priority != i))
            {
                return i;
            }
        }
        return 1;
    }
}
