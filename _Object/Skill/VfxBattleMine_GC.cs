//----------------------------------------------------------------------------------------------------
// 목적 : 설치형 지뢰 VFX (발동 시 본체 VFX를 연쇄해서 생성하고 크기, 간격, 속도 등을 공적으로 조절하기 위함)
// 
// 주요 기능
// - Mine 대기 -> 발동 -> SkillDB에 저장된 Main 타입의 VFX 연쇄 생성
// - 생성 간격/크기, 랜덤 위치 오프셋 등으로 다양한 연출
// - 캐싱된 코루틴 루프로 생성/종료해 재생/종료에서 안전하게 처리
// - 본체 VFX를 위한 "징검다리 역할"을 해주는 VFX 
//----------------------------------------------------------------------------------------------------

using CodeStage.AntiCheat.ObscuredTypes;
using System.Collections;
using UnityEngine;

public class VfxBattleMine_GC : VfxBattleMine
{
    //----------------------------------------------------------------------------------------------------
    // Methods
    //----------------------------------------------------------------------------------------------------
    #region Play Methods
    private Coroutine _createCoroutine;
    private WaitForSeconds _waitForSec;
    public override VFXBattle ActivateMineAction()
    {
        _waitForSec = new WaitForSeconds(_defaultCreateTimeSec);
        _createCoroutine = StartCoroutine(CreateGlacial());

        return null;
    }

    private readonly ObscuredFloat _defaultCreateTimeSec = 0.125f;
    private readonly ObscuredFloat _defaultCreateDistance = 0.5f;
    private readonly ObscuredFloat _minCreateDistance = 0.5f;
    private readonly ObscuredFloat _perSizeRatio = 0.15f; 
    private IEnumerator CreateGlacial()
    {
        // Aoe에 따라 설치거리가 조정됨
        var aoe = (float)MyBattleDB.CurUserSkillDB.CommonAbilityGroups
            [MyBattleDB.CommonAbilityIdx].commonAbilityDict[SkillCommonAbilityType.Aoe].GetAbilityValue(Ability_CalcValueType.Final);

        var maxCreateCnt = (int)MyBattleDB.CurUserSkillDB.UniqueAbilityGroup.GetAbilityValue(Ability_GroupType.Skill, (int)AbilityType_Skill.Object_Creation_Cnt, Ability_ValueType.Value);
        var curCreateCnt = maxCreateCnt;
        while(curCreateCnt > 0)
        {
            // 갈수록 사이즈가 커지도록 설정
            var gapCnt = maxCreateCnt - curCreateCnt;
            var x = (_defaultCreateDistance * aoe) * gapCnt + (_perSizeRatio * gapCnt);
            x += gapCnt == 0 ? _minCreateDistance : 0;
            var randomY = Random.Range(-0.15f, 0.15f) * 2.0f;
            var createPosition = (Vector2)transform.position + new Vector2(x, randomY);

            var battleVFX = VFXManager.Instance.GetVFX
                (MyBattleDB.CurAttackDB.VfxNames[(int)ActionVFXType.Main][0],
                createPosition, 
                BackgroundManager.Instance.GetScrolledWorldTransform()) as VFXBattle;

            var battleDB_Skill = new BattleDB_Skill();
            battleDB_Skill.DeepCopy(MyBattleDB);
            battleDB_Skill.UsePosition = createPosition;
            battleDB_Skill.UseDirection = Vector2.right;
            battleVFX.SetUpStatus(battleDB_Skill, battleDB_Skill.User.InstanceEntityAbility.TargetLayerMask);
            battleVFX.SetScaleRatio(aoe * (1 + (_perSizeRatio * gapCnt)));

            curCreateCnt--;

            yield return _waitForSec;
        }

        Play_EndVFX();
    }

    public override void PlayEndVFX()
    {
        if (onDiedAnimation) return;

        if (_createCoroutine != null)
        {
            StopCoroutine(_createCoroutine);
            _createCoroutine = null;
        }

        base.PlayEndVFX();
    }
    #endregion

    #region End Methods
    public override void KillForced(ObscuredBool isForced)
    {
        if (_createCoroutine != null)
        {
            StopCoroutine(_createCoroutine);
            _createCoroutine = null;
        }

        base.KillForced(isForced);
    }
    #endregion
}




