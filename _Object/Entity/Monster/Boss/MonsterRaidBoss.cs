using CodeStage.AntiCheat.ObscuredTypes;
using System;

public class MonsterRaidBoss : MonsterBoss
{
    //----------------------------------------------------------------------------------------------------
    // Fields
    //----------------------------------------------------------------------------------------------------
    #region Protected Fields
    protected ObscuredInt _curBerserkLevel;
    protected ReinforceDB _curBerserkDB;
    #endregion

    //----------------------------------------------------------------------------------------------------
    // Methods
    //----------------------------------------------------------------------------------------------------
    #region Awake Methods
    private void Awake()
    {
        LoadComponents();
        SetUpOnAwake();
    }
    #endregion

    #region Start Methods
    private void Start()
    {
        SetUpOnStart();
        ResetBerserkAbility();
    }
    #endregion

    #region Summon Methods
    private UIHpBarBoss _bossHpBarUI;
    private readonly ObscuredString _houndSummonAudioName = "Summon_Hound_00";
    public override void StartSummoning()
    {
        if (StageManager.Instance.IsChangingMap) 
            return;

        CurEntityState = EntityState.Summoning;

        _bossHpBarUI = null;

        UpdateInstanceTarget();
        UpdateInstanceAbility();

        FindLongestDistanceAttack();
        SetUpCooldownSliderUI();
        UpdateCooldownSliderUI();

        PlaySummoningVfx();
        _myAnimator.SetTrigger("OnSummon");

        SoundManager.Instance.PlayEffectAudio(_houndSummonAudioName);
    }

    public override void Finish_Summoning()
    {
        base.Finish_Summoning();

        var dbList = DataManager.Instance.ReinforceDBDicts[Reinforce_Category.Enemy][(int)Reinforce_Group_Enemy.Raid][(int)Reinforce_Detail_Enemy_Raid.Berserk];

        _bossHpBarUI = hpBar as UIHpBarBoss;
        _bossHpBarUI.OnFloatValueUpdated += CheckPhaseByTime;
        _bossHpBarUI.EndTimer += BeginDie;

        _curBerserkDB = dbList[_curBerserkLevel];
    }
    #endregion

    #region Die Methods
    public override void KillForced(ObscuredBool isForced)
    {
        _curBerserkLevel = 0;

        ResetBerserkAbility();

        if (_bossHpBarUI != null && _bossHpBarUI.OnFloatValueUpdated != null)
        {
            _bossHpBarUI.OnFloatValueUpdated -= CheckPhaseByTime;
            _bossHpBarUI = null;
        }

        base.KillForced(isForced);
    }
    #endregion

    #region Ability Setting Methods
    protected void SetBerserkAbility()
    {
        for (var i = 0; i < _curBerserkDB.reinforceAbilityDBs.Length; i++)
        {
            CurEntityAbility.SetAbilityValue(_curBerserkDB.reinforceAbilityDBs[i].abilityType, _curBerserkDB.reinforceAbilityDBs[i].default_Vlu, Ability_CalcValueType.Base);
            CurEntityAbility.UpdateAbilityFinalValue(_curBerserkDB.reinforceAbilityDBs[i].abilityType);
        }
    }

    protected void ResetBerserkAbility()
    {
        var db = DataManager.Instance.ReinforceDBDicts[Reinforce_Category.Enemy][(int)Reinforce_Group_Enemy.Raid][(int)Reinforce_Detail_Enemy_Raid.Berserk][0];

        for(var i = 0; i < db.reinforceAbilityDBs.Length; i++)
        {
            CurEntityAbility.SetAbilityValue(db.reinforceAbilityDBs[i].abilityType, db.reinforceAbilityDBs[i].default_Vlu, Ability_CalcValueType.Base);
            CurEntityAbility.UpdateAbilityFinalValue(db.reinforceAbilityDBs[i].abilityType);
        }
    }
    #endregion

    #region Check Methods
    private const string BERSERK_GRADE = "����ȭ {0}�ܰ�!!";
    protected void CheckPhaseByTime(ObscuredFloat _timeValue)
    {
        var dbList = DataManager.Instance.ReinforceDBDicts
            [Reinforce_Category.Enemy]
            [(int)Reinforce_Group_Enemy.Raid]
            [(int)Reinforce_Detail_Enemy_Raid.Berserk];

        var nextBersetkLevel = _curBerserkLevel + 1;
        if(dbList.Count > nextBersetkLevel)
        {
            if (dbList[nextBersetkLevel].reinforceUnlockDBs[0].reqVlu <= _timeValue)
            {
                _curBerserkLevel = nextBersetkLevel;
                _curBerserkDB = dbList[_curBerserkLevel];
                SetBerserkAbility();

                if (_curBerserkLevel == 1)
                {
                    InstanceEntityAbility.IsSuperArmor = true;
                    OnEndAtk = null;
                    OnEndAtk += () => { ChangePhase(1); OnEndAtk = null; };
                }

                if (_curBerserkLevel == 4)
                {
                    OnEndAtk = null;
                    OnEndAtk += () => { ChangePhase(2); OnEndAtk = null; };
                }

                MessageNotificationManager.Instance.ShowInstanceMessageNotification(
                    string.Format(BERSERK_GRADE, _curBerserkLevel.ToString()));
            }
        }
    }

    protected void ChangePhase(ObscuredInt phaseIdx)
    {
        enemyDB_Cur.CurCombatPhase = phaseIdx;
    }

    private Action OnEndAtk;
    public override void Finish_AutoAttack()
    {
        OnEndAtk?.Invoke();

        base.Finish_AutoAttack();
    }

    protected void CancelCurrentActing()
    {
        StopCoroutine(StartAutoAttackCooldown());
        StopCoroutine(UpdateCooldownSliderUIPosition());

        _myAnimator.SetTrigger("OnReset");
        _myAnimator.SetBool("IsRun", false);

        _cooldownSliderUI.gameObject.SetActive(false);
        _cooldownSliderUI.ActivateObject(false);
        _curAttackCooldown = 0.0f;
        InstanceEntityAbility.CanAutoAttack = true;

        CurEntityState = EntityState.Idle;
    }
    #endregion
}