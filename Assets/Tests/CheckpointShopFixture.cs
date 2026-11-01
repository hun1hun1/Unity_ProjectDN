#if UNITY_EDITOR
using System;
using UnityEngine;

public sealed class CheckpointShopFixture : MapContentHandler
{
    [Serializable] private sealed class State {public int stock;public string offer;}
    public override MapContentType ContentType=>MapContentType.Shop;
    public int Stock {get;private set;}
    public string Offer {get;private set;}
    private RunProgress progress;
    private RunNodeVisit visit;
    private Action completed;
    public override void Begin(MapChoice choice,RunProgress current,Action onComplete)
    {progress=current;visit=CaptureVisit(current);completed=onComplete;Stock=1;Offer="fixed-offer-"+current.NodeSeed;}
    public override string CaptureState()=>JsonUtility.ToJson(new State {stock=Stock,offer=Offer});
    public override void RestoreState(string json)
    {if(string.IsNullOrEmpty(json))return;var s=JsonUtility.FromJson<State>(json);Stock=s.stock;Offer=s.offer;}
    public bool Purchase(DeckController deck,CardData card,AmmoCapacityRelic relic)
    {
        if(Stock<=0 || RunGoldInventory.Gold<25)return false;
        return CommitAcquisition(visit,()=>
        {
            int remaining=RunGoldInventory.Gold-25;RunGoldInventory.ResetForNewGame();RunGoldInventory.Add(remaining);
            deck.AddCardToDeck(card,card.deckType);relic.Acquire();Stock--;
        });
    }
    public void Complete() {completed();}
}


#endif
