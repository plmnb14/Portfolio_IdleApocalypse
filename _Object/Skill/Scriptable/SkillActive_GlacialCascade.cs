//----------------------------------------------------------------------------------------------------
// SkillPrefab을 상속받아 스킬 효과/연출한 Class 입니다.
//
// Scriptable Object (SkillPrefab) 기반 스킬 파이프 라인 (시전 -> 효과 실행 -> 관련 VFX 스폰 / 연출)
// Skill 능력치에 따라 Scale, AnimationSpeed, 이펙트 개수가 다르게 나오도록 설정했습니다.
//----------------------------------------------------------------------------------------------------
using CodeStage.AntiCheat.ObscuredTypes;
using UnityEngine;

[CreateAssetMenu(fileName = "SkillActive_GlacialCascade", menuName = "Skill/SkillActive_GlacialCascade", order = 3)]
public class SkillActive_GlacialCascade : SkillPrefab
{
    public override bool StartSkill(Hero user, string skillID)
    {
        var returnValue = base.StartSkill(user, skillID);

        if(returnValue)
        {
            var data = DataManager.Instance.Item_InfoDBDict[skillID] as SkillDB_Server;
            user.SetAnimTrigger(data.AttackDB.AnimationName);
        }

        return returnValue;
    }

    public override void StartSkillEffect(BattleDB_Skill battleDB_Skill, string skillID)
    {
        if (StageManager.Instance.IsChangingMap || StageManager.Instance.IsForcedChangingMap)
            return;

        CheckBeforeIncreaseDmg(battleDB_Skill);
        base.StartSkillEffect(battleDB_Skill, skillID);
        ThrowMine(battleDB_Skill);
    }

    private readonly ObscuredFloat maxRange = 6.0f;
    private readonly ObscuredFloat minXgap = 1.0f;
    private readonly ObscuredFloat maxXgap = 2.0f;
    private void ThrowMine(BattleDB_Skill battleDB_Skill)
    {
        var shooter = (battleDB_Skill.User as Hero).ProjectileShooter;
        var usePosition = shooter.GetActivePosition(battleDB_Skill.CurAttackDB.VfxPositionIndexes[0]);
        var distance = Vector2.Distance(usePosition, battleDB_Skill.Target.transform.position);
        distance = (distance / maxRange) * 2.0f;
        distance =
            distance > maxXgap ? maxXgap :
            distance < minXgap ? minXgap : distance;

        // 설치 개수
        ObscuredInt maxLandCnt = (int)battleDB_Skill.CurUserSkillDB.UniqueAbilityGroup.GetAbilityValue(
            Ability_GroupType.Skill,
            (int)AbilityType_Skill.Object_Land_Cnt,
            Ability_ValueType.Value);

        // 설치 속도
        var finalThrowSpeed =
            battleDB_Skill.CurUserSkillDB.UniqueAbilityGroup.CheckContain(Ability_GroupType.Skill, (int)AbilityType_Skill.Object_ThrowLand_Spd) ?
            (float)battleDB_Skill.CurUserSkillDB.UniqueAbilityGroup.GetAbilityValue(
            Ability_GroupType.Skill,
            (int)AbilityType_Skill.Object_ThrowLand_Spd,
            Ability_ValueType.Value, Ability_CalcValueType.Final) : 1.0f;

        for (var i = 0; i < maxLandCnt; i++)
        {
            var xPosition = 0.0f;
            var yPosition = 0.0f;
            if (i > 0)
            {
                xPosition = Random.Range(-0.25f, 0.25f) * 3.0f;
                yPosition = Random.Range(-0.125f, 0.125f) * 3.0f;

                xPosition += xPosition < 0 ? -0.5f : 0.5f;
                yPosition += yPosition < 0 ? -0.25f : 0.25f;
            }

            var finalPosition = (Vector2)battleDB_Skill.Target.transform.position +
                new Vector2(xPosition, yPosition) - (Vector2.right * distance);            
            battleDB_Skill.UsePosition = usePosition;
            battleDB_Skill.UseDirection = Vector2.right;
            battleDB_Skill.TargetingPosition = finalPosition;

            var mine = VFXManager.Instance.GetVFX(battleDB_Skill.CurAttackDB.VfxNames[(int)ActionVFXType.Extra][battleDB_Skill.CommonAbilityIdx], battleDB_Skill.UsePosition, BackgroundManager.Instance.GetScrolledWorldTransform()) as VfxBattleMine;
            mine.SetUpStatus(battleDB_Skill, battleDB_Skill.User.InstanceEntityAbility.TargetLayerMask);
            mine.Set_AnimationActiveSpeed(finalThrowSpeed);
            mine.MoveToTargetPoint(1.5f * finalThrowSpeed);
        }
    }
}


