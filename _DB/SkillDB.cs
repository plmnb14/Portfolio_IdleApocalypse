//----------------------------------------------------------------------------------------------------
// 목적 : 스킬 데이터 모델과 능력치 그룹 로직
// 
// 주요 기능
// - AttackDB : 공격 정보를 담은 Class 입니다.
//              공격 타입, 속성, 이펙트 Prefab 이름, 사운드 이름 등 정보가 담겨 있습니다.
//
// - SklillDB_Cur : 스킬의 런타임 능력치와 쿨타임, 장착 여부가 담긴 Class
//
// - SkillDB_Save : 서버에 저장할 스킬 정보 (레벨, 장착된 코어스톤, 프리셋 여부 등...)
//
// - SkillDB_Server : 서버 차트에서 불러온 스킬 정보(성장 수치, 스킬 설명, 태그, 속성 등...)
//                    스킬 상세 설명을 위한(설명 UI에 사용할) 별도의 데이터도 관리
//
// - SkillPrefab : 실제 스킬이 동작하는 Scriptable Object입니다.
//                 자식 스킬 Class에서 세부 내용을 구성했습니다. 
//----------------------------------------------------------------------------------------------------

using CodeStage.AntiCheat.ObscuredTypes;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

#region Enum
public enum SkillTag
{ 
    None = 0,              // 없음
    Attack = 1 << 0,        // 공격
    Support = 1 << 1,       // 보조
    Ailment = 1 << 2,       // 상태이상
    Projectile = 1 << 3,    // 투사체
    Area = 1 << 4,          // 범위
    Physic = 1 << 5,        // 물리
    Fire = 1 << 6,          // 화염
    Cold = 1 << 7,          // 냉기
    Lightning = 1 << 8,     // 전기
    Void = 1 << 9,          // 공허
    Duration = 1 << 10,     // 지속
    Creation = 1 << 11,     // 생성
    Passive = 1 << 12,      // 보유효과
    Landing = 1 << 13,      // 설치
    Summon = 1 << 14,       // 소환

    End = 14
}

public enum SkillUseType { Active, Passive, End }
public enum SkillCastType { Self, Shooter, Droid, Land, End }

public enum SkillCommonAbilityType
{
    Energy_Use,     // 에너지 소모량
    Cooldown,       // 재사용 대기시간
    //--------------------------------------------------
    Use_Spd,        // 사용 속도
    Aoe,            // 범위
    Dur,            // 지속 시간
    Dot,            // 지속 피해
    //--------------------------------------------------
    Proj_Angle,     // 투사체 발사각도
    Proj_Spd,       // 투사체 속도
    //--------------------------------------------------
    Hit_Cnt,        // 타격 횟수
    End,
}

public enum ActionVFXType
{
    Begin,          // 사용 직후 나오는 이펙트
    Main,           // 메인이 되는 이펙트
    Extra,          // 추가로 필요한 이펙트
    Hit,            // 피격, 끝날 때 나오는 이펙트
    End
}
#endregion

// 공격 정보를 담는 클래스 입니다.
public class AttackDB
{
    //----------------------------------------------------------------------------------------------------
    // Fields
    //----------------------------------------------------------------------------------------------------
    #region Public Fields
    public ElementType elementType { get; set; } = ElementType.Physic;
    public AttackMethodType attackMethodType { get; set; }
    public ActPositonType actPositionType { get; set; }
    //
    public ObscuredString AnimationName { get; set; } = "OnAttack";
    public ObscuredInt[] VfxPositionIndexes { get; set; }
    public ObscuredString[][] VfxNames { get; set; } = new ObscuredString[(int)ActionVFXType.End][];
    public ObscuredString[][] SfxNames { get; set; } = new ObscuredString[(int)ActionVFXType.End][];
    //
    public ObscuredFloat[] EnemyProjSpds { get; set; }
    public ObscuredFloat[] AbilityValue { get; set; }
    //
    public ObscuredBool[] DefaultAilmentEnables { get; set; }
    //
    public ObscuredFloat AttackRange { get; set; } = 1.0f;
    public ObscuredFloat Cooldown { get; set; } = 0.5f;
    //
    public ObscuredInt PhaseIndex;
    public ObscuredFloat ScaleRatio = 1.0f;
    #endregion


    //----------------------------------------------------------------------------------------------------
    // Methos
    //----------------------------------------------------------------------------------------------------
    #region Methods
    public ObscuredBool CheckCanPlayRandomSound(ActionVFXType actionVfxType)
    {
        return SfxNames[(int)actionVfxType] != null && SfxNames[(int)actionVfxType].Length > 1;
    }
    #endregion
}


// 실시간으로 사용될 스킬 정보
public class SkillDB_Cur
{
    //----------------------------------------------------------------------------------------------------
    // Etc
    //----------------------------------------------------------------------------------------------------
    #region Delegate
    public delegate void DismountSkill();
    public DismountSkill OnDismountSkill { get; set; }
    #endregion

    #region Enum
    public enum AbilityGroupType { Common, Unique, Corestone, End };
    #endregion

    //----------------------------------------------------------------------------------------------------
    // Fields
    //----------------------------------------------------------------------------------------------------
    #region Skill Ability Enable Fields
    public ObscuredBool DisableAilment
    {
        get { return _disableAilment; }
        set 
        {
            _disableAilmentTokenCnt += value == true ? 1 : -1;

            if(_disableAilmentTokenCnt <= 0)
            {
                _disableAilment = false;
                _disableAilmentTokenCnt = 0;
            }

            else if(_disableAilmentTokenCnt == 1)
            {
                _disableAilment = true;
            }
        }
    }

    private ObscuredBool _disableAilment;
    private ObscuredInt _disableAilmentTokenCnt;
    #endregion

    #region Ability Fields
    public SkillAbilityGroup_Common[] CommonAbilityGroups { get; set; }             // 스킬들이 공통으로 가지는 능력치
    public SkillAbilityGroup_Unique UniqueAbilityGroup { get; set; } = new();       // 원래 스킬 고유의 능력치
    public SkillAbilityGroup_Unique CorestoneAbilityGroup { get; set; } = new();    // 코어스톤 능력치
    #endregion

    #region Property Fields
    public SkillCastType ActiveCastType { get; set; }
    public ObscuredString skillId { get; set; }
    //
    public ObscuredBool IsMounted { get; set; }
    public ObscuredBool IsCooldown { get; set; }
    //
    public ObscuredLong CurReinforceCost { get; set; }

    public ObscuredFloat CurCooldown { get; set; }
    public ObscuredFloat MaxCooldown { get; set; }

    public ObscuredInt ActiveCoreStoneSlotCnt { get; set; } = LocalDB_Skill.defaultCoreSlotCnt;
    public ObscuredInt ActiveCoreStonePresetIdx { get; private set; }
    #endregion

    //----------------------------------------------------------------------------------------------------
    // Methods
    //----------------------------------------------------------------------------------------------------
    #region Check Methods
    public ObscuredBool CheckContain(AbilityGroupType abilityGroupType, Ability_GroupType groupType, ObscuredInt detailType)
    {
        ObscuredBool isContain = false;
        switch (abilityGroupType)
        {
            case AbilityGroupType.Common: { break; }

            case AbilityGroupType.Unique:
                {
                    if (UniqueAbilityGroup.CheckContain(groupType, detailType))
                        isContain = true;

                    break;
                }
            case AbilityGroupType.Corestone:
                {
                    if (CorestoneAbilityGroup.CheckContain(groupType, detailType))
                        isContain = true;
                    
                    break;
                }
        }

        return isContain;
    }
    #endregion

    #region CoreStone Methods
    public void AddCoreStoneAbility(Ability_GroupType groupType, ObscuredInt detailType, SkillAbilityUnique skillAbilityUnique, Ability_ValueType originAbilityValueType, Ability_ValueType abilityValueType)
    {
        if (CorestoneAbilityGroup.CheckContain(groupType, detailType))
            CorestoneAbilityGroup.Add(groupType, detailType, skillAbilityUnique, originAbilityValueType, abilityValueType);

        else
            CorestoneAbilityGroup.Insert(groupType, detailType, skillAbilityUnique);
    }

    /// <summary>
    /// 장착된 코어스톤 능력치에 의해 영향 받는지 확인하기 위해 사용함.
    /// </summary>
    /// <param name="groupType"></param>
    /// <param name="detailType"></param>
    /// <param name="skillAbilityUnique"></param>
    /// <param name="isAffacting"></param>
    public void AddCoreStoneAbility(Ability_GroupType groupType, ObscuredInt detailType, SkillAbilityUnique skillAbilityUnique, ObscuredBool isAffacting)
    {
        if (CorestoneAbilityGroup.CheckContain(groupType, detailType))
            CorestoneAbilityGroup.Add(groupType, detailType, skillAbilityUnique, isAffacting);
        
        else
            CorestoneAbilityGroup.Insert(groupType, detailType, skillAbilityUnique, isAffacting);
        
    }

    public void RemoveCoreStoneAbility(Ability_GroupType groupType, ObscuredInt detailType, SkillAbilityUnique skillAbilityUnique)
    {
        CorestoneAbilityGroup.Remove(groupType, detailType, skillAbilityUnique);
    }

    public void RemoveCoreStoneAbility(Ability_GroupType groupType, ObscuredInt detailType, SkillAbilityUnique skillAbilityUnique, Ability_ValueType abilityValueType, Ability_ValueType targetAbiliyValueType)
    {
        CorestoneAbilityGroup.Remove(groupType, detailType, skillAbilityUnique, abilityValueType, targetAbiliyValueType);
    }
    #endregion

    #region Ability Methods
    public void AddUniqueAbility(Ability_GroupType groupType, ObscuredInt detailType, SkillAbilityUnique skillAbilityUnique)
    {
        if (UniqueAbilityGroup.CheckContain(groupType, detailType))
            UniqueAbilityGroup.Add(groupType, detailType, skillAbilityUnique);

        else
            UniqueAbilityGroup.Insert(groupType, detailType, skillAbilityUnique);
    }

    public void AddUniqueAbility(Ability_GroupType groupType, ObscuredInt detailType, SkillAbilityUnique skillAbilityUnique, Ability_ValueType abilityValueType, Ability_ValueType targetAbiliyValueType)
    {
        if (UniqueAbilityGroup.CheckContain(groupType, detailType))
            UniqueAbilityGroup.Add(groupType, detailType, skillAbilityUnique, abilityValueType, targetAbiliyValueType);

        else
            UniqueAbilityGroup.Insert(groupType, detailType, skillAbilityUnique);
    }

    public void RemoveUniqueAbility(Ability_GroupType groupType, ObscuredInt detailType, SkillAbilityUnique skillAbilityUnique, Ability_ValueType abilityValueType, Ability_ValueType targetAbiliyValueType)
    {
        UniqueAbilityGroup.Remove(groupType, detailType, skillAbilityUnique, abilityValueType, targetAbiliyValueType);
    }

    public void RemoveUniqueAbility(Ability_GroupType groupType, ObscuredInt detailType, SkillAbilityUnique skillAbilityUnique)
    {
        UniqueAbilityGroup.Remove(groupType, detailType, skillAbilityUnique);
    }

    public void AddUniqueAbilityPerValue(SkillDB_Server serverDB, ObscuredInt reinforceLv)
    {
        UniqueAbilityGroup.AddPerValue(serverDB, reinforceLv);
    }

    public void RemoveUniqueAbilityPerValue(SkillDB_Server serverDB, ObscuredInt reinforceLv)
    {
        UniqueAbilityGroup.RemovePerValue(serverDB, reinforceLv);
    }
    #endregion

    #region Check Chance Methods
    public List<ObscuredInt> CheckAbilityChance(Ability_GroupType abilityGroup, ObscuredBool getCertainValue, ObscuredBool hasDefaultChance)
    {
        var returnList = hasDefaultChance ?
            UniqueAbilityGroup.Check_ChanceAbility(abilityGroup, getCertainValue) :
            CorestoneAbilityGroup.Check_ChanceAbility(abilityGroup, getCertainValue);

        return returnList;
    }

    public List<ObscuredInt> CheckAbilityChance(Ability_GroupType abilityGroup, ObscuredBool getCertainValue)
    {
        var returnList = UniqueAbilityGroup.Check_ChanceAbility(abilityGroup, getCertainValue);

        return returnList;
    }
    #endregion

    #region CoreStone Methods
    public void SetCoreStonePresetIdx(ObscuredInt corePresetIdx)
    {
        ActiveCoreStonePresetIdx = corePresetIdx;
    }

    public ObscuredBool CheckChangeCorePresetIdx(ObscuredInt corePresetIdx)
    {
        ObscuredBool returnValue = false;
        if(ActiveCoreStonePresetIdx != corePresetIdx)
        {
            ActiveCoreStonePresetIdx = corePresetIdx;
            returnValue = true;
        }

        return returnValue;
    }
    #endregion
}

// 서버에서 불러올 스킬 정보
public class SkillDB_Server : ItemDB
{
    //----------------------------------------------------------------------------------------------------
    // Fields
    //----------------------------------------------------------------------------------------------------
    #region Property Fields
    public SkillUseType UseType { get; set; }
    public ActiveSkillType ActiveSkillType { get; set; }
    public SkillCastType ActiveCastType { get; set; }
    public SkillTag Tags { get; set; }
    public ElementType ElementType { get; set; }
    public ObscuredInt SlotIdx { get; set; }
    public SkillPrefab SkillPrefab { get; set; }
    public AttackDB AttackDB { get; set; }
    public Sprite MiniSprite { get; set; }
    public HashSet<ObscuredInt> EnableSkillAbilityHashSet { get; set; } = new();
    #endregion

    #region Ability Fields
    public SkillAbilityGroup_Common_Server[] CommonAbilityServers { get; set; }
    public SkillAbilityGroup_Unique_Server UniqueAbilityServer { get; set; } = new();
    public SkillAbilityGroup_Unique[] AwakeningAbilityServer { get; set; } = new SkillAbilityGroup_Unique[LocalDB_Skill.maxAwakeningLv];
    #endregion

    #region Text Fields
    public List<KeyValuePair<SkillCommonAbilityType, ObscuredInt>> ExplainBaseAbilityTextList { get; set; } = new();
    public ObscuredString[] explainTexts { get; set; }
    public ObscuredString[][] explainTextParts { get; set; }
    public ObscuredString[] durNames { get; set; }
    public ObscuredString[] keywordStrings { get; set; }
    #endregion

    #region Bool Fields
    public List<KeyValuePair<Ability_GroupType, ObscuredInt>> TextShowList { get; set; } = new();
    public List<KeyValuePair<Ability_GroupType, ObscuredInt>> DefaultAbilityList { get; set; } = new();
    #endregion

    //----------------------------------------------------------------------------------------------------
    // Methods
    //----------------------------------------------------------------------------------------------------
    #region Constructor
    public SkillDB_Server()
    {
        for(var i = 0; i < LocalDB_Skill.maxAwakeningLv; i++)
        {
            AwakeningAbilityServer[i] = new SkillAbilityGroup_Unique();
        }
    }
    #endregion
}

// 서버에 저장할 정보
public class SkillDB_Save
{
    //----------------------------------------------------------------------------------------------------
    // Fields
    //----------------------------------------------------------------------------------------------------
    #region Public Fields
    public ObscuredInt[,] QuickSlotPresets { get; set; } = new ObscuredInt[,] 
    {
        { -1, -1, -1 },
        { -1, -1, -1 }
    };
    #endregion

    #region Property Fields
    public ObscuredInt ReinforceLv { get; protected set; }
    public ObscuredInt AwakeningLv { get; protected set; }
    public ObscuredInt HoldCnt { get; protected set; } = -1;
    #endregion

    //----------------------------------------------------------------------------------------------------
    // Methods
    //----------------------------------------------------------------------------------------------------
    #region Add Methods
    public void AddReinforceLv(ObscuredInt reinforceLv) { ReinforceLv += reinforceLv; }
    public void AddWakeningLv(ObscuredInt awakeningLv) { AwakeningLv += awakeningLv; }
    public ObscuredBool AddHoldCnt(ObscuredInt _addCount)
    {
        ObscuredBool isUnlock = false;
        if (_addCount != 0)
        {
            var count = _addCount;
            if (CheckUnlock())
            {
                HoldCnt += count;
            }

            else
            {
                SetHoldCnt(0);

                if(count > 1)
                {
                    HoldCnt += (count - 1);
                }
            }

            isUnlock = true;
        }

        return isUnlock;
    }
    #endregion

    #region Set Methods
    public void SetReinforceLv(ObscuredInt reinforceLv) { ReinforceLv = reinforceLv; }
    public void SetAwakeningLv(ObscuredInt awakeningLv) { AwakeningLv = awakeningLv; }
    public void SetHoldCnt(ObscuredInt holdCnt) { HoldCnt = holdCnt; }
    #endregion

    #region Check Methods
    public ObscuredBool CheckUnlock() { return HoldCnt == -1 ? false : true; }
    public ObscuredBool CheckHoldCount() { return HoldCnt > 0; }
    public ObscuredBool CheckMaxAwakeningLv() { return AwakeningLv >= LocalDB_Skill.maxAwakeningLv; }
    #endregion
}

// 스킬 프리팹, Scriptable Object
public abstract class SkillPrefab : ScriptableObject
{
    //----------------------------------------------------------------------------------------------------
    // Fields
    //----------------------------------------------------------------------------------------------------
    #region Protected Fields
    protected Queue<Coroutine> SkillCoroutineQueue { get; set; }
    #endregion

    //----------------------------------------------------------------------------------------------------
    // Methods
    //----------------------------------------------------------------------------------------------------
    #region Methods
    public virtual bool StartSkill(Hero user, string skillID) 
    {
        if (StageManager.Instance.IsChangingMap || StageManager.Instance.IsForcedChangingMap)
            return false;

        user.CurCastingSkillId = skillID;

        UpdateUserAtkSpd(user, skillID);

        if (!user.OnSkillRepeating)
            UpdateUserRepeatCnt(user, skillID);

        return true;
    }
    public virtual void StartSkillEffect(BattleDB_Skill battleDBSkill, string skillID) 
    {
        if (StageManager.Instance.IsChangingMap || StageManager.Instance.IsForcedChangingMap)
            return;

        SoundManager.Instance.PlayEffectAudio(battleDBSkill.curAttackDB.SfxNames[(int)ActionVFXType.Begin][0]);
    }

    protected virtual void UpdateUserAtkSpd(Hero user, string skillID)
    {
        var skillServerDB = DataManager.Instance.Item_InfoDBDict[skillID] as SkillDB_Server;
        var skillItem = SkillManager.Instance.Skill_SaveData.SkillList[(int)SkillUseType.Active][skillServerDB.SlotIdx];
        var uniqueSkillAbility = skillItem.SkillDB_Cur.UniqueAbilityGroup.AbilityDicts[Ability_GroupType.Skill];

        var entitySpeed = user.CurEntityAbility[AbilityType_Entity.Atk_Spd_Mul];
        var detailTypeInt = (int)AbilityType_Skill.Atk_Spd;
        if (uniqueSkillAbility.ContainsKey(detailTypeInt))
        {
            var addValue = uniqueSkillAbility[detailTypeInt].Get_Value(Ability_ValueType.Value, Ability_CalcValueType.Add);
            var mulValue = uniqueSkillAbility[detailTypeInt].Get_Value(Ability_ValueType.Value, Ability_CalcValueType.Mul);
            entitySpeed = entitySpeed + addValue * (1 + mulValue);
        }
        user.Update_AttackSpeed((float)entitySpeed);
    }

    protected virtual void UpdateUserRepeatCnt(Hero user, string skillID)
    {
        var skillServerDB = DataManager.Instance.Item_InfoDBDict[skillID] as SkillDB_Server;
        var skillItem = SkillManager.Instance.Skill_SaveData.SkillList[(int)SkillUseType.Active][skillServerDB.SlotIdx];
        var uniqueSkillAbility = skillItem.SkillDB_Cur.UniqueAbilityGroup.AbilityDicts[Ability_GroupType.Skill];

        var detailTypeInt = (int)AbilityType_Skill.Repetation_Cnt;
        if (uniqueSkillAbility.ContainsKey(detailTypeInt))
        {
            user.SkillRepeatCnt = (int)uniqueSkillAbility[detailTypeInt].Get_Value(Ability_ValueType.Value, Ability_CalcValueType.Final);
        }
    }

    protected virtual void CheckBeforeIncreaseDmg(BattleDB_Skill battleDBSkill)
    {
        if (battleDBSkill.user == null)
            return;

        var playerSkillAbility = PlayerInfoManager.Instance.Get_PlayerSkillAbility();
        var detailTypeInt = (int)AbilityType_OnAtk.User_EnergyCon_ExtraDmg;
        if (!playerSkillAbility.CheckContain(Ability_GroupType.OnAtk, (int)AbilityType_OnAtk.User_EnergyCon_ExtraDmg))
            return;

        var standard = playerSkillAbility.GetAbilityValue(Ability_GroupType.OnAtk, detailTypeInt, Ability_ValueType.Standard, Ability_CalcValueType.Final);
        var curEnergy = (int)battleDBSkill.user.InstanceEntityAbility.GetAbilityValue(AbilityType_Instance.Energy);
        var maxEnergy = battleDBSkill.user.CurEntityAbility[AbilityType_Entity.EnergyPoint_Final];
        var useRatio = (int)(maxEnergy * standard);
        useRatio = useRatio < 0 ? 1 : useRatio;

        if (curEnergy < useRatio)
            return;

        battleDBSkill.user.InstanceEntityAbility.AddAbilityValue(AbilityType_Instance.Energy, -useRatio);
        PlayerInfoManager.Instance.UpdateMP();

        var value = playerSkillAbility.GetAbilityValue(Ability_GroupType.OnAtk, detailTypeInt, Ability_ValueType.Value);
        battleDBSkill.extraDmg_Percentage = value;
    }

    protected virtual void ReserveResetCoroutine()
    {
        StageManager.Instance.ChangeWave += ResetCoroutine;
    }

    protected virtual void ResetCoroutine()
    {
        StageManager.Instance.ChangeWave -= ResetCoroutine;

        if (SkillCoroutineQueue == null || SkillCoroutineQueue.Count <= 0)
            return;

        var _coroutine = SkillCoroutineQueue.Dequeue();

        CoroutineRunner.instance.StopCoroutine(_coroutine);
        _coroutine = null;
    }
    #endregion

}




