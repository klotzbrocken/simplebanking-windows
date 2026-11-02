namespace SimpleBanking;

internal static class RecurringAssignmentKeys
{
    internal static string Canonical(string group) {
        var parts=group.Split('|',2);var name=parts[0].Trim().ToLowerInvariant();
        return name is "unbekannt" or "(unbekannt)" && parts.Length==2 && parts[1].Trim().Length>0
            ?name+"|"+parts[1].Trim().ToLowerInvariant():name;
    }
    internal static string? Find(IReadOnlyDictionary<string,string>? assignments,string account,string group) {
        if(assignments==null)return null;
        // Existing exact assignments retain precedence; never merge unnamed counterparties.
        if(assignments.TryGetValue(account+"/"+group,out var exact))return exact;
        var prefix=account+"/";var key=Canonical(group);
        return assignments.FirstOrDefault(p=>p.Key.StartsWith(prefix,StringComparison.Ordinal)&&Canonical(p.Key[prefix.Length..])==key).Value;
    }
    internal static void Set(Dictionary<string,string> assignments,string account,string group,string value) {
        var prefix=account+"/";var canonical=Canonical(group);
        foreach(var old in assignments.Keys.Where(k=>k.StartsWith(prefix,StringComparison.Ordinal)&&Canonical(k[prefix.Length..])==canonical).ToArray())assignments.Remove(old);
        assignments[prefix+canonical]=value;
    }
}
