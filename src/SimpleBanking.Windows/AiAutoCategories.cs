namespace SimpleBanking;
internal sealed partial class MainWindow
{
    bool aiCategorizing;
    readonly Dictionary<string,HashSet<string>> aiCategoryQueue=new();
    CancellationTokenSource? aiCategoryCancellation;
    internal static bool NeedsAutomaticCategory(Entry e)=>!e.Pending&&!e.CategoryManual&&e.Category is "" or "Unkategorisiert" or "Sonstiges";
    internal async Task AutomaticallyCategorize(string accountId,ISet<string> newIds,CancellationToken cancellation=default){
        if(!Unlocked||!data.Ai.AutomaticCategories||newIds.Count==0)return;
        if(!aiCategoryQueue.TryGetValue(accountId,out var queued))aiCategoryQueue[accountId]=queued=[];queued.UnionWith(newIds);
        if(aiCategorizing)return;
        int epoch=Volatile.Read(ref securityEpoch);
        using var lifetime=CancellationTokenSource.CreateLinkedTokenSource(cancellation);aiCategoryCancellation=lifetime;cancellation=lifetime.Token;
        aiCategorizing=true;
        try{
          while(aiCategoryQueue.Count>0){
            var job=aiCategoryQueue.First();accountId=job.Key;newIds=job.Value;aiCategoryQueue.Remove(accountId);
            if(IsDisposed||!Unlocked||epoch!=Volatile.Read(ref securityEpoch)||!data.Ai.AutomaticCategories)return;
            var config=new AiConfiguration{Provider=data.Ai.Provider,Keys=new(data.Ai.Keys)};
            if(!demo&&(!config.Keys.TryGetValue(config.Provider,out var key)||string.IsNullOrWhiteSpace(key)))continue;
            var ruled=AssignmentRules.Sorted(data.Rules).Where(r=>r.Category!=null).SelectMany(r=>MatchRule(r,data.Entries.Where(e=>e.AccountId==accountId))).Select(e=>e.Id).ToHashSet();var candidates=data.Entries.Where(e=>e.AccountId==accountId&&newIds.Contains(e.Id)&&!ruled.Contains(e.Id)&&NeedsAutomaticCategory(e)).ToArray();
            foreach(var rows in candidates.Chunk(50)){
                if(!Unlocked||epoch!=Volatile.Read(ref securityEpoch)||!data.Ai.AutomaticCategories)return;cancellation.ThrowIfCancellationRequested();
                var categories=demo?Enumerable.Range(0,rows.Length).ToDictionary(i=>i,_=>"Sonstiges"):AiAssistant.ParseCategories(await AiAssistant.Complete(config,AiAssistant.CategoryPrompt,AiAssistant.Context(rows),cancellation),rows.Length);
                if(!Unlocked||epoch!=Volatile.Read(ref securityEpoch)||!data.Ai.AutomaticCategories)return;
                var previous=data.Entries.ToList();
                foreach(var pair in categories){int index=data.Entries.FindIndex(e=>e.AccountId==accountId&&e.Id==rows[pair.Key].Id);if(index>=0&&data.Entries[index]==rows[pair.Key]&&NeedsAutomaticCategory(data.Entries[index]))data.Entries[index]=data.Entries[index] with{Category=pair.Value};}
                try{Save();}catch{data.Entries=previous;throw;}Populate();
            }
          }
        }catch(OperationCanceledException){}catch(Exception){if(Unlocked&&!IsDisposed)status.Text="Umsätze gespeichert · Automatische KI-Zuordnung nicht abgeschlossen.";}
        finally{aiCategorizing=false;aiCategoryCancellation=null;aiCategoryQueue.Clear();}
    }
}
