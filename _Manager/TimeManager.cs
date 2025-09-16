//----------------------------------------------------------------------------------------------------
// 목적 : 게임 내 시간 동기화, 오프라인 보상, 일/주/월간 리셋 이벤트 관리
// 
// 주요 기능
// - 서버 시간과 동기화하여 오프라인 보상 및 출석 로직 구현
// - Coroutine 기반 일/주/월 단위 사이클을 초기화 이벤트 및 자동 관리 처리
// - 경량화된 Timer Pool(MyTimer)로 다수의 타이머 효율 관리
// - SaveDataManager과 연동해 주기적 저장 처리 (자동 저장)
//  시간 기반 컨텐츠에 기반이되는 시간 매니저
//----------------------------------------------------------------------------------------------------

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using CodeStage.AntiCheat.ObscuredTypes;
using Unity.VisualScripting;
using BackEnd;

public class TimeManager : Singleton<TimeManager>, SaveDataManagement
{
    //----------------------------------------------------------------------------------------------------
    // Fields
    //----------------------------------------------------------------------------------------------------
    #region Const
    private static readonly ObscuredDouble[] CultureTimes = new ObscuredDouble[(int)SupportLanguageType.End]
    {
        9.0,    // KR 
        9.0     // ENG
    };

    private const int DEFAULT_TIMER_COUNT = 100;
    #endregion

    #region Properties
    public UserInfo_GameSaveData DefaultUserInfo_SaveData { get; private set; }
    #endregion

    #region Private Fields
    private Queue<MyTimer> _timerQueue = new();
    #endregion

    //----------------------------------------------------------------------------------------------------
    // Data SetUp Methods
    //----------------------------------------------------------------------------------------------------
    #region Data SetUp Methods
    public void SetUpSaveData(GameSaveData _gameSaveData)
    {
        DefaultUserInfo_SaveData = _gameSaveData as UserInfo_GameSaveData;
    }
    #endregion

    //----------------------------------------------------------------------------------------------------
    // Methods
    //----------------------------------------------------------------------------------------------------
    #region Awake Methods
    private void Awake()
    {
        if(CheckOverlap())
        {
            InitTimerQueue();
            DontDestroy();
        }
    }
    #endregion

    #region Check Date
    public ObscuredBool CheckOfflineRewardEnabled()
    {
        var minTime_min = DataManager.Instance.RewardInfoDBDict[Reward_Category.OfflineReward]
            [(int)Reward_Group_OfflineReward.Time]
            [(int)Reward_Detail_OfflineReward_Time.MinTime_min][0].req_Vlus[0];

        var enableReward = false;
        if (DefaultUserInfo_SaveData.DailyAccessDB.Acc_OfflineTimeMin >= minTime_min)
        {
            enableReward = true;
        }

        return enableReward;
    }

    public void StartDayChangeCheck()
    {
        StartCoroutine(CheckDayChange());
    }

    private DateTime _todayAccessDateTime;
    private DateTime _tomorrowDateTime;
    private ObscuredDouble _sceneTimeGap;
    public void UpdateDateTimeAsync(Action afterMethod)
    {
        SendQueue.Enqueue(Backend.Utils.GetServerTime, callback =>
        {
            if (callback.IsSuccess())
            {
                var time = callback.GetReturnValuetoJSON()["utcTime"].ToString();

                _todayAccessDateTime = ConvertDateTimeToKor(time);
                _tomorrowDateTime = _todayAccessDateTime.AddDays(1).Date;
                _sceneTimeGap = Time.realtimeSinceStartupAsDouble;

                afterMethod?.Invoke();
            }

            else
            {

            }
        }); 
    }

    private const int KOR_TIME_GAP = 9;
    public DateTime ConvertDateTimeToKor(string timeString)
    {
        var utcDateTime = DateTime.Parse(timeString).ToUniversalTime();
        var korDateTime = utcDateTime.AddHours(KOR_TIME_GAP);

        return korDateTime;
    }

    private readonly WaitForSeconds WAIT_FOR_5_SEC = new(5.0f);
    public IEnumerator CheckDayChange()
    {
        while (true)
        {
            var curDateTime = GetCurDateTime();
            if (curDateTime.Date >= _tomorrowDateTime.Date)
            {
                ChangeDayEvent();

                if (curDateTime.DayOfWeek == DayOfWeek.Monday)
                    ChangeWeekEvent();

                if (curDateTime.Month != _tomorrowDateTime.Month)
                    ChangeMonthEvent();

                _todayAccessDateTime = _todayAccessDateTime.AddDays(1).Date;
                _tomorrowDateTime = _todayAccessDateTime.AddDays(1).Date;
                _sceneTimeGap = Time.realtimeSinceStartupAsDouble;

                SaveDataManager.Instance.SaveAllGameData(true, false);
            }

            yield return WAIT_FOR_5_SEC;
        }
    }
    #endregion

    #region Get Methods
    public DateTime GetCurDateTime()
    {
        var timeGap = Time.realtimeSinceStartupAsDouble - _sceneTimeGap;
        if(timeGap < 0.0) 
            timeGap = 0.0;

        return _todayAccessDateTime.AddSeconds(timeGap);
    }

    public DateTime GetCurDateTime(DateTime dateTime, ObscuredBool isPositive)
    {
        var languageInt = SettingManager.GetLanguangeIndex();
        var curDateTime = dateTime.AddHours(isPositive ? CultureTimes[languageInt] : -CultureTimes[languageInt]);

        return curDateTime;
    }
    #endregion

    #region Change Methods
    private void ChangeDayEvent()
    {
        // 일간 초기화
        SaveDataManager.Instance.ReCheck_DateChange(Product_LimitResetType.Daily);
    }

    private void ChangeWeekEvent()
    {
        // 주간 초기화
        SaveDataManager.Instance.ReCheck_DateChange(Product_LimitResetType.Weekly);
    }

    private void ChangeMonthEvent()
    {
        // 월간 초기화
        SaveDataManager.Instance.ReCheck_DateChange(Product_LimitResetType.Monthly);
    }
    #endregion

    #region Timer Methods
    private void InitTimerQueue()
    {
        for(var i = 0; i < DEFAULT_TIMER_COUNT; i++)
            _timerQueue.Enqueue(new());
    }

    public MyTimer GetTimer()
    {
        if(_timerQueue.Count == 0)
            InitTimerQueue();

        return _timerQueue.Dequeue();
    }

    public void BackTimer(MyTimer timer)
    {
        if(timer != null)
        {
            timer.Finish_Timer();
            _timerQueue.Enqueue(timer);
        }
    }
    #endregion
}

public class MyTimer
{
    //----------------------------------------------------------------------------------------------------
    // ETC
    //----------------------------------------------------------------------------------------------------
    #region Delgate Methods
    public Action OnTimerEnd { get; set; }
    public Action OnLeftTimeUpdate { get; set; }
    #endregion

    //----------------------------------------------------------------------------------------------------
    // Fields
    //----------------------------------------------------------------------------------------------------
    #region Properties
    public ObscuredBool IsTimerEnd { get; private set; }
    public ObscuredFloat RemainingSeconds { get; private set; }
    #endregion

    #region Private Fields
    private Coroutine _timerCorouine;
    #endregion


    //----------------------------------------------------------------------------------------------------
    // Methods
    //----------------------------------------------------------------------------------------------------
    #region Update Methods
    public void UpdateTime(ObscuredFloat timeValue)
    {
        RemainingSeconds = timeValue;
    }

    public void Add_Time(ObscuredFloat timeValue)
    {
        RemainingSeconds += timeValue;
    }
    #endregion

    #region Timer Methods
    public void StartTimer(ObscuredFloat time, Action endAction = null)
    {
        if(endAction != null)
        {
            OnTimerEnd = null;
            OnTimerEnd += endAction;
        }

        IsTimerEnd = false;
        RemainingSeconds = time;

        _timerCorouine = CoroutineRunner.instance.StartCoroutine(Timer());
    }

    public void Finish_Timer()
    {
        if (_timerCorouine == null)
            return;

        CoroutineRunner.instance.StopCoroutine(_timerCorouine);
        Reset_Timer();
    }

    private IEnumerator Timer()
    {
        while(RemainingSeconds > 0.0f)
        {
            RemainingSeconds -= Time.deltaTime;
            OnLeftTimeUpdate?.Invoke();

            yield return null;
        }

        IsTimerEnd = true;
        OnTimerEnd?.Invoke();
    }

    private void Reset_Timer()
    {
        OnLeftTimeUpdate = null;
        OnTimerEnd = null;
        RemainingSeconds = 0.0f;
        IsTimerEnd = false;

        _timerCorouine = null;
    }
    #endregion

}
