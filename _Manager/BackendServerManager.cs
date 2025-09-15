//----------------------------------------------------------------------------------------------------
// 이 파일은 제출 편의를 위해 일부 구간을 발췌했습니다.
// 목적 : 서버 초기화, 버전 관리, 채팅/강고/스토어 등 외부 연동 세팅을 통합 관리하는 매니저 클래스
// 주요 기능
// - 뒤끝(Backend) SDK 연동 및 서버 빌드 전환(개발/라이브)
// - 앱 버전 동기화 및 강제 업데이트 처리 로직 구현
//----------------------------------------------------------------------------------------------------
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
    ArtifactGamble, SkillGamble, CoreStoneGamble,
    ArtifactReinforce,

    ReviewStatus,

    PaidProductSuccessed, PaidProductFailed, PaidProductRecieved,

    MainQuestLine,
    RaidComplate, RaidSweepComplete, DungeonComplete, DungeonSweepComplete,

    OfflineReward,
    GoldRefund_202408, CarvingRefund_202409, AccessoryGamble,
    End 
}
#endregion

public class BackendServerManager : Singleton<BackendServerManager>
{
    //----------------------------------------------------------------------------------------------------
    // Fields
    //----------------------------------------------------------------------------------------------------
    #region Serialize Fields
    [Header("- [빌드 : 라이브] 라이브 서버 활성화")]
    [SerializeField] private bool isLiveBuild;
    [Space(10)]
    [Header(" - [광고 : 라이브] Live AD")]
    [SerializeField] private bool live_AdActivate;
    [Space(10)]
    [Header("- [차트] 강제로 서버에서 받아오기")]
    [SerializeField] private bool force_LoadOnServer;
    [Space(10)]
    [Header("- [이벤트] 현재 진행중인 이벤트")]
    [SerializeField] private string[] activeEventIds;
    [Space(10)]
    [Header("- [뒤끝] 서버 세팅")]
    [SerializeField] private TheBackendSettings backendSettings;
    [SerializeField] private BackndChatSettings backendChatSettings;
    [SerializeField] private TheBackendGoogleSettingsForAndroid backendGoogleSettingsForAndroid;
    [Space(10)]
    [Header("- [스토어] 스토어 세팅")]
    [SerializeField] private string playStoreURL;
    [SerializeField] private string appStoreURL;
    [SerializeField] private string oneStoreURL;
    [Space(10)]
    [Header(" - [개발자] Develop Server")]
    [SerializeField] private string dev_ClientId;
    [SerializeField] private string dev_SignitureKey;
    [SerializeField] private string dev_ChatUUID;
    [SerializeField] private string dev_chartManagedFileId;
    [Space(10)]
    [Header(" - [라이브] Live Server")]
    [SerializeField] private string live_ClientId;
    [SerializeField] private string live_SignitureKey;
    [SerializeField] private string live_ChatUUID;
    [SerializeField] private string live_chartManagedFileId;
    [Space(10)]
    [Header(" - [주소] 관련 링크")]
    [SerializeField] private string naverCafeURL;
    [SerializeField] private string serviceTermURL;
    [SerializeField] private string privircyTermURL;
    #endregion

    #region Property Fields
    public Queue<Action> MainThreadQueue { get; set; } = new();
    public ObscuredBool IsForcedLoadOnServer { get { return force_LoadOnServer; } }
    public ObscuredBool IsCorrectVersion { get; private set; }
    public ObscuredBool IsRunningOnEditor { get; private set; }
    public ObscuredBool NeedReconnectServer { get; private set; }
    public ObscuredBool IsLiveAdActivate { get { return live_AdActivate; } }

    public ObscuredString ChartManagedFileId { get { return isLiveBuild ? live_chartManagedFileId : dev_chartManagedFileId; } }
    public ObscuredString NaverCafeURL { get { return naverCafeURL; } }
    public ObscuredString ServiceTermURL { get { return serviceTermURL; } }
    public ObscuredString PrivircyTermURL { get { return privircyTermURL; } }
    public string[] ActiveEventIds { get { return activeEventIds; } }
    #endregion

    #region Private Fields
    private TheBackend.ToolKit.InvalidFilter.FilterManager _filterMgr = new();
    private ObscuredBool _setupOnTitleScene;
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
            Debug.Log("금지어 필터링 활성화");

        else
            Debug.Log("금지어 필터링 비활성화");
    }
    #endregion

    #region Init Methods
    public void InitBackendServer(Action afterFunc)
    {
        var bro = Backend.Initialize();
        if(bro.IsSuccess())
        {
            Change_VersionText();

            SetUpOnStart();
            CheckAppVersion(afterFunc);
        }
    }

    private void Change_VersionText()
    {
        TitleSceneManager.Instance.Update_VersionText(isLiveBuild ? Application.version : string.Format("[DEV] - {0}", Application.version));
    }

    private const string NEED_UPDATE_MESSAGE = "업데이트가 필요합니다. \n플레이스토어에서 업데이트를 진행 후, 재실행 해주시기 바랍니다.";
    private const string INCORRECT_VERSION_UPDATE_MESSAGE = "버전이 일치하지 않습니다. \n플레이스토어에서 업데이트를 진행 후, 재실행 해주시기 바랍니다.";
    public void CheckAppVersion(Action afterFunc)
    {
        var bro = Backend.Utils.GetLatestVersion();
        if(bro.IsSuccess())
        {
            var clientVersion = new Version(Application.version);

            ObscuredString version = bro.GetReturnValuetoJSON()["version"].ToString();
            var serverVersion = new Version(version.GetDecrypted());
            var versionCheck = serverVersion.CompareTo(clientVersion);

            // 0 이면 같고, -1이면 서버가 작음
            if (versionCheck <= 0 || clientVersion == null)
            {
                IsCorrectVersion = true;
                afterFunc?.Invoke();
            }

            else // 버전이 일치하지 않을 경우 강제 업데이트인지 체크한다.
            {
                var forceUpdate = bro.GetReturnValuetoJSON()["type"].ToString();
                if (forceUpdate == "1") // 강제 업데이트가 아닐경우
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
            INCORRECT_VERSION_UPDATE_MESSAGE,
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

    // --- 중략 ---
}

