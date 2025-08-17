
// ScrollRect���� ���̴� �׸� Ȱ��ȭ�� Canvas Rebuild �� ������� ���̴� ����

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
                // ��ũ�� �������� ���� �ְ�, Ȱ��ȭ �Ǿ� ���� ��
                if (obj.IsActiveComponents)
                    obj.ActivateObject(false);
            }

            else if (rectPosition < scrollRect.viewport.rect.yMin - targetHeight)
            {
                // ��ũ�� �������� �Ʒ��� �ְ�, Ȱ��ȭ �Ǿ� ���� ��
                if (obj.IsActiveComponents)
                {
                    isDeactiveAll = true;
                    obj.ActivateObject(false);
                }
            }
            // �簢�� ���ο� �ְ� ��Ȱ��ȭ �����̸�
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
                // ��ũ�� �������� ���� �ְ�, Ȱ��ȭ �Ǿ� ���� ��
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
                // ��ũ�� �������� �Ʒ��� �ְ�, Ȱ��ȭ �Ǿ� ���� ��
                if (UIObjects[i].IsActiveComponents)
                {
                    DeactiveAll(UIObjects, i, loopCnt);
                    break;
                }
            }
            // �簢�� ���ο� �ְ� ��Ȱ��ȭ �����̸�
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

            // �簢�� ���ο� �ְ� Ȱ��ȭ �����̸�
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
                // ��ũ�� �������� �����ʿ� �ְ�, Ȱ��ȭ �Ǿ� ���� ��
                if (UIObjects[i].IsActiveComponents)
                {
                    int end = Mathf.Min(i + countInGroup - 1, UIObjects.Length - 1);
                    for (var j = i; j <= end; j++)
                        UIObjects[j].ActivateObject(false);

                    i += countInGroup - 1;
                }
            }

            // ��ũ�ѿ������� ����, 
            else if(rectPosition < scrollRect.viewport.rect.xMin - targetWidth)
            {
                if (UIObjects[i].IsActiveComponents)
                    UIObjects[i].ActivateObject(false);
            }

            else
            {
                // �簢�� ���ο� �ְ� ��Ȱ��ȭ �����̸�
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
