//----------------------------------------------------------------------------------------------------
// 목적 : ScrollRect 가시 영역 기반의 활성/비활성화로 Canvas Rbuild 및 DrawCall 최적화
// 
// 주요 기능
// - 세로/가로 스크롤 분리 함수 제공, 한줄 단위(CountInGroup) 배치로 루츠 수 축소(최적화)
// - UIObject 중 활성화된 오브젝트의 RectRransform 좌표를 활용해 '보이는 것만 활성화'하도록 구현
// - ShowSlotIndexList 반환으로 추가 연출 및 제어가 가능하도록 설계
// - 대규모 리스트, 그리드(인벤토리, 미리보기, 목록 등...)에서 성능을 확보하도록 함
//----------------------------------------------------------------------------------------------------

using CodeStage.AntiCheat.ObscuredTypes;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public static class ScrollRectOptimization
{
    //----------------------------------------------------------------------------------------------------
    // Methods
    //----------------------------------------------------------------------------------------------------
    #region Move Methods
    public static void MoveVerticalForChat(Queue<UICardChatting> objQueue, ScrollRect scrollRect, ObscuredInt countInGroup)
    {
        ObscuredBool isDeactiveAll = false;
        foreach (var obj in objQueue)
        {
            if(isDeactiveAll)
            {
                obj.ActivateObject(false);
                continue;
            }

            var rectPosition = obj.MyRectTransform.localPosition.y + scrollRect.content.localPosition.y;
            var targetHeight = obj.MyRectTransform.rect.height;
            if (rectPosition > scrollRect.viewport.rect.yMax + targetHeight)
            {
                // 스크롤 영역보다 위에 있고, 활성화 되어 있을 때
                if (obj.IsActiveComponents)
                    obj.ActivateObject(false);
            }

            else if (rectPosition < scrollRect.viewport.rect.yMin - targetHeight)
            {
                // 스크롤 영역보다 아래에 있고, 활성화 되어 있을 때
                if (obj.IsActiveComponents)
                {
                    isDeactiveAll = true;
                    obj.ActivateObject(false);
                }
            }
            // 사각형 내부에 있고 비활성화 상태이면
            else if (!obj.IsActiveComponents)
            {
                obj.ActivateObject(true);
            }
        }
    }

    public static void MoveVertical(UIObject[] UIObjects, ScrollRect scrollRect, ObscuredInt countInGroup, ref List<ObscuredInt> showedSlotIndexList)
    {
        showedSlotIndexList.Clear();

        ObscuredInt loopCnt = UIObjects.Length;
        for (var i = 0; i < loopCnt; i++)
        {
            if (!UIObjects[i].gameObject.activeSelf)
                break;

            var rectPosition = UIObjects[i].MyRectTransform.localPosition.y + scrollRect.content.localPosition.y;
            var targetHeight = UIObjects[i].MyRectTransform.rect.height * 0.5f;
            if (rectPosition > scrollRect.viewport.rect.yMax + targetHeight)
            {
                // 스크롤 영역보다 위에 있고, 활성화 되어 있을 때
                if (UIObjects[i].IsActiveComponents)
                {
                    int end = Mathf.Min(i + countInGroup - 1, UIObjects.Length - 1);
                    for (var j = i; j <= end; j++)
                        UIObjects[j].ActivateObject(false);

                    i += countInGroup - 1;
                }
            }
            else if (rectPosition < scrollRect.viewport.rect.yMin - targetHeight)
            {
                // 스크롤 영역보다 아래에 있고, 활성화 되어 있을 때
                if (UIObjects[i].IsActiveComponents)
                {
                    DeactiveAll(UIObjects, i, loopCnt);
                    break;
                }
            }
            // 사각형 내부에 있고 비활성화 상태이면
            else if(!UIObjects[i].IsActiveComponents)
            {
                showedSlotIndexList.Add(i);

                var targetNumber = i + countInGroup - 1;
                for (var j = i; j <= targetNumber; j++)
                {
                    UIObjects[j].ActivateObject(true);
                }

                i += countInGroup - 1;
                continue;
            }

            // 사각형 내부에 있고 활성화 상태이면
            else if(UIObjects[i].IsActiveComponents)
            {
                showedSlotIndexList.Add(i);

                i += countInGroup - 1;
            }
        }
    }

    public static void MoveHorizontal(UIObject[] UIObjects, ScrollRect scrollRect, ObscuredInt countInGroup, ref List<ObscuredInt> showedSlotIndexList)
    {
        showedSlotIndexList.Clear();

        ObscuredInt count = UIObjects.Length;
        for (var i = 0; i < count; i++)
        {
            if (!UIObjects[i].gameObject.activeSelf)
            {
                break;
            }

            var rectPosition = UIObjects[i].MyRectTransform.localPosition.x + scrollRect.content.localPosition.x;
            var targetWidth = UIObjects[i].MyRectTransform.rect.width * 0.5f;
            if (rectPosition > scrollRect.viewport.rect.xMax + targetWidth)
            {
                // 스크롤 영역보다 오른쪽에 있고, 활성화 되어 있을 때
                if (UIObjects[i].IsActiveComponents)
                {
                    int end = Mathf.Min(i + countInGroup - 1, UIObjects.Length - 1);
                    for (var j = i; j <= end; j++)
                        UIObjects[j].ActivateObject(false);

                    i += countInGroup - 1;
                }
            }

            // 스크롤영역보다 왼쪽, 
            else if(rectPosition < scrollRect.viewport.rect.xMin - targetWidth)
            {
                if (UIObjects[i].IsActiveComponents)
                    UIObjects[i].ActivateObject(false);
            }

            else
            {
                // 사각형 내부에 있고 비활성화 상태이면
                if (!UIObjects[i].IsActiveComponents)
                {
                    showedSlotIndexList.Add(i);

                    int end = Mathf.Min(i + countInGroup - 1, UIObjects.Length - 1);
                    for (var j = i; j <= end; j++)
                        UIObjects[j].ActivateObject(true);

                    i += countInGroup - 1;
                }

                else if (UIObjects[i].IsActiveComponents)
                {
                    showedSlotIndexList.Add(i);

                    i += countInGroup - 1;
                }
            }
        }
    }
    #endregion

    #region Activate Methods
    private static void DeactiveAll(UIObject[] UIObjects, int index, int maxIndex)
    {
        for (var i = index; i < maxIndex; i++)
            UIObjects[i].ActivateObject(false);
    }
    #endregion
}


