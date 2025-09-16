//----------------------------------------------------------------------------------------------------
// 목적 : 플레이어 캐릭터의 런타임 로직(조준, 자동공격, 스킬(수동/자동), 상태이상, 연출) 및 무기/투사체 제어
// 
// 주요 기능
// - Target 탐색 -> 조준(에이밍) -> 자동/수동 스킬 or 평타 발동 파이프라인
// - DOTween, MaterialProprtyBlock을 활용한 타격/피격 연출 및 무기/팔 스프라이트 동기화
// - ProjectileShooter와 연계한 원거리 공격, 상태머신 적용(Idle, Run, Skill, Die, Aim, Attack, Skill)
// - TimeManager, MonsterManager, SkillManager와 연동
// - 전투의 핵심 루프(조준-> 공격-> 쿭다운)를 구현
//----------------------------------------------------------------------------------------------------
using CodeStage.AntiCheat.ObscuredTypes;
using DG.Tweening;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Hero : Entity
{
    //----------------------------------------------------------------------------------------------------
    // Fields
    //----------------------------------------------------------------------------------------------------
    #region Serialize Fields
    [Space]
    [Header("플레이어 캐릭터 (Player Character)")]
    [SerializeField] protected SpriteRenderer _frontArmRenderer;
    [SerializeField] protected EquipedWeapon _equipedWeapon;
    [SerializeField] protected ObscuredFloat _maxAimTime;
    #endregion

    #region Property Fields
    public ProjectileShooter ProjectileShooter
    {
        get { return _projectileShooter; }
        private set { _projectileShooter = value; }
    }
    private ProjectileShooter _projectileShooter;

    public AttackDB AutoAttackDB { get; private set; }
    public ObscuredBool OnSkillRepeating { get; set; }
    public ObscuredInt SkillRepeatCnt { get; set; }
    public ObscuredString CurCastingSkillId { get; set; } = string.Empty;
    protected ObscuredFloat AttackRange { get; set; } = 7.5f;
    protected ObscuredBool UseManualSkill { get; set; } = false;
    #endregion

    #region Private Fields
    private ObscuredBool _canUseSkill;
    private ObscuredFloat _curAimTime;

    private ObscuredInt _activateSkillSlotIdx;
    private Vector2 _originWeaponPosition = new Vector2(0.0f, -0.0001f);
    #endregion

    //----------------------------------------------------------------------------------------------------
    // Methods
    //----------------------------------------------------------------------------------------------------
    #region Awake Methods
    private void Awake()
    {
        LoadComponents();
        SetUpOnAwake();
        ResetStatus();
    }

    protected override void LoadComponents()
    {
        base.LoadComponents();

        transform.GetChild(0).TryGetComponent(out _mySpriteRenderer);
        if (_mySpriteRenderer != null)
        {
            Origin_MaterialColor = _mySpriteRenderer.sharedMaterial.color;
            Flexible_MaterialColor = _mySpriteRenderer.sharedMaterial.color;
        }

        _originShadowColor = _shadowRenderer.color;
        InstanceEntityAbility.OwnerEntityAbility = CurEntityAbility;

        transform.Find("Projectile Shooter").TryGetComponent(out _projectileShooter);

        _materialBlockedObjects = new MaterialPropertyBlock[3];
        var loopCnt = _materialBlockedObjects.Length;
        for(var i = 0; i < loopCnt; i++)
            _materialBlockedObjects[i] = new();
    }

    protected override void SetUpOnAwake()
    {
        base.SetUpOnAwake();

        var playerStatusMgr = PlayerInfoManager.Instance;
        if (playerStatusMgr != null)
        {
            CurEntityAbility = playerStatusMgr.PlayerAbility_Default;
            InstanceEntityAbility = playerStatusMgr.InstanceEntityAbility;

            playerStatusMgr.UpdateLevelUp_Func += LevelUpEvent;
        }

        CurEntityAbility.ValueChangedDict[AbilityType_Entity.Atk_Spd_Mul] += UpdateAttackSpeed;
        CurEntityAbility.ValueChangedDict[AbilityType_Entity.MovementSpd_Mul] += UpdateMoveSpeed;
    }
    #endregion

    #region Start Methods
    private void Start()
    {
        SetUpOnStart();
    }

    private void SetUpOnStart()
    {
        ResetInstanceStatus();
        ResetAutoAttackElement();

        _projectileShooter.SetUser(this);
        _projectileShooter.SetAttackDB(AutoAttackDB);

        SetUpBeforeActive();

        InstanceEntityAbility.SetAbilityValue(AbilityType_Instance.AtkRange, (double)AttackRange);

        _mySpriteRenderer.GetPropertyBlock(_materialBlockedObjects[0]);
        _mySpriteRenderer.SetPropertyBlock(_materialBlockedObjects[0]);

        _frontArmRenderer.GetPropertyBlock(_materialBlockedObjects[1]);
        _frontArmRenderer.SetPropertyBlock(_materialBlockedObjects[1]);

        _equipedWeapon.MySpriteRenderer.GetPropertyBlock(_materialBlockedObjects[2]);
        _equipedWeapon.MySpriteRenderer.SetPropertyBlock(_materialBlockedObjects[2]);
    }
    #endregion

    #region Ailment Methods
    public override void SetAilment(AilmentType ailmentType, ObscuredBool isActive)
    {
        base.SetAilment(ailmentType, isActive);

        switch (ailmentType)
        {
            case AilmentType.Stun:
                {
                    ResetSkillStatusFull();
                    break;
                }
        }
    }
    #endregion

    #region Levelup Methods
    private void LevelUpEvent()
    {
        VFXManager.Instance.GetVFX(LocalDB_Text.LV_UP_VFX_NAME, transform.position, BackgroundManager.Instance.GetScrolledWorldTransform());
    }
    #endregion

    #region Animation Methods
    public override void SetAnimBool(ObscuredString paramName, ObscuredBool isActive)
    {
        base.SetAnimBool(paramName, isActive);

        _equipedWeapon.SetAnimatorBool(paramName, isActive);
    }

    public override void SetAnimTrigger(ObscuredString paramName)
    {
        base.SetAnimTrigger(paramName);

        _equipedWeapon.SetAnimatorTrigger(paramName);
    }

    public override void SetAnimFloat(ObscuredString paramName, ObscuredFloat value)
    {
        base.SetAnimFloat(paramName, value);

        _equipedWeapon.SetAnimatorFloat(paramName, value);
    }

    public override void ResetAnimTrigger(ObscuredString paramName)
    {
        base.ResetAnimTrigger(paramName);

        _equipedWeapon.ResetAnimTrigger(paramName);
    }

    public override void UpdateAttackSpeed(ObscuredDouble attackSpeed)
    {
        base.UpdateAttackSpeed(attackSpeed);

        _equipedWeapon.SetAnimatorFloat("AttackSpeed", (float)attackSpeed);
    }

    public override void UpdateMoveSpeed(ObscuredDouble _moveSpeed)
    {
        var animationMoveSpd = (_moveSpeed - 1.0f) * 0.5f + 1.0f;

        base.UpdateMoveSpeed(animationMoveSpd);

        _equipedWeapon.SetAnimatorFloat("MoveSpeed", (float)animationMoveSpd);
    }
    #endregion

    #region Begin Stage Methods
    public void BeginStage(ObscuredBool isFirst)
    {
        var stageMgr = StageManager.Instance;
        if (isFirst)
            stageMgr.Initialize_Map();

        else
            stageMgr.Move_NextMap();

        ResetSkillStatusFull();
        UpdateAttackSpeed(CurEntityAbility[AbilityType_Entity.Atk_Spd_Mul]);
        UpdateMoveSpeed(CurEntityAbility[AbilityType_Entity.MovementSpd_Mul]);

        SetOnRun();
    }

    public override void ResetAnimator()
    {
        base.ResetAnimator();

        SetAnimBool("IsUnarmed", false);
        SetAnimBool("IsBattle", false);
        SetAnimBool("OnSkill_Charged", false);

        ResetAnimTrigger("OnSkill_0");
        ResetAnimTrigger("OnSkill_1");
        ResetAnimTrigger("OnSkill_2");
        ResetAnimTrigger("OnAlive");
    }
    #endregion

    #region Run Methods
    private readonly ObscuredString[] _playerFootstepAudioClipNames = new ObscuredString[]
        { "PlayerFootStep_00", "PlayerFootStep_01" };

    private const string _dustWalkVfxNameString = "Dust_Walk_00";
    public void CreateRunDust()
    {
        VFX vfx = VFXManager.Instance.GetVFX(_dustWalkVfxNameString);
        vfx.transform.position = transform.position;
        vfx.transform.SetParent(BackgroundManager.Instance.GetScrolledWorldTransform());

        SoundManager.Instance.PlayEffectAudio(_playerFootstepAudioClipNames);
    }

    public void SetOnRun()
    {
        InstanceEntityAbility.Start_RegenAbility();

        CurEntityState = EntityState.Run;
        SetAnimBool("IsRun", true);
    }

    public void StopRunning()
    {
        SetAnimBool("IsRun", false);
        CurEntityState = EntityState.Idle;
    }
    #endregion

    #region Find Target Methods
    private List<Entity> FindTargets()
    {
        var monsterList = MonsterManager.Instance.CheckEnemyInRange(transform, AttackRange);

        return monsterList.Count <= 0 ? null : monsterList;
    }

    #endregion

    #region Armed Events
    private void UpdateAim()
    {
        InstanceEntityAbility.TargetEntityList = FindTargets();

        if (InstanceEntityAbility.TargetEntityList == null ||
            InstanceEntityAbility.TargetEntityList.Count == 0 ||
            InstanceEntityAbility.TargetEntityList[0] == null ||
            InstanceEntityAbility.TargetEntityList[0].isDead ||
            InstanceEntityAbility.TargetEntityList[0].CurEntityState == EntityState.Die)
        {
            AimTimer();
        }

        else
        {
            _curAimTime = _maxAimTime;

            if (!_myAnimator.GetBool("OnSkill_Charged") &&
                !InstanceEntityAbility.OnUsingSkill &&
                (UseManualSkill || CheckUpdateAimSkill()))
            {
                _canUseSkill = true;
                CurEntityState = EntityState.Skill;
            }

            else
                CurEntityState = EntityState.Attack;
        }
    }

    private ObscuredBool CheckUpdateAimSkill()
    {
        var _skillManager = SkillManager.Instance;
        return _skillManager.Is_AutoSkillMode && _skillManager.Check_CanActiveSkill(out _activateSkillSlotIdx, this);
    }

    private ObscuredBool CheckOnBuffSkill()
    {
        var _skillManager = SkillManager.Instance;
        return _skillManager.Is_AutoSkillMode && _skillManager.Check_CanActiveBuffSkill(out _activateSkillSlotIdx, this);
    }

    private void AimTimer()
    {
        if (_curAimTime <= 0.0f)
        {
            CurEntityState = EntityState.Idle;
            SetAnimBool("IsBattle", false);
        }

        else
            _curAimTime -= Time.deltaTime;
    }

    protected void UpdateArmedIdle()
    {
        var canUseBuffSkill = CheckOnBuffSkill();
        if (!InstanceEntityAbility.OnUsingSkill && canUseBuffSkill)
        {
            _canUseSkill = true;
            CurEntityState = EntityState.Skill;
            _curAimTime = _maxAimTime;
        }

        else
        {
            InstanceEntityAbility.TargetEntityList = FindTargets();

            if (InstanceEntityAbility.TargetEntityList != null && 
                InstanceEntityAbility.TargetEntityList.Count > 0 &&
                InstanceEntityAbility.TargetEntityList[0] != null &&
                !InstanceEntityAbility.TargetEntityList[0].isDead &&
                InstanceEntityAbility.TargetEntityList[0].CurEntityState != EntityState.Die)
            {
                if (!InstanceEntityAbility.OnUsingSkill && (UseManualSkill || CheckUpdateAimSkill()))
                {
                    _canUseSkill = true;
                    CurEntityState = EntityState.Skill;
                }

                else
                {
                    CurEntityState = EntityState.Aim;
                    SetAnimBool("IsBattle", true);
                }

                _curAimTime = _maxAimTime;
            }

            else if (CurEntityState != EntityState.Idle)
            {
                CurEntityState = EntityState.Idle;
                SetAnimBool("IsBattle", false);
            }
        }
    }

    public ObscuredBool UpdateArmedIdleCanUseSkillManual(ObscuredInt useSlotIdx, ObscuredBool isSelfOrBuff)
    {
        ObscuredBool returnValue = false;

        if (!InstanceEntityAbility.OnUsingSkill &&
            SkillRepeatCnt <= 0 &
            !OnSkillRepeating &&
            CurEntityState != EntityState.Skill &&
            CurEntityState != EntityState.Die &&
            CurEntityState != EntityState.Pause &&
            CurEntityState != EntityState.Run &&
            !_myAnimator.GetBool("OnSkill_Charged") &&
            !isDead)
        {
            _activateSkillSlotIdx = useSlotIdx;

            if (isSelfOrBuff)
            {
                _canUseSkill = true;
                CurEntityState = EntityState.Skill;
                _curAimTime = _maxAimTime;
                UseManualSkill = true;

                returnValue = true;
            }

            else
            {
                InstanceEntityAbility.TargetEntityList = FindTargets();

                if (InstanceEntityAbility.TargetEntityList != null && InstanceEntityAbility.TargetEntityList.Count > 0)
                {
                    _canUseSkill = true;
                    CurEntityState = EntityState.Skill;
                    _curAimTime = _maxAimTime;
                    UseManualSkill = true;

                    returnValue = true;
                }

                else
                    MessageNotificationManager.Instance.ShowInstanceMessageNotification("타겟이 존재하지 않습니다.");
            }
        }

        return returnValue;
    }
    #endregion

    #region Die Methods
    public override void BeginDie()
    {
        MonsterManager.Instance.PauseInstanceEnemy();
        AchievementManager.Instance.AddAccureRequireCnt(REQ_Type.UserInfo, (int)REQ_UserInfo.DeathCnt, 1);

        InstanceEntityAbility.Finish_RegenAbility();

        base.BeginDie();
        PlayDieSound();

        _equipedWeapon.ChangeSpriteSorting(0);
        transform.DOMoveX(-0.75f, 0.5f).SetRelative();

        if (StageManager.Instance.CurChapterDB.itemID == LocalDB_Map.CARVING_MAP_BASE_ID)
            return;

        UICombatResult.Instance.ShowCombatMessageUI(CombatMessageType.Defeat);
        StageManager.Instance.PlayAudio_StageFailed();
        StageManager.Instance.StopKillStageTimer();
    }

    public override void EndDie()
    {
        base.EndDie();

        StageManager.Instance.ClearCurrentStage(StageClearType.Defeat);
    }

    protected void PlayDieSound()
    {
        SoundManager.Instance.PlayEffectAudio(LocalDB_Text.PLAYER_DIE_VOICE);
    }
    #endregion

    #region Hit Events
    public override void TakeDamage(ref DmgResultData dmgResult)
    {
        if (!DebugTestManager.Instance.godMode)
            base.TakeDamage(ref dmgResult);

        if (DmgFontManager.Instance.Check_ShowDmgFont())
        {
            switch (dmgResult.finalDmgTypes[0])
            {
                case DmgType.LifeHeal:
                case DmgType.EnergyHeal:
                    {
                        if (dmgResult.finalDmgs[0] > LocalDB_Ability.abilityEpsilon)
                            StartCoroutine(Show_DmgText(dmgResult));

                        break;
                    }

                default:
                    {
                        StartCoroutine(Show_DmgText(dmgResult));
                        break;
                    }
            }
        }

        PlayerInfoManager.Instance.UpdateHP();

        if (EntityState.Die == CurEntityState)
            return;

            if (EntityState.Die != CurEntityState)
        {
            if (dmgResult.finalDmgTypes[0] == DmgType.EnergyHeal)
            {
                PlayerInfoManager.Instance.UpdateMP();
            }

            else
            {
                PlayerInfoManager.Instance.UpdateHP();

                if (dmgResult.finalDmgTypes[0] != DmgType.LifeHeal)
                {
                    PlayHitSound();

                    ScreenVFXManager.Instance.ScreenShake(CameraType.World, 0.1f, 0.1f, 100.0f);

                    ChangeSpriteColor();
                }
            }
        }
    }

    private readonly ObscuredString _hitAudioName = "PlayerHit_00";
    private readonly ObscuredString[] _hitVoiceNames = new ObscuredString[] { "PlayerHit_Voice_00", "PlayerHit_Voice_01" };
    protected void PlayHitSound()
    {
        var soundMgr = SoundManager.Instance;
        soundMgr.PlayEffectAudio(_hitVoiceNames);
        soundMgr.PlayEffectAudio(_hitAudioName);
    }
    #endregion

    #region Skill Methods
    private void StartSkill()
    {
        _canUseSkill = false;

        var skillMgr = SkillManager.Instance;
        if (skillMgr.NullCheck_CanUseQuickSlotSkill(_activateSkillSlotIdx))
        {
            InstanceEntityAbility.TargetEntityList = FindTargets();
            skillMgr.StartSkill(_activateSkillSlotIdx, this);
        }

        else
        {
            SkillRepeatCnt = 0;
            OnSkillEnd();
        }
    }

    private ObscuredInt CheckSkillTargetCnt()
    {
        var skillDB_cur = SkillManager.Instance.GetQuickSlotGroup().Get_SkillQuickSlot(_activateSkillSlotIdx).SkillItem.SkillDB_Cur;
        var maxTargetCnt = skillDB_cur.UniqueAbilityGroup.GetAbilityValue(
                    Ability_GroupType.Skill,
                    (int)AbilityType_Skill.Atk_Target_Cnt,
                    Ability_ValueType.Value);

        return (int)maxTargetCnt;
    }

    public void StartSkillEffect()
    {
        if (_activateSkillSlotIdx != -1)
        {
            var skillMgr = SkillManager.Instance;
            var isSelfUse = SkillManager.Instance.CheckSelfUseType(_activateSkillSlotIdx);

            if (CurCastingSkillId != string.Empty &&
                (skillMgr.CheckCanUseQuickSlotSkill(_activateSkillSlotIdx, CurCastingSkillId) || isSelfUse))
            {
                if (!isSelfUse)
                    CheckCreateCatapultProjectile();

                if (InstanceEntityAbility.TargetEntityList != null && InstanceEntityAbility.TargetEntityList.Count > 0)
                {
                    BattleDB_Skill battleDBSkill = new();
                    battleDBSkill.DeepCopyUserInfo(this, CurEntityAbility);
                    battleDBSkill.Target = InstanceEntityAbility.TargetEntityList[0];

                    skillMgr.StartSkillEffect(_activateSkillSlotIdx, battleDBSkill, OnSkillRepeating);
                }

                else if (isSelfUse || _myAnimator.GetBool("OnSkill_Charged"))
                {
                    BattleDB_Skill battleDBSkill = new();
                    battleDBSkill.DeepCopyUserInfo(this, CurEntityAbility);
                    battleDBSkill.Target = null;

                    skillMgr.StartSkillEffect(_activateSkillSlotIdx, battleDBSkill, OnSkillRepeating);
                }

                else
                {
                    SkillRepeatCnt = 0;
                    OnSkillEnd();
                }
            }

            else
            {
                SkillRepeatCnt = 0;
                OnSkillEnd();
            }
        }

        else
        {
            SkillRepeatCnt = 0;
            OnSkillEnd();
        }
    }

    public void OnSkillEnd()
    {
        if (SkillRepeatCnt <= 0)
        {
            ResetSkillStatus();

            CurEntityState = EntityState.Idle;
            InstanceEntityAbility.CanAutoAttack = true;

            UpdateAttackSpeed(CurEntityAbility[AbilityType_Entity.Atk_Spd_Mul]);
        }

        else if (!InstanceEntityAbility.OnUsingSkill)
            StartReapeatSkill();

        else if (InstanceEntityAbility.OnUsingSkill)
            SetAnimBool("IsBattle", true);
    }

    public void StartReapeatSkill()
    {
        if (!OnSkillRepeating && SkillRepeatCnt > 0)
            OnSkillRepeating = true;

        StartSkill();
        SkillRepeatCnt--;
    }
    #endregion

    #region Attack Methods
    protected override void StartAutoAttack()
    {
        try
        {
            if (InstanceEntityAbility != null &&
                InstanceEntityAbility.TargetEntityList != null &&
                InstanceEntityAbility.TargetEntityList.Count > 0 &&
                InstanceEntityAbility.TargetEntityList[0] != null &&
                !InstanceEntityAbility.TargetEntityList[0].isDead &&
                InstanceEntityAbility.TargetEntityList[0].CurEntityState != EntityState.Die)
            {
                _projectileShooter.Reset_BattleDB();
                _projectileShooter.Set_AttackDB(AutoAttackDB);
                _projectileShooter.ReadyToShoot(this, InstanceEntityAbility.TargetEntityList[0]);

                base.StartAutoAttack();
            }

            else
            {
                Finish_AutoAttack();
            }
        }

        catch
        {
            Finish_AutoAttack();
        }
    }

    public override void FinishAutoAttack()
    {
        base.FinishAutoAttack();

        CurEntityState = EntityState.Aim;
    }

    public void AttackAutomatically()
    {
        var soundMgr = SoundManager.Instance;
        soundMgr.PlayEffectAudio(AutoAttackDB.SfxNames[(int)ActionVFXType.Begin][0]);
        soundMgr.PlayEffectAudio(AutoAttackDB.SfxNames[(int)ActionVFXType.Extra][0]);

        _projectileShooter.Play_ShootEffect();
        _projectileShooter.ShootProjectile();

        var playerSkillAbilityDB = PlayerInfoManager.Instance.GetPlayerSkillAbility();
        if (playerSkillAbilityDB.CheckContain(Ability_GroupType.OnAutoAtk, (int)AbilityType_OnAutoAtk.User_AutoAtk_TargetCnt))
        {
            var extraTargetingCnt = playerSkillAbilityDB.GetAbilityValue(Ability_GroupType.OnAutoAtk, (int)AbilityType_OnAutoAtk.User_AutoAtk_TargetCnt, Ability_ValueType.Value);
            for(var i = 1; i <= extraTargetingCnt; i++)
            {
                if(InstanceEntityAbility != null &&
                    InstanceEntityAbility.TargetEntityList != null &&
                    InstanceEntityAbility.TargetEntityList.Count > i)
                {
                    _projectileShooter.Reset_BattleDB();
                    _projectileShooter.Set_AttackDB(AutoAttackDB);
                    _projectileShooter.ReadyToShoot(this, InstanceEntityAbility.TargetEntityList[i]);

                    _projectileShooter.ShootProjectile();
                }
            }
        }

        CheckCreateCatapultProjectile();
    }

    private void CheckCreateCatapultProjectile()
    {
        var playerSkillAbiltiy = PlayerInfoManager.Instance.GetPlayerSkillAbility();
        if (playerSkillAbiltiy.CheckContain(Ability_GroupType.OnAtk, (int)AbilityType_OnAtk.User_CatapultFireProjectile))
        {
            var chance = (float)playerSkillAbiltiy.GetAbilityValue(Ability_GroupType.OnAtk, (int)AbilityType_OnAtk.User_CatapultFireProjectile, Ability_ValueType.Chance);
            if (MyCalculator.CheckChance_Unedited(chance))
            {
                var catapultDB = SkillManager.Instance.CatapultProjectileServerDB;

                var createPosition = _projectileShooter.GetActivePosition(catapultDB.AttackDB.VfxPositionIndexes[0]);
                var abilityValue = (float)playerSkillAbiltiy.GetAbilityValue(Ability_GroupType.OnAtk, (int)AbilityType_OnAtk.User_CatapultFireProjectile, Ability_ValueType.Value);
                var elementType = CheckAutoAttackElementType(catapultDB.AttackDB.ElementType);

                for (var i = 0; i < 2; i++)
                {
                    var projectile = VFXManager.Instance.GetVFX(catapultDB.AttackDB.VfxNames[(int)ActionVFXType.Main][0],
                        createPosition,
                        BackgroundManager.Instance.GetScrolledWorldTransform()) as Projectile_Catapult;

                    BattleDB_Skill battleDB = new();
                    battleDB.CurAttackDB = catapultDB.AttackDB;
                    battleDB.CurAttackDB.ElementType = elementType;
                    battleDB.CurAttackDB.AbilityValue = new ObscuredFloat[] { (float)(abilityValue * (1.0 + CurEntityAbility[AbilityType_Entity.Skill_Dmg])) };
                    battleDB.User = this;
                    battleDB.UsePosition = createPosition;
                    battleDB.UseDirection = Vector2.right;
                    battleDB.DeepCopyUserInfo(this, CurEntityAbility);

                    projectile.SetUpStatus(battleDB, InstanceEntityAbility.TargetLayerMask);
                    projectile.Shoot();
                }
            }
        }
    }
    #endregion

    #region State Methods
    protected Coroutine _stateMachine;

    private readonly WaitForEndOfFrame _waitForFrame = new();
    private IEnumerator CheckState()
    {
        while (true)
        {
            switch (CurEntityState)
            {
                case EntityState.Unarmed:
                case EntityState.Run:
                case EntityState.Hit:
                case EntityState.Die:
                case EntityState.Pause:
                    {
                        break;
                    }

                case EntityState.Idle:
                    {
                        UpdateArmedIdle();
                        break;
                    }

                case EntityState.Aim:
                    {
                        if (!InstanceEntityAbility.OnUsingSkill)
							UpdateAim();
						break;
                    }

                case EntityState.Attack:
                    {
                        if (InstanceEntityAbility.CanAutoAttack)
							StartAutoAttack();
						break;
                    }

                case EntityState.Skill:
                    {
                        if (_canUseSkill)
							StartSkill();
						break;
                    }
            }

            yield return _waitForFrame;
        }
    }
    #endregion

    #region Activate Methods
    public override void ActivateObject(ObscuredBool isActive)
    {
        base.ActivateObject(isActive);

        if (isActive)
            _stateMachine = StartCoroutine(CheckState());
    }
    #endregion

    #region Reset Methods
    public override void ResetTransform(Vector2 resetPosition)
    {
        base.ResetTransform(resetPosition);

        transform.SetParent(BackgroundManager.Instance.GetNoneScrolledWorldTransform());
        _equipedWeapon.transform.position = _originWeaponPosition;
    }

    public void ResetInstanceLifeEnergy()
    {
        var playerStatusMgr = PlayerInfoManager.Instance;
        var playerAbility_Default = playerStatusMgr.PlayerAbility_Default;

        InstanceEntityAbility.SetAbilityValue(AbilityType_Instance.Life, playerAbility_Default[AbilityType_Entity.LifePoint_Final]);
        playerStatusMgr.UpdateHP();
        InstanceEntityAbility.SetAbilityValue(AbilityType_Instance.Energy, playerAbility_Default[AbilityType_Entity.EnergyPoint_Final]);
        playerStatusMgr.UpdateMP();
    }

    private void ResetInstanceStatus()
    {
        InstanceEntityAbility.Reset_AllBuff();

        InstanceEntityAbility.OwnerEntityAbility = CurEntityAbility;
        InstanceEntityAbility.OwnerSkillAbilityGroup = PlayerInfoManager.Instance.GetPlayerSkillAbility();

        ResetInstanceLifeEnergy();

        InstanceEntityAbility.TargetLayerMask = LayerMask.GetMask("Monster");
        InstanceEntityAbility.SetAbilityValue(AbilityType_Instance.AtkDelay, 0.1f);
    }

    private void ResetSkill()
    {
        SkillManager.Instance.Reset_SkillCooldown();
    }

    public void ResetSkillStatus()
    {
        UseManualSkill = false;
        _activateSkillSlotIdx = -1;
        CurCastingSkillId = string.Empty;
        OnSkillRepeating = false;
        SkillRepeatCnt = 0;
    }

    public void ResetSkillStatusFull()
    {
        ResetSkillStatus();

        InstanceEntityAbility.OnUsingSkill = false;
        SetAnimBool("OnSkill_Charged", false);
    }

    public void ResetAll(Vector2 position)
    {
        ResetSkillStatusFull();
        ResetTransform(position);
        ResetStatus();
        ResetInstanceStatus();
        ResetAnimator();
        ResetSkill();

        SetUpBeforeActive();
    }
    #endregion

    #region Pause Methods
    public override void PauseActing()
    {
        if (isDead)
            return;

        base.PauseActing();

        ResetAnimator();
        ResetSkillStatusFull();
        StopCoroutine(_stateMachine);
    }
    #endregion

    #region Utility Methods
    protected override void ChangeSpriteColor()
    {
        base.ChangeSpriteColor();

        var _originColor = _materialBlockedObjects[0].GetColor(BLOCKED_PROPERTY_STRING);

        _materialBlockedObjects[1].SetColor(BLOCKED_PROPERTY_STRING, _originColor);
        _frontArmRenderer.SetPropertyBlock(_materialBlockedObjects[1]);

        _materialBlockedObjects[2].SetColor(BLOCKED_PROPERTY_STRING, _originColor);
        _equipedWeapon.MySpriteRenderer.SetPropertyBlock(_materialBlockedObjects[2]);
    }

    protected override void ResetInstnaceMaterial()
    {
        base.ResetInstnaceMaterial();

        _materialBlockedObjects[1].SetColor(BLOCKED_PROPERTY_STRING, Flexible_MaterialColor);
        _frontArmRenderer.SetPropertyBlock(_materialBlockedObjects[1]);

        _materialBlockedObjects[2].SetColor(BLOCKED_PROPERTY_STRING, Flexible_MaterialColor);
        _equipedWeapon.MySpriteRenderer.SetPropertyBlock(_materialBlockedObjects[2]);
    }
    #endregion

    #region Costume Method
    public void UpdateAllCostume()
    {
        var saveDict = SaveDataManager.Instance.SaveDataDict;
        var weaponId = (saveDict[SaveDataManager.SaveDataType_Server.UserInfo] as UserInfo_GameSaveData).GetMountedEquipment(DefaultEquipmentType.Weapon);
        var costumeId = (saveDict[SaveDataManager.SaveDataType_Server.PaidStore] as PaidStore_GameSaveData).EquipedPlayerCostumeId;

        ChangeWeaponCostume(weaponId);
        ChangeBodyCostume(costumeId);
    }

    public void ChangeWeaponCostume(ObscuredString weaponId)
    {
        var dict = ResourceManager.Instance.WeaponAnimControllerDict;
        if (dict.ContainsKey(weaponId))
        {
            _equipedWeapon.ChangeAnimController(dict[weaponId]);
        }
    }

    public void ChangeBodyCostume(ObscuredString costumeId)
    {
        var dict = ResourceManager.Instance.PlayerCostumeFullAnimControllerDict;
        if (dict.ContainsKey(costumeId))
        {
            ChangeAnimController(dict[costumeId]);

            var _costumeIdx = DataManager.Instance.Item_InfoDBDict[costumeId].spriteIdx;
            PlayerInfoManager.Instance.Update_UserProfile?.Invoke(_costumeIdx);
        }
    }

    private void ChangeAnimController(AnimatorOverrideController animController)
    {
        _myAnimator.runtimeAnimatorController = animController;
    }
    #endregion

    #region Buff Methods
    public override BuffAddResultType AddBuff(BuffStatus buffStatus)
    {
        var returnValue = base.Add_Buff(buffStatus);
        if (returnValue == BuffAddResultType.New)
        {
            UIManager.Instance.SetUp_PlayerBuff(buffStatus);
            if (buffStatus.GroupType == Ability_GroupType.Utility && buffStatus.DetailType == (int)AbilityType_Utility.AutoAtk_Elemental_Conversion)
            {
                var elementType = CheckAutoAttackElementType();
                if (AutoAttackDB.ElementType != elementType)
                    AutoAttackDB = LocalDB_Player.playerAutoAttacks[(int)elementType];

                buffStatus.Delegate_BuffEnd += ResetAutoAttackElement;
            }

            else if (buffStatus.GroupType == Ability_GroupType.Entity && 
                (buffStatus.DetailType == (int)AbilityType_Entity.Atk_Spd_Mul || buffStatus.DetailType == (int)AbilityType_Entity.AtkPoint_Final))
            {
                UIManager.Instance.UpdatePlayerStatusUI();

                buffStatus.Delegate_BuffEnd += UIManager.Instance.UpdatePlayerStatusUI;
            }
        }

        return returnValue;
    }

    private void ResetAutoAttackElement()
    {
        AutoAttackDB = LocalDB_Player.playerAutoAttacks[(int)ElementType.Physic];
    }

    private ElementType CheckAutoAttackElementType(ElementType defaultType = ElementType.Physic)
    {
        ElementType returnValue = defaultType;

        var physicValue = CurEntityAbility[AbilityType_Entity.Physic_Dmg];
        var fireValue = CurEntityAbility[AbilityType_Entity.Fire_Dmg];
        var coldValue = CurEntityAbility[AbilityType_Entity.Cold_Dmg];
        var lightningValue = CurEntityAbility[AbilityType_Entity.Lightning_Dmg];

        ObscuredDouble abilityValue = 0.0;
        Dictionary<ElementType, ObscuredDouble> elementDmgDict = new()
        {
            { ElementType.Physic, physicValue },
            { ElementType.Fire, fireValue },
            { ElementType.Cold, coldValue },
            { ElementType.Lightning, lightningValue }
        };

        foreach (var dmgType in elementDmgDict)
        {
            if (dmgType.Value > abilityValue)
            {
                returnValue = dmgType.Key;
                abilityValue = dmgType.Value;
            }
        }

        return returnValue;
    }

    public override BuffAddResultType AddAilment(AilmentType ailmentType, AilmentBuffStatus ailmentStatus)
    {
        var returnValue = base.AddAilment(ailmentType, ailmentStatus);
        if (returnValue == BuffAddResultType.New)
            UIManager.Instance.SetUp_PlayerBuff(ailmentStatus);

        return returnValue;
    }
    #endregion
}

