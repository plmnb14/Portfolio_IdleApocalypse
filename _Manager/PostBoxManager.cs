//----------------------------------------------------------------------------------------------------
// 우편 Manager 입니다.
//
// 유저 로그인 우편 목록 1회 갱신 후, 별도의 우편 콜백이 있을 때까지 우편을 불러오지 않도록 설정했습니다.
//----------------------------------------------------------------------------------------------------

using BackEnd;
using System.Collections.Generic;
using UnityEngine;
using System;
using LitJson;
using CodeStage.AntiCheat.ObscuredTypes;
using System.Reflection;
using System.Collections;

public class PostBoxManager : Singleton<PostBoxManager>
{
    //----------------------------------------------------------------------------------------------------
    // Fields
    //----------------------------------------------------------------------------------------------------
    #region Const Fields
    public const int MAX_POSTTYPE_CNT = 4;
    public const int MAX_POSTBOX_SIZE = 50;
    #endregion

    #region Serialize Fields
    [SerializeField] private UIPopUpPostBox postBoxPopUpUI;
    #endregion

    #region Property Fields
    public List<UPostItem>[] PreviewPostItemLists { get; private set; } = new List<UPostItem>[]
        {
            new(), new(), new(), new()
        };

    public List<UPostItem>[] ReceiveAllPostItemLists { get; private set; } = new List<UPostItem>[]
    {
            new(), new(), new(), new()
    };

    public ObscuredBool NeedRefreshPostList { get; private set; } = true;
    #endregion

    //----------------------------------------------------------------------------------------------------
    // Methods
    //----------------------------------------------------------------------------------------------------r
    #region Awake Methods
    private void Awake()
    {
        if (CheckOverlap())
            DontDestroy();
    }
    #endregion

    #region Refresh List Methods
    public void GetPostList(AfterBackendLoadFunc afterBackendLoadFunc, PostType postType)
    {
        ObscuredBool isSuccess = false;
        ObscuredString errorInfo = string.Empty;
        ObscuredString className = GetType().Name;
        ObscuredString funcName = MethodBase.GetCurrentMethod()?.Name;

        SendQueue.Enqueue(Backend.UPost.GetPostList, postType, MAX_POSTBOX_SIZE, callback =>
        {
            try
            {
                MyDebug.ServerLog($"Backend.UPost.GetPostList \n{callback}");

                var typeIdx = (int)postType;
                PreviewPostItemLists[typeIdx].Clear();

                if (callback.IsSuccess())
                {
                    var jsonData = callback.GetReturnValuetoJSON()["postList"];
                    var loopCnt = jsonData.Count;

                    for(var i = 0; i < loopCnt; i++)
                    {
                        UPostItem postItem = new()
                        {
                            PostType = postType,
                            Title = jsonData[i]["title"].ToString(),
                            InDate = jsonData[i]["inDate"].ToString()
                        };

                        if(postType == PostType.Admin || postType == PostType.Rank)
                        {
                            postItem.Content = jsonData[i]["content"].ToString();
                            postItem.ExpirationDate = DateTime.Parse(jsonData[i]["expirationDate"].ToString());
                            postItem.ReservationDate = DateTime.Parse(jsonData[i]["reservationDate"].ToString());
                            postItem.Nickname = jsonData[i]["nickname"]?.ToString();
                            postItem.SentDate = DateTime.Parse(jsonData[i]["sentDate"].ToString());

                            if(jsonData[i].ContainsKey("author"))
                                postItem.Author = jsonData[i]["author"].ToString();

                            if (jsonData[i].ContainsKey("rankType"))
                                postItem.RankType = jsonData[i]["rankType"].ToString();
                        }

                        if (jsonData[i]["items"].Count > 0)
                        {
                            var itemCount = jsonData[i]["items"].Count;
                            for (var j = 0; j < itemCount; j++)
                            {
                                UPostChartItem _chartItem = new()
                                {
                                    ItemCount = int.Parse(jsonData[i]["items"][j]["itemCount"].ToString()),
                                    ChartFileName = jsonData[i]["items"][j]["item"]["chartFileName"].ToString(),
                                    ItemID = jsonData[i]["items"][j]["item"]["ItemID"].ToString(),
                                };

                                postItem.ItemList.Add(_chartItem);
                            }
                        }

                        PreviewPostItemLists[typeIdx].Add(postItem);
                        UIManager.Instance.AlertTopOutMenu(TopOuterMenuType.PostBox, true);
                    }
                }

                isSuccess = true;
            }

            catch (Exception e)
            {
                errorInfo = e.Message;
            }

            finally
            {
                afterBackendLoadFunc(isSuccess, className, funcName, errorInfo);
            }
        });
    }

    public void GetPostListStandAlone(PostType postType, Action afterFunc)
    {
        var _typeIdx = (int)postType;
        PreviewPostItemLists[_typeIdx].Clear();

        SendQueue.Enqueue(Backend.UPost.GetPostList, postType, MAX_POSTBOX_SIZE, callback =>
        {
            MyDebug.ServerLog($"Backend.UPost.GetPostList \n{callback}");

            if (callback.IsSuccess())
            {
                var jsonData = callback.GetReturnValuetoJSON()["postList"];
                var loopCnt = jsonData.Count;
                for (var i = 0; i < loopCnt; i++)
                {
                    UPostItem postItem = new()
                    {
                        PostType = postType,
                        Title = jsonData[i]["title"].ToString(),
                        InDate = jsonData[i]["inDate"].ToString()
                    };

                    if (postType == PostType.Admin || postType == PostType.Rank)
                    {
                        postItem.Content = jsonData[i]["content"].ToString();
                        postItem.ExpirationDate = DateTime.Parse(jsonData[i]["expirationDate"].ToString());
                        postItem.ReservationDate = DateTime.Parse(jsonData[i]["reservationDate"].ToString());
                        postItem.Nickname = jsonData[i]["nickname"]?.ToString();
                        postItem.SentDate = DateTime.Parse(jsonData[i]["sentDate"].ToString());

                        if (jsonData[i].ContainsKey("author"))
                            postItem.Author = jsonData[i]["author"].ToString();

                        if (jsonData[i].ContainsKey("rankType"))
                            postItem.RankType = jsonData[i]["rankType"].ToString();
                    }

                    if (jsonData[i]["items"].Count > 0)
                    {
                        var itemCount = jsonData[i]["items"].Count;
                        for (var j = 0; j < itemCount; j++)
                        {
                            UPostChartItem chartItem = new()
                            {
                                ItemCount = int.Parse(jsonData[i]["items"][j]["itemCount"].ToString()),
                                ChartFileName = jsonData[i]["items"][j]["item"]["chartFileName"].ToString(),
                                ItemID = jsonData[i]["items"][j]["item"]["ItemID"].ToString(),
                            };

                            postItem.ItemList.Add(chartItem);
                        }
                    }

                    PreviewPostItemLists[_typeIdx].Add(postItem);
                    UIManager.Instance.AlertTopOutMenu(TopOuterMenuType.PostBox, true);
                }
            }

            afterFunc?.Invoke();
        });
    }
    #endregion

    #region Receive Methods
    private ObscuredBool CheckCanReceiveAll()
    {
        ObscuredBool returnValue = false;

        foreach(var postlist in PreviewPostItemLists)
        {
            if(postlist.Count > 0)
            {
                returnValue = true;
                break;
            }
        }

        return returnValue;
    }

    public void ReceivePostItem(UPostItem postItem)
    {
        MessageNotificationManager.Instance.EnableServerConnectingScreen(true, LocalDB_Text.RECEIVING_POST);

        SendQueue.Enqueue(Backend.UPost.ReceivePostItem, postItem.PostType, postItem.InDate.GetDecrypted(), callback =>
        {
            try
            {
                if(callback.IsSuccess())
                {
                    var jsonData = callback.GetReturnValuetoJSON()["postItems"];
                    var itemCnt = jsonData.Count;
                    RewardItemDB rewardItemDB = new();
                    for(var i = 0; i < itemCnt; i++)
                    {
                        rewardItemDB.rewardItemIdList.Add(jsonData[i]["item"]["ItemID"].ToString());
                        rewardItemDB.rewardItemCntList.Add(int.Parse(jsonData[i]["itemCount"].ToString()));
                    }
                    rewardItemDB.GenerateRewardPaidTypeAll(ItemCntType.Free);


                    if (rewardItemDB.rewardItemIdList.Count > 0)
                    {
                        ItemManager.Instance.AddItems(rewardItemDB, false);
                        SaveDataManager.Instance.SaveAllGameData(false, false, () =>
                        {
                            PreviewPostItemLists[(int)postItem.PostType].Remove(postItem);

                            MessageNotificationManager.Instance.EnableServerConnectingScreen(false, string.Empty);
                            MessageNotificationManager.Instance.ShowRewardMessageNotification(rewardItemDB);
                            ToggleReceiveAllButtonUI();
                            postBoxPopUpUI.SetUpOnActivate();
                        });
                    }

                    else
                    {
                        PreviewPostItemLists[(int)postItem.PostType].Remove(postItem);

                        MessageNotificationManager.Instance.EnableServerConnectingScreen(false, string.Empty);
                        ToggleReceiveAllButtonUI();
                        postBoxPopUpUI.SetUpOnActivate();
                    }
                }
            }

            catch (Exception e)
            {
                var errorString = string.Format(LocalDB_Text.POST_RECEVING_ERROR, e.ToString());
                MessageNotificationManager.Instance.ShowMessageSystemError(errorString);
            }
        });
    }

    #region Initialize Step Fields
    private delegate void BackendLoadStep();
    private readonly Queue<BackendLoadStep> _initializeStep = new();
    #endregion

    public void ReceivePostItemAll(Action afterFunc = null)
    {
        if (CheckCanReceiveAll())
        {
            MessageNotificationManager.Instance.EnableServerConnectingScreen(true, LocalDB_Text.RECEIVING_POST);

            _initializeStep.Clear();
            _initializeStep.Enqueue(() => { ReceiveAll(NextStepRecieve, PostType.Admin); });
            _initializeStep.Enqueue(() => { ReceiveAll(NextStepRecieve, PostType.Rank); });

            NextStepRecieve(true, string.Empty, string.Empty, string.Empty);
        }

        else
            MessageNotificationManager.Instance.ShowInstanceMessageNotification(LocalDB_Text.NO_POST_TO_RECEIVE);
    }

    private void NextStepRecieve(ObscuredBool isSuccess, string className, string functionName, string errorInfo)
    {
        if (isSuccess)
        {
            if (_initializeStep.Count > 0)
                _initializeStep.Dequeue().Invoke();

            else
                GenerateAndRecievePostItemList();
        }

        else
            Debug.Log($"실패했습니다. : {className} / {functionName} / {errorInfo}");
    }

    private void NextStepActive(ObscuredBool isSuccess, string className, string functionName, string errorInfo)
    {
        if (isSuccess)
        {
            if (_initializeStep.Count > 0)
                _initializeStep.Dequeue().Invoke();

            else
            {
                NeedRefreshPostList = false;

                MessageNotificationManager.Instance.EnableServerConnectingScreen(false, string.Empty);
                postBoxPopUpUI.AddPopUpUI();
                ToggleReceiveAllButtonUI();
            }
        }

        else
            Debug.Log($"실패했습니다. : {className} / {functionName} / {errorInfo}");
    }

    private void NextStepOnlyRefresh(ObscuredBool isSuccess, string className, string functionName, string errorInfo)
    {
        if (isSuccess)
        {
            if (_initializeStep.Count > 0)
                _initializeStep.Dequeue().Invoke();

            else
                NeedRefreshPostList = false;
        }

        else
            Debug.Log($"실패했습니다. : {className} / {functionName} / {errorInfo}");
    }

    private void ToggleReceiveAllButtonUI()
    {
        ObscuredBool activeRecieveButton = false;

        if (!CheckPostBoxEmpty(PostType.Admin) || !CheckPostBoxEmpty(PostType.Rank))
            activeRecieveButton = true;

        postBoxPopUpUI.OnToggleReceiveAllButtonUI(activeRecieveButton);
    }

    private void ReceiveAll(AfterBackendLoadFunc afterBackendLoadFunc, PostType postType)
    {
        ObscuredBool isSuccess = false;
        ObscuredString errorInfo = string.Empty;
        ObscuredString className = GetType().Name;
        ObscuredString funcName = MethodBase.GetCurrentMethod()?.Name;

        ReceiveAllPostItemLists[(int)postType].Clear();

        SendQueue.Enqueue(Backend.UPost.ReceivePostItemAll, postType, callback =>
        {
            try
            {
                Debug.Log($"Backend.UPost.ReceivePostItemAll \n{callback}");

                if (callback.IsSuccess())
                {
                    var  receiveBro = callback.GetReturnValuetoJSON()["postItems"];

                    foreach (JsonData jsonData in receiveBro)
                    {
                        UIManager.Instance.AlertTopOutMenu(TopOuterMenuType.PostBox, false);

                        UPostItem postItem = new()
                        {
                            PostType = postType,
                        };

                        var loopCnt = jsonData.Count;
                        for (var i = 0; i < loopCnt; i++)
                        {
                            if (!jsonData[i].ContainsKey("item"))
                                continue;

                            UPostChartItem postChartItem = new();

                            if (jsonData[i]["item"].ContainsKey("chartFileName"))
                                postChartItem.ChartFileName = jsonData[i]["item"]["chartFileName"].ToString();

                            if (jsonData[i]["item"].ContainsKey("ItemID"))
                                postChartItem.ItemID = jsonData[i]["item"]["ItemID"].ToString();

                            if (jsonData[i].ContainsKey("itemCount"))
                                postChartItem.ItemCount = int.Parse(jsonData[i]["itemCount"].ToString());

                            postItem.ItemList.Add(postChartItem);
                        }

                        if(loopCnt > 0)
                            ReceiveAllPostItemLists[(int)postType].Add(postItem);
                    }
                }

                else
                {
                    for (var i = 0; i < PreviewPostItemLists.Length; i++)
                        PreviewPostItemLists[i].Clear();

                    ToggleReceiveAllButtonUI();
                }

                isSuccess = true;
            }

            catch (Exception e)
            {
                errorInfo = e.Message;
            }

            finally
            {
                afterBackendLoadFunc(isSuccess, className, funcName, errorInfo);
            }
        });
    }

    private void GenerateAndRecievePostItemList()
    {
        RewardItemDB rewardItemDB = new();
        foreach (var list in ReceiveAllPostItemLists)
        {
            foreach(var post in list)
            {
                foreach(var rewardItem in post.ItemList)
                {
                    rewardItemDB.rewardItemIdList.Add(rewardItem.ItemID);
                    rewardItemDB.rewardItemCntList.Add(rewardItem.ItemCount);
                }
            }
        }
        rewardItemDB.GenerateRewardPaidTypeAll(ItemCntType.Free);

        if (rewardItemDB.rewardItemIdList.Count > 0)
        {
            ItemManager.Instance.AddItems(rewardItemDB, false);
            SaveDataManager.Instance.SaveAllGameData(false, false, ()=>
            {
                var cnt = ReceiveAllPostItemLists.Length;
                for(var i = 0; i < cnt; i++)
                {
                    ReceiveAllPostItemLists[i].Clear();
                    PreviewPostItemLists[i].Clear();
                }

                MessageNotificationManager.Instance.EnableServerConnectingScreen(false, string.Empty);
                MessageNotificationManager.Instance.ShowRewardMessageNotification(rewardItemDB);
                postBoxPopUpUI.SetUpOnActivate();
            });
        }

        else
        {
            MessageNotificationManager.Instance.EnableServerConnectingScreen(false, string.Empty);
            MessageNotificationManager.Instance.ShowInstanceMessageNotification(LocalDB_Text.NO_REWARD_TO_RECEIVE);
            postBoxPopUpUI.SetUpOnActivate();
        }
    }

    private ObscuredBool CheckPostBoxEmpty(PostType postType)
    {
        return PreviewPostItemLists[(int)postType].Count <= 0;
    }
    #endregion

    #region PostBox Methods
    public void SetUpOnActivatePostBox() { postBoxPopUpUI.SetUpOnActivate(); }
    #endregion

    #region Open Methods
    public void OpenPostBoxPopUpUI() { RefreshPostListOnActivate(); }

    public void RefreshPostListOnActivate()
    {
        _initializeStep.Clear();

        if (NeedRefreshPostList)
        {
            MessageNotificationManager.Instance.EnableServerConnectingScreen(true, string.Empty);

            _initializeStep.Enqueue(() => { GetPostList(NextStepActive, PostType.Admin); });
            _initializeStep.Enqueue(() => { GetPostList(NextStepActive, PostType.Rank); });
        }

        NextStepActive(true, string.Empty, string.Empty, string.Empty);
    }
    
    public void SetUpPaidProductPost()
    {
        var paidProductList = PaidStoreManager.Instance.PaidStore_SaveData.PaidProductList;
        if (paidProductList == null || paidProductList.Count <= 0)
            return;

        var loopCnt = paidProductList.Count;
        for (var i = 0; i < loopCnt; i++)
            UIManager.Instance.AlertTopOutMenu(TopOuterMenuType.PostBox, true);
    }

    public void SetUpNewPostCreate()
    {
        NeedRefreshPostList = true;
        RefreshPostListOnlyRefresh();
    }

    private WaitForSeconds _waitForPost = new(1.0f);
    public IEnumerator SetUpNewPostCreate_Delay()
    {
        yield return _waitForPost;

        SetUpNewPostCreate();
    }

    private void RefreshPostListOnlyRefresh()
    {
        _initializeStep.Clear();

        if (NeedRefreshPostList)
        {
            _initializeStep.Enqueue(() => { GetPostList(NextStepOnlyRefresh, PostType.Admin); });
            _initializeStep.Enqueue(() => { GetPostList(NextStepOnlyRefresh, PostType.Rank); });
        }

        NextStepOnlyRefresh(true, string.Empty, string.Empty, string.Empty);
    }
    #endregion
}

public class UPostItem
{
    #region Fields
    public PostType PostType { get; set; }

    public ObscuredString Title { get; set; }
    public ObscuredString Content { get; set; }

    public DateTime ExpirationDate { get; set; }
    public DateTime ReservationDate { get; set; }
    public DateTime SentDate { get; set; }

    public ObscuredString Nickname { get; set; }
    public ObscuredString InDate { get; set; }
    public ObscuredString Author { get; set; }
    public ObscuredString RankType { get; set; }

    public List<UPostChartItem> ItemList { get; set; } = new();
    #endregion

    #region Methods
    public override string ToString()
    {
        string totalString =
        $"title : {Title}\n" +
        $"inDate : {InDate}\n";

        if (PostType == PostType.Admin || PostType == PostType.Rank)
        {
            totalString +=
            $"content : {Content}\n" +
            $"expirationDate : {ExpirationDate}\n" +
            $"reservationDate : {ReservationDate}\n" +
            $"sentDate : {SentDate}\n" +
            $"nickname : {Nickname}\n";

            if (PostType == PostType.Admin)
                totalString += $"author : {Author}\n";

            if (PostType == PostType.Rank)
                totalString += $"rankType : {RankType}\n";
        }

        string itemListString = string.Empty;
        for (var i = 0; i < ItemList.Count; i++)
        {
            itemListString += ItemList[i].ToString();
            itemListString += "\n";
        }
        totalString += itemListString;

        return totalString;
    }
    #endregion
}

public class UPostChartItem
{
    #region Fields
    public ObscuredString ChartFileName { get; set; }
    public ObscuredString ItemID { get; set; }
    public ObscuredString ItemName { get; set; }
    public ObscuredLong ItemCount { get; set; }
    #endregion

    #region Methods
    public override string ToString()
    {
        return
        "item : \n" +
        $"| chartFileName : {ChartFileName}\n" +
        $"| itemID : {ItemID}\n" +
        $"| itemName : {ItemName}\n" +
        $"| itemCount : {ItemCount}\n";
    }
    #endregion

}

