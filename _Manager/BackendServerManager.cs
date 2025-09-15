// �� ������ ���� ���Ǹ� ���� �Ϻ� ������ �����߽��ϴ�.

using System.Collections.Generic;
using UnityEngine;
using BackEnd;
using CodeStage.AntiCheat.ObscuredTypes;
using System;
using Backnd.ChatSettings;
using TheBackend.ToolKit.GoogleLogin.Settings.Android;
using Firebase;
using Firebase.Messaging;


#region Enum
public enum MyLogType 
{ 
    Equipment_WeaponGamble, Equipment_HelmetGamble, Equipment_BreastPlateGamble,
    ArtifactGamble,
    SkillGamble, CoreStoneGamble,
    ArtifactReinforce,

    ReviewStatus,

    PaidProductSuccessed, PaidProductFailed,
    PaidProductRecieved,

    MainQuestLine,

    RaidComplate,
    RaidSweepComplete,
    DungeonComplete,
    DungeonSweepComplete,

    OfflineReward,
    GoldRefund_202408,
    CarvingRefund_202409,
    AccessoryGamble,
    End 
}
#endregion

public class BackendServerManager : Singleton<BackendServerManager>
{
    //----------------------------------------------------------------------------------------------------
    // Fields
    //----------------------------------------------------------------------------------------------------
    #region Serialize Fields
    [Header("- [���� : ���̺�] ���̺� ���� Ȱ��ȭ")]
    [SerializeField] private bool isLiveBuild;
    [Space(10)]
    [Header(" - [���� : ���̺�] Live AD")]
    [SerializeField] private bool live_AdActivate;
    [Space(10)]
    [Header("- [��Ʈ] ������ �������� �޾ƿ���")]
    [SerializeField] private bool force_LoadOnServer;
    [Space(10)]
    [Header("- [�̺�Ʈ] ���� �������� �̺�Ʈ")]
    [SerializeField] private string[] activeEventIds;
    [Space(10)]
    [Header("- [�ڳ�] ���� ����")]
    [SerializeField] private TheBackendSettings backendSettings;
    [SerializeField] private BackndChatSettings backendChatSettings;
    [SerializeField] private TheBackendGoogleSettingsForAndroid backendGoogleSettingsForAndroid;
    [Space(10)]
    [Header("- [�����] ����� ����")]
    [SerializeField] private string playStoreURL;
    [SerializeField] private string appStoreURL;
    [SerializeField] private string oneStoreURL;
    [Space(10)]
    [Header(" - [������] Develop Server")]
    [SerializeField] private string dev_ClientId;
    [SerializeField] private string dev_SignitureKey;
    [SerializeField] private string dev_ChatUUID;
    [SerializeField] private string dev_chartManagedFileId;
    [Space(10)]
    [Header(" - [���̺�] Live Server")]
    [SerializeField] private string live_ClientId;
    [SerializeField] private string live_SignitureKey;
    [SerializeField] private string live_ChatUUID;
    [SerializeField] private string live_chartManagedFileId;
    [Space(10)]
    [Header(" - [�ּ�] ���� ��ũ")]
    [SerializeField] private string naverCafeURL;
    [SerializeField] private string serviceTermURL;
    [SerializeField] private string privircyTermURL;
    #endregion

    #region Property Fields
    public Queue<Action> MainThreadQueue { get; set; } = new();
    public ObscuredBool IsForcedLoadOnServer { get { return force_LoadOnServer; } }
    public ObscuredBool IsCorrectVersion { get; private set; } = false;
    public ObscuredBool IsRunningOnEditor { get; private set; } = false;
    public ObscuredBool NeedReconnectServer { get; private set; } = false;
    public ObscuredBool IsLiveAdActivate 
    {
        get { return live_AdActivate; }
    }


    public ObscuredString ChartManagedFileId 
    {
        get { return isLiveBuild ? live_chartManagedFileId : dev_chartManagedFileId; }
    }

    public ObscuredString NaverCafeURL
    {
        get { return naverCafeURL; }
    }

    public ObscuredString ServiceTermURL
    {
        get { return serviceTermURL; }
    }

    public ObscuredString PrivircyTermURL
    {
        get { return privircyTermURL; }
    }

    public string[] ActiveEventIds { get { return activeEventIds; } }
    #endregion

    #region Private Fields
    private TheBackend.ToolKit.InvalidFilter.FilterManager _filterMgr = new();
    private ObscuredBool _setupOnTitleScene = false;
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
            ConvertBuildServer();
            InitLanguageFilter();
        }
    }

    private void ConvertBuildServer()
    {
        ObscuredString cliendId = dev_ClientId;
        ObscuredString signitureKey = dev_SignitureKey;
        ObscuredString chatUUID = dev_ChatUUID;
        if(isLiveBuild)
        {
            cliendId = live_ClientId;
            signitureKey = live_SignitureKey;
            chatUUID = live_ChatUUID;
        }

        backendSettings.clientAppID = cliendId;
        backendSettings.signatureKey = signitureKey;
        backendChatSettings.chatUUID = chatUUID;
    }

    private void InitLanguageFilter()
    {
        if(_filterMgr.LoadInvalidString())
            Debug.Log("������ ���͸� Ȱ��ȭ");

        else
            Debug.Log("������ ���͸� ��Ȱ��ȭ");
    }
    #endregion

    #region Init Methods
    public void InitBackendServer(Action afterFunc)
    {
        var bro = Backend.Initialize();
        var debugMessage = string.Empty;
        if(bro.IsSuccess())
        {
            debugMessage = "�ڳ� ���� �ʱ�ȭ ����!";
            Change_VersionText();

            SetUpOnStart();
            CheckAppVersion(afterFunc);
        }

        else
            debugMessage = "�ڳ� ���� �ʱ�ȭ ����!";

        ServerDebugLog(bro, debugMessage);
    }

    private void Change_VersionText()
    {
        TitleSceneManager.Instance.Update_VersionText(isLiveBuild ? Application.version : string.Format("[DEV] - {0}", Application.version));
    }

    private const string NEED_UPDATE_MESSAGE = "������Ʈ�� �ʿ��մϴ�. \n�÷��̽����� ������Ʈ�� ���� ��, ����� ���ֽñ� �ٶ��ϴ�.";
    private const string INCORRECT_VERSION_UPDATE_MESSAGE = "������ ��ġ���� �ʽ��ϴ�. \n�÷��̽����� ������Ʈ�� ���� ��, ����� ���ֽñ� �ٶ��ϴ�.";
    public void CheckAppVersion(Action afterFunc)
    {
        var bro = Backend.Utils.GetLatestVersion();
        if(bro.IsSuccess())
        {
            var clientVersion = new Version(Application.version);

            ObscuredString version = bro.GetReturnValuetoJSON()["version"].ToString();
            var serverVersion = new Version(version.GetDecrypted());
            var versionCheck = serverVersion.CompareTo(clientVersion);

            // 0 �̸� ����, -1�̸� ������ ����
            if (versionCheck <= 0 || clientVersion == null)
            {
                IsCorrectVersion = true;
                afterFunc?.Invoke();
            }

            else // ������ ��ġ���� ���� ��� ���� ������Ʈ���� üũ�Ѵ�.
            {
                var forceUpdate = bro.GetReturnValuetoJSON()["type"].ToString();
                if (forceUpdate == "1") // ���� ������Ʈ�� �ƴҰ��
                {
                    IsCorrectVersion = true;
                    afterFunc?.Invoke();
                }

                else
                {
                    var messageMgr = MessageNotificationManager.Instance;
                    messageMgr.EnableServerConnectingScreen(false, string.Empty);

                    IsCorrectVersion = false;
                    messageMgr.ShowMessageNotification(NEED_UPDATE_MESSAGE, MessageUI_ButtonGroupType.PlayStore, 
                        () => { Application.OpenURL(playStoreURL); }, true);
                }
            }
        }

        else
        {
            switch (bro.GetStatusCode())
            {
                case "404":
                    {
                        MessageNotificationManager.Instance.ShowMessageNotification(
                            INCORRECT_VERSION_UPDATE_MESSAGE, 
                            MessageUI_ButtonGroupType.PlayStore, 
                            ()=> {
                                Application.OpenURL(playStoreURL);
                            }, true);
                        break;
                    }

                default:
                    {
                        IsRunningOnEditor = true;

                        afterFunc?.Invoke();
                        break;
                    }
            }
        }
    }

    private void ErrorEventAppVersionMismatch()
    {
        IsCorrectVersion = false;

        SecurityManager.Instance.Error_Event();

        MessageNotificationManager.Instance.ShowMessageNotification(
            "������ ��ġ���� �ʽ��ϴ�. \n�÷��̽����� ������Ʈ�� ���� ��, ����� ���ֽñ� �ٶ��ϴ�.",
            MessageUI_ButtonGroupType.PlayStore, () =>
            {
                Application.OpenURL(playStoreURL);
            }, true);
    }
    #endregion

    #region Start Methods
    private void Start()
    {
        var titleMgr = TitleSceneManager.Instance;
        titleMgr.SetUp_BlackScreen();

        if (SecurityManager.Instance.Check_CanStartSetUp())
        {
            if (!_setupOnTitleScene && Application.internetReachability != NetworkReachability.NotReachable)
            {
                _setupOnTitleScene = true;

                InitBackendServer(() =>
                {
                    AdManager.Instance.InitAdMob();
                    SetUpFirebaseToken();
                    titleMgr.SetUpOnTitleScene();
                });
            }
        }

        else
            titleMgr.SetUpOnTitleScene();
    }
    #endregion

    // --- �߷� ---
}
