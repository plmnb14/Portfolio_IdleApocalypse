// 이 파일은 제출 편의를 위해 일부 구간을 발췌했습니다.

using BackEnd;
using CodeStage.AntiCheat.ObscuredTypes;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SaveDataManager : Singleton<SaveDataManager>
{
    //----------------------------------------------------------------------------------------------------
    // ETC
    //----------------------------------------------------------------------------------------------------
    #region Enum
    public enum SaveDataType_Server
    {
        PaidStore, UserInfo, SkillInventory, UserRank, AdvancedInfo,
        End
    }
    #endregion


    #region Delegate
    public delegate void AfterUpdateFunc(BackendReturnObject callBack);
    #endregion


    //----------------------------------------------------------------------------------------------------
    // Fields
    //----------------------------------------------------------------------------------------------------
    #region Serialize Fields
    [Space()]
    [Header("   - Developer Options")]
    [SerializeField] private ObscuredBool addDeveloperCurrency;
    #endregion

    #region Property Fields
    public ObscuredBool PauseAutoSave { get; set; } = false;
    public ObscuredBool PauseManuelSave { get; set; } = false;
    public ObscuredBool ErrorForcedPauseGame { get; set; } = false;
    #endregion

    #region Private Fields
    private readonly string[] dataTableNames = new string[]
        {
            // 2025-09-16 : 실시간으로 적용중인 테이블 이름이라 변경
            "User_PaidStoreData", "User_UserInfoData", "User_SkillInventoryData", "User_Rank", "User_AdvancedInfo"
        };

    public Dictionary<SaveDataType_Server, GameSaveData> SaveDataDict { get; private set; } = new();
    #endregion


    //----------------------------------------------------------------------------------------------------
    // Methods
    //----------------------------------------------------------------------------------------------------
    #region Awake Methods
    private void Awake()
    {
        if(CheckOverlap())
        {
            DontDestroy();
        }
    }
    #endregion

    #region Get Methods
    public string GetTableName(SaveDataType_Server saveDataType)
    {
        var idx = (int)saveDataType;
        return dataTableNames[idx];
    }
    #endregion

    #region SetUp Methods
    public void SetUpGameSaveData()
    {
        var enumIdx = SaveDataType_Server.UserInfo;
        SaveDataDict.Add(enumIdx, new UserInfo_GameSaveData(enumIdx) { TableName = dataTableNames[(int)enumIdx] });

        enumIdx = SaveDataType_Server.SkillInventory;
        SaveDataDict.Add(enumIdx, new SkillInventory_GameSaveData(enumIdx) { TableName = dataTableNames[(int)enumIdx] });

        enumIdx = SaveDataType_Server.PaidStore;
        SaveDataDict.Add(enumIdx, new PaidStore_GameSaveData(enumIdx) { TableName = dataTableNames[(int)enumIdx] });

        enumIdx = SaveDataType_Server.UserRank;
        SaveDataDict.Add(enumIdx, new UserRank_GameServerData(enumIdx) { TableName = dataTableNames[(int)enumIdx] });

        enumIdx = SaveDataType_Server.AdvancedInfo;
        SaveDataDict.Add(enumIdx, new AdvancedInfo_GameSaveData(enumIdx) { TableName = dataTableNames[(int)enumIdx] });
    }

    public void ReSetUpForLogOut()
    {
        SaveDataDict.Clear();
        SetUpGameSaveData();
    }

    public void SetUpGameSaveDataToManagers()
    {
        var enumIdx = SaveDataType_Server.UserInfo;
        PlayerInfoManager.Instance.SetUpSaveData(SaveDataDict[enumIdx]);
        TimeManager.Instance.SetUpSaveData(SaveDataDict[enumIdx]);
        EquipmentManager.Instance.SetUpSaveData(SaveDataDict[enumIdx]);
        PlayerTitleManager.Instance.SetUpSaveData(SaveDataDict[enumIdx]);
        SkillManager.Instance.SetUp_SaveUserData(SaveDataDict[enumIdx]);
        StageManager.Instance.SetUpSaveData(SaveDataDict[enumIdx]);
        AchievementManager.Instance.SetUpSaveData(SaveDataDict[enumIdx]);
        CurrencyManager.Instance.SetUpSaveData(SaveDataDict[enumIdx]);
        ContentsManager.Instance.SetUpSaveData(SaveDataDict[enumIdx]);

        enumIdx = SaveDataType_Server.SkillInventory;
        CoreStoneManager.Instance.SetUp_SkillCoreSaveData(SaveDataDict[enumIdx]);
        SkillManager.Instance.SetUpSaveData(SaveDataDict[enumIdx]);

        enumIdx = SaveDataType_Server.PaidStore;
        PaidStoreManager.Instance.SetUpSaveData(SaveDataDict[enumIdx]);
        GambleManager.Instance.SetUpSaveData(SaveDataDict[enumIdx]);

        enumIdx = SaveDataType_Server.UserRank;
        RankManager.Instance.SetUpSaveData(SaveDataDict[enumIdx]);

        enumIdx = SaveDataType_Server.AdvancedInfo;
        PlayerInfoManager.Instance.SetUp_AdvancedSaveData(SaveDataDict[enumIdx]);
        CollectionManager.Instance.SetUpSaveData(SaveDataDict[enumIdx]);
        EquipmentManager.Instance.SetUp_AdvancedSaveData(SaveDataDict[enumIdx]);
        StageManager.Instance.SetUp_AdvancedSaveData(SaveDataDict[enumIdx]);
        SettingManager.Instance.SetUpSaveData(SaveDataDict[enumIdx]);
    }

    public void InitGameSaveData()
    {
        foreach(var saveData in SaveDataDict)
            saveData.Value.InitGameSaveData();
    }
    #endregion

    #region Save Methods
    public void SaveAllGameData(ObscuredBool isAutoSave, ObscuredBool isButtonSave, Action action = null, bool forceDontCloseServerConntect = false)
    {
        var uiManager = UIManager.Instance;
        if (isButtonSave && PauseManuelSave)
        {
            MessageNotificationManager.Instance.ShowInstanceMessageNotification(
                string.Format("{0}초 후 저장할 수 있습니다.", cur_ManuelSaveCoolSec.ToString("N0")));
        }

        // 저장가능할 때, 최종접속시간을 항상 업데이트 한다.
        else
        {
            var usersaveData = SaveDataDict[SaveDataType_Server.UserInfo] as UserInfo_GameSaveData;
            usersaveData.ForcedUpdateDataChange(true);

            List<GameSaveData> gameSaveDataList = new();
            foreach(var gameData in SaveDataDict)
            {
                if (gameData.Value.IsDataChanged)
                    gameSaveDataList.Add(gameData.Value);
            }

            if (gameSaveDataList.Count <= 0)
            {
                action?.Invoke();
                ResetAutoSaveTime();

                MessageNotificationManager.Instance.EnableServerConnectingScreen(false, string.Empty);
            }

            else
            {
                if(!isAutoSave)
                    MessageNotificationManager.Instance.EnableServerConnectingScreen(true, "데이터를 저장 중입니다.");

                if (gameSaveDataList.Count == 1)
                {
                    var saveData = gameSaveDataList[0];
                    saveData.SetUpLocalDataToServer();

                    // 저장할 데이터가 1개면 그냥 업로드
                    SendQueue.Enqueue(Backend.PlayerData.UpdateMyLatestData,
                        saveData.TableName.GetDecrypted(), saveData.GetParam(), callback =>
                        {
                            if (callback.IsSuccess())
                            {
                                action?.Invoke();
                                saveData.DataChangeDisableAfterSave();
                            }

                            else
                            {
                                StopCoroutine(autoSaveTimer);
                                PauseAutoSave = true;

                                return;
                            }

                            ResetAutoSaveTime();

                            if (!isAutoSave && !forceDontCloseServerConntect)
                                MessageNotificationManager.Instance.EnableServerConnectingScreen(false, string.Empty);
                        });
                }

                else
                {
                    PlayerDataTransactionWrite transactionWrite = new();
                    foreach (var data in gameSaveDataList)
                    {
                        data.SetUpLocalDataToServer();
                        data.AddUpdateTransactionParam(ref transactionWrite);
                    }

                    // 저장할 데이터가 2개 이상이면 Trasnsction방식으로 저장
                    SendQueue.Enqueue(Backend.PlayerData.TransactionWrite, transactionWrite, callback =>
                    {
                        if (callback.IsSuccess())
                        {
                            foreach (var data in gameSaveDataList)
                                data.DataChangeDisableAfterSave();

                            action?.Invoke();
                        }

                        else
                        {
                            StopCoroutine(autoSaveTimer);
                            PauseAutoSave = true;

                            return;
                        }

                        ResetAutoSaveTime();

                        if (!isAutoSave && !forceDontCloseServerConntect)
                            MessageNotificationManager.Instance.EnableServerConnectingScreen(false, string.Empty);
                    });
                }
            }
        }
    }

    public void UpdateAllGameDataForcedSync(ObscuredString purchasedItemId)
    {
        var usersaveData = SaveDataDict[SaveDataType_Server.UserInfo] as UserInfo_GameSaveData;
        usersaveData.ForcedUpdateDataChange(true);

        List <GameSaveData> gameSaveDataList = new();
        foreach (var gameData in SaveDataDict)
        {
            if (gameData.Value.IsDataChanged)
                gameSaveDataList.Add(gameData.Value);
        }

        if (gameSaveDataList.Count == 1)
        {
            foreach (var gameData in gameSaveDataList)
            {
                var data = gameData;
                data.SetUpLocalDataToServer();

                var logtype = MyLogType.PaidProductFailed;
                Param logParam = new() { { "Purchased Product Id : ", purchasedItemId.GetDecrypted() } };

                var bro = Backend.PlayerData.UpdateMyData(data.TableName.GetDecrypted(), data.InDate.GetDecrypted(), data.GetParam());
                if (bro.IsSuccess())
                    logtype = MyLogType.PaidProductSuccessed;

                BackendServerManager.Instance.SendLog(logtype, logParam);
            }
        }

        else
        {
            PlayerDataTransactionWrite _transactionWrite = new();
            foreach (var data in gameSaveDataList)
            {
                data.SetUpLocalDataToServer();
                data.AddUpdateTransactionParam(ref _transactionWrite);
            }

            var logtype = MyLogType.PaidProductFailed;
            Param logParam = new() { { "Purchased Product Id : ", purchasedItemId.GetDecrypted() } };

            var _bro = Backend.PlayerData.TransactionWrite(_transactionWrite);
            if(_bro.IsSuccess())
                logtype = MyLogType.PaidProductSuccessed;

            BackendServerManager.Instance.SendLog(logtype, logParam);
        }
    }

    public void UpdatePaidStoreDataAsync(ObscuredString purchasedItemId, PaidProductDB_Save _paidProductDB,  Action afterSaved)
    {
        var paidStoreSaveData = SaveDataDict[SaveDataType_Server.PaidStore] as PaidStore_GameSaveData;
        paidStoreSaveData.DataChangeEnableOnInsert();
        paidStoreSaveData.SetUpLocalDataToServer();

        SendQueue.Enqueue(Backend.PlayerData.UpdateMyLatestData,
            paidStoreSaveData.TableName.GetDecrypted(), paidStoreSaveData.GetParam(), callback =>
            {
                var logtype = MyLogType.PaidProductFailed;
                if (callback.IsSuccess())
                {
                    logtype = MyLogType.PaidProductSuccessed;
                    paidStoreSaveData.DataChangeDisableAfterSave();
                    afterSaved?.Invoke();
                }

                if (_paidProductDB != null)
                {
                    Param logParam = new();
                    logParam.Add("Purchased Product Id : ", purchasedItemId.GetDecrypted());
                    logParam.Add("Purchaed Receiptd Id : ", _paidProductDB.receiptId.GetDecrypted());
                    logParam.Add("Product Id Id : ", _paidProductDB.productId.GetDecrypted());
                    logParam.Add("Purchaed DateTime : ", _paidProductDB.purchasedDateTime.GetDecrypted());

                    BackendServerManager.Instance.SendLog(logtype, logParam);
                }
            });
    }

    private Coroutine autoSaveTimer;
    public void Start_AllGameDataAuto()
    {
        autoSaveTimer = StartCoroutine(Auto_SaveTimer());
    }

    // 자동저장 15분
    // 절전모드 30분 or 1시간
    private readonly ObscuredFloat max_AutoSaveCoolSec = 1200.0f;
    private readonly ObscuredFloat max_ManuelSaveCoolSec = 30.0f;
    private ObscuredFloat cur_AutoSaveCoolSec = 0.0f;
    private ObscuredFloat cur_ManuelSaveCoolSec = 0.0f;
    private WaitForSeconds waitForSecond = new(1.0f);

    private IEnumerator Auto_SaveTimer()
    {
        ResetAutoSaveTime();
        ResetManualSaveTime();

        while (true)
        {
            if(!PauseAutoSave)
            {
                if(cur_AutoSaveCoolSec > 0.0f)
                {
                    cur_AutoSaveCoolSec -= 1.0f;
                }

                else
                {
                    SaveAllGameData(true, false);
                }

                if(PauseManuelSave && cur_ManuelSaveCoolSec > 0.0f)
                {
                    cur_ManuelSaveCoolSec -= 1.0f;

                    if(cur_ManuelSaveCoolSec <= 0.0f)
                    {
                        cur_ManuelSaveCoolSec = 0.0f;
                        PauseManuelSave = false;
                    }
                }
            }

            yield return waitForSecond;
        }
    }
    #endregion

    // --- 중략 ---
}
