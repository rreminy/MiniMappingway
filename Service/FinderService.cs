using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Plugin.Services;
using Dalamud.Utility;
using FFXIVClientStructs;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using MiniMappingway.Manager;
using MiniMappingway.Model;
using MiniMappingway.Utility;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace MiniMappingway.Service;

public sealed class FinderService : IDisposable
{
    public const string FcMembersKey = "FC Members";
    public const string FriendKey = "Friends";
    public const string EveryoneKey = "Everyone";
    private readonly IEnumerable<int> _enumerable;
    private IEnumerator<int> _enumerator;
    private int _index;

    public FinderService()
    {
        ServiceManager.NaviMapManager.AddOrUpdateSource(FriendKey, new Vector4(0.957f, 0.533f, 0.051f, 1));
        ServiceManager.NaviMapManager.AddOrUpdateSource(FcMembersKey, new Vector4(1, 0, 0, 1));
        ServiceManager.NaviMapManager.AddOrUpdateSource(EveryoneKey, new Vector4(0, 0.7f, 0.7f, 1));
        ServiceManager.Framework.Update += Iterate;

        _enumerable = Enumerable.Range(2, 200).Where(x => x % 2 == 0);
        _enumerator = _enumerable.GetEnumerator();
        _enumerator.MoveNext();
        _index = _enumerator.Current;
    }

    private void Iterate(IFramework framework)
    {
        CheckNewPeople(_index);
        CheckSamePerson(_index);

        var iteratorValid = _enumerator.MoveNext();
        if (!iteratorValid)
        {
            _enumerator = _enumerable.GetEnumerator();
            _enumerator.MoveNext();
        }
        _index = _enumerator.Current;
    }

    private static unsafe void CheckSamePerson(in int i)
    {
        foreach (var dict in ServiceManager.NaviMapManager.PersonDict)
        {
            if (!ServiceManager.NaviMapManager.SourceDataDict[dict.Key].Enabled) continue;
            if (!dict.Value.TryGetValue(i, out var person)) continue;

            var obj = ServiceManager.ObjectTable[i];
            if (obj is null)
            {
                ServiceManager.NaviMapManager.RemoveFromBag(person.Id, dict.Key);
                continue;
            }

            if (obj.ObjectKind is not ObjectKind.Player)
            {
                ServiceManager.NaviMapManager.RemoveFromBag(person.Id, dict.Key);
                continue;
            }

            if (ServiceManager.ObjectTable[i]?.Name.ToString() != person.Name)
            {
                ServiceManager.NaviMapManager.RemoveFromBag(person.Id, dict.Key);
            }
        }
    }

    private static void CheckNewPeople(in int i)
    {
        if (MarkerUtility.ChecksPassed)
        {
            LookFor(i);
        }
    }

    private static unsafe void LookFor(int i)
    {
        ServiceManager.Configuration.SourceConfigs.TryGetValue(FriendKey, out var friendConfig);
        ServiceManager.Configuration.SourceConfigs.TryGetValue(FcMembersKey, out var fcConfig);
        ServiceManager.Configuration.SourceConfigs.TryGetValue(EveryoneKey, out var everyoneConfig);

        var friendsEnabled = friendConfig?.Enabled is true;
        var fcEnabled = fcConfig?.Enabled is true;
        var everyoneEnabled = everyoneConfig?.Enabled is true;
        if (!friendsEnabled && !fcEnabled && !everyoneEnabled) return;

        var player = ServiceManager.ObjectTable.LocalPlayer;
        if (player is null) return;
        var playerPtr = (Character*)player.Address;

        var combat = playerPtr->InCombat;
        ServiceManager.NaviMapManager.InCombat = combat;
        if (combat) return;
        
        var fc = playerPtr->FreeCompanyTag;
        if (fc.IsEmpty) fcEnabled = false;

        var obj = ServiceManager.ObjectTable[i];
        if (obj is null) return;

        var ptr = (Character*)obj.Address;
        if (obj.ObjectKind is not ObjectKind.Player) return;
        if (ptr->IsAllianceMember || ptr->IsPartyMember) return;

        var personDict = ServiceManager.NaviMapManager.PersonDict;
        var alreadyInFriendBag = friendsEnabled && personDict.TryGetValue(FriendKey, out var friendDict) && friendDict.Any(x => x.Value.Id == obj.GameObjectId);
        var alreadyInFcBag = fcEnabled && personDict.TryGetValue(FcMembersKey, out var fcDict) && fcDict.Any(x => x.Value.Id == obj.GameObjectId);
        if (alreadyInFcBag && alreadyInFriendBag) return;

        if (friendsEnabled && !alreadyInFriendBag && ptr->IsFriend)
        {
            var personDetails = new PersonDetails(obj.Name.ToString(), obj.GameObjectId, FriendKey, i);
            alreadyInFriendBag = true;
            ServiceManager.NaviMapManager.AddToBag(personDetails);
        }

        if (fcEnabled && !alreadyInFcBag && fc.SequenceEqual(ptr->FreeCompanyTag))
        {
            var personDetails = new PersonDetails(obj.Name.ToString(), obj.GameObjectId, FcMembersKey, i);
            alreadyInFcBag = true;
            ServiceManager.NaviMapManager.AddToBag(personDetails);
        }

        if (everyoneEnabled && !alreadyInFcBag && !alreadyInFriendBag)
        {
            var personDetails = new PersonDetails(obj.Name.ToString(), obj.GameObjectId, EveryoneKey, i);
            ServiceManager.NaviMapManager.AddToBag(personDetails);
        }
    }

    public void Dispose()
    {
        ServiceManager.Framework.Update -= Iterate;
    }
}
