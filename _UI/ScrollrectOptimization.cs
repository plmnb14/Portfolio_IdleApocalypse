//----------------------------------------------------------------------------------------------------
// 가로, 세로 ScrollRect 최적화를 위한 Class 입니다.
//
// ScrollRect에서 보이는 항목만 활성화해 Canvas Rebuild 및 오버헤드 줄이는 것이 목적입니다.
// 한 줄에 보이는 개수(countInGroup)을 설정해 반복 연산량을 줄였습니다.
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

