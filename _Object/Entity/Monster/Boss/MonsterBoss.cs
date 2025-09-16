//----------------------------------------------------------------------------------------------------
// 목적 : 보스 몬스터의 공격, 쿨다운 및 그래픽 연출 및 페이즈별 공격 방식 변화
// 
// 주요 기능
// - 보스 공격 선택 로직, 쿨다운 슬라이더 활성화 및 업데이트
// - 근/원거리 분리 및 사용, 액션 카운트에 따른 Vfs,Sfx 재생
// - 상태이상 해제 시 페이즈, 공격 인덱스, 행동(애니메이션) 초기화 및 능력치 재계산
// - Hp Bar, 전투 UI/연출과 자연스럽게 연결
//----------------------------------------------------------------------------------------------------

using System.Collections;
using UnityEngine;
using System.Linq;
using CodeStage.AntiCheat.ObscuredTypes;

public class MonsterBoss : Monster
{
    //----------------------------------------------------------------------------------------------------
    // Fields
    //----------------------------------------------------------------------------------------------------
    #region Serialize Fields
    [SerializeField] protected Transform cooldownSliderPosition;
    #endregion

    #region Property Fields
    public ProjectileShooter ProjectileShooter
    {
        get { return _projectileShooter; }
        private set { _projectileShooter = value; }
    }
    private ProjectileShooter _projectileShooter;
    #endregion

    #region Protected Fields
    protected UISliderBarCooldown _cooldownSliderUI;
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

    protected override void LoadComponents()
    {
        base.LoadComponents();

        var projShooter = transform.Find("Projectile Shooter - VFX Play Point 0");
        if(projShooter != null)
            projShooter.TryGetComponent(out _projectileShooter);
    }
    #endregion

    #region Start Methods
    private void Start()
    {
        SetUpOnStart();
    }

    protected virtual void SetUpOnStart()
    {
        if (_projectileShooter != null)
            _projectileShooter.Set_User(this);
    }
    #endregion

    #region Die Methods
    public override void KillForced(ObscuredBool isForced)
    {
        base.KillForced(isForced);

        StopCoroutine(UpdateCooldownSliderUIPosition());

        if (_cooldownSliderUI != null)
            _cooldownSliderUI.BeginDie();

    }
    #endregion

    #region Summoning Methods
    public override void StartSummoning()
    {
        FindLongestDistanceAttack();
        SetUpCooldownSliderUI();
        UpdateCooldownSliderUI();

        base.StartSummoning();
    }

    protected void FindLongestDistanceAttack()
    {
        var attackDBList = enemyDB_Server.attackDBLists[enemyDB_Cur.CurCombatPhase];
        var value = attackDBList.Max(item => item.AttackRange);
        var index = attackDBList.FindIndex(item => item.AttackRange == value);

        enemyDB_Cur.CurAtkDBIdx = index;
    }
    #endregion

    #region Cooldown Methods
    protected void SetUpCooldownSliderUI()
    {
        _cooldownSliderUI = SliderBarUIManager.Instance.GetCooldownBarUI();
        _cooldownSliderUI.MyRectTransform.position = cooldownSliderPosition.position;
        _cooldownSliderUI.ActivateObject(false);
    }

    protected void UpdateCooldownSliderUI()
    {
        var curPhaseIndex = enemyDB_Cur.CurCombatPhase;
        var curAtkIndex = enemyDB_Cur.CurAtkDBIdx;
        var curAtkDB = enemyDB_Server.attackDBLists[curPhaseIndex][curAtkIndex];
        _cooldownSliderUI.SetSliderValueMax(curAtkDB.Cooldown, 0);
        _cooldownSliderUI.SetSliderValueCur(0.0f ,0);
    }

    private WaitForEndOfFrame _waitForFrame = new();
    protected IEnumerator UpdateCooldownSliderUIPosition()
    {
        UpdateCooldownSliderUI();

        while (_cooldownSliderUI.IsActivateComponenets && !InstanceEntityAbility.CanAutoAttack)
        {
            _cooldownSliderUI.MyRectTransform.position = cooldownSliderPosition.position;
            _cooldownSliderUI.SetSliderValueCur(_curAttackCooldown, 0);

            yield return _waitForFrame;
        }

        _cooldownSliderUI.gameObject.SetActive(false);
        _cooldownSliderUI.ActivateObject(false);
    }
    #endregion

    #region Attack Methods
    public override void ActivateAttackVFX()
    {
        var actionCnt = enemyDB_Cur.CurActionCnt; 
        var phaseIdx = enemyDB_Cur.CurCombatPhase;
        var atkDBIdx = enemyDB_Cur.CurAtkDBIdx;

        var atkDB = enemyDB_Server.attackDBLists[phaseIdx][atkDBIdx];

        ObscuredVector2 curPosition = (Vector2)(atkDB.AttackMethodType == AttackMethodType.Melee 
            ? (atkDB.VfxPositionIndexes.Length > 1 && VFXPlayPoints.Length > 1) 
            ? VFXPlayPoints[atkDB.VfxPositionIndexes[actionCnt]].position 
            : VFXPlayPoints[atkDB.VfxPositionIndexes[0]].position 
            : atkDB.ActPositionType == ActPositonType.Target_Bottom 
            ? InstanceEntityAbility.TargetEntityList[0].transform.position 
            : InstanceEntityAbility.TargetEntityList[0].Get_LockOnPosition());

        atkDB.ScaleRatio = InstanceEntityAbility.ScaleRatio;

        if(atkDB.VfxNames[(int)ActionVFXType.Main].Length <= actionCnt)
        {
            --enemyDB_Cur.CurActionCnt;
            --actionCnt;
        }

        var vfxMainString = atkDB.VfxNames[(int)ActionVFXType.Main][actionCnt];
        var vfxMain = VFXManager.Instance.GetVFX(vfxMainString, curPosition, BackgroundManager.Instance.GetScrolledWorldTransform()) as VFXBattle;
        vfxMain.transform.localScale *= atkDB.ScaleRatio;
        vfxMain.SetUpStatus(this, atkDB, actionCnt);

        PlayBossEnemySfx(ActionVFXType.Begin, phaseIdx, atkDBIdx, actionCnt);
        vfxMain.OnCollide += () => { PlayBossEnemySfx(ActionVFXType.Main, phaseIdx, atkDBIdx, actionCnt); };

        ++enemyDB_Cur.CurActionCnt;
    }

    protected override void StartAutoAttack()
    {
        InstanceEntityAbility.CanAutoAttack = false;
        var curAtkDB = enemyDB_Server.attackDBLists[enemyDB_Cur.CurCombatPhase][enemyDB_Cur.CurAtkDBIdx];

        _myAnimator.SetTrigger(curAtkDB.AnimationName);
        if(curAtkDB.AttackMethodType == AttackMethodType.Range)
        {
            _projectileShooter.Set_AttackDB(curAtkDB);
            _projectileShooter.ReadyToShoot(this, InstanceEntityAbility.TargetEntityList[0]);
        }

        enemyDB_Cur.CurActionCnt = 0;
    }

    public override void FinishAutoAttack()
    {
        ReadyNextAttack();

        base.FinishAutoAttack();

        _cooldownSliderUI.gameObject.SetActive(true);
        _cooldownSliderUI.ActivateObject(true);
        
        StartCoroutine(UpdateCooldownSliderUIPosition());
    }

    public virtual void AttackAutomatically()
    {
        var beforePhase = enemyDB_Cur.CurCombatPhase;
        var beforeAtkIdx = enemyDB_Cur.CurAtkDBIdx;
        var beforeActCnt = enemyDB_Cur.CurActionCnt;

        PlayBossEnemySfx(ActionVFXType.Begin, beforePhase, beforeAtkIdx, beforeActCnt);

        _projectileShooter.Play_ShootEffect();
        _projectileShooter.ShootProjectileAddAudioWithSpd(() =>
        {
            PlayBossEnemySfx(ActionVFXType.Main, beforePhase, beforeAtkIdx, beforeActCnt);
        },
        enemyDB_Server.attackDBLists[beforePhase][beforeAtkIdx].EnemyProjSpds[beforeActCnt]);

        ++enemyDB_Cur.CurActionCnt;
    }

    public void PlayBossEnemySfx(ActionVFXType soundType, ObscuredInt phase, ObscuredInt atkCnt, ObscuredInt actionCnt)
    {
        var soundMgr = SoundManager.Instance;
        var sfxNames = enemyDB_Server.attackDBLists[phase][atkCnt].SfxNames[(int)soundType];
        var finalActionCnt = (int)(sfxNames.Length > actionCnt ? actionCnt : sfxNames.Length);

        soundMgr.PlayEffectAudio(sfxNames[finalActionCnt]);
    }

    protected void ReadyNextAttack()
    {
        var maxAttackCount = enemyDB_Server.attackDBLists[enemyDB_Cur.CurCombatPhase].Count;
        enemyDB_Cur.CurAtkDBIdx = GetRandomAttackIndex(0, maxAttackCount, enemyDB_Cur.CurAtkDBIdx);
        enemyDB_Cur.CurActionCnt = 0;

        var curAtkDB = enemyDB_Server.attackDBLists[enemyDB_Cur.CurCombatPhase][enemyDB_Cur.CurAtkDBIdx];
        InstanceEntityAbility.SetAbilityValue(AbilityType_Instance.AtkDelay, (ObscuredDouble)curAtkDB.Cooldown);
        InstanceEntityAbility.SetAbilityValue(AbilityType_Instance.AtkRange, (ObscuredDouble)curAtkDB.AttackRange);
    }

    private int GetRandomAttackIndex(ObscuredInt min, ObscuredInt max, ObscuredInt exceptIndex)
    {
        var randomRange = Enumerable.Range(min, max).Where(i => (i != exceptIndex));
        int index = new System.Random().Next(0, max-1);

        return randomRange.ElementAt(index);
    }
    #endregion

    #region Pause Methods
    public override void PauseActing()
    {
        base.PauseActing();

        if(cooldownSliderPosition.gameObject.activeSelf)
        {
            StopCoroutine(UpdateCooldownSliderUIPosition());

            _cooldownSliderUI.gameObject.SetActive(false);
            _cooldownSliderUI.ActivateObject(false);
        }
    }
    #endregion

    #region Set Ailment
    public override void SetAilment(AilmentType ailmentType, ObscuredBool isActive)
    {
        base.SetAilment(ailmentType, isActive);

        switch (ailmentType)
        {
            case AilmentType.Stun:
                {
                    if (!isActive && !InstanceEntityAbility.isFrozen)
                    {
                        enemyDB_Cur.CurActionCnt = 0;
                        enemyDB_Cur.CurAtkDBIdx = 0;

                        UpdateInstanceAbility();
                    }

                    break;
                }
        }
    }
    #endregion
}




