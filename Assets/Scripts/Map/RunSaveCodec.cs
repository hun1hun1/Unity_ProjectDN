using System;
using System.Collections.Generic;
using System.Linq;

public static class RunSaveCodec
{
    public static RunSaveData Capture(RunProgress progress,RunAssetCatalog catalog,RunRewardData rewards=null,string contentState="")
    {
        return new RunSaveData {progress=progress.Capture(catalog),inventory=CaptureInventory(catalog),hasRewards=rewards!=null,rewards=rewards,contentState=contentState??""};
    }
    public static RunInventoryData CaptureInventory(RunAssetCatalog catalog)
    {
        return new RunInventoryData {gold=RunGoldInventory.Gold,cardRemovals=RunGoldInventory.CardRemovals,decks=RunDeckInventory.Capture(catalog),relics=RunRelicInventory.Capture(catalog)};
    }
    public static void Validate(RunSaveData data,RunAssetCatalog catalog)
    {
        if(data==null || data.version!=1 || data.inventory==null)throw new InvalidOperationException("Invalid run save.");
        RunProgress progress=RunProgress.Restore(data.progress,catalog);
        ValidateInventory(data.inventory,catalog);
        if(progress.Phase==RunPhase.Rewards)
        {
            RunRewardData r=data.rewards;
            if(!data.hasRewards || r==null || r.gold<0 || r.offers==null || r.state<BattleRewardState.Summary || r.state>BattleRewardState.CardChoice ||
                r.goldAvailable&&r.gold==0 || (r.state!=BattleRewardState.Summary&&!r.cardAvailable))
                throw new InvalidOperationException("Invalid pending rewards.");
            CardData[] cards=r.offers.Select(id=>catalog.Resolve<CardData>(id)??throw new InvalidOperationException("Empty reward card.")).ToArray();
            if(r.state==BattleRewardState.CardChoice && (cards.Length<2||cards.Length>3||cards.Distinct().Count()!=cards.Length||cards.Any(c=>c.deckType!=cards[0].deckType)))
                throw new InvalidOperationException("Invalid reward offers.");
            if(r.state!=BattleRewardState.CardChoice && cards.Length!=0)throw new InvalidOperationException("Unexpected reward offers.");
        }
        else if(data.hasRewards)throw new InvalidOperationException("Unexpected reward state.");
    }
    public static void ValidateInventory(RunInventoryData data,RunAssetCatalog catalog)
    {
        if(data==null || data.gold<0 || data.cardRemovals<0 || data.decks==null || data.relics==null || data.decks.Length>100 || data.relics.Length>1000)
            throw new InvalidOperationException("Invalid inventory.");
        var keys=new HashSet<string>();
        foreach(RunDeckData deck in data.decks)
        {
            if(deck==null || deck.dream==null || deck.nightmare==null || deck.dream.Length>10000 || deck.nightmare.Length>10000 ||
                !keys.Add(deck.dreamDatabase+"/"+deck.nightmareDatabase))throw new InvalidOperationException("Invalid saved deck.");
            catalog.Resolve<CardDatabase>(deck.dreamDatabase);catalog.Resolve<CardDatabase>(deck.nightmareDatabase);
            foreach(string id in deck.dream)if(catalog.Resolve<CardData>(id)?.deckType!=CardDeckType.TypeDream)throw new InvalidOperationException("Invalid dream card.");
            foreach(string id in deck.nightmare)if(catalog.Resolve<CardData>(id)?.deckType!=CardDeckType.TypeNightmare)throw new InvalidOperationException("Invalid nightmare card.");
        }
        keys.Clear();long bonus=0;
        foreach(RunRelicData relic in data.relics)
        {
            if(relic==null || relic.bonus<=0 || !keys.Add(relic.id) || catalog.Resolve<AmmoCapacityRelic>(relic.id)==null)
                throw new InvalidOperationException("Invalid saved relic.");
            bonus+=relic.bonus;
        }
        if(bonus>int.MaxValue)throw new InvalidOperationException("Invalid relic bonus.");
    }
    public static void RestoreInventory(RunInventoryData data,RunAssetCatalog catalog)
    {
        ValidateInventory(data,catalog);
        RunDeckInventory.Restore(data.decks,catalog);
        RunGoldInventory.Restore(data.gold,data.cardRemovals);
        RunRelicInventory.Restore(data.relics,catalog);
    }
}
