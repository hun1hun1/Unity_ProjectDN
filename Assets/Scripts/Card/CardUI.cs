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
    [SerializeField] private float discardShrinkDuration = 0.01f;

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

    //public IEnumerator DiscardAnimation(RectTransform discardPoint)
    //{
    //    SetFaceUp(false);

    //    Vector3 startPosition = cardVisual.position;
    //    Vector3 endPosition = discardPoint.position;

    //    float elapsed = 0f;

    //    while (elapsed < moveDuration)
    //    {
    //        elapsed += Time.deltaTime;

    //        float t = Mathf.Clamp01(elapsed / moveDuration);
    //        t = Mathf.SmoothStep(0f, 1f, t);

    //        cardVisual.position =
    //            Vector3.Lerp(startPosition, endPosition, t);

    //        yield return null;
    //    }

    //    cardVisual.position = endPosition;
    //}

    public IEnumerator DiscardAnimation(RectTransform discardPilePoint)
    {
        if (cardVisual == null || discardPilePoint == null)
            yield break;

        // 현재 위치와 크기 저장
        Vector3 startPosition = cardVisual.position;
        Vector3 startScale = cardVisual.localScale;

        // 버림 더미의 위치
        Vector3 endPosition = discardPilePoint.position;

        // 버림 더미의 크기를 기준으로 목표 크기 계산
        Vector3 targetScale = CalculateTargetScale(discardPilePoint);

        // -------------------------------------------------
        // 1. 카드 뒷면으로 변경
        // -------------------------------------------------

        SetFaceUp(false);

        // -------------------------------------------------
        // 2. 카드 크기 축소
        // -------------------------------------------------

        float elapsed = 0f;

        while (elapsed < discardShrinkDuration)
        {
            elapsed += Time.deltaTime;

            float t = Mathf.Clamp01(
                elapsed / discardShrinkDuration
            );

            t = Mathf.SmoothStep(0f, 1f, t);

            cardVisual.localScale =
                Vector3.Lerp(startScale, targetScale, t);

            yield return null;
        }

        cardVisual.localScale = targetScale;

        // -------------------------------------------------
        // 3. 버림 더미 위치로 이동
        // -------------------------------------------------

        elapsed = 0f;

        while (elapsed < moveDuration)
        {
            elapsed += Time.deltaTime;

            float t = Mathf.Clamp01(
                elapsed / moveDuration
            );

            t = Mathf.SmoothStep(0f, 1f, t);

            cardVisual.position =
                Vector3.Lerp(startPosition, endPosition, t);

            yield return null;
        }

        cardVisual.position = endPosition;
    }

    private Vector3 CalculateTargetScale(
        RectTransform discardPilePoint)
    {
        RectTransform cardRect = cardVisual;

        // 카드와 버림 더미의 실제 크기 비율
        float scaleX =
            discardPilePoint.rect.width /
            cardRect.rect.width;

        float scaleY =
            discardPilePoint.rect.height /
            cardRect.rect.height;

        float scale = Mathf.Min(scaleX, scaleY);

        return new Vector3(
            scale,
            scale,
            1f
        );
    }

    public IEnumerator UsedCardAnimation(
    RectTransform cardUsingPoint,
    RectTransform discardPilePoint)
    {
        if (cardVisual == null ||
            cardUsingPoint == null ||
            discardPilePoint == null)
        {
            yield break;
        }

        // 카드 사용 연출 중에는 카드 뒷면을 표시
        SetFaceUp(true);

        // 1. CardUsingArea로 이동
        Vector3 startPosition = cardVisual.position;
        Vector3 usingPosition = cardUsingPoint.position;

        // 시작 스케일
        Vector3 startScale = cardVisual.localScale;

        // CardUsingArea에 맞는 목표 스케일 계산
        RectTransform usingArea = cardUsingPoint;

        float scaleX =
            usingArea.rect.width / cardVisual.rect.width;

        float scaleY =
            usingArea.rect.height / cardVisual.rect.height;

        // 카드 비율을 유지하면서 영역 안에 맞춤
        float targetScaleFactor = Mathf.Min(scaleX, scaleY);

        Vector3 targetScale =
            startScale * targetScaleFactor;

        float elapsed = 0f;

        while (elapsed < moveDuration)
        {
            elapsed += Time.deltaTime;

            float t = Mathf.Clamp01(elapsed / moveDuration);
            t = Mathf.SmoothStep(0f, 1f, t);

            cardVisual.position = Vector3.Lerp(
                startPosition,
                usingPosition,
                t
            );

            // 크기 축소
            cardVisual.localScale = Vector3.Lerp(
                startScale,
                targetScale,
                t
            );

            yield return null;
        }

        cardVisual.position = usingPosition;
        cardVisual.localScale = targetScale;

        // 2. CardUsingArea에 도착한 뒤 기존 버림 애니메이션 실행
        yield return StartCoroutine(
            DiscardAnimation(discardPilePoint)
        );
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
