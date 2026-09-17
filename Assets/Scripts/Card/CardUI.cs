using TMPro;
using System.Collections;
using UnityEngine.EventSystems;
using UnityEngine;
using UnityEngine.UI;

public class CardUI : MonoBehaviour, IPointerClickHandler
{
    [SerializeField] private Image artwork;
    [SerializeField] private TMP_Text costText;
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_Text descriptionText;
    [SerializeField] private TMP_Text typeText;

    [Header("Card BackImages")]
    [SerializeField] private Image backImage;
    [SerializeField] private Sprite dreamBackImage;
    [SerializeField] private Sprite nightmareBackImage;

    [Header("Card Faces")]
    [SerializeField] private GameObject front;
    [SerializeField] private GameObject back;

    [Header("Animation")]
    [SerializeField] private RectTransform cardVisual;
    [SerializeField] private float moveDuration = 0.35f;
    [SerializeField] private float flipDuration = 0.25f;

    private CardData cardData;
    private BattleManager battleManager;
    private CardHandController handController;

    private bool isFaceUp = true;
    private Coroutine drawCoroutine;

    public void SetFaceUp(bool faceUp)
    {
        isFaceUp = faceUp;

        front.SetActive(faceUp);
        back.SetActive(!faceUp);
    }

    public void PlayDrawAnimation(RectTransform startPoint)
    {
        if (drawCoroutine != null)
            StopCoroutine(drawCoroutine);

        drawCoroutine = StartCoroutine(
            DrawAnimation(startPoint));
    }

    private IEnumerator DrawAnimation(RectTransform startPoint)
    {
        SetFaceUp(false);

        cardVisual.localScale = Vector3.one;

        // 시작점의 월드 좌표를 CardVisual 부모 기준 좌표로 변환
        RectTransform visualParent =
            cardVisual.parent as RectTransform;

        Vector3 startLocalPosition =
            visualParent.InverseTransformPoint(startPoint.position);

        // 목표 위치는 Card 루트의 중심
        Vector3 targetLocalPosition = Vector3.zero;

        cardVisual.localPosition = startLocalPosition;

        float elapsed = 0f;

        while (elapsed < moveDuration)
        {
            elapsed += Time.deltaTime;

            float t = Mathf.Clamp01(elapsed / moveDuration);
            t = Mathf.SmoothStep(0f, 1f, t);

            cardVisual.localPosition =
                Vector3.Lerp(startLocalPosition, targetLocalPosition, t);

            yield return null;
        }

        cardVisual.localPosition = targetLocalPosition;

        // 뒷면을 접고 앞면으로 전환
        yield return FlipHalf(1f, 0f, flipDuration * 0.5f);

        SetFaceUp(true);

        yield return FlipHalf(0f, 1f, flipDuration * 0.5f);

        cardVisual.localScale = Vector3.one;
        drawCoroutine = null;
    }

    private IEnumerator FlipHalf(
        float from, float to, float duration)
    {
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;

            float t = Mathf.Clamp01(elapsed / duration);
            Vector3 scale = cardVisual.localScale;
            scale.x = Mathf.Lerp(from, to, t);
            cardVisual.localScale = scale;

            yield return null;
        }

        Vector3 finalScale = cardVisual.localScale;
        finalScale.x = to;
        cardVisual.localScale = finalScale;
    }

    public IEnumerator DiscardAnimation(RectTransform discardPoint)
    {
        SetFaceUp(false);

        Vector3 startPosition = cardVisual.position;
        Vector3 endPosition = discardPoint.position;

        float elapsed = 0f;

        while (elapsed < moveDuration)
        {
            elapsed += Time.deltaTime;

            float t = Mathf.Clamp01(elapsed / moveDuration);
            t = Mathf.SmoothStep(0f, 1f, t);

            cardVisual.position =
                Vector3.Lerp(startPosition, endPosition, t);

            yield return null;
        }

        cardVisual.position = endPosition;
    }

    public void SetCard(CardData data, BattleManager manager)
    {
        cardData = data;
        battleManager = manager;

        artwork.sprite = data.artwork;
        if (data.deckType == CardDeckType.TypeDream)
        {
            costText.text = data.cost.dreamCost.ToString();
            backImage.sprite = dreamBackImage;
        }
        if (data.deckType == CardDeckType.TypeNightmare)
        {
            costText.text = data.cost.nightmareCost.ToString();
            backImage.sprite = nightmareBackImage;
        }
        nameText.text = data.cardName;
        descriptionText.text = data.description;
        typeText.text = data.type.ToString();
    }

    public void SetHandController(CardHandController controller)
    {
        handController = controller;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        Debug.Log($"Card Clicked: {eventData.clickCount}");

        if (eventData.clickCount == 2)
        {
            Debug.Log("더블클릭 감지");
            UseCard();
        }
    }

    private void UseCard()
    {
        Debug.Log("UseCard 호출");

        if (cardData == null)
        {
            Debug.LogError("cardData가 null입니다.");
            return;
        }

        if (battleManager == null)
        {
            Debug.LogError("battleManager가 null입니다.");
            return;
        }

        Debug.Log($"카드 사용 요청: {cardData.cardName}");

        battleManager.UseCard(this);
    }

    public CardData GetCardData()
    {
        return cardData;
    }

    public CardHandController GetHandController()
    {
        return handController;
    }
}
